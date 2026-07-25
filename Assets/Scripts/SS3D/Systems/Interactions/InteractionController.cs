using FishNet.Connection;
using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Coimbra.Services.Events;
using Coimbra.Services.PlayerLoopEvents;
using SS3D.Core;
using SS3D.Core.Behaviours;
using SS3D.Interactions;
using SS3D.Interactions.Extensions;
using SS3D.Interactions.Interfaces;
using SS3D.Logging;
using SS3D.Systems.Inputs;
using SS3D.Systems.Entities;
using SS3D.Systems.Entities.Humanoid;
using SS3D.Systems.Entities.Humanoid.Body;
using SS3D.Systems.Audio;
using AudioType = SS3D.Systems.Audio.AudioType;
using SS3D.Systems.Combat;
using SS3D.Systems.Combat.Interactions;
using SS3D.Systems.Health;
using SS3D.Systems.Screens;
using SS3D.Systems.Selection;
using SS3D.Systems.Inventory.Containers;
using SS3D.Systems.Inventory.Items;
using SS3D.Systems.Stamina;
using SS3D.Systems.StructuralDamage;
using SS3D.Systems.Tile;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Unity.Profiling;
using InputSubSystem = SS3D.Systems.Inputs.InputSubSystem;

namespace SS3D.Systems.Interactions
{
    /// <summary>
    /// Attached to the player, initiates interactions.
    /// </summary>
    public sealed class InteractionController : NetworkActor, IIntentProvider
    {
        private const string ExamineInteractionName = "Examine";

        private Controls.InteractionsActions _controls;
        private Controls.HotkeysActions _hotkeysControls;
        private InputAction _cancelInteractionAction;
        private InputSubSystem _inputSystem;
        private IInputHandle _gameplayHandle;

        private Camera _camera;
        private RadialInteractionSubSystem _radialView;
        private ArmedInteractionSubSystem _armedSystem;
        private SelectionSubSystem _selectionSystem;

        [SyncVar(OnChange = nameof(SyncIntent))] private IntentType _currentIntent = IntentType.Help;

        private IntentType _ownerIntent = IntentType.Help;

        private int _clientActiveReferenceId = -1;
        private IInteractionSource _clientActiveSource;
        private InteractionReference _serverActiveReference;
        private IInteractionSource _serverActiveSource;

        private Vector3 _meleeAimRayOrigin;
        private Vector3 _meleeAimPoint;
        private bool _hasMeleeAimRay;

        /// <summary>Server-scheduled Harm primary connect (bypasses DelayedInteraction.Update).</summary>
        private float _pendingMeleeConnectAt = -1f;
        private Hand _pendingMeleeHand;
        private MeleeWeaponProfile _pendingMeleeProfile;
        private int _meleeSwingSerial;

        private Selectable _activeOutlineSelectable;
        private InteractionOutlineView _activeOutlineView;
        private readonly List<IInteractionTarget> _outlineTargets = new(8);

        private static readonly ProfilerMarker OutlinePerformanceMarker = new("SS3D.Interactions.Outline");

        public IntentType CurrentIntent => IsOwner ? _ownerIntent : _currentIntent;

        public override void OnStartServer()
        {
            base.OnStartServer();
            // Owner-only Update() never runs on a dedicated server — schedule melee connect here.
            AddHandle(UpdateEvent.AddListener(HandleServerMeleeConnectUpdate));
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);

            if (IsOwner)
            {
                SubscribeToInput();
                _armedSystem.EvaluateTarget += EvaluateArmedTarget;
            }
            else if (prevOwner.Equals(LocalConnection))
            {
                UnsubscribeFromInput();
                _armedSystem.EvaluateTarget -= EvaluateArmedTarget;
            }
        }

        protected override void OnAwake()
        {
            base.OnAwake();

            // Wire inputs before camera — a missing PlayerCamera must not leave _controls null
            // (OnOwnershipClient → SubscribeToInput would cascade-NRE).
            _inputSystem = SubSystems.Get<InputSubSystem>();
            Controls controls = _inputSystem.Inputs;
            _controls = controls.Interactions;
            _hotkeysControls = controls.Hotkeys;
            _cancelInteractionAction = controls.Interactions.Get().FindAction("Cancel Interaction", throwIfNotFound: true);

            _radialView = SubSystems.Get<RadialInteractionSubSystem>();
            _armedSystem = SubSystems.Get<ArmedInteractionSubSystem>();
            _selectionSystem = SubSystems.Get<SelectionSubSystem>();

            Actor playerCamera = SubSystems.Get<CameraSubSystem>()?.PlayerCamera;
            if (playerCamera != null)
            {
                _camera = playerCamera.GetComponent<Camera>();
            }

            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_camera == null)
            {
                Log.Error(this, "No gameplay camera resolved for InteractionController", Logs.Important);
            }
        }

        private void Update()
        {
            if (!IsOwner)
            {
                return;
            }

            RefreshActiveInteractionTracking();
            TrySyncMeleeAimDuringSwing();
        }

        private void LateUpdate()
        {
            if (!IsOwner)
            {
                return;
            }

            RefreshInteractionOutline();
        }

        protected override void OnEnabled()
        {
            base.OnEnabled();

            if (IsOwner)
            {
                SubscribeToInput();
            }
        }

        protected override void OnDisabled()
        {
            base.OnDisabled();

            if (IsOwner)
            {
                UnsubscribeFromInput();
                ClearInteractionOutline();
                _armedSystem.EvaluateTarget -= EvaluateArmedTarget;
            }
        }

        private void SubscribeToInput()
        {
            // OnEnabled and OnOwnershipClient can both fire for an owner; subscribe exactly once.
            if (_gameplayHandle != null)
            {
                return;
            }

            _controls.RunPrimary.performed += HandleRunPrimary;
            _controls.ViewInteractions.performed += HandleView;
            _cancelInteractionAction.performed += HandleCancelInteraction;
            _hotkeysControls.Use.performed += HandleUse;
            _gameplayHandle = _inputSystem.PushContext(InputContext.Gameplay);
        }

        private void UnsubscribeFromInput()
        {
            _controls.RunPrimary.performed -= HandleRunPrimary;
            _controls.ViewInteractions.performed -= HandleView;
            _cancelInteractionAction.performed -= HandleCancelInteraction;
            _hotkeysControls.Use.performed -= HandleUse;
            _gameplayHandle?.Dispose();
            _gameplayHandle = null;
        }

        [Client]
        private void HandleCancelInteraction(InputAction.CallbackContext callbackContext)
        {
            if (_clientActiveReferenceId < 0)
            {
                return;
            }

            int referenceId = _clientActiveReferenceId;
            ClearClientActiveInteractionTracking();
            InteractionOptimisticFeedback.Clear(transform);
            InteractionOutlineView.ClearPending();
            CmdCancelInteraction(referenceId);
        }

        /// <summary>
        /// Runs the most prioritised interaction
        /// </summary>
        [Client]
        public void HandleRunPrimary(InputAction.CallbackContext callbackContext)
        {
            if (InputInterface.IsPointerOverInterface())
            {
                return;
            }

            if (_armedSystem.IsArmed)
            {
                TryResolveArmedInteraction();
                return;
            }

            // Harm is combat-exclusive: ranged fire when holding a firearm, else melee swing.
            // Never fall through to Drop/Open/MI.
            if (CurrentIntent == IntentType.Harm)
            {
                if (TryRunRangedFirePrimary())
                {
                    return;
                }

                TryRunMeleeSwingPrimary();
                return;
            }

            List<InteractionEntry> viableInteractions = FilterRadialInteractions(
                GetViableInteractionsFromSelection(out InteractionEvent interactionEvent));

            if (viableInteractions.Count <= 0)
            {
                return;
            }

            InteractionEntry interaction = viableInteractions[0];
            interactionEvent.Target = interaction.Target ?? ResolveFallbackTarget(interactionEvent, interaction);

            Log.Information(this, "Running interaction {interactionId} on target {target}", Logs.Generic, interaction.Id.GenericName, interaction.Target);
            if (!TryGetNetworkTargetForDispatch(interaction, interactionEvent, out NetworkObject networkTarget))
            {
                return;
            }

            InteractionOptimisticFeedback.TryBeginDelayed(interaction.Interaction, interactionEvent);
            InteractionOutlineView.TryBeginPending(interaction.Interaction, interactionEvent);
            CmdRunInteraction(networkTarget, interactionEvent.Point, interaction.Id.GenericName, interaction.Id.TargetComponentIndex);
        }

        /// <summary>
        /// Starts windup/swing/recovery for Harm primary regardless of hover target.
        /// Damage (if any) is applied at connect from synced aim.
        /// </summary>
        [Client]
        private bool TryRunMeleeSwingPrimary()
        {
            if (!TryCreateMeleeHitInteraction(out MeleeHitInteraction hit, out Hand hand))
            {
                return false;
            }

            if (!hit.CanStartSwing(hand))
            {
                return false;
            }

            // Optimistic busy lock is for pure clients (Cmd latency). On host/listen-server the same
            // Hand tracker is shared: locking before Cmd makes server CanStartSwing fail immediately
            // (cooldown UI, no connect / hitmarker). ServerBeginSwing + recovery TargetRpc lock instead.
            if (!IsServer)
            {
                BeginLocalSwingCycle(hand, hit.Profile);
            }

            TryPlayMeleeSwingTelegraph(hit);
            // No world-space LoadingBar — windup is swing telegraph; recovery is reticle lock-on recharge.
            TrySyncMeleeAimToServer();
            CmdRunMeleeSwing();
            return true;
        }

        /// <summary>
        /// Harm primary fire when the selected hand holds a <see cref="RangedWeaponItemExtension"/>.
        /// Instant hitscan — no windup; reload/cooldown pace the gun.
        /// </summary>
        [Client]
        private bool TryRunRangedFirePrimary()
        {
            if (!TryGetHeldRangedWeapon(out Hand hand, out RangedWeaponItemExtension weapon))
            {
                return false;
            }

            weapon.ServerCompleteReloadIfDue();

            // Empty mag → start reload instead of falling through to melee with the rifle.
            if (weapon.RoundsRemaining <= 0)
            {
                if (weapon.CanStartReload())
                {
                    TryRunRangedReloadPrimary();
                }

                return true;
            }

            if (!weapon.CanStartFire())
            {
                return true;
            }

            if (!IsServer)
            {
                weapon.BeginLocalFireCooldown();
            }

            TrySyncMeleeAimToServer();
            CmdRunRangedFire();
            return true;
        }

        [Client]
        private bool TryRunRangedReloadPrimary()
        {
            if (!TryGetHeldRangedWeapon(out _, out RangedWeaponItemExtension weapon))
            {
                return false;
            }

            if (!weapon.CanStartReload())
            {
                return false;
            }

            if (!IsServer)
            {
                weapon.BeginLocalReload(weapon.Profile.ReloadSeconds);
            }

            CmdRunRangedReload();
            return true;
        }

        [ServerOrClient]
        private bool TryGetHeldRangedWeapon(out Hand hand, out RangedWeaponItemExtension weapon)
        {
            hand = null;
            weapon = null;

            Hands hands = GetComponent<Hands>();
            hand = hands != null ? hands.SelectedHand : null;
            if (hand == null)
            {
                return false;
            }

            Item item = hand.ItemInHand;
            if (item == null || !item.TryGetComponent(out weapon))
            {
                weapon = null;
                return false;
            }

            return true;
        }

        [ServerRpc]
        private void CmdRunRangedFire()
        {
            if (_currentIntent != IntentType.Harm)
            {
                TargetRejectInteraction(Owner);
                return;
            }

            if (!TryGetHeldRangedWeapon(out _, out RangedWeaponItemExtension weapon))
            {
                TargetRejectInteraction(Owner);
                return;
            }

            weapon.ServerCompleteReloadIfDue();

            if (weapon.RoundsRemaining <= 0)
            {
                if (weapon.ServerTryBeginReload())
                {
                    ServerNotifyRangedReloadStarted(weapon);
                }

                return;
            }

            if (!weapon.CanStartFire() || !weapon.ServerTryConsumeRound())
            {
                TargetRejectInteraction(Owner);
                return;
            }

            StaminaController stamina = GetComponent<StaminaController>();
            if (weapon.Profile.StaminaCost > 0f)
            {
                stamina?.ServerDepleteStamina(weapon.Profile.StaminaCost);
            }

            if (!TryGetMeleeAimRay(out Ray aimRay))
            {
                // Fall back to entity facing if aim never synced.
                Entity entity = GetComponent<Entity>();
                Vector3 origin = entity != null
                    ? entity.transform.position + Vector3.up * 1.5f
                    : transform.position + Vector3.up * 1.5f;
                Vector3 direction = entity != null ? entity.transform.forward : transform.forward;
                aimRay = new Ray(origin, direction);
            }

            PlayGunfireSound(aimRay.origin);

            float maxRange = Mathf.Max(1f, weapon.Profile.MaxRangeMeters);
            float aimDistance = maxRange;
            if (Physics.Raycast(aimRay, out RaycastHit aimHit, maxRange, ~0, QueryTriggerInteraction.Ignore))
            {
                aimDistance = aimHit.distance;
            }

            float horizontalSpeed = GetHorizontalMoveSpeed();
            float exertionPenalty = stamina?.ExertionPenalty ?? 0f;
            float spread = weapon.CurrentSpreadDegrees(horizontalSpeed, aimDistance, exertionPenalty);
            var rng = new System.Random(unchecked(Environment.TickCount ^ GetInstanceID() ^ weapon.RoundsRemaining));

            HumanHealthController selfHealth = GetComponentInChildren<HumanHealthController>();
            bool resolved = RangedHitscanResolver.TryResolveShot(
                aimRay,
                weapon.Profile,
                spread,
                rng,
                selfHealth,
                out HumanHealthController health,
                out BodyZone zone,
                out TileCoord structuralCoord,
                out bool hitLiving,
                out bool hitStructural,
                out Vector3 impactPoint,
                out bool hasImpact,
                out Vector3 shotDirection);

            bool landed = false;
            if (resolved && hitLiving && health != null)
            {
                health.ApplyDamage(zone, weapon.Profile.ToDamagePacket());
                landed = true;
            }
            else if (resolved && hitStructural)
            {
                float force = weapon.Profile.ResolveStructuralForce();
                if (force > 0f
                    && SubSystems.TryGet(out StructuralDamageSubSystem structural)
                    && structural.TryApplyStructuralDamage(structuralCoord, force, StructuralDamageSource.Ranged))
                {
                    landed = true;
                }
            }

            ClearMeleeAimPoint();
            ServerNotifyRangedFireState(weapon, landed, hasImpact, impactPoint, shotDirection);
        }

        [ServerRpc]
        private void CmdRunRangedReload()
        {
            if (!TryGetHeldRangedWeapon(out _, out RangedWeaponItemExtension weapon))
            {
                TargetRejectInteraction(Owner);
                return;
            }

            if (!weapon.ServerTryBeginReload())
            {
                TargetRejectInteraction(Owner);
                return;
            }

            ServerNotifyRangedReloadStarted(weapon);
        }

        /// <summary>
        /// Positional gunshot report (audio.md §3) — a side effect of the existing fire event, not a
        /// new trigger. Occlusion/falloff come free from the pool's <c>AudioSourceOcclusion</c>.
        /// </summary>
        [Server]
        private void PlayGunfireSound(Vector3 position)
        {
            string[] clips = CombatAudioTrackIds.GunFire;
            string clipId = clips[UnityEngine.Random.Range(0, clips.Length)];
            float pitch = UnityEngine.Random.Range(0.95f, 1.05f);
            SubSystems.Get<AudioSubSystem>()?.PlayAudioSource(AudioType.Sfx, clipId, position, null, false, 0.9f, pitch);
        }

        [Server]
        public void ServerNotifyRangedReloadStarted(RangedWeaponItemExtension weapon)
        {
            if (Owner == null || weapon == null)
            {
                return;
            }

            SubSystems.Get<AudioSubSystem>()?.PlayAudioSource(
                AudioType.Sfx, CombatAudioTrackIds.ReloadMagazineOut, transform.position, null);

            TargetNotifyRangedReload(
                Owner,
                weapon.Profile.ReloadSeconds,
                weapon.RoundsRemaining,
                weapon.RecoilStacks);
        }

        [Server]
        private void ServerNotifyRangedFireState(
            RangedWeaponItemExtension weapon,
            bool landed,
            bool hasImpact,
            Vector3 impactPoint,
            Vector3 shotDirection)
        {
            if (Owner == null || weapon == null)
            {
                return;
            }

            weapon.GetMuzzleWorldPose(out Vector3 muzzlePosition, out Vector3 muzzleForward);
            ObserversNotifyMuzzleFlash(muzzlePosition, muzzleForward);

            TargetNotifyRangedFireState(
                Owner,
                weapon.Profile.FireCooldownSeconds,
                weapon.RoundsRemaining,
                weapon.RecoilStacks,
                landed,
                hasImpact,
                impactPoint,
                shotDirection);
        }

        /// <summary>
        /// Diegetic muzzle flash for all observers (not owner-only TargetRpc impact chrome).
        /// </summary>
        [ObserversRpc(RunLocally = true)]
        private void ObserversNotifyMuzzleFlash(Vector3 worldPosition, Vector3 worldForward)
        {
            MuzzleFlashVfx.Play(worldPosition, worldForward);
        }

        [TargetRpc]
        private void TargetNotifyRangedFireState(
            NetworkConnection connection,
            float cooldownSeconds,
            int rounds,
            float recoilStacks,
            bool landed,
            bool hasImpact,
            Vector3 impactPoint,
            Vector3 shotDirection)
        {
            if (TryGetHeldRangedWeapon(out _, out RangedWeaponItemExtension weapon))
            {
                weapon.BeginLocalFireCooldown();
                weapon.ClientSetRounds(rounds);
                weapon.ClientSetRecoil(recoilStacks);
            }

            if (hasImpact)
            {
                RangedShotFeedback.NotifyLocalShotImpact(impactPoint, landed, shotDirection);
            }
            else if (landed)
            {
                MeleeConnectFeedback.NotifyLocalConnectHitLanded();
            }
        }

        [TargetRpc]
        private void TargetNotifyRangedReload(
            NetworkConnection connection,
            float reloadSeconds,
            int rounds,
            float recoilStacks)
        {
            if (TryGetHeldRangedWeapon(out _, out RangedWeaponItemExtension weapon))
            {
                weapon.BeginLocalReload(reloadSeconds);
                weapon.ClientSetRounds(rounds);
                weapon.ClientSetRecoil(recoilStacks);
            }
        }

        [ServerOrClient]
        private float GetHorizontalMoveSpeed()
        {
            if (TryGetComponent(out CharacterController character) && character != null)
            {
                Vector3 v = character.velocity;
                v.y = 0f;
                return v.magnitude;
            }

            return 0f;
        }

        [Client]
        private static void BeginLocalSwingCycle(Hand hand, MeleeWeaponProfile profile)
        {
            if (hand == null)
            {
                return;
            }

            if (!hand.TryGetComponent(out MeleeRecoveryTracker tracker))
            {
                tracker = hand.gameObject.AddComponent<MeleeRecoveryTracker>();
            }

            tracker.BeginSwingCycle(profile.WindupSeconds, profile.RecoverySeconds);
        }

        [ServerRpc]
        private void CmdRunMeleeSwing()
        {
            // Harm whitelist uses the server SyncVar — unrestricted verbs are Help-default;
            // MeleeHitInteraction opts in via IIntentRestrictedInteraction.
            if (!TryCreateMeleeHitInteraction(out MeleeHitInteraction hit, out Hand hand))
            {
                TargetRejectInteraction(Owner);
                return;
            }

            if (!InteractionPipeline.MatchesIntent(hit, _currentIntent))
            {
                Log.Warning(this, "Rejected melee swing — intent {intent} is not Harm-whitelisted",
                    Logs.Generic, _currentIntent);
                TargetRejectInteraction(Owner);
                return;
            }

            if (!hit.CanStartSwing(hand))
            {
                TargetRejectInteraction(Owner);
                return;
            }

            // Do not use InteractionSource.Interact / DelayedInteraction for Harm primary.
            // Connect is scheduled on this controller so it cannot be skipped when Hand/Item
            // Update fails to tick through StartDelayed.
            float effectiveWindupSeconds = hit.ServerBeginSwing(hand);
            ServerScheduleMeleeConnect(hand, hit.Profile, effectiveWindupSeconds);

            _meleeSwingSerial++;
            RpcExecuteMeleeSwing(_meleeSwingSerial);
        }

        [ObserversRpc(RunLocally = true)]
        private void RpcExecuteMeleeSwing(int swingId)
        {
            if (!IsOwner)
            {
                return;
            }

            // No client DelayedInteraction (CreateClient is null) — keep an id so cancel/aim
            // paths have a handle; aim sync itself keys off MeleeRecoveryTracker.IsBusy.
            if (!TryCreateMeleeHitInteraction(out _, out Hand hand))
            {
                return;
            }

            _clientActiveSource = hand;
            _clientActiveReferenceId = swingId;
            TrySyncMeleeAimToServer();
        }

        [Server]
        private void ServerScheduleMeleeConnect(Hand hand, MeleeWeaponProfile profile, float windupSeconds)
        {
            _pendingMeleeHand = hand;
            _pendingMeleeProfile = profile;
            _pendingMeleeConnectAt = Time.time + Mathf.Max(0.01f, windupSeconds);
        }

        [Server]
        private void ClearPendingMeleeConnect()
        {
            _pendingMeleeConnectAt = -1f;
            _pendingMeleeHand = null;
            _pendingMeleeProfile = default;
        }

        private void HandleServerMeleeConnectUpdate(ref EventContext context, in UpdateEvent updateEvent)
        {
            if (!IsServer || _pendingMeleeConnectAt < 0f || Time.time < _pendingMeleeConnectAt)
            {
                return;
            }

            Hand hand = _pendingMeleeHand;
            MeleeWeaponProfile profile = _pendingMeleeProfile;
            ClearPendingMeleeConnect();

            // Re-check Harm whitelist at connect — Help mid-windup should not apply damage.
            if (_currentIntent != IntentType.Harm)
            {
                ClearMeleeAimPoint();
                return;
            }

            if (hand == null)
            {
                return;
            }

            var hit = new MeleeHitInteraction(profile);
            hit.ServerApplyConnect(hand, this);
        }

        /// <summary>
        /// Builds a melee hit from the active tool profile, but always returns the Hand that must
        /// host the delayed interaction (never the held Item).
        /// </summary>
        [ServerOrClient]
        private bool TryCreateMeleeHitInteraction(out MeleeHitInteraction hit, out Hand hand)
        {
            hit = null;
            hand = null;

            IInteractionSource source = GetActiveInteractionSource();
            if (source == null)
            {
                return false;
            }

            hand = ResolveSwingHand(source);
            if (hand == null)
            {
                return false;
            }

            // Keep tool→hand Source wired for any ResolveHand walks that still expect it.
            if (source is Item item)
            {
                item.Source = hand;
            }

            MeleeWeaponProfile profile = ResolveMeleeProfile(source);
            hit = new MeleeHitInteraction(profile);
            return true;
        }

        [ServerOrClient]
        private static Hand ResolveSwingHand(IInteractionSource source)
        {
            if (source == null)
            {
                return null;
            }

            if (source.GetRootSource() is Hand rootHand)
            {
                return rootHand;
            }

            return source.GetComponentInTree<Hand>();
        }

        [ServerOrClient]
        private static MeleeWeaponProfile ResolveMeleeProfile(IInteractionSource source)
        {
            if (source is Item item)
            {
                if (item.TryGetComponent(out MeleeWeaponItemExtension dedicated))
                {
                    return dedicated.Profile;
                }

                return MeleeWeaponProfile.Improvised;
            }

            return MeleeWeaponProfile.Fists;
        }

        [Client]
        public void RequestToggleIntent()
        {
            _ownerIntent = _ownerIntent == IntentType.Harm ? IntentType.Help : IntentType.Harm;
            CmdSetIntent(_ownerIntent);
            ApplyCombatModeForIntent(_ownerIntent);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (IsOwner)
            {
                _ownerIntent = _currentIntent;
                ApplyCombatModeForIntent(_ownerIntent);
            }
        }

        private void SyncIntent(IntentType oldValue, IntentType newValue, bool asServer)
        {
            if (IsOwner)
            {
                _ownerIntent = newValue;
                ApplyCombatModeForIntent(newValue);
            }
        }

        /// <summary>
        /// Harm always enters combat stance; Help returns to peaceful. Owner-driven Cmd.
        /// </summary>
        [Client]
        private void ApplyCombatModeForIntent(IntentType intent)
        {
            if (!IsOwner)
            {
                return;
            }

            if (!TryGetComponent(out HumanoidBodyStateMachine body))
            {
                return;
            }

            if (intent == IntentType.Harm)
            {
                HumanoidCombatMode stance = HumanoidCombatMode.Melee;
                if (TryGetComponent(out HumanoidBodyStateBridge bridge))
                {
                    stance = bridge.ResolveCombatStance();
                }

                if (body.CombatMode != stance)
                {
                    body.CmdSetCombatMode(stance);
                }
            }
            else if (body.CombatMode.IsCombat())
            {
                body.CmdSetCombatMode(HumanoidCombatMode.Peaceful);
            }
        }

        /// <summary>
        /// Plays melee swing telegraph on the owning client when Run Primary dispatches a Hit.
        /// Windup timing on the interaction matches <see cref="Combat.MeleeWeaponProfile.WindupSeconds"/>.
        /// </summary>
        private void TryPlayMeleeSwingTelegraph(IInteraction interaction)
        {
            if (interaction is not MeleeHitInteraction)
            {
                return;
            }

            if (!TryGetComponent(out HumanoidCombatController combat))
            {
                return;
            }

            combat.RequestAttack(AnimationTriggerId.AttackSwing);
        }

        [Client]
        private void HandleView(InputAction.CallbackContext callbackContext)
        {
            if (_armedSystem.IsArmed)
            {
                _armedSystem.Cancel();
                return;
            }

            if (InputInterface.IsPointerOverInterface())
            {
                return;
            }
            List<InteractionEntry> viableInteractions = FilterRadialInteractions(
                GetViableInteractionsFromSelection(out InteractionEvent interactionEvent));

            ViewTargetInteractions(viableInteractions, interactionEvent);
        }

        [Client]
        private void HandleUse(InputAction.CallbackContext callbackContext)
        {
            // Activate item in selected hand — reload takes priority for firearms.
            Hands hands = GetComponent<Hands>();
            if (hands == null)
            {
                return;
            }

            Item item = hands.SelectedHand.ItemInHand;
            if (item != null
                && item.TryGetComponent(out RangedWeaponItemExtension ranged)
                && ranged.CanStartReload())
            {
                TryRunRangedReloadPrimary();
                return;
            }

            if (item != null)
            {
                InteractInHand(item.gameObject, gameObject);
            }
        }

        /// <summary>
        /// Gets and opens the menu for a target's interactions
        /// </summary>
        /// <param name="viableInteractions"></param>
        /// <param name="interactionEvent"></param>
        [Client]
        private void ViewTargetInteractions(List<InteractionEntry> viableInteractions, InteractionEvent interactionEvent)
        {
            List<IInteraction> interactions = viableInteractions.Select(entry => entry.Interaction).ToList();

            if (interactions.Count <= 0) { return; }

            _radialView.SuppressLeftButtonForMenu();

            void handleInteractionSelected(IInteraction interaction)
            {
                _radialView.OnInteractionSelected -= handleInteractionSelected;

                if (!TryRouteRadialInteraction(interaction, interactionEvent, out _))
                {
                    return;
                }

                InteractionEntry entry = viableInteractions.Find(e => e.Interaction == interaction);
                if (entry.Interaction == null)
                {
                    return;
                }

                interactionEvent.Target = entry.Target ?? ResolveFallbackTarget(interactionEvent, entry);

                if (!TryGetNetworkTargetForDispatch(entry, interactionEvent, out NetworkObject networkTarget))
                {
                    return;
                }

                InteractionOptimisticFeedback.TryBeginDelayed(entry.Interaction, interactionEvent);
                InteractionOutlineView.TryBeginPending(entry.Interaction, interactionEvent);
                CmdRunInteraction(networkTarget, interactionEvent.Point, entry.Id.GenericName, entry.Id.TargetComponentIndex);
            }

            _radialView.SetInteractions(interactions, interactionEvent, Mouse.current.position.ReadValue());
            _radialView.OnInteractionSelected += handleInteractionSelected;
            _radialView.ShowInteractionsMenu();
        }

        /// <summary>
        /// Performs an in-hand interaction
        /// </summary>
        /// <param name="target">The target clicked on</param>
        /// <param name="source">The current selected item or the hands</param>
        /// <param name="showMenu">If a selection menu should be shown</param>
        [Client]
        public void InteractInHand(GameObject target, GameObject sourceObject, bool showMenu = false)
        {
            if (!sourceObject.TryGetComponent(out IInteractionSource source))
            {
                return;
            }

            InteractionEvent interactionEvent = new(source, null, source.GameObject.transform.position, Vector3.up);

            List<IInteractionTarget> targets = GetTargetsFromGameObject(source, target);
            List<InteractionEntry> entries = InteractionPipeline.GetViableInteractions(source, targets, interactionEvent, CurrentIntent);

            if (entries.Count < 1)
            {
                return;
            }

            interactionEvent.Target = entries[0].Target;
            List<IInteraction> interactions = entries.Select(entry => entry.Interaction).ToList();

            if (showMenu && interactions.Count > 0)
            {
                Vector3 mousePosition = Mouse.current.position.ReadValue();
                mousePosition.y = Mathf.Max(_radialView.MenuHeight, mousePosition.y);

                _radialView.SetInteractions(interactions, interactionEvent, mousePosition);

                void handleInteractionSelected(IInteraction interaction)
                {
                    _radialView.OnInteractionSelected -= handleInteractionSelected;

                    if (!TryRouteRadialInteraction(interaction, interactionEvent, out _))
                    {
                        return;
                    }

                    InteractionEntry entry = entries.Find(x => x.Interaction == interaction);
                    if (entry.Interaction == null)
                    {
                        return;
                    }

                    InteractionOptimisticFeedback.TryBeginDelayed(entry.Interaction, interactionEvent);
                    InteractionOutlineView.TryBeginPending(entry.Interaction, interactionEvent);
                    CmdRunInventoryInteraction(target, sourceObject, entry.Id.GenericName, entry.Id.TargetComponentIndex);
                }

                _radialView.OnInteractionSelected += handleInteractionSelected;
            }
            else
            {
                InteractionEntry firstEntry = entries.First();
                InteractionOptimisticFeedback.TryBeginDelayed(firstEntry.Interaction, interactionEvent);
                InteractionOutlineView.TryBeginPending(firstEntry.Interaction, interactionEvent);
                CmdRunInventoryInteraction(target, sourceObject, firstEntry.Id.GenericName, firstEntry.Id.TargetComponentIndex);
            }
        }

        [ServerRpc]
        private void CmdSetIntent(IntentType intent)
        {
            _currentIntent = intent;
        }

        [Client]
        private bool TryRouteRadialInteraction(IInteraction interaction, InteractionEvent interactionEvent, out string interactionName)
        {
            interactionName = interaction.GetName(interactionEvent);
            InteractionTier tier = interaction.GetInteractionTier(interactionEvent);

            if (tier == InteractionTier.Instant)
            {
                return true;
            }

            _armedSystem.Arm(interaction, interactionEvent, tier, interactionName);
            return false;
        }

        [Client]
        private ArmedTargetEvaluation EvaluateArmedTarget(Selectable selectable)
        {
            if (!_armedSystem.IsArmed)
            {
                return ArmedTargetEvaluation.None;
            }

            ArmedInteractionState state = _armedSystem.CurrentState;
            if (!TryBuildArmedTargetEvent(selectable, state.OriginEvent, out InteractionEvent targetEvent))
            {
                return ArmedTargetEvaluation.None;
            }

            bool isValid = ValidateArmedTarget(state, targetEvent);
            return new ArmedTargetEvaluation(true, isValid);
        }

        [Client]
        private bool TryResolveArmedInteraction()
        {
            ArmedInteractionState state = _armedSystem.CurrentState;
            if (state == null)
            {
                return false;
            }

            if (!_selectionSystem.TryGetCurrentSelectable(out Selectable selectable))
            {
                return false;
            }

            if (!TryBuildArmedTargetEvent(selectable, state.OriginEvent, out InteractionEvent targetEvent))
            {
                return false;
            }

            if (!ValidateArmedTarget(state, targetEvent))
            {
                return false;
            }

            List<InteractionEntry> viableInteractions = GetViableInteractionsFromTarget(
                selectable.gameObject,
                targetEvent.HasPoint,
                targetEvent.Point,
                targetEvent.Normal,
                out _);

            string genericName = state.Interaction.GetGenericName();
            InteractionEntry entry = viableInteractions.Find(e => e.Interaction.GetGenericName() == genericName);
            if (entry.Interaction == null)
            {
                return false;
            }

            targetEvent.Target = entry.Target;

            if (!TryGetNetworkTarget(targetEvent, out NetworkObject networkTarget))
            {
                return false;
            }

            InteractionOptimisticFeedback.TryBeginDelayed(entry.Interaction, targetEvent);
            InteractionOutlineView.TryBeginPending(entry.Interaction, targetEvent);
            _armedSystem.Cancel();
            CmdRunInteraction(networkTarget, targetEvent.Point, entry.Id.GenericName, entry.Id.TargetComponentIndex);
            return true;
        }

        [Client]
        private bool TryBuildArmedTargetEvent(Selectable selectable, InteractionEvent originEvent, out InteractionEvent targetEvent)
        {
            targetEvent = null;

            if (selectable == null || originEvent == null)
            {
                return false;
            }

            IInteractionSource source = originEvent.Source;
            if (!SelectionTargetUtility.TryResolveInteractionPoint(_camera, selectable, out Vector3 point, out Vector3 normal))
            {
                point = selectable.transform.position;
                normal = Vector3.up;
            }

            List<IInteractionTarget> targets = GetTargetsFromGameObject(source, selectable.gameObject);
            if (targets.Count < 1)
            {
                return false;
            }

            targetEvent = new InteractionEvent(source, targets[0], point, normal);
            return true;
        }

        [Client]
        private static bool ValidateArmedTarget(ArmedInteractionState state, InteractionEvent targetEvent)
        {
            if (state.Interaction is ITargetedInteraction targeted)
            {
                return targeted.CanTarget(state.OriginEvent, targetEvent);
            }

            return state.Interaction.CanInteract(targetEvent);
        }

        /// <summary>
        /// Runs an interaction (chosen on the client) on the server. For reasons of serialization and security, some code is re-run.
        /// </summary>
        [ServerRpc]
        private void CmdRunInteraction(NetworkObject target, Vector3 point, string genericName, int targetComponentIndex)
        {
            if (!TryValidateInteractionTarget(target, out GameObject targetGameObject))
            {
                return;
            }

            List<InteractionEntry> viableInteractions = GetViableInteractionsFromTarget(targetGameObject, point, out InteractionEvent interactionEvent);
            InteractionIdentifier id = new(genericName, targetComponentIndex);

            if (!TryResolveDispatchedInteraction(viableInteractions, id, out InteractionEntry interaction))
            {
                Log.Error(this, "Failed to resolve interaction {genericName} at target index {targetIndex} on {target}",
                    Logs.Generic, genericName, targetComponentIndex, targetGameObject);

                TargetRejectInteraction(Owner);
                return;
            }

            if (!TryValidateGameplayGates(interaction.Interaction, interactionEvent))
            {
                Log.Warning(this, "Rejected interaction {genericName} due to gameplay gates", Logs.Generic, genericName);
                TargetRejectInteraction(Owner);
                return;
            }

            interactionEvent.Target = interaction.Target;

            InteractionReference reference = interactionEvent.Source.Interact(interactionEvent, interaction.Interaction);
            TrackActiveInteraction(interactionEvent.Source, reference, interaction.Interaction);
            RpcExecuteClientInteraction(target, point, genericName, targetComponentIndex, reference.Id);
        }

        /// <summary>
        /// Confirms an interaction issued by a client
        /// </summary>
        [ObserversRpc(RunLocally = true)]
        private void RpcExecuteClientInteraction(NetworkObject target, Vector3 point, string genericName, int targetComponentIndex, int referenceId)
        {
            if (!IsOwner)
            {
                return;
            }

            try
            {
                if (!TryValidateInteractionTarget(target, out GameObject targetGameObject))
                {
                    return;
                }

                List<InteractionEntry> viableInteractions = GetViableInteractionsFromTarget(targetGameObject, point, out InteractionEvent interactionEvent);
                InteractionIdentifier id = new(genericName, targetComponentIndex);

                if (!TryResolveDispatchedInteraction(viableInteractions, id, out InteractionEntry interaction))
                {
                    Log.Warning(this, "Observer failed to resolve interaction {genericName} at target index {targetIndex}",
                        Logs.Generic, genericName, targetComponentIndex);

                    return;
                }

                interactionEvent.Target = interaction.Target;
                interactionEvent.Source.ClientInteract(interactionEvent, interaction.Interaction, new InteractionReference(referenceId));
                _clientActiveSource = interactionEvent.Source;
                _clientActiveReferenceId = referenceId;
            }
            finally
            {
                InteractionOutlineView.ClearPending();
            }
        }

        /// <summary>
        /// Gets all possible interactions from the shader selection pick on the client.
        /// </summary>
        [Client]
        private List<InteractionEntry> GetViableInteractionsFromSelection(out InteractionEvent interactionEvent)
        {
            IInteractionSource source = GetActiveInteractionSource();

            if (source == null)
            {
                interactionEvent = null;
                return new List<InteractionEntry>();
            }

            Selectable current = _selectionSystem.GetCurrentSelectable();
            if (current == null)
            {
                interactionEvent = null;
                return new List<InteractionEntry>();
            }

            bool hasPoint = SelectionTargetUtility.TryResolveInteractionPoint(_camera, current, out Vector3 point, out Vector3 normal);
            return GetViableInteractionsFromTarget(current.gameObject, hasPoint, point, normal, out interactionEvent);
        }

        /// <summary>
        /// Gets all possible interactions for a resolved target object and interaction point.
        /// </summary>
        [ServerOrClient]
        private List<InteractionEntry> GetViableInteractionsFromTarget(
            GameObject targetGameObject,
            bool hasPoint,
            Vector3 point,
            Vector3 normal,
            out InteractionEvent interactionEvent)
        {
            IInteractionSource source = GetActiveInteractionSource();

            if (source == null)
            {
                interactionEvent = null;
                return new List<InteractionEntry>();
            }

            List<IInteractionTarget> targets = GetTargetsFromGameObject(source, targetGameObject);
            interactionEvent = hasPoint
                ? new InteractionEvent(source, targets[0], point, normal)
                : new InteractionEvent(source, targets[0]);

            return InteractionPipeline.GetViableInteractions(source, targets, interactionEvent, CurrentIntent);
        }

        /// <summary>
        /// Resolves a client-dispatched interaction. Exact <see cref="InteractionIdentifier"/> match first;
        /// falls back to generic name when the client hovered a child Selectable (body part) but the RPC
        /// revalidates against the parent NetworkObject root (different target-component indices).
        /// </summary>
        private static bool TryResolveDispatchedInteraction(
            List<InteractionEntry> viableInteractions,
            InteractionIdentifier id,
            out InteractionEntry interaction)
        {
            if (InteractionEntry.TryResolve(viableInteractions, id, out interaction))
            {
                return true;
            }

            for (int i = 0; i < viableInteractions.Count; i++)
            {
                if (string.Equals(viableInteractions[i].Id.GenericName, id.GenericName, System.StringComparison.Ordinal))
                {
                    interaction = viableInteractions[i];
                    return true;
                }
            }

            interaction = default;
            return false;
        }

        /// <summary>
        /// RPC path: point was chosen on the client and sent over the wire (always treated as resolved).
        /// </summary>
        [ServerOrClient]
        private List<InteractionEntry> GetViableInteractionsFromTarget(GameObject targetGameObject, Vector3 point, out InteractionEvent interactionEvent)
        {
            return GetViableInteractionsFromTarget(targetGameObject, hasPoint: true, point, Vector3.zero, out interactionEvent);
        }

        [Client]
        private static bool TryGetNetworkTarget(InteractionEvent interactionEvent, out NetworkObject networkObject)
        {
            networkObject = null;

            if (interactionEvent?.Target == null)
            {
                return false;
            }

            return TryGetNetworkObject(interactionEvent.Target, out networkObject);
        }

        [Client]
        private bool TryGetNetworkTargetForDispatch(
            InteractionEntry entry,
            InteractionEvent interactionEvent,
            out NetworkObject networkObject)
        {
            if (TryGetNetworkTarget(interactionEvent, out networkObject))
            {
                return true;
            }

            if (entry.Target != null)
            {
                return false;
            }

            Selectable current = _selectionSystem.GetCurrentSelectable();
            if (current == null)
            {
                return false;
            }

            networkObject = current.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                networkObject = current.GetComponentInParent<NetworkObject>();
            }

            return networkObject != null;
        }

        [Client]
        private static bool TryGetNetworkObject(IInteractionTarget target, out NetworkObject networkObject)
        {
            networkObject = null;

            GameObject targetGameObject = null;
            if (target is IGameObjectProvider targetProvider)
            {
                targetGameObject = targetProvider.GameObject;
            }
            else if (target is Component targetComponent)
            {
                targetGameObject = targetComponent.gameObject;
            }

            if (targetGameObject == null)
            {
                return false;
            }

            networkObject = targetGameObject.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                networkObject = targetGameObject.GetComponentInParent<NetworkObject>();
            }

            return networkObject != null;
        }

        [Client]
        private IInteractionTarget ResolveFallbackTarget(InteractionEvent interactionEvent, InteractionEntry entry)
        {
            if (entry.Target != null)
            {
                return entry.Target;
            }

            if (interactionEvent?.Target != null)
            {
                return interactionEvent.Target;
            }

            Selectable current = _selectionSystem.GetCurrentSelectable();
            if (current == null)
            {
                return null;
            }

            IInteractionSource source = GetActiveInteractionSource();
            if (source == null)
            {
                return null;
            }

            List<IInteractionTarget> targets = GetTargetsFromGameObject(source, current.gameObject);
            return targets.Count > 0 ? targets[0] : null;
        }

        [Client]
        private static List<InteractionEntry> FilterRadialInteractions(List<InteractionEntry> interactions)
        {
            return interactions
                .Where(entry => entry.Interaction.GetGenericName() != ExamineInteractionName)
                .ToList();
        }

        [ServerOrClient]
        private static bool TryValidateInteractionTarget(NetworkObject target, out GameObject targetGameObject)
        {
            targetGameObject = null;

            if (target == null || !target.IsSpawned)
            {
                return false;
            }

            targetGameObject = target.gameObject;

            if (targetGameObject.GetComponent<Selectable>() == null && targetGameObject.GetComponentInChildren<Selectable>() == null)
            {
                return false;
            }

            return true;
        }

        [Client]
        private void RefreshInteractionOutline()
        {
            using (OutlinePerformanceMarker.Auto())
            {
                RefreshInteractionOutlineUnguarded();
            }
        }

        [Client]
        private void RefreshInteractionOutlineUnguarded()
        {
            Selectable current = _selectionSystem.GetCurrentSelectable();
            InteractionOutlineView.ClearPendingExcept(current);

            if (current == null || IsEntityOutlineExcluded(current))
            {
                ClearInteractionOutline();
                return;
            }

            if (InteractionOutlineView.IsPending(current))
            {
                if (current != _activeOutlineSelectable)
                {
                    ClearInteractionOutline();
                    _activeOutlineSelectable = current;
                    _activeOutlineView = InteractionOutlineView.GetOrCreate(current);
                }

                _activeOutlineView?.SetState(InteractionOutlineView.OutlineState.Pending);
                return;
            }

            if (current != _activeOutlineSelectable)
            {
                ClearInteractionOutline();
                _activeOutlineSelectable = current;
                _activeOutlineView = InteractionOutlineView.GetOrCreate(current);
            }

            if (_activeOutlineView == null)
            {
                return;
            }

            if (!TryEvaluateInteractability(current, out bool hasViableInteractions))
            {
                _activeOutlineView.SetState(InteractionOutlineView.OutlineState.Hidden);
                return;
            }

            InteractionOutlineView.OutlineState state = hasViableInteractions
                ? InteractionOutlineView.OutlineState.Available
                : InteractionOutlineView.OutlineState.Unavailable;

            _activeOutlineView.SetState(state);
        }

        /// <summary>
        /// Player-controlled entities use dedicated UIs (e.g. medical) instead of world interaction outlines.
        /// </summary>
        private static bool IsEntityOutlineExcluded(Selectable selectable)
        {
            return selectable.GetComponentInParent<Entity>() != null;
        }

        [Client]
        private bool TryEvaluateInteractability(Selectable selectable, out bool hasViableInteractions)
        {
            hasViableInteractions = false;

            IInteractionSource source = GetActiveInteractionSource();
            if (source == null)
            {
                return false;
            }

            bool hasPoint = SelectionTargetUtility.TryResolveInteractionPoint(_camera, selectable, out Vector3 point, out Vector3 normal);
            CollectTargetsInto(source, selectable.gameObject, _outlineTargets);

            InteractionEvent outlineEvent = hasPoint
                ? new InteractionEvent(source, null, point, normal)
                : new InteractionEvent(source, null);

            // Outline LateUpdate must not run full Discover (source-only Drop, ToArray, Filter lists).
            return InteractionPipeline.TryEvaluateOutlineInteractability(
                source,
                _outlineTargets,
                outlineEvent,
                CurrentIntent,
                out hasViableInteractions);
        }

        private void ClearInteractionOutline()
        {
            if (_activeOutlineView != null)
            {
                _activeOutlineView.SetState(InteractionOutlineView.OutlineState.Hidden);
            }

            _activeOutlineView = null;
            _activeOutlineSelectable = null;
        }

        /// <summary>
        /// Gets all valid interaction targets from a game object
        /// </summary>
        /// <param name="source">The source of the interaction</param>
        /// <param name="targetGameObject">The game objects the interaction targets are on</param>
        /// <returns>A list of all valid interaction targets</returns>
        [ServerOrClient]
        private List<IInteractionTarget> GetTargetsFromGameObject(IInteractionSource source, GameObject targetGameObject)
        {
            List<IInteractionTarget> targets = new();
            CollectTargetsInto(source, targetGameObject, targets);
            return targets;
        }

        [ServerOrClient]
        private static void CollectTargetsInto(
            IInteractionSource source,
            GameObject targetGameObject,
            List<IInteractionTarget> targets)
        {
            targets.Clear();

            // Interface GetComponents still allocates an array; avoid LINQ Where/ToList on top.
            IInteractionTarget[] components = targetGameObject.GetComponents<IInteractionTarget>();
            for (int i = 0; i < components.Length; i++)
            {
                IInteractionTarget target = components[i];
                if ((target as MonoBehaviour)?.enabled == false)
                {
                    continue;
                }

                if (!source.CanInteractWithTarget(target))
                {
                    continue;
                }

                targets.Add(target);
            }

            if (targets.Count < 1)
            {
                targets.Add(new InteractionTargetGameObject(targetGameObject));
            }
        }

        [ServerOrClient]
        private IInteractionSource GetActiveInteractionSource()
        {
            IHandsController handsController = GetComponent<IHandsController>();
            var interactionSource = handsController.GetActiveInteractionSource();

            return interactionSource;
        }

        [ServerRpc]
        private void CmdRunInventoryInteraction(GameObject target, GameObject sourceObject, string genericName, int targetComponentIndex)
        {
            if (!TryValidateInventorySource(sourceObject))
            {
                Log.Error(this, "Rejected inventory interaction from invalid source {source}", Logs.Generic, sourceObject);
                TargetRejectInteraction(Owner);
                return;
            }

            IInteractionSource source = sourceObject.GetComponent<IInteractionSource>();
            List<IInteractionTarget> targets = GetTargetsFromGameObject(source, target);
            InteractionEvent interactionEvent = new(source, null, source.GameObject.transform.position, Vector3.up);

            List<InteractionEntry> entries = InteractionPipeline.GetViableInteractions(source, targets, interactionEvent, CurrentIntent);

            InteractionIdentifier id = new(genericName, targetComponentIndex);

            if (!InteractionEntry.TryResolve(entries, id, out InteractionEntry chosenEntry))
            {
                Log.Error(target, "Failed to resolve inventory interaction {genericName} at target index {targetIndex}",
                    Logs.Generic, genericName, targetComponentIndex);

                TargetRejectInteraction(Owner);
                return;
            }

            if (!TryValidateGameplayGates(chosenEntry.Interaction, interactionEvent))
            {
                Log.Warning(this, "Rejected inventory interaction {genericName} due to gameplay gates", Logs.Generic, genericName);
                TargetRejectInteraction(Owner);
                return;
            }

            interactionEvent.Target = chosenEntry.Target;

            InteractionReference reference = interactionEvent.Source.Interact(interactionEvent, chosenEntry.Interaction);
            TrackActiveInteraction(source, reference, chosenEntry.Interaction);
            if (chosenEntry.Interaction is IClientInteractionSource)
            {
                RpcExecuteClientInventoryInteraction(target, sourceObject, genericName, targetComponentIndex, reference.Id);
            }
        }

        /// <summary>
        /// Executes the interaction client-side
        /// </summary>
        /// <param name="target"></param>
        /// <param name="sourceObject"></param>
        /// <param name="interactionName"></param>
        /// <param name="referenceId"></param>
        [ObserversRpc(RunLocally = true)]
        private void RpcExecuteClientInventoryInteraction(GameObject target, GameObject sourceObject, string genericName, int targetComponentIndex, int referenceId)
        {
            if (!IsOwner)
            {
                return;
            }

            try
            {
                IInteractionSource source = sourceObject.GetComponent<IInteractionSource>();
                List<IInteractionTarget> targets = GetTargetsFromGameObject(source, target);
                InteractionEvent interactionEvent = new(source, new InteractionTargetGameObject(target));
                List<InteractionEntry> entries = InteractionPipeline.GetViableInteractions(source, targets, interactionEvent, CurrentIntent);
                InteractionIdentifier id = new(genericName, targetComponentIndex);

                if (!InteractionEntry.TryResolve(entries, id, out InteractionEntry chosenInteraction))
                {
                    Log.Warning(this, "Observer failed to resolve inventory interaction {genericName} at target index {targetIndex}",
                        Logs.Generic, genericName, targetComponentIndex);

                    return;
                }

                interactionEvent.Target = chosenInteraction.Target;
                interactionEvent.Source.ClientInteract(interactionEvent, chosenInteraction.Interaction, new InteractionReference(referenceId));
                _clientActiveSource = source;
                _clientActiveReferenceId = referenceId;
            }
            finally
            {
                InteractionOutlineView.ClearPending();
            }
        }

        [ServerRpc]
        private void CmdCancelInteraction(int referenceId)
        {
            // Harm primary connect is controller-scheduled (not InteractionSource.Interact).
            ClearPendingMeleeConnect();

            if (_serverActiveReference == null || _serverActiveReference.Id != referenceId || _serverActiveSource == null)
            {
                return;
            }

            if (!_serverActiveSource.HasInteraction(_serverActiveReference))
            {
                ClearActiveInteractionTracking();
                return;
            }

            _serverActiveSource.CancelInteraction(_serverActiveReference);
            ClearActiveInteractionTracking();
        }

        [Server]
        private void TrackActiveInteraction(IInteractionSource source, InteractionReference reference, IInteraction interaction)
        {
            if (interaction is not IDelayedInteraction)
            {
                return;
            }

            _serverActiveReference = reference;
            _serverActiveSource = source;
        }

        [Server]
        private void ClearActiveInteractionTracking()
        {
            _serverActiveReference = null;
            _serverActiveSource = null;
            ClearPendingMeleeConnect();
            ClearMeleeAimPoint();
        }

        private void ClearClientActiveInteractionTracking()
        {
            _clientActiveReferenceId = -1;
            _clientActiveSource = null;
        }

        private void RefreshActiveInteractionTracking()
        {
            if (IsServer && _serverActiveReference != null && _serverActiveSource != null
                && !_serverActiveSource.HasInteraction(_serverActiveReference))
            {
                ClearActiveInteractionTracking();
            }

            if (IsClient && _clientActiveReferenceId >= 0 && _clientActiveSource != null)
            {
                var reference = new InteractionReference(_clientActiveReferenceId);

                if (!_clientActiveSource.HasInteraction(reference))
                {
                    ClearClientActiveInteractionTracking();
                }
            }
        }

        /// <summary>
        /// Client-synced camera aim ray for the active melee swing (matches zone reticle; not hand bone).
        /// </summary>
        [Server]
        public bool TryGetMeleeAimRay(out Ray aimRay)
        {
            aimRay = default;
            if (!_hasMeleeAimRay)
            {
                return false;
            }

            Vector3 direction = _meleeAimPoint - _meleeAimRayOrigin;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            aimRay = new Ray(_meleeAimRayOrigin, direction.normalized);
            return true;
        }

        [Server]
        public void ClearMeleeAimPoint()
        {
            _hasMeleeAimRay = false;
            _meleeAimRayOrigin = default;
            _meleeAimPoint = default;
        }

        private void TrySyncMeleeAimDuringSwing()
        {
            if (CurrentIntent != IntentType.Harm)
            {
                return;
            }

            // Melee CreateClient returns null — there is no client delayed interaction to keep
            // _clientActiveReferenceId alive. Sync while the hand recovery tracker is busy instead.
            Hands hands = GetComponent<Hands>();
            Hand hand = hands != null ? hands.SelectedHand : null;
            if (hand == null
                || !hand.TryGetComponent(out MeleeRecoveryTracker tracker)
                || !tracker.IsBusy)
            {
                return;
            }

            TrySyncMeleeAimToServer();
        }

        private void TrySyncMeleeAimToServer()
        {
            if (!IsOwner)
            {
                return;
            }

            if (!TryGetComponent(out HumanoidController humanoid))
            {
                return;
            }

            if (!humanoid.TryGetCombatAimRay(out Ray aimRay, out Vector3 aimPoint))
            {
                return;
            }

            CmdSyncMeleeAim(aimRay.origin, aimPoint);
        }

        [ServerRpc(RequireOwnership = true)]
        private void CmdSyncMeleeAim(Vector3 rayOrigin, Vector3 aimPoint)
        {
            _meleeAimRayOrigin = rayOrigin;
            _meleeAimPoint = aimPoint;
            _hasMeleeAimRay = true;
        }

        [TargetRpc]
        private void TargetRejectInteraction(NetworkConnection connection)
        {
            ClearClientActiveInteractionTracking();
            InteractionOptimisticFeedback.Clear(transform);
            InteractionOutlineView.ClearPending();
        }

        /// <summary>
        /// Server → owning client: melee swing cycle lock started (windup+recovery). Mirrors the
        /// server tracker onto the client Hand so CanStartSwing / HUD bracket recharge work off-host.
        /// </summary>
        [Server]
        public void ServerNotifyMeleeRecovery(Hand hand, float cycleSeconds)
        {
            if (Owner == null || cycleSeconds <= 0f)
            {
                return;
            }

            int handIndex = -1;
            if (hand != null && hand.HandsController is Hands hands)
            {
                handIndex = hands.PlayerHands.IndexOf(hand);
            }

            TargetNotifyMeleeRecovery(Owner, handIndex, cycleSeconds);
        }

        [TargetRpc]
        private void TargetNotifyMeleeRecovery(NetworkConnection connection, int handIndex, float cycleSeconds)
        {
            Hand hand = ResolveLocalHand(handIndex);
            if (hand != null)
            {
                if (!hand.TryGetComponent(out MeleeRecoveryTracker tracker))
                {
                    tracker = hand.gameObject.AddComponent<MeleeRecoveryTracker>();
                }

                // Full cycle already summed on the server (windup + recovery).
                tracker.BeginSwingCycle(0f, cycleSeconds);
            }

            MeleeRecoveryFeedback.NotifyLocalRecoveryStarted(cycleSeconds);
        }

        /// <summary>
        /// Server → owning client: melee connect applied damage. HUD cross-flash; whiffs stay silent.
        /// </summary>
        [Server]
        public void ServerNotifyMeleeConnectHit()
        {
            if (Owner == null)
            {
                return;
            }

            TargetNotifyMeleeConnectHit(Owner);
        }

        [TargetRpc]
        private void TargetNotifyMeleeConnectHit(NetworkConnection connection)
        {
            MeleeConnectFeedback.NotifyLocalConnectHitLanded();
        }

        private Hand ResolveLocalHand(int handIndex)
        {
            Hands hands = GetComponentInChildren<Hands>();
            if (hands == null)
            {
                return null;
            }

            if (handIndex >= 0 && handIndex < hands.PlayerHands.Count)
            {
                return hands.PlayerHands[handIndex];
            }

            return hands.SelectedHand;
        }

        private bool TryValidateGameplayGates(IInteraction interaction, InteractionEvent interactionEvent)
        {
            if (!InteractionPipeline.MatchesIntent(interaction, _currentIntent))
            {
                return false;
            }

            return interactionEvent.Source.CanExecuteInteraction(interaction);
        }

        private bool TryValidateInventorySource(GameObject sourceObject)
        {
            if (sourceObject == null)
            {
                return false;
            }

            if (sourceObject == gameObject)
            {
                return true;
            }

            if (sourceObject.transform.IsChildOf(transform))
            {
                return true;
            }

            Hands hands = GetComponent<Hands>();
            Item item = sourceObject.GetComponent<Item>();

            if (hands != null && item != null && hands.SelectedHand?.ItemInHand == item)
            {
                return true;
            }

            return false;
        }
    }
}
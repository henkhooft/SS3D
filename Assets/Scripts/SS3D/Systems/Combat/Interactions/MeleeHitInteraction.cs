using FishNet.Object;
using SS3D.Core;
using SS3D.Data;
using SS3D.Data.Generated;
using SS3D.Interactions;
using SS3D.Interactions.Extensions;
using SS3D.Interactions.Interfaces;
using SS3D.Systems.Entities;
using SS3D.Systems.Entities.Humanoid.Body;
using SS3D.Systems.Health;
using SS3D.Systems.Interactions;
using SS3D.Systems.Inventory.Containers;
using SS3D.Systems.Stamina;
using SS3D.Systems.StructuralDamage;
using SS3D.Systems.Tile;
using UnityEngine;

namespace SS3D.Systems.Combat.Interactions
{
    /// <summary>
    /// Harm-intent melee: click always starts windup → connect → recovery (and stamina cost).
    /// Zone damage is resolved only at the connect frame from current synced aim — not at click.
    /// </summary>
    public sealed class MeleeHitInteraction : DelayedInteraction, IInteractionTierProvider, ITargetedInteraction, IIntentRestrictedInteraction
    {
        private readonly MeleeWeaponProfile _profile;

        public MeleeHitInteraction(MeleeWeaponProfile profile)
        {
            _profile = profile;
            Delay = profile.WindupSeconds;
            CheckInterval = 0.1f;
            Icon = Assets.Get<Sprite>(AssetDatabases.InteractionIcons, InteractionIcons.Nuke);
        }

        public MeleeWeaponProfile Profile => _profile;

        public IntentType AllowedIntent => IntentType.Harm;

        public int Priority => 100;

        /// <summary>
        /// Melee uses swing telegraph + reticle lock-on recharge — never the world-space LoadingBar.
        /// </summary>
        public override IClientInteraction CreateClient(InteractionEvent interactionEvent) => null;

        /// <summary>Instant so Harm primary always runs the swing without arming.</summary>
        public InteractionTier GetTier(InteractionEvent interactionEvent) => InteractionTier.Instant;

        public override string GetName(InteractionEvent interactionEvent) => "Hit";

        public override string GetGenericName() => "Hit";

        /// <summary>
        /// Radial/armed targeting still requires a living zone under the cursor.
        /// Primary Harm click bypasses discovery and always swings (see InteractionController).
        /// </summary>
        public bool CanTarget(InteractionEvent originEvent, InteractionEvent targetEvent)
        {
            if (!CanStartSwing(originEvent.Source))
            {
                return false;
            }

            InteractionEvent combined = targetEvent.WithSource(originEvent.Source);
            HumanHealthController health = ResolveHealth(combined.Target);
            return health != null
                && ZoneTargetResolver.TryResolveCombatZone(combined, health, out _);
        }

        /// <summary>
        /// Continue gate for an in-flight swing — hand still valid. Must not require !IsBusy:
        /// <see cref="Start"/> locks the tracker for windup+recovery, and
        /// <see cref="DelayedInteraction.Update"/> re-checks CanInteract every CheckInterval
        /// (and again at delay expiry). Requiring !IsBusy cancelled every swing before connect.
        /// </summary>
        public override bool CanInteract(InteractionEvent interactionEvent)
        {
            return ResolveHand(interactionEvent?.Source) != null;
        }

        /// <summary>Gate for starting a new Harm swing (not for continuing windup).</summary>
        public bool CanStartSwing(IInteractionSource source)
        {
            Hand hand = ResolveHand(source);
            if (hand == null)
            {
                return false;
            }

            return GetRecoveryTracker(hand)?.IsBusy != true;
        }

        [Server]
        public override bool Start(InteractionEvent interactionEvent, InteractionReference reference)
        {
            CaptureStartPosition(interactionEvent);
            StartCounter();

            Hand hand = ResolveHand(interactionEvent.Source);
            if (hand != null)
            {
                ServerBeginSwing(hand);
            }

            return true;
        }

        /// <summary>
        /// Stamina + recovery lock for a Harm swing (primary or discovered Hit).
        /// </summary>
        [Server]
        public void ServerBeginSwing(Hand hand)
        {
            if (hand == null)
            {
                return;
            }

            TryConsumeSwingStamina(hand);
            float cycleSeconds = _profile.WindupSeconds + _profile.RecoverySeconds;
            GetOrCreateRecoveryTracker(hand).BeginSwingCycle(_profile.WindupSeconds, _profile.RecoverySeconds);
            InteractionController controller = hand.GetComponentInParent<InteractionController>();
            controller?.ServerNotifyMeleeRecovery(hand, cycleSeconds);
        }

        /// <summary>
        /// Connect-frame resolve: living zone first, else structural turf. Used by Harm primary
        /// (controller-scheduled) and by DelayedInteraction StartDelayed for discovered Hits.
        /// </summary>
        [Server]
        public void ServerApplyConnect(Hand hand, InteractionController controller)
        {
            if (hand == null)
            {
                return;
            }

            bool landed = false;
            if (TryResolveConnectHit(hand, out HumanHealthController health, out BodyZone zone))
            {
                health.ApplyDamage(zone, _profile.ToDamagePacket());
                landed = true;
            }
            else if (TryResolveStructuralConnect(hand, out TileCoord structuralCoord))
            {
                float force = _profile.ResolveStructuralForce();
                if (force > 0f
                    && SubSystems.TryGet(out StructuralDamageSubSystem structural)
                    && structural.TryApplyStructuralDamage(structuralCoord, force, StructuralDamageSource.Melee))
                {
                    landed = true;
                }
            }

            controller?.ClearMeleeAimPoint();
            if (landed)
            {
                controller?.ServerNotifyMeleeConnectHit();
            }
        }

        protected override void StartDelayed(InteractionEvent interactionEvent, InteractionReference reference)
        {
            Hand hand = ResolveHand(interactionEvent.Source);
            InteractionController controller = hand != null
                ? hand.GetComponentInParent<InteractionController>()
                : null;
            ServerApplyConnect(hand, controller);
        }

        public override void Cancel(InteractionEvent interactionEvent, InteractionReference reference)
        {
        }

        /// <summary>
        /// Combat swings must complete while walking — cancel-on-move would skip StartDelayed
        /// and never begin recovery, so walking felt like no cooldown.
        /// </summary>
        protected override bool CancelOnMove => false;

        /// <summary>
        /// Hands animate during the swing — if cancel-on-move is re-enabled, use entity root
        /// rather than the hand bone.
        /// </summary>
        protected override Vector3 ResolveMoveCheckPosition(InteractionEvent interactionEvent)
        {
            Hand hand = ResolveHand(interactionEvent.Source);
            if (hand != null)
            {
                Entity entity = hand.GetComponentInParent<Entity>();
                if (entity != null)
                {
                    return entity.transform.position;
                }
            }

            return base.ResolveMoveCheckPosition(interactionEvent);
        }

        private static bool TryResolveConnectHit(Hand hand, out HumanHealthController health, out BodyZone zone)
        {
            health = null;
            zone = BodyZone.Chest;

            if (!TryBuildConnectAimRay(hand, out Ray aimRay))
            {
                return false;
            }

            HumanHealthController selfHealth = hand.GetComponentInParent<HumanHealthController>();
            if (!ZoneTargetResolver.TryResolveHoverZone(
                    aimRay,
                    selfHealth,
                    out zone,
                    out health,
                    out _,
                    out Collider zoneCollider))
            {
                return false;
            }

            return ZoneTargetResolver.IsMeleeZoneReachInRange(
                hand.InteractionOrigin,
                hand.GetInteractionRange(),
                zoneCollider);
        }

        private static bool TryResolveStructuralConnect(Hand hand, out TileCoord coord)
        {
            coord = default;

            // Reach from entity root — swinging hand bone often sits past RangeLimit at connect.
            Entity entity = hand.GetComponentInParent<Entity>();
            Vector3 attackerPosition = entity != null
                ? entity.transform.position
                : hand.InteractionOrigin;

            if (TryBuildConnectAimRay(hand, out Ray aimRay)
                && MeleeStructuralHitResolver.TryResolve(
                    aimRay,
                    attackerPosition,
                    hand.GetInteractionRange(),
                    out coord))
            {
                return true;
            }

            // Facing-based adjacent wall (same as hurtstructure) when aim ray misses or never synced.
            if (entity != null)
            {
                return MeleeStructuralHitResolver.TryResolveAdjacent(
                    attackerPosition,
                    entity.transform.forward,
                    out coord);
            }

            return false;
        }

        private static bool TryBuildConnectAimRay(Hand hand, out Ray aimRay)
        {
            aimRay = default;

            // Match zone reticle: camera mouse ray synced during windup — not hand→aim (swing anim skews that).
            InteractionController controller = hand.GetComponentInParent<InteractionController>();
            if (controller != null && controller.TryGetMeleeAimRay(out aimRay))
            {
                return true;
            }

            Entity entity = hand.GetComponentInParent<Entity>();
            HumanoidBodyStateMachine body = hand.GetComponentInParent<HumanoidBodyStateMachine>();
            if (body == null || entity == null)
            {
                return false;
            }

            Vector3 origin = entity.transform.position + Vector3.up * 1.5f;
            Vector3 direction = AimDirectionFromYawPitch(body.AimYaw, body.AimPitch);
            if (direction.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            aimRay = new Ray(origin, direction.normalized);
            return true;
        }

        private static Vector3 AimDirectionFromYawPitch(float yawDegrees, float pitchDegrees)
        {
            float yaw = yawDegrees * Mathf.Deg2Rad;
            float pitch = pitchDegrees * Mathf.Deg2Rad;
            float cosPitch = Mathf.Cos(pitch);
            return new Vector3(
                Mathf.Sin(yaw) * cosPitch,
                Mathf.Sin(pitch),
                Mathf.Cos(yaw) * cosPitch);
        }

        private void TryConsumeSwingStamina(Hand hand)
        {
            if (_profile.StaminaCost <= 0f || hand == null)
            {
                return;
            }

            StaminaController stamina = hand.GetComponentInParent<StaminaController>();
            stamina?.ServerDepleteStamina(_profile.StaminaCost);
        }

        private static Hand ResolveHand(IInteractionSource source)
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

        private static HumanHealthController ResolveHealth(IInteractionTarget target)
        {
            if (target is not IGameObjectProvider targetBehaviour)
            {
                return null;
            }

            Entity entity = targetBehaviour.GameObject.GetComponentInParent<Entity>();
            return entity != null ? entity.GetComponentInChildren<HumanHealthController>() : null;
        }

        private static MeleeRecoveryTracker GetRecoveryTracker(Hand hand)
        {
            hand.TryGetComponent(out MeleeRecoveryTracker tracker);
            return tracker;
        }

        private static MeleeRecoveryTracker GetOrCreateRecoveryTracker(Hand hand)
        {
            if (!hand.TryGetComponent(out MeleeRecoveryTracker tracker))
            {
                tracker = hand.gameObject.AddComponent<MeleeRecoveryTracker>();
            }

            return tracker;
        }
    }
}

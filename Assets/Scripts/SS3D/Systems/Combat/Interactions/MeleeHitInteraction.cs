using FishNet.Object;
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

            InteractionEvent combined = new(originEvent.Source, targetEvent.Target, targetEvent.Point, targetEvent.Normal);
            HumanHealthController health = ResolveHealth(combined.Target);
            return health != null
                && ZoneTargetResolver.TryResolveCombatZone(combined, health, out _);
        }

        /// <summary>
        /// Start/continue gates only — no target or range. Connect resolves what (if anything) is hit.
        /// </summary>
        public override bool CanInteract(InteractionEvent interactionEvent)
        {
            return CanStartSwing(interactionEvent?.Source);
        }

        public bool CanStartSwing(IInteractionSource source)
        {
            Hand hand = ResolveHand(source);
            if (hand == null)
            {
                return false;
            }

            return GetRecoveryTracker(hand)?.IsRecovering != true;
        }

        [Server]
        public override bool Start(InteractionEvent interactionEvent, InteractionReference reference)
        {
            CaptureStartPosition(interactionEvent);
            StartCounter();
            TryConsumeSwingStamina(interactionEvent.Source);
            return true;
        }

        protected override void StartDelayed(InteractionEvent interactionEvent, InteractionReference reference)
        {
            Hand hand = ResolveHand(interactionEvent.Source);
            bool landed = false;
            if (hand != null
                && TryResolveConnectHit(hand, out HumanHealthController health, out BodyZone zone))
            {
                health.ApplyDamage(zone, _profile.ToDamagePacket());
                landed = true;
            }

            if (hand != null)
            {
                GetOrCreateRecoveryTracker(hand).BeginRecovery(_profile.RecoverySeconds);
                InteractionController controller = hand.GetComponentInParent<InteractionController>();
                controller?.ClearMeleeAimPoint();
                controller?.ServerNotifyMeleeRecovery(hand, _profile.RecoverySeconds);
                if (landed)
                {
                    controller?.ServerNotifyMeleeConnectHit();
                }
            }
        }

        public override void Cancel(InteractionEvent interactionEvent, InteractionReference reference)
        {
        }

        /// <summary>
        /// Hands animate during the swing — cancel-on-move must use the entity root, not the hand bone.
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

        private void TryConsumeSwingStamina(IInteractionSource source)
        {
            if (_profile.StaminaCost <= 0f)
            {
                return;
            }

            Hand hand = ResolveHand(source);
            StaminaController stamina = hand != null
                ? hand.GetComponentInParent<StaminaController>()
                : null;
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

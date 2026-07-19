using FishNet.Object;
using SS3D.Data;
using SS3D.Data.Generated;
using SS3D.Interactions;
using SS3D.Interactions.Extensions;
using SS3D.Interactions.Interfaces;
using SS3D.Systems.Entities;
using SS3D.Systems.Entities.Humanoid;
using SS3D.Systems.Health;
using SS3D.Systems.Inventory.Containers;
using UnityEngine;

namespace SS3D.Systems.Combat.Interactions
{
    /// <summary>
    /// Harm-intent melee: windup → zone damage → recovery. Swing telegraph is played by
    /// <see cref="SS3D.Systems.Interactions.InteractionController"/> via <see cref="HumanoidCombatController.RequestAttack"/>.
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

        public InteractionTier GetTier(InteractionEvent interactionEvent) => InteractionTier.Targeted;

        public override string GetName(InteractionEvent interactionEvent) => "Hit";

        public override string GetGenericName() => "Hit";

        public bool CanTarget(InteractionEvent originEvent, InteractionEvent targetEvent)
        {
            InteractionEvent combined = new(originEvent.Source, targetEvent.Target, targetEvent.Point, targetEvent.Normal);
            HumanHealthController health = ResolveHealth(combined);
            return CanInteract(combined)
                && health != null
                && ZoneTargetResolver.TryResolveCombatZone(combined, health, out _);
        }

        public override bool CanInteract(InteractionEvent interactionEvent)
        {
            Hand hand = ResolveHand(interactionEvent.Source);
            if (hand == null)
            {
                return false;
            }

            if (GetRecoveryTracker(hand)?.IsRecovering == true)
            {
                return false;
            }

            if (ResolveHealth(interactionEvent) == null)
            {
                return false;
            }

            return InteractionExtensions.RangeCheck(interactionEvent);
        }

        [Server]
        public override bool Start(InteractionEvent interactionEvent, InteractionReference reference)
        {
            CaptureStartPosition(interactionEvent);
            StartCounter();
            return true;
        }

        protected override void StartDelayed(InteractionEvent interactionEvent, InteractionReference reference)
        {
            HumanHealthController health = ResolveHealth(interactionEvent);
            if (health == null)
            {
                return;
            }

            if (!ZoneTargetResolver.TryResolveCombatZone(interactionEvent, health, out BodyZone zone))
            {
                return;
            }

            health.ApplyDamage(zone, _profile.ToDamagePacket());

            Hand hand = ResolveHand(interactionEvent.Source);
            if (hand != null)
            {
                GetOrCreateRecoveryTracker(hand).BeginRecovery(_profile.RecoverySeconds);
            }
        }

        public override void Cancel(InteractionEvent interactionEvent, InteractionReference reference)
        {
        }

        private static Hand ResolveHand(IInteractionSource source)
        {
            if (source == null)
            {
                return null;
            }

            if (source.GetRootSource() is Hand hand)
            {
                return hand;
            }

            return source.GetComponentInTree<Hand>();
        }

        private static HumanHealthController ResolveHealth(InteractionEvent interactionEvent)
        {
            if (interactionEvent.Target is not IGameObjectProvider targetBehaviour)
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

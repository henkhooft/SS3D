using System;

namespace SS3D.Systems.Entities.Humanoid.Body
{
    /// <summary>
    /// Compact networked animation state replicated to all clients.
    /// Packed into a single uint for bandwidth efficiency.
    /// </summary>
    [Serializable]
    public struct BodyAnimationSnapshot : IEquatable<BodyAnimationSnapshot>
    {
        public BodyState State;
        public LocomotionMode Locomotion;
        public ArmHoldPose ArmHold;
        public AnimationTriggerId ActiveTrigger;
        /// <summary>0–2 melee swing cycle index when <see cref="ActiveTrigger"/> is AttackSwing.</summary>
        public byte AttackVariant;
        public HumanoidCombatMode CombatMode;
        public LimpSide LimpSide;
        public float AimYaw;
        public float AimPitch;
        public float MovementSpeed;
        public float InjuredArmLeft;
        public float InjuredArmRight;
        /// <summary>Max left/right leg brute fraction — drives limp idle severity and additive weight.</summary>
        public float InjuredLeg;
        public bool IsSeated;
        public bool IsCrawling;
        public bool IsFloating;
        public bool IsDragging;
        /// <summary>True when the active hand is Left — mirrors Upper Body Mixamo holds/swings.</summary>
        public bool MirrorUpperBody;

        public uint Pack()
        {
            uint packed = 0;
            packed |= (uint)State & 0x7;
            packed |= ((uint)Locomotion & 0x7) << 3;
            packed |= ((uint)ArmHold & 0x3) << 6;
            packed |= ((uint)ActiveTrigger & 0x7) << 8;
            packed |= ((uint)CombatMode & 0x3) << 11;
            packed |= ((uint)LimpSide & 0x3) << 13;
            if (IsSeated) packed |= 1u << 15;
            if (IsCrawling) packed |= 1u << 16;
            if (IsFloating) packed |= 1u << 17;
            if (IsDragging) packed |= 1u << 18;
            packed |= ((uint)AttackVariant & 0x3) << 19;
            if (MirrorUpperBody) packed |= 1u << 21;
            return packed;
        }

        public static BodyAnimationSnapshot Unpack(
            uint packed,
            float aimYaw,
            float aimPitch,
            float movementSpeed,
            float injuredArmLeft,
            float injuredArmRight,
            float injuredLeg)
        {
            return new BodyAnimationSnapshot
            {
                State = (BodyState)(packed & 0x7),
                Locomotion = (LocomotionMode)((packed >> 3) & 0x7),
                ArmHold = (ArmHoldPose)((packed >> 6) & 0x3),
                ActiveTrigger = (AnimationTriggerId)((packed >> 8) & 0x7),
                CombatMode = (HumanoidCombatMode)((packed >> 11) & 0x3),
                LimpSide = (LimpSide)((packed >> 13) & 0x3),
                IsSeated = (packed & (1u << 15)) != 0,
                IsCrawling = (packed & (1u << 16)) != 0,
                IsFloating = (packed & (1u << 17)) != 0,
                IsDragging = (packed & (1u << 18)) != 0,
                AttackVariant = (byte)((packed >> 19) & 0x3),
                MirrorUpperBody = (packed & (1u << 21)) != 0,
                AimYaw = aimYaw,
                AimPitch = aimPitch,
                MovementSpeed = movementSpeed,
                InjuredArmLeft = injuredArmLeft,
                InjuredArmRight = injuredArmRight,
                InjuredLeg = injuredLeg,
            };
        }

        public bool Equals(BodyAnimationSnapshot other)
        {
            return Pack() == other.Pack()
                && Math.Abs(AimYaw - other.AimYaw) < 0.01f
                && Math.Abs(AimPitch - other.AimPitch) < 0.01f
                && Math.Abs(MovementSpeed - other.MovementSpeed) < 0.01f
                && Math.Abs(InjuredArmLeft - other.InjuredArmLeft) < 0.01f
                && Math.Abs(InjuredArmRight - other.InjuredArmRight) < 0.01f
                && Math.Abs(InjuredLeg - other.InjuredLeg) < 0.01f;
        }

        public override bool Equals(object obj) => obj is BodyAnimationSnapshot other && Equals(other);

        public override int GetHashCode() => Pack().GetHashCode();

        public static BodyAnimationSnapshot Default => new()
        {
            State = BodyState.Locomotion,
            Locomotion = LocomotionMode.Idle,
            ArmHold = ArmHoldPose.Default,
            CombatMode = HumanoidCombatMode.Peaceful,
        };
    }
}

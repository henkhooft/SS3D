using SS3D.Systems.Atmospherics;
using SS3D.Systems.Entities.Humanoid.Body;
using UnityEngine;

namespace SS3D.Systems.Entities.Humanoid
{
    /// <summary>What a wind or blast knockdown does when its timer runs out.</summary>
    public enum KnockdownRecovery
    {
        /// <summary>The server can't tell yet whether the body is over the deck.</summary>
        Wait,
        StandUp,
        /// <summary>Over open space: get up into Floating instead of lying there for good (R2).</summary>
        Float,
    }

    /// <summary>
    /// Pure rules for moving in zero gravity (Documents/plans/drag-and-zero-g.plan.md R4–R6, Decisions 4–6). Coasting
    /// is planar, like the rest of space float: nothing here lifts a body off the deck plane. The numbers are owner
    /// question 4's first guesses, tuned at the hand test.
    /// </summary>
    public static class ZeroGravityRules
    {
        /// <summary>How far a floating body can reach a structure to push against it.</summary>
        public const float ReachRadius = 1.5f;

        /// <summary>Traction within reach is this share of walking acceleration ...</summary>
        public const float TractionShare = 0.25f;

        /// <summary>... and letting go of the keys stops a walking-speed coast in this long.</summary>
        public const float BrakeSeconds = 0.5f;

        public const float MinThrowDeltaV = 1f;
        public const float MaxThrowDeltaV = 2.5f;
        public const float ThrowDeltaVPerKilogram = 1.5f;

        /// <summary>Speed change for the thrower of an item of <paramref name="kilograms"/>: clamp(1 + 1.5 × kg, 1, 2.5) m/s.</summary>
        public static float ThrowDeltaV(float kilograms)
        {
            if (float.IsNaN(kilograms) || kilograms < 0f)
            {
                kilograms = 0f;
            }

            return Mathf.Clamp(MinThrowDeltaV + ThrowDeltaVPerKilogram * kilograms, MinThrowDeltaV, MaxThrowDeltaV);
        }

        /// <summary>The push on the thrower: against the throw's planar heading. Nothing for a throw straight up or down.</summary>
        public static Vector3 ThrowRecoil(Vector3 throwDirection, float kilograms)
        {
            throwDirection.y = 0f;
            if (throwDirection.sqrMagnitude < 0.0001f)
            {
                return Vector3.zero;
            }

            return -throwDirection.normalized * ThrowDeltaV(kilograms);
        }

        /// <summary>The coast after an impulse: added on the plane, then capped at <paramref name="maxSpeed"/> (run speed).</summary>
        public static Vector3 AddImpulse(Vector3 coast, Vector3 impulse, float maxSpeed)
        {
            if (!IsFinite(impulse))
            {
                return coast;
            }

            Vector3 next = coast + impulse;
            next.y = 0f;
            return Vector3.ClampMagnitude(next, maxSpeed);
        }

        /// <summary>
        /// One frame of grabbing on (R4). Within reach, held keys (<paramref name="inputDirection"/>, world space) push at
        /// <see cref="TractionShare"/> of <paramref name="walkAcceleration"/> up to <paramref name="walkSpeed"/>, without
        /// slowing a faster coast; no keys brake at <paramref name="walkSpeed"/> / <see cref="BrakeSeconds"/>. Out of
        /// reach the coast carries on unchanged.
        /// </summary>
        public static Vector3 TractionStep(Vector3 coast, Vector3 inputDirection, bool inReach, float walkSpeed,
            float walkAcceleration, float seconds)
        {
            if (!inReach)
            {
                return coast;
            }

            inputDirection.y = 0f;
            if (inputDirection.sqrMagnitude < 0.0001f)
            {
                float speed = coast.magnitude;
                float slowed = Mathf.Max(0f, speed - walkSpeed / BrakeSeconds * seconds);
                return slowed <= 0f ? Vector3.zero : coast * (slowed / speed);
            }

            float cap = Mathf.Max(coast.magnitude, walkSpeed);
            Vector3 next = coast + inputDirection.normalized * (walkAcceleration * TractionShare * seconds);
            next.y = 0f;
            return Vector3.ClampMagnitude(next, cap);
        }

        /// <summary>
        /// Reach means static station geometry (R5): tile objects such as walls, windows, grilles, airlocks, girders,
        /// lattices and floor edges. Draggable tile objects (lockers), items and bodies move, so they don't count.
        /// </summary>
        public static bool CountsAsReach(bool isTileObject, bool isDraggable, bool isItem, bool isBody) =>
            isTileObject && !isDraggable && !isItem && !isBody;

        /// <summary>
        /// A wind or blast knockdown that runs out over open space recovers into Floating (R2, Decision 2); it used to
        /// wait for floor support and so ragdolled forever. Only an unknown map still holds it down.
        /// </summary>
        public static KnockdownRecovery WindKnockdownRecovery(HumanoidSupportState support) => support switch
        {
            HumanoidSupportState.Supported => KnockdownRecovery.StandUp,
            HumanoidSupportState.Unsupported => KnockdownRecovery.Float,
            _ => KnockdownRecovery.Wait,
        };

        /// <summary>While Floating, wind pushes but never knocks down (R3, Decision 3).</summary>
        public static bool WindKnocksDown(float windSpeed, bool knockedDown, bool floating) =>
            !knockedDown && !floating && windSpeed >= AtmosWind.KnockdownSpeed;

        private static bool IsFinite(Vector3 v) =>
            !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)
            && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
    }
}

using NUnit.Framework;
using SS3D.Systems.Entities.Humanoid;
using UnityEngine;

namespace EditorTests
{
    /// <summary>Combat facing: big turns are capped, small aim corrections keep the slerp's ease.</summary>
    public sealed class CombatFacingTests
    {
        private const float Rate = 3.5f;
        private const float MaxDegreesPerSecond = 180f;
        private const float Tick = 1f / 60f;

        private static float DegreesTurnedInOneStep(float startAngle)
        {
            Quaternion start = Quaternion.identity;
            Quaternion target = Quaternion.Euler(0f, startAngle, 0f);
            Quaternion next = HumanoidController.StepCombatYaw(start, target, Rate, MaxDegreesPerSecond, Tick);
            return Quaternion.Angle(start, next);
        }

        [Test]
        public void FullTurn_IsCappedAtTheMaxSpeed()
        {
            Assert.AreEqual(MaxDegreesPerSecond * Tick, DegreesTurnedInOneStep(180f), 0.01f);
        }

        [Test]
        public void SmallCorrection_KeepsTheSlerpEase()
        {
            Assert.AreEqual(10f * Rate * Tick, DegreesTurnedInOneStep(10f), 0.01f);
        }

        [Test]
        public void FullTurn_TakesOverASecond_AndArrives()
        {
            Quaternion facing = Quaternion.identity;
            Quaternion target = Quaternion.Euler(0f, 180f, 0f);
            int ticks = 0;
            while (Quaternion.Angle(facing, target) > 5f && ticks < 600)
            {
                facing = HumanoidController.StepCombatYaw(facing, target, Rate, MaxDegreesPerSecond, Tick);
                ticks++;
            }

            Assert.Greater(ticks * Tick, 1f);
            Assert.Less(ticks * Tick, 2f);
        }
    }
}

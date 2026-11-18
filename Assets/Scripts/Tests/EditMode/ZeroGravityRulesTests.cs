using NUnit.Framework;
using SS3D.Systems.Atmospherics;
using SS3D.Systems.Entities.Humanoid;
using SS3D.Systems.Entities.Humanoid.Body;
using UnityEngine;

namespace EditorTests
{
    /// <summary>
    /// Moving in zero-g: throw-to-propel, impulses into the coast, traction and braking within reach, and what counts as
    /// reach (Documents/plans/drag-and-zero-g.plan.md R4–R6, Decisions 4–6; owner question 4's first-guess numbers).
    /// </summary>
    public sealed class ZeroGravityRulesTests
    {
        private const float Walk = 1.5f;
        private const float Run = 4f;
        private const float WalkAcceleration = 50f;

        [TestCase(0f, 1f)]
        [TestCase(0.2f, 1.3f)]
        [TestCase(0.5f, 1.75f)]
        [TestCase(1f, 2.5f)]
        [TestCase(5f, 2.5f)]
        public void ThrowDeltaV_GrowsWithWeightBetweenOneAndTwoAndAHalf(float kilograms, float expected)
        {
            Assert.AreEqual(expected, ZeroGravityRules.ThrowDeltaV(kilograms), 1e-4f);
        }

        [Test]
        public void ThrowDeltaV_NegativeOrNonFiniteWeightIsTheMinimum()
        {
            Assert.AreEqual(1f, ZeroGravityRules.ThrowDeltaV(-3f), 1e-4f);
            Assert.AreEqual(1f, ZeroGravityRules.ThrowDeltaV(float.NaN), 1e-4f);
        }

        [Test]
        public void Recoil_PushesTheThrowerTheOtherWayOnThePlane()
        {
            Vector3 recoil = ZeroGravityRules.ThrowRecoil(new Vector3(0f, 2f, 3f), 1f);

            Assert.AreEqual(0f, recoil.y, 1e-4f);
            Assert.AreEqual(-2.5f, recoil.z, 1e-4f);
            Assert.AreEqual(0f, recoil.x, 1e-4f);
        }

        [Test]
        public void Recoil_StraightUpOrDownGivesNothing()
        {
            Assert.AreEqual(Vector3.zero, ZeroGravityRules.ThrowRecoil(Vector3.up, 1f));
        }

        [Test]
        public void Impulse_AddsToTheCoast()
        {
            Vector3 coast = ZeroGravityRules.AddImpulse(new Vector3(1f, 0f, 0f), new Vector3(0f, 0f, 1f), Run);

            Assert.AreEqual(new Vector3(1f, 0f, 1f), coast);
        }

        [Test]
        public void Impulse_IsCappedAtRunSpeed()
        {
            Vector3 coast = ZeroGravityRules.AddImpulse(new Vector3(3.5f, 0f, 0f), new Vector3(2.5f, 0f, 0f), Run);

            Assert.AreEqual(Run, coast.magnitude, 1e-4f);
        }

        [Test]
        public void Impulse_DropsHeight_TheCoastStaysOnTheDeckPlane()
        {
            Vector3 coast = ZeroGravityRules.AddImpulse(Vector3.zero, new Vector3(1f, 5f, 0f), Run);

            Assert.AreEqual(0f, coast.y);
        }

        [Test]
        public void Impulse_NonFiniteIsIgnored()
        {
            Vector3 coast = new(1f, 0f, 0f);
            Assert.AreEqual(coast, ZeroGravityRules.AddImpulse(coast, new Vector3(float.NaN, 0f, 0f), Run));
        }

        [Test]
        public void Traction_OutOfReachChangesNothing()
        {
            Vector3 coast = new(0.7f, 0f, 0f);
            Vector3 next = ZeroGravityRules.TractionStep(coast, Vector3.forward, inReach: false, Walk, WalkAcceleration, 0.1f);

            Assert.AreEqual(coast, next);
        }

        [Test]
        public void Traction_InReachAcceleratesAtAQuarterOfWalking()
        {
            Vector3 next = ZeroGravityRules.TractionStep(Vector3.zero, Vector3.forward, inReach: true, Walk, WalkAcceleration, 0.01f);

            Assert.AreEqual(0.125f, next.z, 1e-4f); // 50 m/s² × 0.25 × 0.01 s
        }

        [Test]
        public void Traction_TopsOutAtWalkSpeed()
        {
            Vector3 v = Vector3.zero;
            for (int i = 0; i < 100; i++)
            {
                v = ZeroGravityRules.TractionStep(v, Vector3.forward, inReach: true, Walk, WalkAcceleration, 0.02f);
            }

            Assert.AreEqual(Walk, v.magnitude, 1e-3f);
        }

        [Test]
        public void Traction_DoesNotCutAFasterCoastInTheSameDirection()
        {
            Vector3 fast = new(0f, 0f, 3f);
            Vector3 next = ZeroGravityRules.TractionStep(fast, Vector3.forward, inReach: true, Walk, WalkAcceleration, 0.1f);

            Assert.AreEqual(3f, next.magnitude, 1e-4f);
        }

        [Test]
        public void Traction_CanTurnAFasterCoastAside()
        {
            Vector3 fast = new(0f, 0f, 3f);
            Vector3 next = ZeroGravityRules.TractionStep(fast, Vector3.right, inReach: true, Walk, WalkAcceleration, 0.1f);

            Assert.Greater(next.x, 0f);
            Assert.LessOrEqual(next.magnitude, 3f + 1e-4f);
        }

        [Test]
        public void Brake_StopsAWalkingCoastInHalfASecond()
        {
            Vector3 v = new(0f, 0f, Walk);
            v = ZeroGravityRules.TractionStep(v, Vector3.zero, inReach: true, Walk, WalkAcceleration, 0.25f);
            Assert.AreEqual(Walk * 0.5f, v.magnitude, 1e-4f);

            v = ZeroGravityRules.TractionStep(v, Vector3.zero, inReach: true, Walk, WalkAcceleration, 0.3f);
            Assert.AreEqual(Vector3.zero, v);
        }

        [Test]
        public void Brake_NeedsReach()
        {
            Vector3 v = new(0f, 0f, Walk);
            Assert.AreEqual(v, ZeroGravityRules.TractionStep(v, Vector3.zero, inReach: false, Walk, WalkAcceleration, 0.25f));
        }

        [Test]
        public void Reach_StaticTileGeometryCounts()
        {
            Assert.IsTrue(ZeroGravityRules.CountsAsReach(isTileObject: true, isDraggable: false, isItem: false, isBody: false));
        }

        [TestCase(true, true, false, false)] // a locker is a tile object but you can drag it
        [TestCase(false, false, true, false)] // a loose item
        [TestCase(false, false, false, true)] // another body
        [TestCase(false, false, false, false)] // something that isn't station geometry at all
        public void Reach_LockersItemsAndBodiesDoNot(bool tile, bool draggable, bool item, bool body)
        {
            Assert.IsFalse(ZeroGravityRules.CountsAsReach(tile, draggable, item, body));
        }

        [Test]
        public void KnockdownRecovery_OnTheDeckStandsUp()
        {
            Assert.AreEqual(KnockdownRecovery.StandUp, ZeroGravityRules.WindKnockdownRecovery(HumanoidSupportState.Supported));
        }

        [Test]
        public void KnockdownRecovery_OverOpenSpaceFloatsInsteadOfStayingDownForever()
        {
            Assert.AreEqual(KnockdownRecovery.Float, ZeroGravityRules.WindKnockdownRecovery(HumanoidSupportState.Unsupported));
        }

        [Test]
        public void KnockdownRecovery_WaitsWhileTheMapIsUnknown()
        {
            Assert.AreEqual(KnockdownRecovery.Wait, ZeroGravityRules.WindKnockdownRecovery(HumanoidSupportState.Unknown));
        }

        [Test]
        public void Wind_StrongGustKnocksDownAStandingBody()
        {
            Assert.IsTrue(ZeroGravityRules.WindKnocksDown(AtmosWind.KnockdownSpeed, knockedDown: false, floating: false));
        }

        [Test]
        public void Wind_NeverKnocksDownAFloatingBody()
        {
            Assert.IsFalse(ZeroGravityRules.WindKnocksDown(AtmosWind.MaxSpeed, knockedDown: false, floating: true));
        }

        [Test]
        public void Wind_WeakGustOrAlreadyDownDoesNotKnockDown()
        {
            Assert.IsFalse(ZeroGravityRules.WindKnocksDown(AtmosWind.KnockdownSpeed - 0.1f, knockedDown: false, floating: false));
            Assert.IsFalse(ZeroGravityRules.WindKnocksDown(AtmosWind.MaxSpeed, knockedDown: true, floating: false));
        }

        [Test]
        public void FloatingWind_PushesAStillBodyAlongTheWind()
        {
            Vector3 impulse = ZeroGravityRules.FloatingWindImpulse(new Vector3(3f, 0f, 0f), Vector3.zero, 0.1f);

            Assert.Greater(impulse.x, 0f);
            Assert.AreEqual(0f, impulse.z, 1e-4f);
        }

        [Test]
        public void FloatingWind_NeverBrakesABodyOutrunningTheAir()
        {
            Vector3 impulse = ZeroGravityRules.FloatingWindImpulse(new Vector3(1f, 0f, 0f), new Vector3(3f, 0f, 0f), 0.1f);

            Assert.GreaterOrEqual(impulse.x, 0f);
        }
    }
}

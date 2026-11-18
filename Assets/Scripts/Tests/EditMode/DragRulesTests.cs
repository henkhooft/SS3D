using NUnit.Framework;
using SS3D.Systems.Entities.Humanoid;
using SS3D.Systems.Furniture;
using UnityEngine;

namespace EditorTests
{
    /// <summary>
    /// The rope, break and eligibility rules for dragging bodies and furniture
    /// (Documents/plans/drag-and-zero-g.plan.md, Decisions 9, 12, 13, 26; owner question 4).
    /// </summary>
    public sealed class DragRulesTests
    {
        [Test]
        public void Rope_TautPullsTheTargetToRestDistanceAlongTheLine()
        {
            Vector3 dragger = Vector3.zero;
            Vector3 target = new(3f, 0f, 0f);

            Vector3 point = DragRules.TetherPoint(dragger, target, 1f);

            Assert.AreEqual(1f, point.x, 1e-4f);
            Assert.AreEqual(0f, point.z, 1e-4f);
        }

        [Test]
        public void Rope_IsIndependentOfFacing_TheTargetTrailsWhereverItLies()
        {
            Vector3 dragger = new(5f, 0f, 5f);
            Vector3 target = new(5f, 0f, 8f);

            Vector3 point = DragRules.TetherPoint(dragger, target, 1f);

            Assert.AreEqual(5f, point.x, 1e-4f);
            Assert.AreEqual(6f, point.z, 1e-4f);
        }

        [Test]
        public void Rope_SlackLeavesTheTargetWhereItIs()
        {
            Vector3 dragger = Vector3.zero;
            Vector3 target = new(0.4f, 0f, 0.3f);

            Assert.AreEqual(target, DragRules.TetherPoint(dragger, target, 1f));
        }

        [Test]
        public void Rope_KeepsTheTargetsHeight()
        {
            Vector3 dragger = new(0f, 1f, 0f);
            Vector3 target = new(4f, 0.1f, 0f);

            Assert.AreEqual(0.1f, DragRules.TetherPoint(dragger, target, 1f).y, 1e-4f);
        }

        [Test]
        public void RestDistance_BodiesTrailAtOneMetre()
        {
            Assert.AreEqual(1f, DragRules.RestDistance(isBody: true, startDistance: 2.2f), 1e-4f);
            Assert.AreEqual(1f, DragRules.RestDistance(isBody: true, startDistance: 0.3f), 1e-4f);
        }

        [Test]
        public void RestDistance_FurnitureKeepsItsStartDistanceUpToOneAndAHalfMetres()
        {
            Assert.AreEqual(0.8f, DragRules.RestDistance(isBody: false, startDistance: 0.8f), 1e-4f);
            Assert.AreEqual(1.5f, DragRules.RestDistance(isBody: false, startDistance: 2.6f), 1e-4f);
        }

        [Test]
        public void Break_SnagsOnlyAfterOneSecondPastTwoAndAHalfMetres()
        {
            float over = 0f;
            Assert.IsFalse(DragRules.TickBreak(ref over, 2.6f, 0.5f));
            Assert.IsFalse(DragRules.TickBreak(ref over, 2.6f, 0.4f));
            Assert.IsTrue(DragRules.TickBreak(ref over, 2.6f, 0.2f));
        }

        [Test]
        public void Break_ComingBackWithinRangeResetsTheTimer()
        {
            float over = 0f;
            DragRules.TickBreak(ref over, 3f, 0.9f);
            DragRules.TickBreak(ref over, 2f, 0.1f);

            Assert.IsFalse(DragRules.TickBreak(ref over, 3f, 0.9f));
        }

        [Test]
        public void Break_AtExactlyTheLimitDoesNotCount()
        {
            float over = 0f;
            Assert.IsFalse(DragRules.TickBreak(ref over, DragRules.BreakDistance, 5f));
        }

        [TestCase(BodyPresentationState.Downed, BodyPresentationState.Downed, true)]
        [TestCase(BodyPresentationState.Collapsed, BodyPresentationState.Collapsed, true)] // Critical
        [TestCase(BodyPresentationState.Dead, BodyPresentationState.Dead, true)]
        [TestCase(BodyPresentationState.Collapsed, BodyPresentationState.Downed, true)] // a Downed body knocked over
        [TestCase(BodyPresentationState.Locomotion, BodyPresentationState.Locomotion, false)] // standing
        [TestCase(BodyPresentationState.Collapsed, BodyPresentationState.Locomotion, false)] // a timed knockdown
        [TestCase(BodyPresentationState.Locomotion, BodyPresentationState.Downed, false)] // health says Downed, body still up
        public void Eligibility_OnlyDownedCriticalAndDeadBodies(
            BodyPresentationState presentation, BodyPresentationState healthIntent, bool expected)
        {
            Assert.AreEqual(expected, DragRules.MayBeDragged(presentation, healthIntent));
        }

        [Test]
        public void Eligibility_DeadStaysDraggableWhateverHealthSays()
        {
            Assert.IsTrue(DragRules.MayBeDragged(BodyPresentationState.Dead, BodyPresentationState.Locomotion));
        }

        [Test]
        public void Cone_SixtyDegreesEitherSideOfFacing()
        {
            Vector3 forward = Vector3.forward;
            Assert.IsTrue(DragRules.IsInDragCone(Vector3.zero, forward, new Vector3(0f, 0f, 1f)));
            Assert.IsTrue(DragRules.IsInDragCone(Vector3.zero, forward, Quaternion.Euler(0f, 55f, 0f) * Vector3.forward));
            Assert.IsFalse(DragRules.IsInDragCone(Vector3.zero, forward, Quaternion.Euler(0f, 70f, 0f) * Vector3.forward));
            Assert.IsFalse(DragRules.IsInDragCone(Vector3.zero, forward, new Vector3(0f, 0f, -1f)));
        }

        [Test]
        public void Cone_IgnoresHeight_HipsOnTheFloorStillCount()
        {
            // Hips lie well below eye level: a 3D angle would leave a body at your feet outside a 60° cone.
            Assert.IsTrue(DragRules.IsInDragCone(new Vector3(0f, 1f, 0f), Vector3.forward, new Vector3(0f, 0.1f, 0.6f)));
        }

        [Test]
        public void Cone_TargetUnderfootCounts()
        {
            Assert.IsTrue(DragRules.IsInDragCone(Vector3.zero, Vector3.forward, new Vector3(0f, -0.5f, 0f)));
        }

        [Test]
        public void End_NothingWrongKeepsDragging()
        {
            Assert.AreEqual(DragEndReason.None, DragRules.EvaluateEnd(Healthy()));
        }

        [Test]
        public void End_EachRuleEndsTheDrag()
        {
            DragEndInputs s = Healthy();
            s.Released = true;
            Assert.AreEqual(DragEndReason.Released, DragRules.EvaluateEnd(s));

            s = Healthy();
            s.TargetGone = true;
            Assert.AreEqual(DragEndReason.Despawned, DragRules.EvaluateEnd(s));

            s = Healthy();
            s.DraggerGone = true;
            Assert.AreEqual(DragEndReason.Despawned, DragRules.EvaluateEnd(s));

            s = Healthy();
            s.DraggerCanAct = false;
            Assert.AreEqual(DragEndReason.DraggerDown, DragRules.EvaluateEnd(s));

            s = Healthy();
            s.Snagged = true;
            Assert.AreEqual(DragEndReason.Snagged, DragRules.EvaluateEnd(s));

            s = Healthy();
            s.TargetStillDraggable = false;
            Assert.AreEqual(DragEndReason.TargetStood, DragRules.EvaluateEnd(s));

            s = Healthy();
            s.DraggerFloating = true;
            Assert.AreEqual(DragEndReason.Floating, DragRules.EvaluateEnd(s));

            s = Healthy();
            s.TargetFloating = true;
            Assert.AreEqual(DragEndReason.Floating, DragRules.EvaluateEnd(s));
        }

        [Test]
        public void Resend_OnlyWhenTheTargetIsWellOffTheRope()
        {
            Vector3 tether = new(1f, 0f, 0f);
            Assert.IsFalse(DragRules.NeedsResend(new Vector3(2f, 0f, 0f), tether));
            Assert.IsTrue(DragRules.NeedsResend(new Vector3(2.6f, 0f, 0f), tether));
        }

        private static DragEndInputs Healthy() => new()
        {
            DraggerCanAct = true,
            TargetStillDraggable = true,
        };
    }
}

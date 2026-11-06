using NUnit.Framework;
using SS3D.Systems.Vision;
using UnityEngine;

namespace EditorTests
{
    /// <summary>
    /// The FOV lights a little past each wall hit so the wall's own mesh stays visible. That slack
    /// has to stop inside the wall layer, or the floor behind it shows (issue #145). Gameplay reads
    /// the same depths through IsWithinDepths, so examine agrees with the black mask.
    /// </summary>
    public class VisionFovTests
    {
        // Measured from the first collider face to the far edge of the tile.
        private const float WallDepth = 1.0f; // SteelWall box: 1 m deep.
        private const float AirlockDepth = 0.9f; // Airlock box: 0.8 m deep, centered on a 1 m tile.

        // SteelWallI faces sit up to 0.11 m behind the collider.
        private const float WallFaceRecess = 0.11f;

        // Total slack before this fix, which stayed clear of glancing-angle wall blackout.
        private const float PreviousTotalSlack = 1.15f;

        private static readonly Vector3 WallNormal = Vector3.back;

        private static float TotalSlack(float degreesFromPerpendicular)
        {
            Vector3 ray = Quaternion.AngleAxis(degreesFromPerpendicular, Vector3.up) * Vector3.forward;
            return VisionSubSystem.OccluderSlack(ray, WallNormal) + VisionSubSystem.ShaderDepthSlack;
        }

        private static float DepthReachedIntoWall(float degreesFromPerpendicular)
        {
            return TotalSlack(degreesFromPerpendicular) * Mathf.Cos(degreesFromPerpendicular * Mathf.Deg2Rad);
        }

        [Test]
        public void Slack_NeverReachesPastTheFarEdgeOfAWallOrAirlockTile(
            [NUnit.Framework.Range(0f, 89f, 1f)] float degrees)
        {
            Assert.Less(DepthReachedIntoWall(degrees), WallDepth);
            Assert.Less(DepthReachedIntoWall(degrees), AirlockDepth);
        }

        [Test]
        public void Slack_NeverExceedsThePreviousConstant([NUnit.Framework.Range(0f, 89f, 1f)] float degrees)
        {
            Assert.LessOrEqual(TotalSlack(degrees), PreviousTotalSlack + 1e-4f);
        }

        [Test]
        public void Slack_MatchesThePreviousConstant_WhereRecessedFacesNeedIt(
            [Values(45f, 60f, 75f, 85f)] float degrees)
        {
            Assert.AreEqual(PreviousTotalSlack, TotalSlack(degrees), 1e-4f);
        }

        [Test]
        public void Slack_KeepsRecessedWallFacesLit_WhereItWasReduced(
            [NUnit.Framework.Range(0f, 40f, 1f)] float degrees)
        {
            float rayLengthToFace = WallFaceRecess / Mathf.Cos(degrees * Mathf.Deg2Rad);
            Assert.Greater(TotalSlack(degrees), rayLengthToFace);
        }

        private const float Range = 35f;

        private static bool IsVisible(float[] depths, Vector3 point, float coneWidth = 360f, float yaw = 0f)
        {
            return VisionSubSystem.IsWithinDepths(depths, depths.Length, Range, coneWidth, Vector3.zero, yaw, point);
        }

        private static float[] OpenExceptAhead(float metersAhead)
        {
            float[] depths = new float[360];
            for (int i = 0; i < depths.Length; i++)
                depths[i] = 1f;

            // A 360 degree cone starts behind the viewer, so bin 180 looks along yaw.
            depths[179] = depths[180] = metersAhead / Range;
            return depths;
        }

        [Test]
        public void IsWithinDepths_HidesAPointPastTheWallAhead()
        {
            float[] depths = OpenExceptAhead(3f);

            Assert.IsTrue(IsVisible(depths, new Vector3(0f, 0f, 2.9f)));
            Assert.IsFalse(IsVisible(depths, new Vector3(0f, 0f, 3.5f)));
        }

        [Test]
        public void IsWithinDepths_LeavesOtherDirectionsAlone()
        {
            float[] depths = OpenExceptAhead(3f);

            Assert.IsTrue(IsVisible(depths, new Vector3(0f, 0f, -10f)));
            Assert.IsTrue(IsVisible(depths, new Vector3(10f, 0f, 0f)));
        }

        [Test]
        public void IsWithinDepths_IgnoresHeight()
        {
            Assert.IsFalse(IsVisible(OpenExceptAhead(3f), new Vector3(0f, 9f, 3.5f)));
        }

        [Test]
        public void IsWithinDepths_FollowsYaw()
        {
            float[] depths = OpenExceptAhead(3f);

            Assert.IsFalse(IsVisible(depths, new Vector3(3.5f, 0f, 0f), yaw: 90f));
            Assert.IsTrue(IsVisible(depths, new Vector3(0f, 0f, 3.5f), yaw: 90f));
        }

        [Test]
        public void IsWithinDepths_OutsideANarrowCone_ShowsOnlyTheNearBubble()
        {
            float[] depths = { 1f, 1f, 1f, 1f };

            Assert.IsTrue(IsVisible(depths, new Vector3(0f, 0f, 5f), coneWidth: 90f));
            Assert.IsFalse(IsVisible(depths, new Vector3(0f, 0f, -5f), coneWidth: 90f));
            Assert.IsTrue(IsVisible(depths, new Vector3(0f, 0f, -0.5f), coneWidth: 90f));
        }

        [Test]
        public void Slack_FallsBackToTheCap_WithoutAHitNormal()
        {
            float slack = VisionSubSystem.OccluderSlack(Vector3.forward, Vector3.zero);

            Assert.AreEqual(PreviousTotalSlack, slack + VisionSubSystem.ShaderDepthSlack, 1e-4f);
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using SS3D.Rendering.URP;
using UnityEngine;

namespace EditorTests
{
    public class SelectionPickFrustumTests
    {
        [TearDown]
        public void TearDown()
        {
            SelectionPickContext.ClearRequest();
            SelectionPickContext.ClearAllSources();
        }

        [Test]
        public void IsInPickFrustum_WithoutRequest_FailsOpen()
        {
            SelectionPickContext.ClearRequest();
            Assert.IsTrue(SelectionPickContext.IsInPickFrustum(new Bounds(Vector3.zero, Vector3.one)));
        }

        [Test]
        public void IsNearPickCursor_WithoutRequest_FailsOpen()
        {
            SelectionPickContext.ClearRequest();
            Assert.IsTrue(SelectionPickContext.IsNearPickCursor(new Bounds(Vector3.zero, Vector3.one)));
        }

        [Test]
        public void IsNearPickCursor_WithRequestButNoScreen_FailsClosed()
        {
            var cameraGo = new GameObject("PickCursorFailClosedCamera");
            try
            {
                Camera camera = cameraGo.AddComponent<Camera>();
                SelectionPickContext.SetRequest(new SelectionPickContext.Request
                {
                    SourceCamera = camera,
                    Target = null,
                    DebugView = false,
                    HasScreenPosition = false,
                });

                var buffer = new List<SelectionPickContext.PickDraw>();
                SelectionPickContext.CollectPickDraws(buffer);

                Assert.IsFalse(
                    SelectionPickContext.IsNearPickCursor(new Bounds(Vector3.zero, Vector3.one)),
                    "Live pick request without a mouse ray must not draw the frustum");
            }
            finally
            {
                Object.DestroyImmediate(cameraGo);
            }
        }

        [Test]
        public void CollectPickDraws_RefreshesFrustumFromSourceCamera()
        {
            var cameraGo = new GameObject("PickFrustumTestCamera");
            try
            {
                Camera camera = cameraGo.AddComponent<Camera>();
                camera.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);
                camera.fieldOfView = 60f;
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = 100f;

                SelectionPickContext.SetRequest(new SelectionPickContext.Request
                {
                    SourceCamera = camera,
                    Target = null,
                    DebugView = false,
                });

                var buffer = new List<SelectionPickContext.PickDraw>();
                SelectionPickContext.CollectPickDraws(buffer);

                Assert.IsTrue(
                    SelectionPickContext.IsInPickFrustum(new Bounds(Vector3.zero, Vector3.one)),
                    "Origin should be inside a camera at z=-10 looking +Z");
                Assert.IsFalse(
                    SelectionPickContext.IsInPickFrustum(new Bounds(new Vector3(0f, 0f, -50f), Vector3.one)),
                    "Bounds behind the camera should be culled");
            }
            finally
            {
                Object.DestroyImmediate(cameraGo);
            }
        }

        [Test]
        public void CollectPickDraws_OnlyIncludesBoundsHitByCursorRay()
        {
            var cameraGo = new GameObject("PickRayIntersectCamera");
            try
            {
                Camera camera = cameraGo.AddComponent<Camera>();
                camera.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);
                camera.fieldOfView = 60f;
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = 100f;
                camera.pixelRect = new Rect(0f, 0f, 200f, 200f);

                SelectionPickContext.SetRequest(new SelectionPickContext.Request
                {
                    SourceCamera = camera,
                    Target = null,
                    DebugView = false,
                    ScreenPosition = new Vector2(100f, 100f),
                    HasScreenPosition = true,
                });

                var buffer = new List<SelectionPickContext.PickDraw>();
                SelectionPickContext.CollectPickDraws(buffer);

                Assert.IsTrue(
                    SelectionPickContext.IsNearPickCursor(new Bounds(Vector3.zero, Vector3.one * 2f)),
                    "Bounds on the center ray should be included");
                Assert.IsFalse(
                    SelectionPickContext.IsNearPickCursor(new Bounds(new Vector3(20f, 0f, 0f), Vector3.one)),
                    "Bounds off the cursor ray must be excluded");
            }
            finally
            {
                Object.DestroyImmediate(cameraGo);
            }
        }

        [Test]
        public void CollectPickDraws_WithoutCursorCull_InvokesAllRegisteredSources()
        {
            var onRay = new StubPickSource(Vector3.zero, Color.red);
            var offRay = new StubPickSource(new Vector3(80f, 0f, 0f), Color.blue);
            SelectionPickContext.RegisterSource(onRay);
            SelectionPickContext.RegisterSource(offRay);

            var buffer = new List<SelectionPickContext.PickDraw>();
            SelectionPickContext.CollectPickDraws(buffer);

            Assert.AreEqual(2, onRay.CollectCalls + offRay.CollectCalls,
                "EditMode / no request must fail-open and walk every source");
            Assert.AreEqual(1, onRay.CollectCalls);
            Assert.AreEqual(1, offRay.CollectCalls);
        }

        [Test]
        public void CollectPickDraws_WithCursorRay_SkipsSourcesFarFromRayCells()
        {
            var cameraGo = new GameObject("PickSpatialIndexCamera");
            try
            {
                Camera camera = cameraGo.AddComponent<Camera>();
                camera.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);
                camera.fieldOfView = 60f;
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = 100f;
                camera.pixelRect = new Rect(0f, 0f, 200f, 200f);

                // Center screen ray travels near world origin along +Z.
                var onRay = new StubPickSource(Vector3.zero, Color.red);
                // Far laterally — outside ray cells and Moore pad at 4m cell size.
                var offRay = new StubPickSource(new Vector3(80f, 0f, 0f), Color.blue);
                SelectionPickContext.RegisterSource(onRay);
                SelectionPickContext.RegisterSource(offRay);

                SelectionPickContext.SetRequest(new SelectionPickContext.Request
                {
                    SourceCamera = camera,
                    Target = null,
                    DebugView = false,
                    ScreenPosition = new Vector2(100f, 100f),
                    HasScreenPosition = true,
                });

                var buffer = new List<SelectionPickContext.PickDraw>();
                SelectionPickContext.CollectPickDraws(buffer);

                Assert.AreEqual(1, onRay.CollectCalls, "Source on the pick ray must be collected");
                Assert.AreEqual(0, offRay.CollectCalls,
                    "Spatial index must not invoke CollectPickDraws for sources far from the ray");
                Assert.AreEqual(1, buffer.Count);
                Assert.AreEqual((Color32)Color.red, buffer[0].Color);
            }
            finally
            {
                Object.DestroyImmediate(cameraGo);
            }
        }

        [Test]
        public void CollectPickDraws_AlwaysScanSource_CollectedEvenOffRayCells()
        {
            var cameraGo = new GameObject("PickAlwaysScanCamera");
            try
            {
                Camera camera = cameraGo.AddComponent<Camera>();
                camera.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);
                camera.fieldOfView = 60f;
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = 100f;
                camera.pixelRect = new Rect(0f, 0f, 200f, 200f);

                var mobile = new StubPickSource(new Vector3(80f, 0f, 0f), Color.green, preferAlwaysScan: true);
                SelectionPickContext.RegisterSource(mobile);

                SelectionPickContext.SetRequest(new SelectionPickContext.Request
                {
                    SourceCamera = camera,
                    Target = null,
                    DebugView = false,
                    ScreenPosition = new Vector2(100f, 100f),
                    HasScreenPosition = true,
                });

                var buffer = new List<SelectionPickContext.PickDraw>();
                SelectionPickContext.CollectPickDraws(buffer);

                Assert.AreEqual(1, mobile.CollectCalls,
                    "PreferAlwaysScan sources must still be invoked (final ray test may reject draws)");
            }
            finally
            {
                Object.DestroyImmediate(cameraGo);
            }
        }

        sealed class StubPickSource : SelectionPickContext.ISelectionPickSource
        {
            readonly Vector3 _center;
            readonly Color32 _color;
            readonly bool _preferAlwaysScan;

            public int CollectCalls { get; private set; }

            public StubPickSource(Vector3 center, Color color, bool preferAlwaysScan = false)
            {
                _center = center;
                _color = color;
                _preferAlwaysScan = preferAlwaysScan;
            }

            public Vector3 PickWorldCenter => _center;

            public bool PreferAlwaysScan => _preferAlwaysScan;

            public void CollectPickDraws(List<SelectionPickContext.PickDraw> buffer)
            {
                CollectCalls++;
                buffer.Add(new SelectionPickContext.PickDraw
                {
                    Mesh = null,
                    Matrix = Matrix4x4.identity,
                    Color = _color,
                    SubmeshIndex = 0,
                    Transparent = false,
                    SkinnedRenderer = null,
                });
            }
        }
    }
}

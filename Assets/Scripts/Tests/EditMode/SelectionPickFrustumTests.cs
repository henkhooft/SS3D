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
        }

        [Test]
        public void IsInPickFrustum_WithoutRequest_FailsOpen()
        {
            SelectionPickContext.ClearRequest();
            Assert.IsTrue(SelectionPickContext.IsInPickFrustum(new Bounds(Vector3.zero, Vector3.one)));
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
    }
}

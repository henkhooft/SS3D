using System.Collections.Generic;
using UnityEngine;

namespace SS3D.Rendering.URP
{
    /// <summary>
    /// Per-frame selection pick state consumed by <see cref="SelectionPickRendererFeature"/>.
    /// Selectables register as <see cref="ISelectionPickSource"/> so the pick pass can draw
    /// ID colours via transient MaterialPropertyBlocks without leaving MPBs on world MeshRenderers
    /// (which breaks SRP Batcher / GPU Instancing).
    /// </summary>
    public static class SelectionPickContext
    {
        public struct Request
        {
            public Camera SourceCamera;
            public RenderTexture Target;
            public bool DebugView;
        }

        /// <summary>One mesh draw for the offscreen pick pass.</summary>
        public struct PickDraw
        {
            public Mesh Mesh;
            public Matrix4x4 Matrix;
            public Color32 Color;
            public int SubmeshIndex;
            public bool Transparent;

            /// <summary>When set, use <c>DrawRenderer</c> (skinned) instead of <c>DrawMesh</c>.</summary>
            public Renderer SkinnedRenderer;
        }

        public interface ISelectionPickSource
        {
            void CollectPickDraws(List<PickDraw> buffer);
        }

        static Request? s_Request;
        static readonly List<ISelectionPickSource> s_Sources = new();
        static readonly Plane[] s_FrustumPlanes = new Plane[6];
        static bool s_HasFrustum;

        public static void SetRequest(Request request)
        {
            s_Request = request;
        }

        public static void ClearRequest()
        {
            s_Request = null;
            s_HasFrustum = false;
        }

        public static bool TryGetRequest(out Request request)
        {
            if (s_Request.HasValue && s_Request.Value.SourceCamera != null)
            {
                request = s_Request.Value;
                return true;
            }

            request = default;
            return false;
        }

        public static void RegisterSource(ISelectionPickSource source)
        {
            if (source == null || s_Sources.Contains(source))
                return;

            s_Sources.Add(source);
        }

        public static void UnregisterSource(ISelectionPickSource source)
        {
            if (source == null)
                return;

            s_Sources.Remove(source);
        }

        /// <summary>
        /// True when <paramref name="bounds"/> intersects the current pick-camera frustum
        /// (or when no frustum was refreshed — fail open so EditMode/tests still collect).
        /// </summary>
        public static bool IsInPickFrustum(Bounds bounds)
        {
            return !s_HasFrustum || GeometryUtility.TestPlanesAABB(s_FrustumPlanes, bounds);
        }

        public static void CollectPickDraws(List<PickDraw> buffer)
        {
            RefreshFrustumFromRequest();
            buffer.Clear();
            for (int i = 0; i < s_Sources.Count; i++)
            {
                ISelectionPickSource source = s_Sources[i];
                if (source == null)
                    continue;

                source.CollectPickDraws(buffer);
            }
        }

        static void RefreshFrustumFromRequest()
        {
            if (!TryGetRequest(out Request request) || request.SourceCamera == null)
            {
                s_HasFrustum = false;
                return;
            }

            GeometryUtility.CalculateFrustumPlanes(request.SourceCamera, s_FrustumPlanes);
            s_HasFrustum = true;
        }
    }
}

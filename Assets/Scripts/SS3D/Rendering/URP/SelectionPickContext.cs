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

        public static void SetRequest(Request request)
        {
            s_Request = request;
        }

        public static void ClearRequest()
        {
            s_Request = null;
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

        public static void CollectPickDraws(List<PickDraw> buffer)
        {
            buffer.Clear();
            for (int i = 0; i < s_Sources.Count; i++)
            {
                ISelectionPickSource source = s_Sources[i];
                if (source == null)
                    continue;

                source.CollectPickDraws(buffer);
            }
        }
    }
}

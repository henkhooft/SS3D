using System;
using Coimbra;
using System.Collections.Generic;
using SS3D.Core;
using SS3D.Systems.Area;
using UnityEngine;
using UnityEngine.Rendering;

namespace SS3D.Systems.Tile.MapEditor
{
    /// <summary>Client-local authoring overlay using the same membership and colors as area gizmos.</summary>
    public sealed class MapEditorAreaOverlay : IDisposable
    {
        private AreaFloorVisualCache _cache;
        private GameObject _root;
        private Material _material;
        private readonly List<Mesh> _meshes = new();
        private bool _dirty = true;
        public Dictionary<ushort, Vector3> LabelPositions { get; } = new();

        public void SetVisible(bool visible)
        {
            if (!visible)
            {
                if (_root != null) _root.SetActive(false);
                return;
            }
            if (!SubSystems.TryGet(out AreaSubSystem areas)) return;
            if (_cache != areas.FloorVisualCache)
            {
                if (_cache != null) _cache.OnDirty -= MarkDirty;
                _cache = areas.FloorVisualCache;
                _cache.OnDirty += MarkDirty;
                _dirty = true;
            }
            if (_root == null)
            {
                Shader shader = Resources.Load<Shader>("MapEditor/AreaOverlay");
                if (shader == null) return;
                _material = new Material(shader) { name = "MapEditorAreaOverlay", hideFlags = HideFlags.DontSave };
                _root = new GameObject("MapEditorAreaOverlay") { hideFlags = HideFlags.DontSave };
                _dirty = true;
            }
            _root.SetActive(true);
            if (!_dirty) return;
            Rebuild();
            _dirty = false;
        }

        private void MarkDirty() => _dirty = true;

        private void Rebuild()
        {
            foreach (Transform child in _root.transform) child.gameObject.Dispose(true);
            foreach (Mesh mesh in _meshes) UnityEngine.Object.Destroy(mesh);
            _meshes.Clear();
            LabelPositions.Clear();
            Dictionary<ushort, List<Vector3>> tiles = new();
            foreach (KeyValuePair<Vector2Int, ushort[]> chunk in _cache.EnumerateChunks())
            {
                for (int i = 0; i < chunk.Value.Length; i++)
                {
                    ushort id = chunk.Value[i];
                    if (id == AreaId.None) continue;
                    if (!tiles.TryGetValue(id, out List<Vector3> positions)) tiles[id] = positions = new();
                    positions.Add(new Vector3(chunk.Key.x * TileConstants.ChunkSize + i % TileConstants.ChunkSize,
                        0.3f, chunk.Key.y * TileConstants.ChunkSize + i / TileConstants.ChunkSize));
                }
                Mesh mesh = BuildChunkMesh(chunk.Value);
                if (mesh.vertexCount == 0) { UnityEngine.Object.Destroy(mesh); continue; }
                _meshes.Add(mesh);
                GameObject go = new($"Areas {chunk.Key}");
                go.transform.SetParent(_root.transform, false);
                go.transform.localPosition = new Vector3(chunk.Key.x * TileConstants.ChunkSize, 0.22f, chunk.Key.y * TileConstants.ChunkSize);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            foreach (KeyValuePair<ushort, List<Vector3>> area in tiles)
                LabelPositions[area.Key] = ChooseLabelPosition(area.Value);
        }

        public static Vector3 ChooseLabelPosition(IReadOnlyList<Vector3> tiles)
        {
            if (tiles.Count == 0) return Vector3.zero;
            Vector3 center = Vector3.zero;
            foreach (Vector3 tile in tiles) center += tile;
            center /= tiles.Count;
            Vector3 closest = tiles[0];
            foreach (Vector3 tile in tiles)
                if ((tile - center).sqrMagnitude < (closest - center).sqrMagnitude) closest = tile;
            return closest;
        }

        public static Mesh BuildChunkMesh(ushort[] ids)
        {
            List<Vector3> vertices = new();
            List<Color> colors = new();
            List<int> triangles = new();
            if (ids != null)
            {
                int count = Math.Min(ids.Length, TileConstants.ChunkSize * TileConstants.ChunkSize);
                for (int i = 0; i < count; i++)
                {
                    if (ids[i] == AreaId.None) continue;
                    float x = i % TileConstants.ChunkSize;
                    float z = i / TileConstants.ChunkSize;
                    int start = vertices.Count;
                    vertices.Add(new Vector3(x - 0.45f, 0, z - 0.45f));
                    vertices.Add(new Vector3(x + 0.45f, 0, z - 0.45f));
                    vertices.Add(new Vector3(x - 0.45f, 0, z + 0.45f));
                    vertices.Add(new Vector3(x + 0.45f, 0, z + 0.45f));
                    Color tint = AreaDevSettings.GetColorForArea(ids[i]);
                    for (int v = 0; v < 4; v++) colors.Add(tint);
                    triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
                    triangles.Add(start + 2); triangles.Add(start + 3); triangles.Add(start + 1);
                }
            }
            Mesh result = new() { name = "AreaOverlayChunk", indexFormat = IndexFormat.UInt32 };
            result.SetVertices(vertices);
            result.SetColors(colors);
            result.SetTriangles(triangles, 0);
            result.RecalculateBounds();
            return result;
        }

        public void Dispose()
        {
            if (_cache != null) _cache.OnDirty -= MarkDirty;
            _cache = null;
            foreach (Mesh mesh in _meshes) UnityEngine.Object.Destroy(mesh);
            _meshes.Clear();
            if (_root != null) _root.Dispose(true);
            if (_material != null) UnityEngine.Object.Destroy(_material);
        }
    }
}

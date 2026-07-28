using UnityEngine;

namespace SS3D.Systems.Tile
{
    /// <summary>
    /// Client-side logical occlusion for underfloor tile layers. When a cell has a covering
    /// turf (floor/wall/door), play-mode skips drawing plenum/wires/disposal/underfloor pipes
    /// on that cell. Map Editor authoring keeps them visible.
    /// </summary>
    public static class TileUnderfloorVisibility
    {
        private static readonly TileLayer[] UnderfloorLayers =
        {
            TileLayer.Plenum,
            TileLayer.Wire,
            TileLayer.Disposal,
            TileLayer.PipeLeft,
            TileLayer.PipeMiddle,
            TileLayer.PipeRight,
        };

        public static bool IsUnderfloorLayer(TileLayer layer) =>
            layer is TileLayer.Plenum
                or TileLayer.Wire
                or TileLayer.Disposal
                or TileLayer.PipeLeft
                or TileLayer.PipeMiddle
                or TileLayer.PipeRight;

        public static bool IsCoveringTurfGenericType(TileObjectGenericType genericType) =>
            genericType is TileObjectGenericType.Floor
                or TileObjectGenericType.Wall
                or TileObjectGenericType.Door;

        public static bool CellHasCoveringTurf(TileMap map, Vector3 worldPosition)
        {
            if (map == null)
                return false;

            if (!map.TryGetTileLocation(TileLayer.Turf, worldPosition, out ITileLocation location))
                return false;

            if (!location.TryGetPlacedObject(out PlacedTileObject turf))
                return false;

            return IsCoveringTurfGenericType(turf.GenericType);
        }

        /// <summary>
        /// Whether play-mode should hide underfloor MeshRenderers at this cell.
        /// </summary>
        public static bool ShouldHideUnderfloor(TileMap map, Vector3 worldPosition, bool mapEditorAuthoring)
        {
            if (mapEditorAuthoring)
                return false;

            return CellHasCoveringTurf(map, worldPosition);
        }

        /// <summary>
        /// Re-apply host MeshRenderer visibility for every underfloor object on a cell
        /// (after turf place/clear so cover state stays in sync).
        /// </summary>
        public static void RefreshUnderfloorAt(TileMap map, Vector3 worldPosition)
        {
            if (map == null)
                return;

            for (int i = 0; i < UnderfloorLayers.Length; i++)
            {
                if (!map.TryGetTileLocation(UnderfloorLayers[i], worldPosition, out ITileLocation location))
                    continue;

                if (!location.TryGetPlacedObject(out PlacedTileObject placed) || placed == null)
                    continue;

                placed.RefreshHostVisibility();
            }
        }

        /// <summary>
        /// Refresh underfloor visibility for every cell occupied by a (multi-tile) turf.
        /// </summary>
        public static void RefreshUnderfloorForTurf(TileMap map, PlacedTileObject turf)
        {
            if (map == null || turf == null)
                return;

            Vector3 origin = new Vector3(turf.WorldOrigin.x, 0f, turf.WorldOrigin.y);
            foreach (Vector2Int gridOffset in turf.GridOffsetList)
            {
                Vector3 cell = origin + new Vector3(gridOffset.x, 0f, gridOffset.y);
                RefreshUnderfloorAt(map, cell);
            }
        }

        public static void DisableChildRenderers(GameObject root)
        {
            if (root == null)
                return;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].enabled = false;
            }
        }
    }
}

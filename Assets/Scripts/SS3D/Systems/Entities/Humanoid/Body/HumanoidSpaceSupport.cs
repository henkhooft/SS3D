using SS3D.Core;
using SS3D.Systems.Tile;
using UnityEngine;

namespace SS3D.Systems.Entities.Humanoid.Body
{
    /// <summary>
    /// Shared "no floor underfoot" check for living space float (plenum absence).
    /// Missing tile map ≠ unsupported — matches health SafeDefault pitfall.
    /// </summary>
    public static class HumanoidSpaceSupport
    {
        public static bool IsUnsupportedAt(Vector3 worldPosition)
        {
            if (!SubSystems.TryGet(out TileSubSystem tiles)
                || tiles.CurrentMap == null
                || tiles.QueryService == null)
            {
                return false;
            }

            TileCoord coord = tiles.QueryService.WorldToTile(worldPosition, tiles.CurrentMap.MapId);
            if (!tiles.QueryService.TryGetOccupancy(coord, out TileOccupancy occupancy))
            {
                return true;
            }

            return !occupancy.HasPlenum;
        }
    }
}

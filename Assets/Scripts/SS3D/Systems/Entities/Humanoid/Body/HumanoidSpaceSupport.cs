using FishNet;
using SS3D.Core;
using SS3D.Core.WorldReadiness;
using SS3D.Systems.Tile;
using SS3D.Systems.WorldReadiness;
using UnityEngine;

namespace SS3D.Systems.Entities.Humanoid.Body
{
    /// <summary>
    /// Floor-support probe for living space float and health vacuum.
    /// Missing map / AOI lag / tiles not loaded = <see cref="HumanoidSupportState.Unknown"/> (not vacuum, not float).
    /// </summary>
    public enum HumanoidSupportState : byte
    {
        /// <summary>No map yet, tiles not loaded, or pure-client AOI incomplete — do not treat as open space.</summary>
        Unknown = 0,
        /// <summary>Occupancy known with plenum underfoot.</summary>
        Supported = 1,
        /// <summary>Confirmed no plenum (server), or server past map after TileMapLoaded.</summary>
        Unsupported = 2,
    }

    /// <summary>
    /// Shared plenum / open-space check for living space float and health exposure.
    /// </summary>
    public static class HumanoidSpaceSupport
    {
        public static HumanoidSupportState GetSupportAt(Vector3 worldPosition)
        {
            if (!SubSystems.TryGet(out TileSubSystem tiles)
                || tiles.CurrentMap == null
                || tiles.QueryService == null)
            {
                return HumanoidSupportState.Unknown;
            }

            TileCoord coord = tiles.QueryService.WorldToTile(worldPosition, tiles.CurrentMap.MapId);
            if (!tiles.QueryService.TryGetOccupancy(coord, out TileOccupancy occupancy))
            {
                // Empty chunk: only the server may treat this as open space, and only after the
                // station template is loaded. Before TileMapLoaded the map is empty → every cell
                // misses — that must not pack Floating SyncVars onto joining clients.
                if (InstanceFinder.IsServer && IsServerTileWorldReady(tiles))
                {
                    return HumanoidSupportState.Unsupported;
                }

                return HumanoidSupportState.Unknown;
            }

            if (occupancy.HasPlenum)
            {
                return HumanoidSupportState.Supported;
            }

            // Server: known cell with no plenum = open lattice / hole.
            // Client: other layers often arrive before Plenum via AOI — !HasPlenum is incomplete, not space.
            return InstanceFinder.IsServer
                ? HumanoidSupportState.Unsupported
                : HumanoidSupportState.Unknown;
        }

        public static bool IsUnsupportedAt(Vector3 worldPosition) =>
            GetSupportAt(worldPosition) == HumanoidSupportState.Unsupported;

        private static bool IsServerTileWorldReady(TileSubSystem tiles)
        {
            if (SubSystems.TryGet(out WorldReadinessSubSystem readiness)
                && readiness.IsReady(WorldReadyPhase.TileMapLoaded))
            {
                return true;
            }

            // EditMode / readiness not wired yet — require at least one chunk so empty UnnamedMap
            // does not count as loaded open space.
            return tiles.CurrentMap.ChunkCount > 0;
        }
    }
}

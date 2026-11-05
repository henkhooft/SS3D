using SS3D.Core.Behaviours;
using SS3D.Systems.Tile;
using UnityEngine;

namespace SS3D.Systems.Atmospherics.Pipes
{
    /// <summary>
    /// Starts the pipe network on one line of its tile filled, so a map has air in its pipes from round start.
    /// </summary>
    [RequireComponent(typeof(PlacedTileObject))]
    public sealed class AtmosLineFill : NetworkActor
    {
        [SerializeField]
        private TileLayer _line = TileLayer.PipeLeft;

        [SerializeField]
        private AtmosSupplyMix _mix;

        [SerializeField]
        private float _pressureKpa = AtmosPipeConstants.LineFillPressureKpa;

        public TileLayer[] Lines => new[] { _line };

        /// <summary>
        /// Fills the line under every placed fill. Runs once per round, right after the networks are built.
        /// </summary>
        public static void ApplyAll(TileMap map, GasPipeNetworkRegistry registry)
        {
            foreach (TileChunk chunk in map.GetAllChunks())
            {
                foreach (PlacedTileObject placed in chunk.GetAllTilePlacedObjects())
                {
                    if (placed != null && placed.TryGetComponent(out AtmosLineFill fill))
                        fill.Apply(map, registry, new TileCoord(placed.MapId, placed.WorldOrigin));
                }
            }
        }

        private void Apply(TileMap map, GasPipeNetworkRegistry registry, TileCoord coord)
        {
            if (AtmosPipeConnectivity.TryGetSegment(map, coord, _line, out PlacedTileObject segment))
                registry.TryFillNetwork(GasPipeSegmentKey.From(segment), _mix, _pressureKpa);
        }
    }
}

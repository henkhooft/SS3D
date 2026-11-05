using SS3D.Systems.Tile;

namespace SS3D.Systems.Atmospherics.Pipes
{
    /// <summary>
    /// Resolves the gas pipe network a device attaches to: the first of its lines present on its own tile.
    /// </summary>
    public static class AtmosDevicePipeResolver
    {
        /// <summary>Supply, then Mixed.</summary>
        public static readonly TileLayer[] VentLayers = { TileLayer.PipeLeft, TileLayer.PipeMiddle };

        /// <summary>Waste, then Mixed.</summary>
        public static readonly TileLayer[] ScrubberLayers = { TileLayer.PipeRight, TileLayer.PipeMiddle };

        /// <summary>Surface only; nothing attaches to it automatically but inline devices.</summary>
        public static readonly TileLayer[] InlineLayers = { TileLayer.PipeSurface };

        public static bool TryResolveNetwork(
            TileMap map,
            GasPipeNetworkRegistry registry,
            TileCoord deviceCoord,
            TileLayer[] layers,
            out GasPipeNetworkId networkId,
            out GasPipeSegmentKey segmentKey)
        {
            networkId = GasPipeNetworkId.None;
            segmentKey = default;

            if (map == null || registry == null)
                return false;

            for (int i = 0; i < layers.Length; i++)
            {
                if (!AtmosPipeConnectivity.TryGetSegment(map, deviceCoord, layers[i], out PlacedTileObject segment))
                    continue;

                segmentKey = GasPipeSegmentKey.From(segment);
                if (registry.TryGetNetworkForSegment(segmentKey, out networkId, out _))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Whether a pipe sits where <see cref="TryResolveNetwork"/> looks for one. Reads the
        /// tilemap, so it needs no running atmos simulation.
        /// </summary>
        public static bool HasPipeSegment(TileMap map, TileCoord deviceCoord, TileLayer[] layers)
        {
            for (int i = 0; i < layers.Length; i++)
            {
                if (AtmosPipeConnectivity.TryGetSegment(map, deviceCoord, layers[i], out _))
                    return true;
            }

            return false;
        }
    }
}

using SS3D.Systems.Tile;

namespace SS3D.Systems.Atmospherics.Pipes
{
    /// <summary>
    /// Resolves the gas pipe networks a device attaches to: the first of its lines present on its own
    /// tile, or for an inline device the pipes at its two ports.
    /// </summary>
    public static class AtmosDevicePipeResolver
    {
        /// <summary>Supply, then Mixed.</summary>
        public static readonly TileLayer[] VentLayers = { TileLayer.PipeLeft, TileLayer.PipeMiddle };

        /// <summary>Waste, then Mixed.</summary>
        public static readonly TileLayer[] ScrubberLayers = { TileLayer.PipeRight, TileLayer.PipeMiddle };

        /// <summary>Surface only; nothing attaches to it automatically but inline devices, at their ports.</summary>
        public static readonly TileLayer[] InlineLayers = { TileLayer.PipeSurface };

        /// <summary>
        /// Gas leaves an inline device through its front and enters through its back, so its facing
        /// decides which way gas moves.
        /// </summary>
        public static bool HasInlinePortToward(Direction facing, Direction direction) =>
            direction == facing || direction == TileHelper.GetOpposite(facing);

        /// <summary>
        /// The networks at an inline device's two ports: a Surface pipe, or a manifold port facing
        /// the device. Either is None when nothing is there, and the result says whether both were.
        /// </summary>
        public static bool TryResolveInlineNetworks(
            TileMap map,
            GasPipeNetworkRegistry registry,
            TileCoord deviceCoord,
            Direction facing,
            out GasPipeNetworkId inletId,
            out GasPipeNetworkId outletId)
        {
            bool hasInlet = TryResolvePort(map, registry, deviceCoord, TileHelper.GetOpposite(facing), out inletId);
            bool hasOutlet = TryResolvePort(map, registry, deviceCoord, facing, out outletId);
            return hasInlet && hasOutlet;
        }

        /// <summary>
        /// Whether both of an inline device's ports reach a line. Reads the tilemap, like
        /// <see cref="HasPipeSegment"/>.
        /// </summary>
        public static bool HasInlinePipes(TileMap map, TileCoord deviceCoord, Direction facing) =>
            HasPortLine(map, deviceCoord, facing) && HasPortLine(map, deviceCoord, TileHelper.GetOpposite(facing));

        private static bool TryResolvePort(
            TileMap map,
            GasPipeNetworkRegistry registry,
            TileCoord deviceCoord,
            Direction port,
            out GasPipeNetworkId networkId)
        {
            TileCoord portCoord = AtmosPipeConnectivity.Offset(deviceCoord, port);
            if (TryResolveNetwork(map, registry, portCoord, InlineLayers, out networkId, out _))
                return true;

            return map != null
                && AtmosPipeConnectivity.TryGetManifoldPort(map, portCoord, TileLayer.PipeSurface, TileHelper.GetOpposite(port), out GasPipeManifold manifold)
                && registry.TryGetNetworkForSegment(GasPipeSegmentKey.From(manifold.PlacedObject), out networkId, out _);
        }

        private static bool HasPortLine(TileMap map, TileCoord deviceCoord, Direction port)
        {
            TileCoord portCoord = AtmosPipeConnectivity.Offset(deviceCoord, port);
            return HasPipeSegment(map, portCoord, InlineLayers)
                || AtmosPipeConnectivity.TryGetManifoldPort(map, portCoord, TileLayer.PipeSurface, TileHelper.GetOpposite(port), out _);
        }

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

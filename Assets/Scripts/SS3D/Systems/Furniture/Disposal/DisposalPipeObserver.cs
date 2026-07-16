using SS3D.Systems.Tile;
using System;
using System.Collections.Generic;

namespace SS3D.Systems.Furniture.Disposal
{
    /// <summary>
    /// Rebuilds disposal pipe network topology when pipe segments are placed or removed, mirroring
    /// <c>AtmosPipeObserver</c>. Also reports cut segments immediately (before the topology rebuild)
    /// so <c>DisposalSubSystem</c> can spill in-transit capsules — sabotage must be immediate,
    /// never deferred (design doc §7).
    /// </summary>
    public sealed class DisposalPipeObserver : ITileMutationObserver
    {
        private readonly TileMap _map;
        private readonly DisposalNetworkRegistry _registry;
        private readonly Action<PlacedTileObject> _onSegmentCut;
        private readonly HashSet<TileCoord> _pendingRebuild = new();

        public DisposalPipeObserver(TileMap map, DisposalNetworkRegistry registry, Action<PlacedTileObject> onSegmentCut)
        {
            _map = map;
            _registry = registry;
            _onSegmentCut = onSegmentCut;
        }

        public void OnChunkCreated(TileChunkRef chunk)
        {
        }

        public void OnTilePlaced(ITileOccupant occupant, TileCoord coord)
        {
            if (occupant is PlacedTileObject placed && DisposalPipeConnectivity.ParticipatesInDisposalNetwork(placed))
                _registry.RebuildAround(_map, coord);
        }

        public void OnTileCleared(ITileOccupant occupant, TileCoord coord, TileLayer layer)
        {
            if (layer != TileLayer.Disposal)
                return;

            if (occupant is PlacedTileObject placed && DisposalPipeConnectivity.ParticipatesInDisposalNetwork(placed))
                _onSegmentCut?.Invoke(placed);

            _pendingRebuild.Add(coord);
        }

        public void OnTileStateChanged(TileCoord coord)
        {
        }

        public void FlushPendingRebuilds()
        {
            if (_pendingRebuild.Count == 0)
                return;

            foreach (TileCoord coord in _pendingRebuild)
                _registry.RebuildAround(_map, coord);

            _pendingRebuild.Clear();
        }
    }
}

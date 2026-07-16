using Cysharp.Threading.Tasks;
using SS3D.Core;
using SS3D.Core.Behaviours;
using SS3D.Logging;
using SS3D.Systems.Furniture;
using SS3D.Systems.IdAccess;
using SS3D.Systems.Inventory.Items;
using SS3D.Systems.Tile;
using System.Collections.Generic;
using UnityEngine;

namespace SS3D.Systems.Furniture.Disposal
{
    /// <summary>
    /// Server-authoritative disposal network coordinator — owns pipe topology, routes items dropped
    /// into a chute, and ticks in-transit capsules along their route (design doc §3-§7).
    /// Mirrors <c>ElectricitySubSystem</c>/<c>AtmosSubSystem</c>'s role for their own networks.
    /// </summary>
    public sealed class DisposalSubSystem : NetworkSubSystem
    {
        private DisposalNetworkRegistry _registry;
        private DisposalPipeObserver _observer;
        private TileMap _map;

        private readonly List<DisposalCapsule> _activeCapsules = new();

        public DisposalNetworkRegistry Registry => _registry;

        public override void OnStartServer()
        {
            base.OnStartServer();
            InitializeWhenMapReady().Forget();
        }

        private async UniTaskVoid InitializeWhenMapReady()
        {
            TileSubSystem tileSubSystem = null;
            await UniTask.WaitUntil(() =>
            {
                tileSubSystem = SubSystems.Get<TileSubSystem>();
                return tileSubSystem != null && tileSubSystem.CurrentMap != null;
            });

            if (!IsServer)
                return;

            _map = tileSubSystem.CurrentMap;
            _registry = new DisposalNetworkRegistry();
            _registry.RebuildAll(_map);

            _observer = new DisposalPipeObserver(_map, _registry, OnSegmentCut);
            tileSubSystem.RegisterTileMutationObserver(_observer);

            Log.Information(this, $"Disposal network started with {_registry.NetworkCount} network(s).");
        }

        protected override void OnDestroyed()
        {
            TileSubSystem tileSubSystem = SubSystems.Get<TileSubSystem>();
            if (_observer != null)
                tileSubSystem?.UnregisterTileMutationObserver(_observer);

            _observer = null;
            _registry = null;
            base.OnDestroyed();
        }

        private void Update()
        {
            if (!IsServer || _registry == null)
                return;

            _observer?.FlushPendingRebuilds();
            TickCapsules(Time.deltaTime);
        }

        private void TickCapsules(float deltaTime)
        {
            if (_activeCapsules.Count == 0)
                return;

            float distance = DisposalConstants.TransitSpeed * deltaTime;

            for (int i = _activeCapsules.Count - 1; i >= 0; i--)
            {
                DisposalCapsule capsule = _activeCapsules[i];
                if (capsule.Item == null)
                {
                    _activeCapsules.RemoveAt(i);
                    continue;
                }

                bool arrived = capsule.Advance(distance);
                capsule.Item.transform.position = capsule.CurrentPosition;

                if (arrived)
                {
                    _activeCapsules.RemoveAt(i);
                    HandleArrival(capsule);
                }
            }
        }

        private void HandleArrival(DisposalCapsule capsule)
        {
            if (capsule.Destination is DisposalOutlet outlet)
            {
                outlet.OnItemArrived(capsule.Item);
                return;
            }

            capsule.Item.Unfreeze();
        }

        /// <summary>
        /// Entry point for the chute drop-in interaction (design doc §2/§4). Resolves a route from the
        /// bin's pipe to the tagged destination (or the main outlet if untagged/unreachable) and spawns
        /// a capsule. Returns false if the bin isn't connected to any disposal network.
        /// </summary>
        public bool TryEnterNetwork(DisposalBin bin, Item item, Department destinationTag)
        {
            if (_registry == null || _map == null || bin == null || item == null)
                return false;

            if (!TryGetPipeBelow(bin, out PlacedTileObject entrySegment))
                return false;

            DisposalSegmentKey entryKey = DisposalSegmentKey.From(entrySegment);
            if (!_registry.TryGetNetworkForSegment(entryKey, out DisposalNetworkId networkId, out DisposalNetworkRecord network))
                return false;

            if (!TryResolveDestination(network, destinationTag, out IDisposalElement destination))
                return false;

            if (!DisposalPipeConnectivity.TryFindRoute(_map, entrySegment, destination, out List<PlacedTileObject> path))
                return false;

            item.Freeze();
            DisposalCapsule capsule = new(item, destination, networkId, path);
            _activeCapsules.Add(capsule);
            return true;
        }

        private static bool TryResolveDestination(DisposalNetworkRecord network, Department destinationTag, out IDisposalElement destination)
        {
            destination = null;
            IDisposalElement mainOutlet = null;

            foreach (IDisposalElement terminal in network.Terminals)
            {
                if (terminal is not DisposalOutlet outlet)
                    continue;

                if (destinationTag != Department.None && outlet.TargetDepartment == destinationTag)
                {
                    destination = outlet;
                    return true;
                }

                if (outlet.IsMainOutlet)
                    mainOutlet = outlet;
            }

            // No reachable outlet matches the tag (or it was untagged) — fall back to the main outlet (§4).
            destination = mainOutlet;
            return destination != null;
        }

        private bool TryGetPipeBelow(IDisposalElement element, out PlacedTileObject pipe)
        {
            pipe = null;
            if (_map == null)
                return false;

            if (!_map.TryGetTileLocation(TileLayer.Disposal, element.GameObject.transform.position, out ITileLocation location))
                return false;

            if (!location.TryGetPlacedObject(out pipe, Direction.North))
                return false;

            return DisposalPipeConnectivity.ParticipatesInDisposalNetwork(pipe);
        }

        /// <summary>
        /// Pipe-cut sabotage (design doc §7): immediately spills any capsule that hasn't yet passed the
        /// cut segment, right where the cut happened — never a hidden roll.
        /// </summary>
        private void OnSegmentCut(PlacedTileObject cutSegment)
        {
            DisposalSegmentKey key = DisposalSegmentKey.From(cutSegment);
            Vector3 spillPosition = cutSegment.transform.position;

            for (int i = _activeCapsules.Count - 1; i >= 0; i--)
            {
                DisposalCapsule capsule = _activeCapsules[i];
                if (!capsule.HasNotYetPassed(key))
                    continue;

                _activeCapsules.RemoveAt(i);

                if (capsule.Item == null)
                    continue;

                capsule.Item.Unfreeze();
                capsule.Item.transform.position = spillPosition;
            }
        }
    }
}

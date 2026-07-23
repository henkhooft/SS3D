using SS3D.Systems.Tile;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SS3D.Systems.StructuralDamage
{
    /// <summary>
    /// Breadth-first blast spread: open hops via BlockedEdges (same rules as atmos CanFlow),
    /// structural force on blocking turf, cascade when Destroyed clears an edge.
    /// </summary>
    public sealed class BlastResolutionService : IBlastResolutionService
    {
        public const float MinUsefulForce = 1f;

        private static readonly Direction[] Cardinals =
        {
            Direction.North, Direction.East, Direction.South, Direction.West,
        };

        private readonly ITileQueryService _query;
        private readonly IStructuralDamageService _structural;
        private readonly Action<TileCoord, float> _applyCrewBrute;

        /// <param name="applyCrewBrute">Optional; invoked as (tile, force) for each BFS visit.</param>
        public BlastResolutionService(
            ITileQueryService query,
            IStructuralDamageService structural,
            Action<TileCoord, float> applyCrewBrute = null)
        {
            _query = query;
            _structural = structural;
            _applyCrewBrute = applyCrewBrute;
        }

        public void Resolve(TileCoord epicenter, float yield, float falloff)
        {
            if (_query == null || _structural == null || yield < MinUsefulForce || falloff < 0f)
            {
                return;
            }

            var bestForce = new Dictionary<TileCoord, float>();
            var queue = new Queue<(TileCoord Coord, float Force)>();
            queue.Enqueue((epicenter, yield));

            while (queue.Count > 0)
            {
                (TileCoord coord, float force) = queue.Dequeue();
                if (force < MinUsefulForce)
                {
                    continue;
                }

                if (bestForce.TryGetValue(coord, out float seen) && force <= seen)
                {
                    continue;
                }

                bestForce[coord] = force;

                _structural.TryApplyStructuralDamage(coord, force, StructuralDamageSource.Blast);
                _applyCrewBrute?.Invoke(coord, force);

                if (!_query.TryGetOccupancy(coord, out TileOccupancy fromOccupancy))
                {
                    continue;
                }

                for (int i = 0; i < Cardinals.Length; i++)
                {
                    Direction direction = Cardinals[i];
                    float nextForce = force - falloff;
                    if (nextForce < MinUsefulForce)
                    {
                        continue;
                    }

                    TileCoord neighbor = GetNeighbourCoord(coord, direction);
                    if (!_query.TryGetOccupancy(neighbor, out TileOccupancy toOccupancy))
                    {
                        continue;
                    }

                    if (CanHop(fromOccupancy, toOccupancy, direction))
                    {
                        queue.Enqueue((neighbor, nextForce));
                        continue;
                    }

                    // Blocked edge: spend force on the neighbor structural turf; cascade if it clears.
                    if (!_structural.TryApplyStructuralDamage(neighbor, nextForce, StructuralDamageSource.Blast))
                    {
                        continue;
                    }

                    if (!_query.TryGetOccupancy(coord, out fromOccupancy)
                        || !_query.TryGetOccupancy(neighbor, out toOccupancy))
                    {
                        continue;
                    }

                    if (CanHop(fromOccupancy, toOccupancy, direction))
                    {
                        queue.Enqueue((neighbor, nextForce));
                    }
                }
            }
        }

        /// <summary>Same edge rules as atmos <c>CanFlow</c> — kept local to avoid Atmospherics.internal coupling.</summary>
        internal static bool CanHop(TileOccupancy from, TileOccupancy to, Direction direction)
        {
            int edgeIndex = DirectionToEdgeIndex(direction);
            if (edgeIndex < 0)
            {
                return false;
            }

            int oppositeEdge = (edgeIndex + 2) % 4;
            if ((from.BlockedEdges & (1 << edgeIndex)) != 0)
            {
                return false;
            }

            if ((to.BlockedEdges & (1 << oppositeEdge)) != 0)
            {
                return false;
            }

            return true;
        }

        internal static TileCoord GetNeighbourCoord(TileCoord coord, Direction direction)
        {
            Tuple<int, int> vector = TileHelper.ToCardinalVector(direction);
            return new TileCoord(coord.MapId, coord.Grid.x + vector.Item1, coord.Grid.y + vector.Item2);
        }

        private static int DirectionToEdgeIndex(Direction direction)
        {
            return direction switch
            {
                Direction.North => 0,
                Direction.East => 1,
                Direction.South => 2,
                Direction.West => 3,
                _ => -1,
            };
        }
    }
}

using FishNet.Connection;
using FishNet.Object;
using SS3D.Core;
using SS3D.Permissions;
using SS3D.Systems.Entities;
using SS3D.Systems.StructuralDamage;
using SS3D.Systems.Tile;
using System;
using UnityEngine;

namespace SS3D.Systems.IngameConsoleSystem.Commands
{
    /// <summary>
    /// Admin: apply structural force to the Turf wall/door on the tile in front of the caller.
    /// </summary>
    public class HurtStructureCommand : Command
    {
        private const float DefaultForce = 40f;

        public override string ShortDescription => "Damage structural turf in front of you";
        public override string LongDescription =>
            "Applies structural integrity damage to the wall/door/window on the cardinal tile ahead of your standing tile. Usage: hurtstructure [force] (default 40).";
        public override string Usage => "[force]";
        public override ServerRoleTypes AccessLevel => ServerRoleTypes.Administrator;
        public override CommandType Type => CommandType.Server;

        [Server]
        public override string Perform(string[] args, NetworkConnection conn = null)
        {
            CheckArgsResponse check = CheckArgs(args);
            if (!check.IsValid)
            {
                return check.InvalidArgs;
            }

            float force = DefaultForce;
            if (args.Length == 1)
            {
                float.TryParse(args[0], out force);
            }

            EntitySubSystem entities = SubSystems.Get<EntitySubSystem>();
            if (entities == null || !entities.TryGetOwnedEntity(conn, out Entity entity))
            {
                return "Connection does not own any entity";
            }

            TileSubSystem tiles = SubSystems.Get<TileSubSystem>();
            if (tiles == null || tiles.CurrentMap == null || tiles.QueryService == null)
            {
                return "TileSubSystem / map not ready";
            }

            StructuralDamageSubSystem structural = SubSystems.Get<StructuralDamageSubSystem>();
            if (structural == null)
            {
                return "StructuralDamageSubSystem not registered";
            }

            // Standing-tile + one cardinal step ahead — not world-position + forward*distance,
            // which often rounds back onto the floor underfoot or overshoots past the wall.
            int mapId = tiles.CurrentMap.MapId;
            TileCoord standing = tiles.QueryService.WorldToTile(entity.transform.position, mapId);
            Direction facing = CardinalFromForward(entity.transform.forward);
            TileCoord ahead = OffsetCardinal(standing, facing);

            if (!structural.TryApplyStructuralDamage(ahead, force, StructuralDamageSource.Console))
            {
                return $"No structural turf ahead at {ahead.Grid.x},{ahead.Grid.y} map {mapId} (standing {standing.Grid.x},{standing.Grid.y} facing {facing})";
            }

            if (!structural.TryGetIntegrity(ahead, out StructuralIntegrityStage stage, out float remaining, out float max))
            {
                return $"Structural Destroyed at {ahead.Grid.x},{ahead.Grid.y}";
            }

            return $"Structure at {ahead.Grid.x},{ahead.Grid.y}: {stage} ({remaining:0.#}/{max:0.#})";
        }

        protected override CheckArgsResponse CheckArgs(string[] args)
        {
            CheckArgsResponse response = new();

            if (args.Length > 1)
            {
                return response.MakeInvalid("Usage: hurtstructure [force]");
            }

            if (args.Length == 1)
            {
                if (!float.TryParse(args[0], out float parsedForce))
                {
                    return response.MakeInvalid("Invalid force amount");
                }

                if (parsedForce <= 0f)
                {
                    return response.MakeInvalid("Force must be positive");
                }
            }

            response.IsValid = true;
            return response;
        }

        private static Direction CardinalFromForward(Vector3 forward)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                return Direction.North;
            }

            forward.Normalize();
            if (Mathf.Abs(forward.z) >= Mathf.Abs(forward.x))
            {
                return forward.z >= 0f ? Direction.North : Direction.South;
            }

            return forward.x >= 0f ? Direction.East : Direction.West;
        }

        private static TileCoord OffsetCardinal(TileCoord coord, Direction direction)
        {
            Tuple<int, int> offset = TileHelper.ToCardinalVector(direction);
            return new TileCoord(coord.MapId, coord.Grid.x + offset.Item1, coord.Grid.y + offset.Item2);
        }
    }
}

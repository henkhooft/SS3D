using FishNet.Connection;
using FishNet.Object;
using SS3D.Core;
using SS3D.Permissions;
using SS3D.Systems.Entities;
using SS3D.Systems.StructuralDamage;
using SS3D.Systems.Tile;
using UnityEngine;

namespace SS3D.Systems.IngameConsoleSystem.Commands
{
    /// <summary>
    /// Admin: apply structural force to the Turf wall/door on the tile in front of the caller.
    /// </summary>
    public class HurtStructureCommand : Command
    {
        private const float DefaultForce = 40f;
        private const float TargetDistance = 1.25f;

        public override string ShortDescription => "Damage structural turf in front of you";
        public override string LongDescription =>
            "Applies structural integrity damage to the wall/door/window on the tile ahead. Usage: hurtstructure [force] (default 40).";
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
            if (tiles == null)
            {
                return "TileSubSystem not registered";
            }

            Vector3 target = entity.transform.position + entity.transform.forward * TargetDistance;
            TileCoord coord = tiles.QueryService.WorldToTile(target);

            StructuralDamageSubSystem structural = SubSystems.Get<StructuralDamageSubSystem>();
            if (structural == null)
            {
                return "StructuralDamageSubSystem not registered";
            }

            if (!structural.TryApplyStructuralDamage(coord, force, StructuralDamageSource.Console))
            {
                return $"No structural turf at {coord.Grid.x},{coord.Grid.y}";
            }

            if (!structural.TryGetIntegrity(coord, out StructuralIntegrityStage stage, out float remaining, out float max))
            {
                return $"Structural Destroyed at {coord.Grid.x},{coord.Grid.y}";
            }

            return $"Structure at {coord.Grid.x},{coord.Grid.y}: {stage} ({remaining:0.#}/{max:0.#})";
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
                if (!float.TryParse(args[0], out float force))
                {
                    return response.MakeInvalid("Invalid force amount");
                }

                if (force <= 0f)
                {
                    return response.MakeInvalid("Force must be positive");
                }
            }

            response.IsValid = true;
            return response;
        }
    }
}

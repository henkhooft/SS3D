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
    /// Admin: run blast BFS from the caller's standing tile.
    /// </summary>
    public class BlastCommand : Command
    {
        private const float DefaultYield = 120f;
        private const float DefaultFalloff = 25f;

        public override string ShortDescription => "Detonate a blast at your standing tile";
        public override string LongDescription =>
            "Runs hop-based blast resolution from your standing tile. Usage: blast [yield] [falloff] (defaults 120, 25).";
        public override string Usage => "[yield] [falloff]";
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

            float yield = DefaultYield;
            float falloff = DefaultFalloff;
            if (args.Length >= 1)
            {
                float.TryParse(args[0], out yield);
            }

            if (args.Length >= 2)
            {
                float.TryParse(args[1], out falloff);
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
            if (structural == null || structural.Blast == null)
            {
                return "StructuralDamageSubSystem / blast not ready";
            }

            int mapId = tiles.CurrentMap.MapId;
            TileCoord epicenter = tiles.QueryService.WorldToTile(entity.transform.position, mapId);
            structural.ResolveBlast(epicenter, yield, falloff);

            return $"Blast at {epicenter.Grid.x},{epicenter.Grid.y} map {mapId}: yield {yield:0.#}, falloff {falloff:0.#}";
        }

        protected override CheckArgsResponse CheckArgs(string[] args)
        {
            CheckArgsResponse response = new();

            if (args.Length > 2)
            {
                return response.MakeInvalid("Usage: blast [yield] [falloff]");
            }

            if (args.Length >= 1)
            {
                if (!float.TryParse(args[0], out float parsedYield) || parsedYield <= 0f)
                {
                    return response.MakeInvalid("Yield must be a positive number");
                }
            }

            if (args.Length >= 2)
            {
                if (!float.TryParse(args[1], out float parsedFalloff) || parsedFalloff < 0f)
                {
                    return response.MakeInvalid("Falloff must be a non-negative number");
                }
            }

            response.IsValid = true;
            return response;
        }
    }
}

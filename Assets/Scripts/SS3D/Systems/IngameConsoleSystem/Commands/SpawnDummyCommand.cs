using FishNet.Connection;
using FishNet.Object;
using SS3D.Core;
using SS3D.Permissions;
using SS3D.Systems.Entities;
using UnityEngine;

namespace SS3D.Systems.IngameConsoleSystem.Commands
{
    public class SpawnDummyCommand : Command
    {
        private const float SpawnDistance = 2f;

        public override string ShortDescription => "Spawn a standing combat dummy in front of you";
        public override string LongDescription =>
            "Spawns a mindless Human with player controls frozen for melee/interaction tests. Equips JumpsuitSecurity for armor absorption tests. Usage: spawndummy";
        public override string Usage => "";
        public override ServerRoleTypes AccessLevel => ServerRoleTypes.Administrator;
        public override CommandType Type => CommandType.Server;

        [Server]
        public override string Perform(string[] args, NetworkConnection conn = null)
        {
            CheckArgsResponse checkArgsResponse = CheckArgs(args);
            if (!checkArgsResponse.IsValid)
            {
                return checkArgsResponse.InvalidArgs;
            }

            EntitySubSystem entities = SubSystems.Get<EntitySubSystem>();
            if (!entities.TryGetOwnedEntity(conn, out Entity entity))
            {
                return "Connection does not own any entity registered in entity system.";
            }

            Transform transform = entity.transform;
            Vector3 spawnPosition = transform.position + transform.forward * SpawnDistance;
            Quaternion spawnRotation = Quaternion.LookRotation(-transform.forward, Vector3.up);

            Entity dummy = entities.ServerSpawnCombatDummy(spawnPosition, spawnRotation);
            if (dummy == null)
            {
                return "Failed to spawn combat dummy (no human prefab configured).";
            }

            return $"Combat dummy spawned at {spawnPosition} (wearing JumpsuitSecurity)";
        }

        protected override CheckArgsResponse CheckArgs(string[] args)
        {
            CheckArgsResponse response = new();
            if (args.Length != 0)
            {
                return response.MakeInvalid("spawndummy takes no arguments");
            }

            response.IsValid = true;
            return response;
        }
    }
}

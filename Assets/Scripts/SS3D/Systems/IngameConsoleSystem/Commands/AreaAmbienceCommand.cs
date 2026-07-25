using FishNet.Connection;
using FishNet.Object;
using SS3D.Core;
using SS3D.Permissions;
using SS3D.Systems.Area;
using SS3D.Systems.Entities;

namespace SS3D.Systems.IngameConsoleSystem.Commands
{
    /// <summary>
    /// Thin dev-console authoring surface for <see cref="AreaSubSystem.SetAreaAmbienceTrackId"/>
    /// (audio.md §2) — no Map Editor UI exists yet. Always targets the area the calling player
    /// currently stands in, same "no mouse to click a tile with" reasoning as
    /// <see cref="AtmosDebugCommand"/>.
    /// </summary>
    public class AreaAmbienceCommand : Command
    {
        public override string ShortDescription => "Set the ambience track of the area you're standing in";
        public override string Usage => "(trackId | clear)\nexample: areaambience stationambience3\nexample: areaambience clear";
        public override ServerRoleTypes AccessLevel => ServerRoleTypes.Administrator;
        public override CommandType Type => CommandType.Server;

        private record CalculatedValues(string TrackId) : ICalculatedValues;

        [Server]
        public override string Perform(string[] args, NetworkConnection conn = null)
        {
            if (!ReceiveCheckResponse(args, out CheckArgsResponse response, out CalculatedValues values))
            {
                return response.InvalidArgs;
            }

            if (!SubSystems.Get<EntitySubSystem>().TryGetSpawnedEntity(conn, out Entity entity))
            {
                return "You have no spawned entity to target from — embark first";
            }

            AreaSubSystem areaSubSystem = SubSystems.Get<AreaSubSystem>();
            if (areaSubSystem == null || !areaSubSystem.TryResolveAreaIdForWorldPosition(entity.Transform.position, out AreaId areaId))
            {
                return "Your current position isn't inside a claimed area";
            }

            areaSubSystem.SetAreaAmbienceTrackId(areaId, values.TrackId);

            return string.IsNullOrEmpty(values.TrackId)
                ? $"Cleared ambience track for area {areaId.Value}"
                : $"Area {areaId.Value} ambience set to '{values.TrackId}'";
        }

        protected override CheckArgsResponse CheckArgs(string[] args)
        {
            CheckArgsResponse response = new();

            if (args.Length != 1)
            {
                return response.MakeInvalid(WrongArgsText);
            }

            string trackId = args[0].Equals("clear", System.StringComparison.OrdinalIgnoreCase) ? string.Empty : args[0];
            return response.MakeValid(new CalculatedValues(trackId));
        }
    }
}

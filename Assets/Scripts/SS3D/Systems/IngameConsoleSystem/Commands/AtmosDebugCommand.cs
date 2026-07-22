using FishNet.Connection;
using FishNet.Object;
using SS3D.Core;
using SS3D.Permissions;
using SS3D.Systems.Atmospherics;
using SS3D.Systems.Entities;
using SS3D.Systems.Tile;

namespace SS3D.Systems.IngameConsoleSystem.Commands
{
    /// <summary>
    /// Console equivalent of <see cref="AtmosDebugController"/>'s GUI buttons (add heat/gas,
    /// wake a region) — that panel is OnGUI-only, so it's unreachable on a dedicated server or
    /// in the headless multiplayer test harness. Always targets the calling player's own tile,
    /// since a debug/automation caller has no mouse to click a tile with.
    /// </summary>
    public class AtmosDebugCommand : Command
    {
        public override string ShortDescription => "Add heat/gas or wake atmos cells at your own tile";
        public override string Usage => "(heat (deltaKelvin) | gas (oxygen|nitrogen|carbondioxide|plasma) (moles) | wake (radius))\n"
            + "example: atmosdebug heat 500\n"
            + "example: atmosdebug gas plasma 10\n"
            + "example: atmosdebug wake 1";
        public override ServerRoleTypes AccessLevel => ServerRoleTypes.Administrator;
        public override CommandType Type => CommandType.Server;

        private enum DebugAction { Heat, Gas, Wake }

        private record CalculatedValues(DebugAction Action, GasId GasId, float Amount, int Radius) : ICalculatedValues;

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

            TileSubSystem tileSubSystem = SubSystems.Get<TileSubSystem>();
            AtmosSubSystem atmos = SubSystems.Get<AtmosSubSystem>();
            if (tileSubSystem?.QueryService == null || tileSubSystem.CurrentMap == null || atmos == null)
            {
                return "Atmos or tile system not ready";
            }

            TileCoord coord = tileSubSystem.QueryService.WorldToTile(entity.Transform.position, tileSubSystem.CurrentMap.MapId);

            return values.Action switch
            {
                DebugAction.Heat => PerformHeat(atmos, coord, values.Amount),
                DebugAction.Gas => PerformGas(atmos, coord, values.GasId, values.Amount),
                _ => PerformWake(atmos, coord, values.Radius),
            };
        }

        private static string PerformHeat(AtmosSubSystem atmos, TileCoord coord, float deltaKelvin)
        {
            atmos.DebugAddHeat(coord, deltaKelvin);
            return $"Added {deltaKelvin:F0} K at {coord.Grid.x},{coord.Grid.y}";
        }

        private static string PerformGas(AtmosSubSystem atmos, TileCoord coord, GasId gasId, float moles)
        {
            atmos.DebugAddGas(coord, gasId, moles);
            return $"Added {moles:F1} mol at {coord.Grid.x},{coord.Grid.y}";
        }

        private static string PerformWake(AtmosSubSystem atmos, TileCoord coord, int radius)
        {
            atmos.DebugWakeRegion(coord, radius);
            return $"Woke radius {radius} at {coord.Grid.x},{coord.Grid.y}";
        }

        protected override CheckArgsResponse CheckArgs(string[] args)
        {
            CheckArgsResponse response = new();

            if (args.Length < 2)
            {
                return response.MakeInvalid(WrongArgsText);
            }

            switch (args[0].ToLowerInvariant())
            {
                case "heat":
                    if (args.Length != 2 || !float.TryParse(args[1], out float deltaKelvin))
                    {
                        return response.MakeInvalid("Invalid heat delta");
                    }

                    return response.MakeValid(new CalculatedValues(DebugAction.Heat, default, deltaKelvin, 0));

                case "gas":
                    if (args.Length != 3 || !TryParseGas(args[1], out GasId gasId) || !float.TryParse(args[2], out float moles))
                    {
                        return response.MakeInvalid("Invalid gas name or amount");
                    }

                    return response.MakeValid(new CalculatedValues(DebugAction.Gas, gasId, moles, 0));

                case "wake":
                    if (args.Length != 2 || !int.TryParse(args[1], out int radius))
                    {
                        return response.MakeInvalid("Invalid radius");
                    }

                    return response.MakeValid(new CalculatedValues(DebugAction.Wake, default, 0f, radius));

                default:
                    return response.MakeInvalid("Unknown action; use heat, gas, or wake");
            }
        }

        private static bool TryParseGas(string name, out GasId gasId)
        {
            switch (name.ToLowerInvariant())
            {
                case "oxygen":
                    gasId = AtmosConstants.Oxygen;
                    return true;
                case "nitrogen":
                    gasId = AtmosConstants.Nitrogen;
                    return true;
                case "carbondioxide":
                    gasId = AtmosConstants.CarbonDioxide;
                    return true;
                case "plasma":
                    gasId = AtmosConstants.Plasma;
                    return true;
                default:
                    gasId = default;
                    return false;
            }
        }
    }
}

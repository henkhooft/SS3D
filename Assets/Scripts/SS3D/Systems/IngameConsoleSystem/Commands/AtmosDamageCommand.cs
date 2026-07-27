using FishNet.Connection;
using FishNet.Object;
using SS3D.Permissions;
using SS3D.Systems.Health;

namespace SS3D.Systems.IngameConsoleSystem.Commands
{
    /// <summary>
    /// Toggles turf atmosphere → health damage/oxy/alerts (default on). Does not pause the gas sim.
    /// </summary>
    public class AtmosDamageCommand : Command
    {
        public override string ShortDescription => "Enable/disable atmospheric damage on players";
        public override string Usage => "[on|off|status]\n"
            + "off — skip turf oxy shortfall, breath exchange, env burn, atmos alerts/screens\n"
            + "example: atmosdamage off";
        public override ServerRoleTypes AccessLevel => ServerRoleTypes.Administrator;
        public override CommandType Type => CommandType.Server;

        private record CalculatedValues(string Mode) : ICalculatedValues;

        [Server]
        public override string Perform(string[] args, NetworkConnection conn = null)
        {
            if (!ReceiveCheckResponse(args, out CheckArgsResponse response, out CalculatedValues values))
            {
                return response.InvalidArgs;
            }

            if (values.Mode == "status")
            {
                return HealthEnvironmentSettings.AtmosphericDamageDisabled
                    ? "Atmospheric damage: OFF (disabled)"
                    : "Atmospheric damage: ON";
            }

            bool disable = values.Mode == "off";
            HealthEnvironmentSettings.AtmosphericDamageDisabled = disable;
            return disable
                ? "Atmospheric damage disabled"
                : "Atmospheric damage enabled";
        }

        [Server]
        protected override CheckArgsResponse CheckArgs(string[] args)
        {
            CheckArgsResponse response = new();

            if (args.Length == 0)
            {
                return response.MakeValid(new CalculatedValues("status"));
            }

            if (args.Length != 1)
            {
                return response.MakeInvalid(WrongArgsText);
            }

            string mode = args[0].ToLowerInvariant();
            if (mode is not ("on" or "off" or "status"))
            {
                return response.MakeInvalid("Expected on, off, or status");
            }

            return response.MakeValid(new CalculatedValues(mode));
        }
    }
}

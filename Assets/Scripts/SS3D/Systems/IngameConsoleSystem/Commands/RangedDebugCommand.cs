using FishNet.Connection;
using SS3D.Permissions;
using SS3D.Systems.Combat;

namespace SS3D.Systems.IngameConsoleSystem.Commands
{
    /// <summary>
    /// Client toggle for gold/grey ranged impact spheres (default off).
    /// </summary>
    public class RangedDebugCommand : Command
    {
        public override string ShortDescription => "Toggle ranged impact debug markers";
        public override string Usage => "[on|off|status]\n"
            + "on — show gold/grey spheres at hitscan impact points\n"
            + "off — hide markers (default; bullet holes + muzzle flash stay)\n"
            + "example: rangeddebug on";
        public override ServerRoleTypes AccessLevel => ServerRoleTypes.User;
        public override CommandType Type => CommandType.Client;

        private record CalculatedValues(string Mode) : ICalculatedValues;

        public override string Perform(string[] args, NetworkConnection conn = null)
        {
            if (!ReceiveCheckResponse(args, out CheckArgsResponse response, out CalculatedValues values))
            {
                return response.InvalidArgs;
            }

            if (values.Mode == "status")
            {
                return RangedShotFeedback.ShowImpactMarkers
                    ? "Ranged impact markers: ON"
                    : "Ranged impact markers: OFF";
            }

            bool enable = values.Mode == "on";
            RangedShotFeedback.ShowImpactMarkers = enable;
            return enable
                ? "Ranged impact markers enabled"
                : "Ranged impact markers disabled";
        }

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

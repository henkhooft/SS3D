using System;
using System.Linq;

namespace SS3D.Systems.Rounds
{
    /// <summary>
    /// The sandbox server profile (<c>sandbox-server.md</c>): a server launched with
    /// <see cref="CommandLineFlag"/> runs short Extended rounds where everyone builds, respawn is on and
    /// nothing is saved. The flag only overlays the in-memory <see cref="ServerSettings"/> at boot and
    /// implies <c>-openbuild</c>; it never writes saved settings. Everything else reads the settings,
    /// not the flag, so each rule can be switched on its own.
    /// </summary>
    public static class SandboxProfile
    {
        public const string CommandLineFlag = "-sandbox";

        public const int RoundLengthMinutes = 20;
        public const int PlayerCap = 16;
        public const int RespawnCooldownSeconds = 30;

        public const string Motd = "Sandbox server: everything is allowed. Anyone can build with the map editor (press B), "
            + "and you can respawn after dying. The station resets every 20 minutes and nothing is saved. "
            + "Report bugs with /bug <what happened>.";

        public static bool RequestedOnCommandLine =>
            Environment.GetCommandLineArgs().Any(arg => string.Equals(arg, CommandLineFlag, StringComparison.OrdinalIgnoreCase));

        /// <summary>The sandbox rules over <paramref name="settings"/>; map and server name are kept.</summary>
        public static ServerSettings Apply(ServerSettings settings)
        {
            settings.Mode = "Extended";
            settings.RoundLengthMinutes = RoundLengthMinutes;
            settings.AllowLateJoin = true;
            settings.PlayerCap = PlayerCap;
            settings.AutoStart = true;
            settings.RespawnEnabled = true;
            settings.RespawnCooldownSeconds = RespawnCooldownSeconds;
            settings.MapPersistence = false;
            settings.AllConnectedReady = true;
            settings.Motd = Motd;
            return settings;
        }
    }
}

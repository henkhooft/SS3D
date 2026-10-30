using System;
using SS3D.Systems.Roles;

namespace SS3D.UI.Lobby
{
    /// <summary>Phase A mock data for the lobby shell visual pass.</summary>
    public static class LobbyMockData
    {
        public const string ServerName = "USS Horizon // RE:SS3D Test Build";
        public const string MapName = "Cerestation";
        public const string ModeLabel = "Secret";
        public const int RoundNumber = 14;
        public const string BuildVersion = "0.14.2-pre";
        public const string UptimeLabel = "6h 12m";
        public const string CharacterName = "Marcus Voss";
        public const string Motd =
            "Welcome aboard. This build is community-maintained pre-alpha — expect things to break, and report what you find. Voice chat is proximity-based. Text chat has department radio channels — check your headset frequency in the Jobs tab.";

        public static readonly ChangelogEntry[] Changelog =
        {
            new("0.14.2", "Fixed chat scroll jumping when new messages arrive."),
            new("0.14.1", "Added department radio channels to the Jobs tab."),
            new("0.14.0", "Reworked lobby ready-up flow and admin round controls."),
            new("0.13.6", "Cerestation map lighting pass, minor pathing fixes."),
        };

        public static readonly PlayerRow[] Players =
        {
            new("kowalski", "Kowalski", true, "admin", "38ms", "19:02", "3y 2mo"),
            new("reyes", "Reyes", true, null, "54ms", "19:03", "8mo"),
            new("voss", "Voss", true, "mentor", "61ms", "19:04", "1y 5mo"),
            new("adebayo", "Adebayo", false, null, "112ms", "19:05", "4mo"),
            new("lindqvist", "Lindqvist", false, null, "47ms", "19:06", "2y 1mo"),
            new("okafor", "Okafor", true, null, "29ms", "19:01", "11mo"),
        };

        public readonly struct ChangelogEntry
        {
            public readonly string Version;
            public readonly string Text;

            public ChangelogEntry(string version, string text)
            {
                Version = version;
                Text = text;
            }
        }

        public readonly struct PlayerRow
        {
            public readonly string Id;
            public readonly string Name;
            public readonly bool Ready;
            public readonly string Rank;
            public readonly string Ping;
            public readonly string JoinTime;
            public readonly string AccountAge;

            public PlayerRow(
                string id,
                string name,
                bool ready,
                string rank,
                string ping,
                string joinTime,
                string accountAge)
            {
                Id = id;
                Name = name;
                Ready = ready;
                Rank = rank;
                Ping = ping;
                JoinTime = joinTime;
                AccountAge = accountAge;
            }
        }
    }

    public static class JobPriorityUtil
    {
        public static readonly JobPriority[] CycleOrder =
        {
            JobPriority.Never,
            JobPriority.Low,
            JobPriority.Medium,
            JobPriority.High,
        };

        public static string ShortLabel(JobPriority priority) => priority switch
        {
            JobPriority.Never => "N",
            JobPriority.Low => "L",
            JobPriority.Medium => "M",
            JobPriority.High => "H",
            _ => "?",
        };

        public static string FullLabel(JobPriority priority) => priority switch
        {
            JobPriority.Never => "Never",
            JobPriority.Low => "Low",
            JobPriority.Medium => "Medium",
            JobPriority.High => "High",
            _ => "?",
        };

        public static JobPriority Next(JobPriority current)
        {
            int index = Array.IndexOf(CycleOrder, current);
            if (index < 0)
            {
                return JobPriority.Medium;
            }

            return CycleOrder[(index + 1) % CycleOrder.Length];
        }
    }
}

using System;
using SS3D.Systems.Roles;

namespace SS3D.UI.Lobby
{
    /// <summary>Placeholder character name and player-row type for the lobby shell.</summary>
    public static class LobbyMockData
    {
        public const string CharacterName = "Marcus Voss";

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

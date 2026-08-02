using System;
using System.Collections.Generic;

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

        public static readonly AntagRole[] AntagRoles =
        {
            new("traitor", "Traitor"),
            new("changeling", "Changeling"),
            new("nukeop", "Nuclear Operative"),
            new("cultist", "Cultist"),
            new("revolutionary", "Revolutionary"),
            new("blob", "Blob"),
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

        public static readonly Department[] Departments =
        {
            new("command", "Command", "command", new JobRow[]
            {
                Job("captain", "Captain", "Captain", 0, 1, 4, false, null),
            }),
            new("security", "Security", "security", new JobRow[]
            {
                Job("warden", "Warden", "Warden", 0, 1, 3, false, null),
                Job("security-officer", "Security Officer", "SecurityOfficer", 1, 4, 6, false, null),
            }),
            new("engineering", "Engineering", "engineering", new JobRow[]
            {
                Job("chief-engineer", "Chief Engineer", "ChiefEngineer", 0, 1, 2, true, "Requires 10h as Engineer"),
                Job("engineer", "Engineer", "Engineer", 2, 4, 9, false, null),
                Job("plumber", "Plumber", "Plumber", 1, 2, 3, false, null),
            }),
            new("medical", "Medical", "medical", new JobRow[]
            {
                Job("doctor", "Doctor", "Doctor", 1, 3, 5, false, null),
                Job("chemist", "Chemist", "Chemist", 1, 1, 2, false, null),
            }),
            new("science", "Science", "science", new JobRow[]
            {
                Job("roboticist", "Roboticist", "Roboticist", 1, 2, 2, false, null),
            }),
            new("cargo", "Cargo", "cargo", new JobRow[]
            {
                Job("cargo-technician", "Cargo Technician", "CargoTechnician", 2, 3, 4, false, null),
                Job("miner", "Miner", "Miner", 1, 2, 3, false, null),
            }),
            new("service", "Service", "service", new JobRow[]
            {
                Job("bartender", "Bartender", "Bartender", 1, 1, 2, false, null),
                Job("chef", "Chef", "Chef", 1, 1, 3, false, null),
                Job("janitor", "Janitor", "Janitor", 1, 2, 2, false, null),
            }),
        };

        public static Dictionary<string, JobPriority> DefaultPriorities()
        {
            Dictionary<string, JobPriority> map = new();
            foreach (Department dept in Departments)
            {
                foreach (JobRow job in dept.Jobs)
                {
                    map[job.Id] = job.Open > 0 && !job.Locked ? JobPriority.Medium : JobPriority.Never;
                }
            }

            return map;
        }

        public static Dictionary<string, bool> DefaultAntagSelections()
        {
            Dictionary<string, bool> map = new();
            foreach (AntagRole role in AntagRoles)
            {
                map[role.Key] = true;
            }

            return map;
        }

        private static JobRow Job(
            string id,
            string name,
            string iconId,
            int open,
            int slots,
            int interested,
            bool locked,
            string lockReason) =>
            new(id, name, iconId, open, slots, interested, locked, lockReason);

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

        public readonly struct AntagRole
        {
            public readonly string Key;
            public readonly string Name;

            public AntagRole(string key, string name)
            {
                Key = key;
                Name = name;
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

        public sealed class Department
        {
            public readonly string Key;
            public readonly string Name;
            public readonly string IconId;
            public readonly JobRow[] Jobs;

            public Department(string key, string name, string iconId, JobRow[] jobs)
            {
                Key = key;
                Name = name;
                IconId = iconId;
                Jobs = jobs;
            }
        }

        public sealed class JobRow
        {
            public readonly string Id;
            public readonly string Name;
            public readonly string IconId;
            public readonly int Open;
            public readonly int Slots;
            public readonly int Interested;
            public readonly bool Locked;
            public readonly string LockReason;

            public JobRow(
                string id,
                string name,
                string iconId,
                int open,
                int slots,
                int interested,
                bool locked,
                string lockReason)
            {
                Id = id;
                Name = name;
                IconId = iconId;
                Open = open;
                Slots = slots;
                Interested = interested;
                Locked = locked;
                LockReason = lockReason;
            }
        }
    }

    public enum JobPriority : byte
    {
        Never = 0,
        Low = 1,
        Medium = 2,
        High = 3,
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

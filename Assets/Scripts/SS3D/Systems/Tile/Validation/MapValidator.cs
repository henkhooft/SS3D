using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SS3D.Systems.Tile.SpawnPoints;

namespace SS3D.Systems.Tile.Validation
{
    /// <summary>Shared save-warning and round-start policy. No scene-wide object searches.</summary>
    public static class MapValidator
    {
        public static MapValidationReport Validate(string mode, IEnumerable<string> jobs,
            IReadOnlyList<SpawnPointRecord> spawns, bool hasNuke, bool hasDisk)
        {
            MapValidationReport report = new();
            string[] requiredJobs = jobs.Where(job => !string.IsNullOrWhiteSpace(job))
                .Distinct(StringComparer.Ordinal).OrderBy(job => job, StringComparer.Ordinal).ToArray();
            if (requiredJobs.Length == 0)
                report.AddIssue("jobs-config", false, "Jobs", "No jobs configured", "Configure the server's offered jobs before starting.");
            foreach (string job in requiredJobs)
            {
                if (!spawns.Any(spawn => spawn.Kind == SpawnPointKind.Job && spawn.JobName == job))
                    report.AddIssue("job:" + job, false, "Jobs", $"Missing job spawn: {job}",
                        $"Missing job spawn: {job}. Add it under Scripts → Spawn Placements.", "spawn:job:" + job);
            }

            // Secret resolves to the sole existing antagonist mode, just as GamemodeSubSystem does.
            if (mode is "Secret" or "Nuclear Emergency")
            {
                if (!spawns.Any(spawn => spawn.Kind == SpawnPointKind.Antagonist
                    && spawn.AntagonistCategory == AntagonistSpawnCategory.NuclearOperatives))
                    report.AddIssue("operative", false, "Game Modes", "Missing Nuclear Operatives spawn",
                        "Missing Nuclear Operatives spawn. Add it under Scripts → Spawn Placements.", "spawns");
                if (!hasNuke)
                    report.AddIssue("nuke", false, "Game Modes", "Missing nuclear device",
                        "Missing nuclear device. Place a NukeCart on the map.", "NukeCart");
                if (!hasDisk)
                    report.AddIssue("disk", false, "Game Modes", "Missing authentication disk",
                        "Missing authentication disk. Place a NuclearAuthenticationDisk on the map.", "NuclearAuthenticationDisk");
            }
            else if (mode != "Extended")
                report.AddIssue("mode", false, "Game Modes", "Unsupported game mode",
                    "Unsupported game mode. Select Extended, Secret or Nuclear Emergency.");

            foreach (string job in spawns.Where(spawn => spawn.Kind == SpawnPointKind.Job)
                .Select(spawn => spawn.JobName).Distinct(StringComparer.Ordinal)
                .OrderBy(job => job, StringComparer.Ordinal))
            {
                if (!requiredJobs.Contains(job, StringComparer.Ordinal))
                    report.AddIssue("unavailable:" + job, true, "Jobs", $"Spawn for unavailable job: {job}",
                        $"Spawn for unavailable job: {job}. Check its job name or server role configuration.",
                        "spawns", spawns.First(spawn => spawn.Kind == SpawnPointKind.Job && spawn.JobName == job).Position, true);
            }
            // The policy retains one error per missing requirement; the editor groups related
            // failures into a single expandable checklist, including requirements already met.
            if (report.Issues.Any(issue => issue.Id.StartsWith("job:", StringComparison.Ordinal)))
            {
                report.Issues.RemoveAll(issue => issue.Id.StartsWith("job:", StringComparison.Ordinal));
                report.Issues.Insert(0, new MapValidationIssue
                {
                    Id = "job-spawns", Category = "Jobs", Title = "Offered jobs missing spawn markers",
                    Body = "Every offered job needs an authored spawn marker.", Target = "spawns",
                    Checks = requiredJobs.Select(job => new MapValidationCheck
                    {
                        Label = job, IsMet = spawns.Any(spawn => spawn.Kind == SpawnPointKind.Job && spawn.JobName == job),
                    }).ToArray(),
                });
            }
            if (report.Issues.Any(issue => issue.Id is "operative" or "nuke" or "disk"))
            {
                report.Issues.RemoveAll(issue => issue.Id is "operative" or "nuke" or "disk");
                report.Issues.Add(new MapValidationIssue
                {
                    Id = "nuclear", Category = "Game Modes", Title = "Nuclear requirements incomplete",
                    Body = "Nuclear Emergency and Secret require an operative marker, nuclear device and authentication disk.",
                    Target = "spawns",
                    Checks = new[]
                    {
                        new MapValidationCheck { Label = "Operative marker", IsMet = spawns.Any(spawn => spawn.Kind == SpawnPointKind.Antagonist
                            && spawn.AntagonistCategory == AntagonistSpawnCategory.NuclearOperatives), Target = "spawns" },
                        new MapValidationCheck { Label = "Nuclear device", IsMet = hasNuke, Target = "NukeCart" },
                        new MapValidationCheck { Label = "Authentication disk", IsMet = hasDisk, Target = "NuclearAuthenticationDisk" },
                    },
                });
            }
            return report;
        }
    }

    [Serializable]
    public struct MapValidationCheck
    {
        public string Label;
        public bool IsMet;
        public string Target;
    }

    /// <summary>Server-authored issue data shared by save policy and the editor's Map Check panel.</summary>
    [Serializable]
    public struct MapValidationIssue
    {
        public string Id;
        public bool IsWarning;
        public string Category;
        public string Title;
        public string Body;
        public string Target;
        public Vector3 Position;
        public bool HasPosition;
        public MapValidationCheck[] Checks;
    }

    public sealed class MapValidationReport
    {
        public List<MapValidationIssue> Issues { get; } = new();

        public void AddIssue(string id, bool warning, string category, string title, string body,
            string target = null, Vector3 position = default, bool hasPosition = false)
        {
            Issues.Add(new MapValidationIssue { Id = id, IsWarning = warning, Category = category,
                Title = title, Body = body, Target = target, Position = position, HasPosition = hasPosition });
            (warning ? Warnings : Errors).Add(body);
        }

        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();
        public bool CanStart => Errors.Count == 0;
        public bool HasIssues => Errors.Count > 0 || Warnings.Count > 0;

        public string Describe(string map, string mode, bool saved)
        {
            List<string> lines = new()
            {
                saved ? $"Saved {map}. Validation for {mode}:" : $"Cannot start {map} ({mode}):",
            };
            lines.AddRange(Errors.Select(error => "• " + error));
            lines.AddRange(Warnings.Select(warning => "• Warning: " + warning));
            if (saved && Errors.Count > 0)
                lines.Add("Saving is allowed. Fix these issues before starting this mode.");
            if (!saved)
                lines.Add("Fix the map in the editor, save it, then try Start again.");
            return string.Join("\n", lines);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
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
                report.Errors.Add("No jobs are configured. Configure the server's offered jobs before starting.");
            foreach (string job in requiredJobs)
            {
                if (!spawns.Any(spawn => spawn.Kind == SpawnPointKind.Job && spawn.JobName == job))
                    report.Errors.Add($"Missing job spawn: {job}. Add it under Scripts → Spawn Placements.");
            }

            // Secret resolves to the sole existing antagonist mode, just as GamemodeSubSystem does.
            if (mode is "Secret" or "Nuclear Emergency")
            {
                if (!spawns.Any(spawn => spawn.Kind == SpawnPointKind.Antagonist
                    && spawn.AntagonistCategory == AntagonistSpawnCategory.NuclearOperatives))
                    report.Errors.Add("Missing Nuclear Operatives spawn. Add it under Scripts → Spawn Placements.");
                if (!hasNuke)
                    report.Errors.Add("Missing nuclear device. Place a NukeCart on the map.");
                if (!hasDisk)
                    report.Errors.Add("Missing authentication disk. Place a NuclearAuthenticationDisk on the map.");
            }
            else if (mode != "Extended")
                report.Errors.Add("Unsupported game mode. Select Extended, Secret or Nuclear Emergency.");

            foreach (string job in spawns.Where(spawn => spawn.Kind == SpawnPointKind.Job)
                .Select(spawn => spawn.JobName).Distinct(StringComparer.Ordinal)
                .OrderBy(job => job, StringComparer.Ordinal))
            {
                if (!requiredJobs.Contains(job, StringComparer.Ordinal))
                    report.Warnings.Add($"Spawn for unavailable job: {job}. Check its job name or server role configuration.");
            }
            return report;
        }
    }

    public sealed class MapValidationReport
    {
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

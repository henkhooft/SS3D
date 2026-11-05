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
                        $"Missing job spawn: {job}. Add it under Scripts → Spawn Placements.");
            }

            // Secret resolves to the sole existing antagonist mode, just as GamemodeSubSystem does.
            if (mode is "Secret" or "Nuclear Emergency")
            {
                if (!spawns.Any(spawn => spawn.Kind == SpawnPointKind.Antagonist
                    && spawn.AntagonistCategory == AntagonistSpawnCategory.NuclearOperatives))
                    report.AddIssue("operative", false, "Game Modes", "Missing Nuclear Operatives spawn",
                        "Missing Nuclear Operatives spawn. Add it under Scripts → Spawn Placements.");
                if (!hasNuke)
                    report.AddIssue("nuke", false, "Game Modes", "Missing nuclear device",
                        "Missing nuclear device. Place a NukeCart on the map.");
                if (!hasDisk)
                    report.AddIssue("disk", false, "Game Modes", "Missing authentication disk",
                        "Missing authentication disk. Place a NuclearAuthenticationDisk on the map.");
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
                        spawns.First(spawn => spawn.Kind == SpawnPointKind.Job && spawn.JobName == job).Position, true);
            }
            // The policy retains one error per missing requirement; the editor groups related
            // failures into a single expandable checklist, including requirements already met.
            if (report.Issues.Any(issue => issue.Id.StartsWith("job:", StringComparison.Ordinal)))
            {
                report.Issues.RemoveAll(issue => issue.Id.StartsWith("job:", StringComparison.Ordinal));
                report.Issues.Insert(0, new MapValidationIssue
                {
                    Id = "job-spawns", Category = "Jobs", Title = "Offered jobs missing spawn markers",
                    Body = "Every offered job needs an authored spawn marker.",
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
                    Checks = new[]
                    {
                        new MapValidationCheck { Label = "Operative marker", IsMet = spawns.Any(spawn => spawn.Kind == SpawnPointKind.Antagonist
                            && spawn.AntagonistCategory == AntagonistSpawnCategory.NuclearOperatives) },
                        new MapValidationCheck { Label = "Nuclear device", IsMet = hasNuke },
                        new MapValidationCheck { Label = "Authentication disk", IsMet = hasDisk },
                    },
                });
            }
            return report;
        }

        /// <summary>
        /// Warns about vents and scrubbers no air alarm can control: an alarm drives every device
        /// in its own room, so one warning per room covers all of that room's devices.
        /// </summary>
        public static void AddAirAlarmCoverage(MapValidationReport report,
            IEnumerable<AtmosDeviceRecord> devices, ICollection<ushort> alarmedAreas)
        {
            AtmosDeviceRecord[] ordered = devices
                .OrderBy(device => device.Position.x).ThenBy(device => device.Position.z).ToArray();
            foreach (AtmosDeviceRecord device in ordered.Where(device => device.AreaId == 0))
            {
                Vector2Int cell = SpawnPointRegistry.TileKey(device.Position);
                string title = $"{device.Kind} at ({cell.x}, {cell.y}) is outside every room";
                report.AddIssue($"air-alarm-room:{device.Kind}:{cell.x}:{cell.y}", true, "Atmos", title,
                    $"{title}, so no air alarm can control it. Add its tile to a room.",
                    device.Position, true);
            }

            foreach (IGrouping<ushort, AtmosDeviceRecord> room in ordered
                .Where(device => device.AreaId != 0 && !alarmedAreas.Contains(device.AreaId))
                .GroupBy(device => device.AreaId).OrderBy(room => room.Key))
            {
                AtmosDeviceRecord first = room.First();
                string counts = string.Join(" and ", room.GroupBy(device => device.Kind)
                    .OrderByDescending(kind => kind.Key, StringComparer.Ordinal)
                    .Select(kind => $"{kind.Count()} {kind.Key.ToLowerInvariant()}{(kind.Count() == 1 ? "" : "s")}"));
                report.AddIssue($"air-alarm:{room.Key}", true, "Atmos", $"No air alarm in {first.AreaName}",
                    $"No air alarm in {first.AreaName}, so nothing controls its {counts}. "
                    + "Place one on a wall facing into the room.", first.Position, true);
            }
        }
    }

    /// <summary>A placed vent or scrubber and the room it belongs to (area 0 means no room).</summary>
    public readonly struct AtmosDeviceRecord
    {
        public readonly string Kind;
        public readonly Vector3 Position;
        public readonly ushort AreaId;
        public readonly string AreaName;

        public AtmosDeviceRecord(string kind, Vector3 position, ushort areaId, string areaName)
        {
            Kind = kind;
            Position = position;
            AreaId = areaId;
            AreaName = areaName;
        }
    }

    [Serializable]
    public struct MapValidationCheck
    {
        public string Label;
        public bool IsMet;
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
        /// <summary>Map location the issue points at (e.g. a spawn without floor); drives the Locate button.</summary>
        public Vector3 Position;
        public bool HasPosition;
        public MapValidationCheck[] Checks;
    }

    public sealed class MapValidationReport
    {
        public List<MapValidationIssue> Issues { get; } = new();

        public void AddIssue(string id, bool warning, string category, string title, string body,
            Vector3 position = default, bool hasPosition = false)
        {
            Issues.Add(new MapValidationIssue { Id = id, IsWarning = warning, Category = category,
                Title = title, Body = body, Position = position, HasPosition = hasPosition });
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

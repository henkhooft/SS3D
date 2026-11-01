using System.Linq;
using SS3D.Core;
using SS3D.Systems.Furniture;
using SS3D.Systems.Inventory.Items.Generic;
using SS3D.Systems.Roles;
using UnityEngine;

namespace SS3D.Systems.Tile.Validation
{
    /// <summary>Validates only objects tracked by the current template, excluding inventory/preview objects.</summary>
    public static class MapValidationService
    {
        public static MapValidationReport Validate(TileSubSystem tiles, string mode)
        {
            if (tiles == null || tiles.CurrentMap == null)
            {
                MapValidationReport missing = new();
                missing.AddIssue("map", false, "Map", "No station map loaded",
                    "No station map is loaded. Load or create a map first.");
                return missing;
            }

            TileMap map = tiles.CurrentMap;
            bool hasNuke = map.GetAllChunks().SelectMany(chunk => chunk.GetAllTilePlacedObjects())
                .Any(placed => placed != null && placed.GetComponentInChildren<Nuke>(true) != null);
            bool hasDisk = map.PlacedItems.Any(placed => placed != null
                && placed.GetComponentInChildren<NukeCard>(true) != null);
            string[] jobs = SubSystems.TryGet(out RoleSubSystem roles)
                ? roles.Jobs.Select(job => job.Name).ToArray() : System.Array.Empty<string>();
            MapValidationReport report = MapValidator.Validate(mode, jobs, tiles.SpawnPoints.Records, hasNuke, hasDisk);
            foreach (SpawnPoints.SpawnPointRecord spawn in tiles.SpawnPoints.Records)
            {
                if (!map.TryGetTileLocation(TileLayer.Plenum, spawn.Position, out ITileLocation floor)
                    || floor.IsFullyEmpty())
                {
                    Vector2Int cell = SpawnPoints.SpawnPointRegistry.TileKey(spawn.Position);
                    report.AddIssue($"floor:{cell.x}:{cell.y}:{spawn.Kind}:{spawn.JobName}", true, "Jobs", $"Spawn at ({cell.x}, {cell.y}) has no floor",
                        $"Spawn at ({cell.x}, {cell.y}) has no floor. Place flooring beneath it.",
                        spawn.Position, true);
                }
            }
            return report;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using SS3D.Systems.Tile.MapImport.Dmm;

namespace SS3D.Systems.Tile.MapImport
{
    /// <summary>
    /// Builds a placement plan (Plenum + structural + infrastructure overlays) from parsed DMM cells.
    /// Does not touch the live tilemap — safe for EditMode tests.
    /// </summary>
    public static class MapImportPlanner
    {
        public const string PlenumSoName = "Plenum";

        // FurnitureBase is single-occupant: Vent > Scrubber > DisposalBin > DisposalOutlet.
        private const int FurniturePriorityVent = 4;
        private const int FurniturePriorityScrubber = 3;
        private const int FurniturePriorityDisposalBin = 2;
        private const int FurniturePriorityDisposalOutlet = 1;

        public static MapImportPlan Build(
            DmmMap map,
            Ss13TypeMapper mapper,
            MapImportBBox bbox = default,
            int? zFilter = null)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));
            if (mapper == null)
                throw new ArgumentNullException(nameof(mapper));

            mapper.ResetUnmapped();
            MapImportPlan plan = new MapImportPlan();

            int originX = 0;
            int originY = 0;
            if (map.Cells.Count > 0)
            {
                originX = map.Cells.Min(c => c.X);
                originY = map.Cells.Min(c => c.Y);
            }

            foreach (DmmCell cell in map.Cells)
            {
                if (zFilter.HasValue && cell.Z != zFilter.Value)
                {
                    plan.SkippedCells++;
                    continue;
                }

                if (!bbox.Contains(cell.X, cell.Y))
                {
                    plan.SkippedCells++;
                    continue;
                }

                if (!TryBuildCell(cell, mapper, out MapImportKind structuralKind, out List<MapImportPlacement> placements,
                        out OverlayCounts overlays))
                {
                    plan.SkippedCells++;
                    continue;
                }

                MapImportCellPlan cellPlan = new MapImportCellPlan
                {
                    SourceX = cell.X,
                    SourceY = cell.Y,
                    WorldX = cell.X - originX,
                    WorldZ = cell.Y - originY,
                };
                cellPlan.Placements.AddRange(placements);
                plan.Cells.Add(cellPlan);

                switch (structuralKind)
                {
                    case MapImportKind.Floor: plan.FloorCells++; break;
                    case MapImportKind.Wall: plan.WallCells++; break;
                    case MapImportKind.Window: plan.WindowCells++; break;
                    case MapImportKind.Door: plan.DoorCells++; break;
                }

                plan.CablePlacements += overlays.Cable;
                plan.PipePlacements += overlays.Pipe;
                plan.DisposalPlacements += overlays.Disposal;
                plan.DisposalTerminalPlacements += overlays.DisposalTerminal;
                plan.VentPlacements += overlays.Vent;
                plan.ScrubberPlacements += overlays.Scrubber;
                plan.ApcPlacements += overlays.Apc;
                plan.LightPlacements += overlays.Light;
            }

            foreach (KeyValuePair<string, int> pair in mapper.UnmappedCounts)
                plan.UnmappedCounts[pair.Key] = pair.Value;

            return plan;
        }

        private struct OverlayCounts
        {
            public int Cable;
            public int Pipe;
            public int Disposal;
            public int DisposalTerminal;
            public int Vent;
            public int Scrubber;
            public int Apc;
            public int Light;
        }

        private static bool TryBuildCell(
            DmmCell cell,
            Ss13TypeMapper mapper,
            out MapImportKind structuralKind,
            out List<MapImportPlacement> placements,
            out OverlayCounts overlays)
        {
            structuralKind = MapImportKind.Skip;
            placements = new List<MapImportPlacement>();
            overlays = default;

            Ss13TypeMatch? wall = null;
            Ss13TypeMatch? window = null;
            Ss13TypeMatch? door = null;
            Ss13TypeMatch? floor = null;
            Direction doorDir = Direction.North;

            Ss13TypeMatch? cable = null;
            Ss13TypeMatch? disposal = null;
            Ss13TypeMatch? apc = null;
            Ss13TypeMatch? light = null;
            Direction apcDir = Direction.North;
            Direction lightDir = Direction.North;

            // Pipe SO name → best match (multi-layer OK when SO differs).
            Dictionary<string, Ss13TypeMatch> pipesBySo = new Dictionary<string, Ss13TypeMatch>(StringComparer.Ordinal);

            int furniturePriority = 0;
            string furnitureSo = null;
            Direction furnitureDir = Direction.North;
            MapImportKind furnitureKind = MapImportKind.Skip;

            foreach (DmmAtom atom in cell.Atoms)
            {
                if (!mapper.TryMatch(atom.Path, out Ss13TypeMatch match))
                    continue;

                switch (match.Kind)
                {
                    case MapImportKind.Wall:
                        wall = Prefer(wall, match);
                        break;
                    case MapImportKind.Window:
                        window = Prefer(window, match);
                        break;
                    case MapImportKind.Door:
                        if (TakeIfBetter(ref door, match))
                            doorDir = ResolveDirection(atom);
                        break;
                    case MapImportKind.Floor:
                        floor = Prefer(floor, match);
                        break;
                    case MapImportKind.Cable:
                        cable = Prefer(cable, match);
                        break;
                    case MapImportKind.Pipe:
                    {
                        string pipeSo = MapImportPipeResolver.Resolve(atom.Path, atom);
                        var resolved = new Ss13TypeMatch(MapImportKind.Pipe, pipeSo, match.MatchedPrefix);
                        if (!pipesBySo.TryGetValue(pipeSo, out Ss13TypeMatch existing) ||
                            resolved.MatchedPrefix.Length > existing.MatchedPrefix.Length)
                        {
                            pipesBySo[pipeSo] = resolved;
                        }

                        break;
                    }
                    case MapImportKind.Disposal:
                        disposal = Prefer(disposal, match);
                        break;
                    case MapImportKind.Vent:
                        ConsiderFurniture(FurniturePriorityVent, match.SoName, ResolveDirection(atom),
                            MapImportKind.Vent, ref furniturePriority, ref furnitureSo, ref furnitureDir, ref furnitureKind);
                        break;
                    case MapImportKind.Scrubber:
                        ConsiderFurniture(FurniturePriorityScrubber, match.SoName, ResolveDirection(atom),
                            MapImportKind.Scrubber, ref furniturePriority, ref furnitureSo, ref furnitureDir, ref furnitureKind);
                        break;
                    case MapImportKind.DisposalTerminal:
                    {
                        int priority = string.Equals(match.SoName, "DisposalBin", StringComparison.Ordinal)
                            ? FurniturePriorityDisposalBin
                            : FurniturePriorityDisposalOutlet;
                        ConsiderFurniture(priority, match.SoName, ResolveDirection(atom),
                            MapImportKind.DisposalTerminal, ref furniturePriority, ref furnitureSo, ref furnitureDir,
                            ref furnitureKind);
                        break;
                    }
                    case MapImportKind.Apc:
                        if (TakeIfBetter(ref apc, match))
                            apcDir = ResolveDirection(atom);
                        break;
                    case MapImportKind.Light:
                        if (TakeIfBetter(ref light, match))
                            lightDir = ResolveDirection(atom);
                        break;
                }
            }

            // Turf layer is single-occupant: wall > window > door > floor.
            string structuralSo;
            Direction structuralDir = Direction.North;
            if (wall.HasValue)
            {
                structuralKind = MapImportKind.Wall;
                structuralSo = wall.Value.SoName;
            }
            else if (window.HasValue)
            {
                structuralKind = MapImportKind.Window;
                structuralSo = window.Value.SoName;
            }
            else if (door.HasValue)
            {
                structuralKind = MapImportKind.Door;
                structuralSo = door.Value.SoName;
                structuralDir = doorDir;
            }
            else if (floor.HasValue)
            {
                structuralKind = MapImportKind.Floor;
                structuralSo = floor.Value.SoName;
            }
            else
            {
                return false;
            }

            placements.Add(new MapImportPlacement { SoName = PlenumSoName, Direction = Direction.North });
            placements.Add(new MapImportPlacement { SoName = structuralSo, Direction = structuralDir });

            if (cable.HasValue)
            {
                placements.Add(new MapImportPlacement { SoName = cable.Value.SoName, Direction = Direction.North });
                overlays.Cable = 1;
            }

            foreach (KeyValuePair<string, Ss13TypeMatch> pipe in pipesBySo)
            {
                placements.Add(new MapImportPlacement { SoName = pipe.Key, Direction = Direction.North });
                overlays.Pipe++;
            }

            if (disposal.HasValue)
            {
                placements.Add(new MapImportPlacement { SoName = disposal.Value.SoName, Direction = Direction.North });
                overlays.Disposal = 1;
            }

            if (furnitureSo != null)
            {
                placements.Add(new MapImportPlacement { SoName = furnitureSo, Direction = furnitureDir });
                switch (furnitureKind)
                {
                    case MapImportKind.Vent:
                        overlays.Vent = 1;
                        break;
                    case MapImportKind.Scrubber:
                        overlays.Scrubber = 1;
                        break;
                    case MapImportKind.DisposalTerminal:
                        overlays.DisposalTerminal = 1;
                        break;
                }
            }

            if (apc.HasValue)
            {
                placements.Add(new MapImportPlacement { SoName = apc.Value.SoName, Direction = apcDir });
                overlays.Apc = 1;
            }

            if (light.HasValue)
            {
                placements.Add(new MapImportPlacement { SoName = light.Value.SoName, Direction = lightDir });
                overlays.Light = 1;
            }

            return true;
        }

        private static void ConsiderFurniture(
            int priority,
            string soName,
            Direction dir,
            MapImportKind kind,
            ref int currentPriority,
            ref string currentSo,
            ref Direction currentDir,
            ref MapImportKind currentKind)
        {
            if (priority <= currentPriority)
                return;
            currentPriority = priority;
            currentSo = soName;
            currentDir = dir;
            currentKind = kind;
        }

        private static Ss13TypeMatch? Prefer(Ss13TypeMatch? current, Ss13TypeMatch next)
        {
            if (!current.HasValue)
                return next;
            return next.MatchedPrefix.Length > current.Value.MatchedPrefix.Length ? next : current;
        }

        /// <summary>Updates <paramref name="current"/> when <paramref name="next"/> has a longer match. Returns whether it won.</summary>
        private static bool TakeIfBetter(ref Ss13TypeMatch? current, Ss13TypeMatch next)
        {
            if (!current.HasValue || next.MatchedPrefix.Length > current.Value.MatchedPrefix.Length)
            {
                current = next;
                return true;
            }

            return false;
        }

        /// <summary>
        /// BYOND dir bitflags: 1 N, 2 S, 4 E, 8 W. Also accepts cardinal 1/2/4/8 only.
        /// </summary>
        public static Direction ResolveDirection(DmmAtom atom)
        {
            if (atom != null && atom.TryGetInt("dir", out int byondDir))
                return FromByondDir(byondDir);

            int px = 0;
            int py = 0;
            bool hasPx = atom != null && atom.TryGetInt("pixel_x", out px);
            bool hasPy = atom != null && atom.TryGetInt("pixel_y", out py);
            if (hasPx || hasPy)
            {
                if (Math.Abs(px) >= Math.Abs(py) && px != 0)
                    return px > 0 ? Direction.East : Direction.West;
                if (py != 0)
                    return py > 0 ? Direction.North : Direction.South;
            }

            return Direction.North;
        }

        public static Direction FromByondDir(int byondDir)
        {
            switch (byondDir)
            {
                case 1: return Direction.North;
                case 2: return Direction.South;
                case 4: return Direction.East;
                case 8: return Direction.West;
                case 5: return Direction.NorthEast;
                case 6: return Direction.SouthEast;
                case 9: return Direction.NorthWest;
                case 10: return Direction.SouthWest;
                default: return Direction.North;
            }
        }
    }
}

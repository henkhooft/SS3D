using System;
using System.Collections.Generic;
using System.Linq;
using SS3D.Systems.Tile.MapImport.Dmm;

namespace SS3D.Systems.Tile.MapImport
{
    /// <summary>
    /// Builds a placement plan (Plenum + structural SO) from parsed DMM cells.
    /// Does not touch the live tilemap — safe for EditMode tests.
    /// </summary>
    public static class MapImportPlanner
    {
        public const string PlenumSoName = "Plenum";

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

                if (!TryClassify(cell, mapper, out MapImportKind kind, out string soName, out Direction dir))
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
                cellPlan.Placements.Add(new MapImportPlacement { SoName = PlenumSoName, Direction = Direction.North });
                cellPlan.Placements.Add(new MapImportPlacement { SoName = soName, Direction = dir });
                plan.Cells.Add(cellPlan);

                switch (kind)
                {
                    case MapImportKind.Floor: plan.FloorCells++; break;
                    case MapImportKind.Wall: plan.WallCells++; break;
                    case MapImportKind.Window: plan.WindowCells++; break;
                    case MapImportKind.Door: plan.DoorCells++; break;
                }
            }

            foreach (KeyValuePair<string, int> pair in mapper.UnmappedCounts)
                plan.UnmappedCounts[pair.Key] = pair.Value;

            return plan;
        }

        private static bool TryClassify(
            DmmCell cell,
            Ss13TypeMapper mapper,
            out MapImportKind kind,
            out string soName,
            out Direction dir)
        {
            kind = MapImportKind.Skip;
            soName = string.Empty;
            dir = Direction.North;

            Ss13TypeMatch? wall = null;
            Ss13TypeMatch? window = null;
            Ss13TypeMatch? door = null;
            Ss13TypeMatch? floor = null;
            Direction doorDir = Direction.North;

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
                        door = Prefer(door, match);
                        doorDir = ResolveDirection(atom);
                        break;
                    case MapImportKind.Floor:
                        floor = Prefer(floor, match);
                        break;
                }
            }

            // Turf layer is single-occupant: wall > window > door > floor.
            if (wall.HasValue)
            {
                kind = MapImportKind.Wall;
                soName = wall.Value.SoName;
                return true;
            }

            if (window.HasValue)
            {
                kind = MapImportKind.Window;
                soName = window.Value.SoName;
                return true;
            }

            if (door.HasValue)
            {
                kind = MapImportKind.Door;
                soName = door.Value.SoName;
                dir = doorDir;
                return true;
            }

            if (floor.HasValue)
            {
                kind = MapImportKind.Floor;
                soName = floor.Value.SoName;
                return true;
            }

            return false;
        }

        private static Ss13TypeMatch? Prefer(Ss13TypeMatch? current, Ss13TypeMatch next)
        {
            if (!current.HasValue)
                return next;
            return next.MatchedPrefix.Length > current.Value.MatchedPrefix.Length ? next : current;
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
            // Single-bit or legacy numeric.
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

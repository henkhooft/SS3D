using System;
using SS3D.Systems.Tile.MapImport.Dmm;

namespace SS3D.Systems.Tile.MapImport
{
    /// <summary>
    /// Resolves SS13 facing from modern <c>/directional/…</c> path suffixes, <c>dir</c> vars,
    /// or pixel offsets. BYOND's default dir is South when nothing is specified.
    /// </summary>
    public static class MapImportDirection
    {
        /// <summary>BYOND atom default when maps omit dir.</summary>
        public const Direction ByondDefault = Direction.South;

        /// <summary>
        /// True when the atom encodes an explicit facing (path suffix, dir var, or dominant pixel offset).
        /// </summary>
        public static bool TryResolveExplicit(DmmAtom atom, out Direction direction)
        {
            direction = ByondDefault;
            if (atom == null)
                return false;

            if (TryFromPath(atom.Path, out direction))
                return true;

            if (atom.TryGetInt("dir", out int byondDir))
            {
                direction = FromByondDir(byondDir);
                return true;
            }

            int px = 0;
            int py = 0;
            bool hasPx = atom.TryGetInt("pixel_x", out px);
            bool hasPy = atom.TryGetInt("pixel_y", out py);
            if (hasPx || hasPy)
            {
                if (Math.Abs(px) >= Math.Abs(py) && px != 0)
                {
                    direction = px > 0 ? Direction.East : Direction.West;
                    return true;
                }

                if (py != 0)
                {
                    direction = py > 0 ? Direction.North : Direction.South;
                    return true;
                }
            }

            return false;
        }

        public static Direction Resolve(DmmAtom atom, Direction fallback = ByondDefault) =>
            TryResolveExplicit(atom, out Direction direction) ? direction : fallback;

        public static bool TryFromPath(string path, out Direction direction)
        {
            direction = ByondDefault;
            if (string.IsNullOrEmpty(path))
                return false;

            // Prefer the last /directional/<cardinal> segment (tgstation mapping helpers).
            const string marker = "/directional/";
            int idx = path.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
                return false;

            string rest = path[(idx + marker.Length)..];
            int slash = rest.IndexOf('/');
            if (slash >= 0)
                rest = rest[..slash];
            int brace = rest.IndexOf('{');
            if (brace >= 0)
                rest = rest[..brace];
            rest = rest.Trim().TrimEnd(',');

            switch (rest.ToLowerInvariant())
            {
                case "north":
                    direction = Direction.North;
                    return true;
                case "south":
                    direction = Direction.South;
                    return true;
                case "east":
                    direction = Direction.East;
                    return true;
                case "west":
                    direction = Direction.West;
                    return true;
                default:
                    return false;
            }
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
                default: return ByondDefault;
            }
        }

        /// <summary>
        /// When airlocks omit dir, infer facing from neighbouring walls:
        /// N/S walls → East (E–W corridor); otherwise South (BYOND default / N–S corridor).
        /// </summary>
        public static Direction InferDoorDirection(
            int sourceX,
            int sourceY,
            System.Collections.Generic.IReadOnlyDictionary<(int X, int Y), MapImportKind> structuralKinds)
        {
            bool WallAt(int dx, int dy)
            {
                if (structuralKinds == null ||
                    !structuralKinds.TryGetValue((sourceX + dx, sourceY + dy), out MapImportKind kind))
                    return false;
                return kind is MapImportKind.Wall or MapImportKind.Window;
            }

            bool nsWalls = WallAt(0, 1) || WallAt(0, -1);
            bool ewWalls = WallAt(1, 0) || WallAt(-1, 0);
            if (nsWalls && !ewWalls)
                return Direction.East;

            return ByondDefault;
        }
    }
}

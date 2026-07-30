using System;
using SS3D.Systems.Tile.MapImport.Dmm;

namespace SS3D.Systems.Tile.MapImport
{
    /// <summary>
    /// Collapses SS13 pipe network types / piping_layer onto SS3D AtmosPipesL1–L4.
    /// Network type in the path beats the numeric piping_layer var.
    /// </summary>
    public static class MapImportPipeResolver
    {
        public const string AtmosPipesL1 = "AtmosPipesL1";
        public const string AtmosPipesL2 = "AtmosPipesL2";
        public const string AtmosPipesL3 = "AtmosPipesL3";
        public const string AtmosPipesL4 = "AtmosPipesL4";

        public static string Resolve(string path, DmmAtom atom)
        {
            if (!string.IsNullOrEmpty(path))
            {
                if (ContainsSegment(path, "/supply"))
                    return AtmosPipesL1;
                if (ContainsSegment(path, "/scrubbers"))
                    return AtmosPipesL2;
                if (ContainsSegment(path, "/waste") ||
                    ContainsSegment(path, "/general") ||
                    ContainsSegment(path, "/yellow"))
                    return AtmosPipesL3;
            }

            if (atom != null && atom.TryGetInt("piping_layer", out int layer))
            {
                return layer switch
                {
                    1 => AtmosPipesL1,
                    2 => AtmosPipesL2,
                    3 => AtmosPipesL3,
                    4 => AtmosPipesL4,
                    _ => AtmosPipesL3,
                };
            }

            return AtmosPipesL3;
        }

        private static bool ContainsSegment(string path, string segment) =>
            path.IndexOf(segment, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}

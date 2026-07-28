using System.Collections.Generic;

namespace SS3D.Systems.Tile.MapImport
{
    public sealed class MapImportPlacement
    {
        public string SoName { get; set; } = string.Empty;

        public Direction Direction { get; set; } = Direction.North;
    }

    /// <summary>
    /// Planned placements for one BYOND cell (world tile after origin remap).
    /// </summary>
    public sealed class MapImportCellPlan
    {
        public int SourceX { get; set; }

        public int SourceY { get; set; }

        public int WorldX { get; set; }

        public int WorldZ { get; set; }

        public List<MapImportPlacement> Placements { get; } = new List<MapImportPlacement>();
    }

    public sealed class MapImportPlan
    {
        public List<MapImportCellPlan> Cells { get; } = new List<MapImportCellPlan>();

        public int FloorCells { get; set; }

        public int WallCells { get; set; }

        public int WindowCells { get; set; }

        public int DoorCells { get; set; }

        public int SkippedCells { get; set; }

        public Dictionary<string, int> UnmappedCounts { get; set; } =
            new Dictionary<string, int>();
    }

    public readonly struct MapImportBBox
    {
        public int MinX { get; }

        public int MinY { get; }

        public int MaxX { get; }

        public int MaxY { get; }

        public bool Enabled { get; }

        public MapImportBBox(int minX, int minY, int maxX, int maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
            Enabled = true;
        }

        public static MapImportBBox None => default;

        public bool Contains(int x, int y) =>
            !Enabled || (x >= MinX && x <= MaxX && y >= MinY && y <= MaxY);
    }
}

using System.IO;
using NUnit.Framework;
using SS3D.Systems.Tile;
using SS3D.Systems.Tile.MapImport;
using SS3D.Systems.Tile.MapImport.Dmm;
using UnityEngine;

namespace SS3D.Tests.EditMode.MapImport
{
    public class MapImportPlannerTests
    {
        private static string FixturePath =>
            Path.GetFullPath(Path.Combine(Application.dataPath,
                "Scripts/Tests/EditMode/MapImport/Fixtures/tiny_box.dmm"));

        private static string TypeMapPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "../Tools/map_import/ss13_type_map.yaml"));

        [Test]
        public void TypeMapYaml_ParsesDefaultsAndSortedPrefixes()
        {
            Ss13TypeMapConfig config = Ss13TypeMapYaml.ParseFile(TypeMapPath);
            Assert.AreEqual("TileGrey", config.DefaultFloor);
            Assert.Greater(config.Prefixes.Count, 5);
            Assert.GreaterOrEqual(config.Prefixes[0].Match.Length, config.Prefixes[^1].Match.Length);
        }

        [Test]
        public void Mapper_LongestPrefixWins()
        {
            Ss13TypeMapConfig config = Ss13TypeMapYaml.ParseFile(TypeMapPath);
            Ss13TypeMapper mapper = new Ss13TypeMapper(config);
            Assert.IsTrue(mapper.TryMatch("/turf/closed/wall/r_wall", out Ss13TypeMatch match));
            Assert.AreEqual(MapImportKind.Wall, match.Kind);
            Assert.AreEqual("SteelWallReinforced", match.SoName);
        }

        [Test]
        public void Planner_TinyBox_AddsPlenumAndCountsKinds()
        {
            DmmMap map = DmmParser.ParseFile(FixturePath);
            Ss13TypeMapper mapper = new Ss13TypeMapper(Ss13TypeMapYaml.ParseFile(TypeMapPath));
            MapImportPlan plan = MapImportPlanner.Build(map, mapper);

            Assert.Greater(plan.FloorCells, 0);
            Assert.Greater(plan.WallCells, 0);
            Assert.Greater(plan.DoorCells, 0);
            Assert.Greater(plan.WindowCells, 0);
            Assert.IsTrue(plan.UnmappedCounts.ContainsKey("/obj/structure/table"));

            foreach (MapImportCellPlan cell in plan.Cells)
            {
                Assert.AreEqual(MapImportPlanner.PlenumSoName, cell.Placements[0].SoName);
                Assert.AreEqual(2, cell.Placements.Count);
            }

            MapImportCellPlan doorCell = plan.Cells.Find(c => c.Placements[1].SoName == "CivillianAirlock");
            Assert.IsNotNull(doorCell);
            Assert.AreEqual(Direction.East, doorCell.Placements[1].Direction);
        }

        [Test]
        public void Planner_BBox_FiltersCells()
        {
            DmmMap map = DmmParser.ParseFile(FixturePath);
            Ss13TypeMapper mapper = new Ss13TypeMapper(Ss13TypeMapYaml.ParseFile(TypeMapPath));
            MapImportBBox bbox = new MapImportBBox(1, 1, 1, 1);
            MapImportPlan plan = MapImportPlanner.Build(map, mapper, bbox);
            Assert.Less(plan.Cells.Count, 18);
            Assert.Greater(plan.SkippedCells, 0);
        }
    }
}

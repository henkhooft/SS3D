using System.IO;
using System.Linq;
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

        private static string InfraFixturePath =>
            Path.GetFullPath(Path.Combine(Application.dataPath,
                "Scripts/Tests/EditMode/MapImport/Fixtures/tiny_infra.dmm"));

        private static string TypeMapPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "../Tools/map_import/ss13_type_map.yaml"));

        [Test]
        public void TypeMapYaml_ParsesDefaultsAndSortedPrefixes()
        {
            Ss13TypeMapConfig config = Ss13TypeMapYaml.ParseFile(TypeMapPath);
            Assert.AreEqual("TileGrey", config.DefaultFloor);
            Assert.Greater(config.Prefixes.Count, 5);
            Assert.GreaterOrEqual(config.Prefixes[0].Match.Length, config.Prefixes[^1].Match.Length);
            Assert.IsTrue(config.Prefixes.Any(p => p.Kind == MapImportKind.Cable));
            Assert.IsTrue(config.Prefixes.Any(p => p.Kind == MapImportKind.Pipe));
            Assert.IsTrue(config.Prefixes.Any(p => p.Kind == MapImportKind.Apc));
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
        public void Mapper_InfrastructurePrefixes()
        {
            Ss13TypeMapConfig config = Ss13TypeMapYaml.ParseFile(TypeMapPath);
            Ss13TypeMapper mapper = new Ss13TypeMapper(config);

            Assert.IsTrue(mapper.TryMatch("/obj/structure/cable", out Ss13TypeMatch cable));
            Assert.AreEqual(MapImportKind.Cable, cable.Kind);
            Assert.AreEqual("Cables", cable.SoName);

            Assert.IsTrue(mapper.TryMatch("/obj/machinery/light/small", out Ss13TypeMatch bulb));
            Assert.AreEqual("LightBulbFixture", bulb.SoName);

            Assert.IsTrue(mapper.TryMatch("/obj/machinery/light", out Ss13TypeMatch tube));
            Assert.AreEqual("LightTubeFixture", tube.SoName);
        }

        [Test]
        public void PipeResolver_NetworkTypeBeatsPipingLayer()
        {
            var atom = new DmmAtom { Path = "/obj/machinery/atmospherics/pipe/smart/manifold4w/supply" };
            atom.Vars["piping_layer"] = "3";
            Assert.AreEqual(MapImportPipeResolver.AtmosPipesL1, MapImportPipeResolver.Resolve(atom.Path, atom));

            atom = new DmmAtom { Path = "/obj/machinery/atmospherics/pipe/smart/manifold4w/scrubbers" };
            Assert.AreEqual(MapImportPipeResolver.AtmosPipesL2, MapImportPipeResolver.Resolve(atom.Path, atom));

            atom = new DmmAtom { Path = "/obj/machinery/atmospherics/pipe/smart/pipe" };
            atom.Vars["piping_layer"] = "4";
            Assert.AreEqual(MapImportPipeResolver.AtmosPipesL4, MapImportPipeResolver.Resolve(atom.Path, atom));
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
                Assert.GreaterOrEqual(cell.Placements.Count, 2);
            }

            MapImportCellPlan doorCell = plan.Cells.Find(c => c.Placements[1].SoName == "CivillianAirlock");
            Assert.IsNotNull(doorCell);
            Assert.AreEqual(Direction.East, doorCell.Placements[1].Direction);
        }

        [Test]
        public void Planner_TinyInfra_PlacesOverlaysAndResolvesPipes()
        {
            DmmMap map = DmmParser.ParseFile(InfraFixturePath);
            Ss13TypeMapper mapper = new Ss13TypeMapper(Ss13TypeMapYaml.ParseFile(TypeMapPath));
            MapImportPlan plan = MapImportPlanner.Build(map, mapper);

            Assert.AreEqual(1, plan.CablePlacements);
            Assert.GreaterOrEqual(plan.PipePlacements, 3); // supply + scrubbers + layer4
            Assert.AreEqual(1, plan.DisposalPlacements);
            Assert.AreEqual(1, plan.DisposalTerminalPlacements);
            Assert.AreEqual(1, plan.VentPlacements);
            Assert.AreEqual(1, plan.ApcPlacements);
            Assert.AreEqual(1, plan.LightPlacements);

            MapImportCellPlan cableCell = plan.Cells.Find(c =>
                c.Placements.Any(p => p.SoName == "Cables"));
            Assert.IsNotNull(cableCell);
            Assert.IsTrue(cableCell.Placements.Any(p => p.SoName == MapImportPipeResolver.AtmosPipesL1));
            Assert.IsTrue(cableCell.Placements.Any(p => p.SoName == MapImportPipeResolver.AtmosPipesL2));

            MapImportCellPlan disposalCell = plan.Cells.Find(c =>
                c.Placements.Any(p => p.SoName == "DisposalPipes"));
            Assert.IsNotNull(disposalCell);
            Assert.IsTrue(disposalCell.Placements.Any(p => p.SoName == "DisposalBin"));

            MapImportCellPlan ventCell = plan.Cells.Find(c =>
                c.Placements.Any(p => p.SoName == "Vent"));
            Assert.IsNotNull(ventCell);
            Assert.AreEqual(Direction.South,
                ventCell.Placements.Find(p => p.SoName == "Vent").Direction);

            MapImportCellPlan wallMountCell = plan.Cells.Find(c =>
                c.Placements.Any(p => p.SoName == "APC"));
            Assert.IsNotNull(wallMountCell);
            Assert.AreEqual(Direction.East,
                wallMountCell.Placements.Find(p => p.SoName == "APC").Direction);
            Assert.AreEqual(Direction.West,
                wallMountCell.Placements.Find(p => p.SoName == "LightTubeFixture").Direction);

            MapImportCellPlan layer4Cell = plan.Cells.Find(c =>
                c.Placements.Any(p => p.SoName == MapImportPipeResolver.AtmosPipesL4));
            Assert.IsNotNull(layer4Cell);
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

using System.Collections.Generic;
using NUnit.Framework;
using SS3D.Systems.Atmospherics;
using SS3D.Systems.Atmospherics.Pipes;
using SS3D.Systems.Tile;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EditorTests.Atmospherics
{
    /// <summary>
    /// The gas a map's storage chambers and starting line fills provide.
    /// </summary>
    public class AtmosGasSupplyTests
    {
        private readonly List<GameObject> _objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _objects)
                Object.DestroyImmediate(go);
            _objects.Clear();
        }

        [Test]
        public void HoldCell_RestoresTheMixEveryTick_WhileGasLeavesThroughABreach()
        {
            TileMapTestUtilities.MapContext context = TileMapTestUtilities.CreateContext(_objects);
            using AtmosSimulation sim = AtmosTestFixtures.CreateSealedRoomSimulation(context, 4, 4, out int map);
            var held = new TileCoord(map, 1, 1);
            float[] moles = MixMoles(AtmosSupplyMix.Oxygen, 200f);

            for (int tick = 0; tick < 20; tick++)
            {
                sim.HoldCell(held, moles, AtmosConstants.StandardTemperature);
                sim.Tick(AtmosConstants.TickInterval);
            }

            sim.HoldCell(held, moles, AtmosConstants.StandardTemperature);
            Assert.That(sim.DebugGetMoles(held, AtmosConstants.Oxygen), Is.EqualTo(200f).Within(0.01f));
            Assert.That(sim.DebugGetMoles(held, AtmosConstants.Nitrogen), Is.EqualTo(0f).Within(0.01f));
            Assert.Greater(sim.DebugGetMoles(new TileCoord(map, 2, 1), AtmosConstants.Oxygen), AtmosConstants.StationOxygenMoles * 1.5f,
                "The held cell pours into its neighbours");
        }

        [Test]
        public void HoldCell_AlreadyAtTheMix_LetsTheCellSleep()
        {
            TileMapTestUtilities.MapContext context = TileMapTestUtilities.CreateContext(_objects);
            using AtmosSimulation sim = AtmosTestFixtures.CreateSealedRoomSimulation(context, 4, 4, out int map);
            var held = new TileCoord(map, 1, 1);
            for (int tick = 0; tick < 5; tick++)
                sim.Tick(AtmosConstants.TickInterval);
            Assert.AreEqual(0, sim.ActiveCellCount, "Station air settles");

            float[] moles = MixMoles(AtmosSupplyMix.Air, sim.GetCellMoles(CellIndex(sim, held)));
            sim.HoldCell(held, moles, AtmosConstants.StandardTemperature);
            sim.Tick(AtmosConstants.TickInterval);

            Assert.AreEqual(0, sim.ActiveCellCount, "Holding the air a cell already has wakes nothing");
        }

        [TestCase(AtmosSupplyMix.Oxygen, 10f, 0f)]
        [TestCase(AtmosSupplyMix.Nitrogen, 0f, 10f)]
        public void FillMoles_PureMix_ClearsOtherGases(AtmosSupplyMix mix, float oxygen, float nitrogen)
        {
            float[] moles = { 3f, 3f, 3f, 3f };
            AtmosSupplyMixes.FillMoles(mix, 10f, moles);
            Assert.AreEqual(new[] { oxygen, nitrogen, 0f, 0f }, moles);
        }

        [Test]
        public void FillMoles_Air_MatchesStationAir()
        {
            float[] moles = MixMoles(AtmosSupplyMix.Air, AtmosConstants.StationOxygenMoles + AtmosConstants.StationNitrogenMoles);
            Assert.That(moles[AtmosConstants.Oxygen.Value], Is.EqualTo(AtmosConstants.StationOxygenMoles).Within(0.01f));
            Assert.That(moles[AtmosConstants.Nitrogen.Value], Is.EqualTo(AtmosConstants.StationNitrogenMoles).Within(0.01f));
        }

        private static float[] MixMoles(AtmosSupplyMix mix, float total)
        {
            var moles = new float[AtmosConstants.DefaultGasCount];
            AtmosSupplyMixes.FillMoles(mix, total, moles);
            return moles;
        }

        private static int CellIndex(AtmosSimulation sim, TileCoord coord)
        {
            Assert.IsTrue(sim.TryGetCellIndex(coord, out int index));
            return index;
        }
    }
}

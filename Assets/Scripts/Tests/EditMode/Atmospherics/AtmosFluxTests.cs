using NUnit.Framework;
using SS3D.Systems.Atmospherics;
using SS3D.Systems.Atmospherics.ECS;
using SS3D.Systems.Tile;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EditorTests.Atmospherics
{
    public class AtmosFluxTests
    {
        private List<GameObject> _instantiated;

        [SetUp]
        public void SetUp() => _instantiated = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _instantiated)
                Object.DestroyImmediate(go);
            _instantiated.Clear();
        }

        [Test]
        public void SealedRoom_ConservesMolesOverTicks()
        {
            const int size = 2;
            TileMapTestUtilities.MapContext context = TileMapTestUtilities.CreateContext(_instantiated);
            InitializeSealedRoomSimulation(context, size, out AtmosSimulation simulation);
            using (simulation)
            {
                float initialMoles = simulation.GetTotalMoles();
                Assert.Greater(initialMoles, 0f);

                for (int tick = 0; tick < 10; tick++)
                    simulation.Tick(AtmosConstants.TickInterval);

                Assert.AreEqual(initialMoles, simulation.GetTotalMoles(), 0.01f);
            }
        }

        [Test]
        public void RoomAdjacentToVacuum_LosesMoles()
        {
            TileMapTestUtilities.MapContext context = TileMapTestUtilities.CreateContext(_instantiated);
            TileMapTestUtilities.PlacePlenum(context, new Vector3(0, 0, 0));

            using var simulation = new AtmosSimulation(context.Query, context.Map.MapId, AtmosConstants.DefaultGasCount);
            simulation.CreateChunk(new TileChunkRef
            {
                MapId = context.Map.MapId,
                ChunkKey = Vector2Int.zero,
                Origin = Vector3.zero,
            });
            simulation.UpdateCell(new TileCoord(context.Map.MapId, 0, 0));

            float initialMoles = simulation.GetTotalMoles();
            for (int tick = 0; tick < 20; tick++)
                simulation.Tick(AtmosConstants.TickInterval);

            Assert.Less(simulation.GetTotalMoles(), initialMoles);
        }

        [Test]
        public void SealedRoom_ConservesThermalEnergyWithGradient()
        {
            const int size = 3;
            TileMapTestUtilities.MapContext context = TileMapTestUtilities.CreateContext(_instantiated);
            InitializeSealedRoomSimulation(context, size, out AtmosSimulation simulation);
            using (simulation)
            {
                // Heat one corner so a pressure gradient forms and advected moles carry enthalpy.
                simulation.DebugSetTemperature(new TileCoord(context.Map.MapId, AtmosTestFixtures.InteriorOrigin, AtmosTestFixtures.InteriorOrigin), 1000f);

                float initialEnergy = simulation.GetTotalThermalEnergy();
                float initialMoles = simulation.GetTotalMoles();
                Assert.Greater(initialEnergy, 0f);

                for (int tick = 0; tick < 30; tick++)
                    simulation.Tick(AtmosConstants.TickInterval);

                // Advection must conserve both moles and total thermal energy in a sealed room.
                Assert.AreEqual(initialMoles, simulation.GetTotalMoles(), initialMoles * 0.005f);
                Assert.AreEqual(initialEnergy, simulation.GetTotalThermalEnergy(), initialEnergy * 0.005f);
            }
        }

        [Test]
        public void CellExposedToVacuum_RadiatesHeatTowardSpace()
        {
            TileMapTestUtilities.MapContext context = TileMapTestUtilities.CreateContext(_instantiated);
            TileMapTestUtilities.PlacePlenum(context, new Vector3(0, 0, 0));

            using var simulation = new AtmosSimulation(context.Query, context.Map.MapId, AtmosConstants.DefaultGasCount);
            simulation.CreateChunk(new TileChunkRef
            {
                MapId = context.Map.MapId,
                ChunkKey = Vector2Int.zero,
                Origin = Vector3.zero,
            });
            simulation.UpdateCell(new TileCoord(context.Map.MapId, 0, 0));

            var coord = new TileCoord(context.Map.MapId, 0, 0);
            simulation.DebugSetTemperature(coord, 1000f);

            simulation.Tick(AtmosConstants.TickInterval);

            Assert.IsTrue(simulation.TryGetCellDebugInfo(coord, out AtmosCellDebugInfo info));

            // Venting alone preserves temperature and the cell has no gas neighbours, so any drop
            // is the new radiative heat sink to space. It must cool but never overshoot below space.
            Assert.Less(info.Temperature, 1000f);
            Assert.GreaterOrEqual(info.Temperature, AtmosConstants.SpaceTemperature);
        }

        [Test]
        public void SealedRoom_ConductionEqualizesTemperature()
        {
            const int size = 3;
            TileMapTestUtilities.MapContext context = TileMapTestUtilities.CreateContext(_instantiated);
            InitializeSealedRoomSimulation(context, size, out AtmosSimulation simulation);
            using (simulation)
            {
                simulation.DebugSetTemperature(new TileCoord(context.Map.MapId, AtmosTestFixtures.InteriorOrigin, AtmosTestFixtures.InteriorOrigin), 1000f);

                float initialEnergy = simulation.GetTotalThermalEnergy();
                GetTemperatureSpread(simulation, context.Map.MapId, size, out float initialMin, out float initialMax);
                float initialSpread = initialMax - initialMin;

                for (int tick = 0; tick < 120; tick++)
                    simulation.Tick(AtmosConstants.TickInterval);

                GetTemperatureSpread(simulation, context.Map.MapId, size, out float finalMin, out float finalMax);
                float finalSpread = finalMax - finalMin;

                // Conduction should pull temperatures together while energy stays conserved.
                Assert.Less(finalSpread, initialSpread * 0.25f);
                Assert.AreEqual(initialEnergy, simulation.GetTotalThermalEnergy(), initialEnergy * 0.005f);
            }
        }

        [Test]
        public void PlasmaFire_ConsumesPlasmaAndProducesCarbonDioxide()
        {
            const int size = 3;
            TileMapTestUtilities.MapContext context = TileMapTestUtilities.CreateContext(_instantiated);
            InitializeSealedRoomSimulation(context, size, out AtmosSimulation simulation);
            using (simulation)
            {
                var center = AtmosTestFixtures.InteriorCoord(context.Map.MapId, size);
                simulation.DebugAddMoles(center, AtmosConstants.Plasma, 10f);
                simulation.DebugAddMoles(center, AtmosConstants.Oxygen, 40f);
                simulation.DebugSetTemperature(center, 1000f);

                float initialPlasma = SumGas(simulation, context.Map.MapId, size, AtmosConstants.Plasma);
                Assert.Greater(initialPlasma, 0f);

                for (int tick = 0; tick < 60; tick++)
                    simulation.Tick(AtmosConstants.TickInterval);

                float finalPlasma = SumGas(simulation, context.Map.MapId, size, AtmosConstants.Plasma);
                float finalCarbonDioxide = SumGas(simulation, context.Map.MapId, size, AtmosConstants.CarbonDioxide);

                // Combustion should consume plasma and produce carbon dioxide.
                Assert.Less(finalPlasma, initialPlasma * 0.8f);
                Assert.Greater(finalCarbonDioxide, 0f);
            }
        }

        [Test]
        public void PlasmaFire_WithExcessGasTypeCount_DoesNotThrow()
        {
            const int size = 3;
            TileMapTestUtilities.MapContext context = TileMapTestUtilities.CreateContext(_instantiated);
            BuildSealedRoom(context, size);

            using var simulation = new AtmosSimulation(context.Query, context.Map.MapId, 65);
            simulation.CreateChunk(new TileChunkRef
            {
                MapId = context.Map.MapId,
                ChunkKey = Vector2Int.zero,
                Origin = Vector3.zero,
            });

            const int origin = 0;
            int outerSize = size + 2;
            for (int x = 0; x < outerSize; x++)
            {
                for (int z = 0; z < outerSize; z++)
                    simulation.UpdateCell(new TileCoord(context.Map.MapId, origin + x, origin + z));
            }

            var center = AtmosTestFixtures.InteriorCoord(context.Map.MapId, size);
            simulation.DebugAddMoles(center, AtmosConstants.Plasma, 5f);
            simulation.DebugAddMoles(center, AtmosConstants.Oxygen, 10f);
            simulation.DebugAddHeat(center, 1000f);

            Assert.DoesNotThrow(() => simulation.Tick(AtmosConstants.TickInterval));
        }

        [Test]
        public void EvacuatedSealedRoom_RetainsSmallVentAmounts()
        {
            const int size = 2;
            TileMapTestUtilities.MapContext context = TileMapTestUtilities.CreateContext(_instantiated);
            InitializeSealedRoomSimulation(context, size, out AtmosSimulation simulation);
            using (simulation)
            {
                var center = new TileCoord(context.Map.MapId, AtmosTestFixtures.InteriorOrigin, AtmosTestFixtures.InteriorOrigin);
                EvacuateInterior(context.Map.MapId, size, simulation);

                simulation.TryAddMolesAtTemperature(center, AtmosConstants.Oxygen, 2f, AtmosConstants.StandardTemperature);
                simulation.TryAddMolesAtTemperature(center, AtmosConstants.Nitrogen, 8f, AtmosConstants.StandardTemperature);

                for (int tick = 0; tick < 10; tick++)
                    simulation.Tick(AtmosConstants.TickInterval);

                Assert.Greater(simulation.GetTotalMoles(), 5f);
            }
        }

        [Test]
        public void SealedRoom_EqualPressureCompositionDiffuses()
        {
            const int size = 3;
            const float breathSwapMoles = 5f;

            TileMapTestUtilities.MapContext context = TileMapTestUtilities.CreateContext(_instantiated);
            InitializeSealedRoomSimulation(context, size, out AtmosSimulation simulation);
            using (simulation)
            {
                // Let pressure settle so the room is uniform before the breath-like swap.
                for (int tick = 0; tick < 20; tick++)
                    simulation.Tick(AtmosConstants.TickInterval);

                var center = AtmosTestFixtures.InteriorCoord(context.Map.MapId, size);
                var neighbour = new TileCoord(center.MapId, center.Grid.x + 1, center.Grid.y);

                Assert.IsTrue(simulation.TryRemoveMoles(
                    center,
                    AtmosConstants.Oxygen,
                    breathSwapMoles,
                    out float removed,
                    out float sourceTemperature));
                Assert.AreEqual(breathSwapMoles, removed, 0.01f);
                Assert.IsTrue(simulation.TryAddMolesAtTemperature(
                    center,
                    AtmosConstants.CarbonDioxide,
                    removed,
                    sourceTemperature));

                float initialCenterCo2 = simulation.DebugGetMoles(center, AtmosConstants.CarbonDioxide);
                float initialNeighbourCo2 = simulation.DebugGetMoles(neighbour, AtmosConstants.CarbonDioxide);
                float initialMoles = simulation.GetTotalMoles();
                Assert.Greater(initialCenterCo2, initialNeighbourCo2);

                for (int tick = 0; tick < 80; tick++)
                    simulation.Tick(AtmosConstants.TickInterval);

                float finalCenterCo2 = simulation.DebugGetMoles(center, AtmosConstants.CarbonDioxide);
                float finalNeighbourCo2 = simulation.DebugGetMoles(neighbour, AtmosConstants.CarbonDioxide);

                Assert.Less(finalCenterCo2, initialCenterCo2);
                Assert.Greater(finalNeighbourCo2, initialNeighbourCo2);
                Assert.AreEqual(initialMoles, simulation.GetTotalMoles(), initialMoles * 0.005f);

                GetPressureSpread(simulation, context.Map.MapId, size, out float minPressure, out float maxPressure);
                Assert.Less(maxPressure - minPressure, AtmosFluxConstants.PressureEpsilon * 2f);
            }
        }

        private static void EvacuateInterior(int mapId, int interiorSize, AtmosSimulation simulation)
        {
            for (int x = 0; x < interiorSize; x++)
            {
                for (int z = 0; z < interiorSize; z++)
                {
                    var coord = new TileCoord(mapId, AtmosTestFixtures.InteriorOrigin + x, AtmosTestFixtures.InteriorOrigin + z);
                    foreach (GasId gasId in new[] { AtmosConstants.Oxygen, AtmosConstants.Nitrogen, AtmosConstants.CarbonDioxide, AtmosConstants.Plasma })
                    {
                        while (simulation.TryRemoveMoles(coord, gasId, 1000f, out _, out _))
                        {
                        }
                    }
                }
            }
        }

        private static float SumGas(AtmosSimulation simulation, int mapId, int interiorSize, GasId gasId)
        {
            float total = 0f;
            for (int x = 0; x < interiorSize; x++)
            {
                for (int z = 0; z < interiorSize; z++)
                {
                    total += simulation.DebugGetMoles(
                        new TileCoord(mapId, AtmosTestFixtures.InteriorOrigin + x, AtmosTestFixtures.InteriorOrigin + z),
                        gasId);
                }
            }

            return total;
        }

        private static void GetTemperatureSpread(
            AtmosSimulation simulation, int mapId, int interiorSize, out float min, out float max)
        {
            min = float.MaxValue;
            max = float.MinValue;
            for (int x = 0; x < interiorSize; x++)
            {
                for (int z = 0; z < interiorSize; z++)
                {
                    Assert.IsTrue(simulation.TryGetCellDebugInfo(
                        new TileCoord(mapId, AtmosTestFixtures.InteriorOrigin + x, AtmosTestFixtures.InteriorOrigin + z),
                        out AtmosCellDebugInfo info));
                    min = Mathf.Min(min, info.Temperature);
                    max = Mathf.Max(max, info.Temperature);
                }
            }
        }

        private static void GetPressureSpread(
            AtmosSimulation simulation, int mapId, int interiorSize, out float min, out float max)
        {
            min = float.MaxValue;
            max = float.MinValue;
            for (int x = 0; x < interiorSize; x++)
            {
                for (int z = 0; z < interiorSize; z++)
                {
                    float pressure = simulation.GetCellPressure(
                        new TileCoord(mapId, AtmosTestFixtures.InteriorOrigin + x, AtmosTestFixtures.InteriorOrigin + z));
                    min = Mathf.Min(min, pressure);
                    max = Mathf.Max(max, pressure);
                }
            }
        }

        private static void BuildSealedRoom(TileMapTestUtilities.MapContext context, int interiorSize)
        {
            TileMapTestUtilities.BuildWalledRoom(context, 0, 0, interiorSize);
        }

        private static void InitializeSealedRoomSimulation(
            TileMapTestUtilities.MapContext context,
            int interiorSize,
            out AtmosSimulation simulation)
        {
            BuildSealedRoom(context, interiorSize);

            simulation = new AtmosSimulation(context.Query, context.Map.MapId, AtmosConstants.DefaultGasCount);
            simulation.CreateChunk(new TileChunkRef
            {
                MapId = context.Map.MapId,
                ChunkKey = Vector2Int.zero,
                Origin = Vector3.zero,
            });

            int outerSize = interiorSize + 2;
            for (int x = 0; x < outerSize; x++)
            {
                for (int z = 0; z < outerSize; z++)
                    simulation.UpdateCell(new TileCoord(context.Map.MapId, x, z));
            }
        }
    }
}

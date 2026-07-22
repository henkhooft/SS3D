using NUnit.Framework;
using SS3D.Systems.Atmospherics;
using SS3D.Systems.Atmospherics.Visualization;
using SS3D.Systems.Tile;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EditorTests.Atmospherics
{
    public class AtmosDirtyChunkTrackerTests
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
        public void DirtyChunkTracker_NotDirtyWhenNothingChangedBetweenUpdates()
        {
            TileMapTestUtilities.MapContext context = TileMapTestUtilities.CreateContext(_instantiated);
            using var simulation = AtmosTestFixtures.CreateSealedRoomSimulation(context, 2, out _);

            var tracker = new AtmosDirtyChunkTracker();
            var buffer = new List<Vector2Int>();

            // First update establishes the baseline; everything reads as "changed" from the
            // zeroed-out previous state.
            tracker.Update(simulation);
            tracker.ConsumeDirtyChunks(buffer);

            tracker.Update(simulation);
            tracker.ConsumeDirtyChunks(buffer);

            Assert.IsEmpty(buffer);
        }

        [Test]
        public void DirtyChunkTracker_MarksChunkDirtyWhenTemperatureChangesMeaningfully()
        {
            TileMapTestUtilities.MapContext context = TileMapTestUtilities.CreateContext(_instantiated);
            using var simulation = AtmosTestFixtures.CreateSealedRoomSimulation(context, 2, out int mapId);
            TileCoord coord = AtmosTestFixtures.InteriorCoord(mapId, 2);

            var tracker = new AtmosDirtyChunkTracker();
            var buffer = new List<Vector2Int>();
            tracker.Update(simulation);
            tracker.ConsumeDirtyChunks(buffer);

            simulation.DebugSetTemperature(coord, 850f);
            tracker.Update(simulation);
            tracker.ConsumeDirtyChunks(buffer);

            Assert.Contains(Vector2Int.zero, buffer);
        }

        [Test]
        public void DirtyChunkTracker_MarksChunkDirtyWhenFireIgnites()
        {
            TileMapTestUtilities.MapContext context = TileMapTestUtilities.CreateContext(_instantiated);
            using var simulation = AtmosTestFixtures.CreateSealedRoomSimulation(context, 1, out int mapId);
            TileCoord coord = AtmosTestFixtures.InteriorCoord(mapId, 1);

            var tracker = new AtmosDirtyChunkTracker();
            var buffer = new List<Vector2Int>();
            tracker.Update(simulation);
            tracker.ConsumeDirtyChunks(buffer);

            AtmosTestFixtures.IgnitePlasmaFire(simulation, coord, plasmaMoles: 10f, oxygenMoles: 40f);
            simulation.Tick(AtmosConstants.TickInterval);

            tracker.Update(simulation);
            tracker.ConsumeDirtyChunks(buffer);

            Assert.Contains(Vector2Int.zero, buffer);
        }
    }
}

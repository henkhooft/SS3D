using System.Collections.Generic;
using NUnit.Framework;
using SS3D.Systems.Atmospherics;
using SS3D.Systems.Tile;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EditorTests.Atmospherics
{
    public class AtmosWindTests
    {
        private readonly List<GameObject> _objects = new();
        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _objects) if (go != null) Object.DestroyImmediate(go);
            _objects.Clear();
        }

        [Test]
        public void WallBlocksWind_DestructionOpensFlow_RepairStopsIt()
        {
            var context = TileMapTestUtilities.CreateContext(_objects);
            using var sim = AtmosTestFixtures.CreateSealedRoomSimulation(context, 3, out int map);
            var inside = new TileCoord(map, 3, 2);
            Assert.That(AtmosWind.Sample(sim, inside), Is.EqualTo(Vector3.zero));
            Vector3 breach = new(4, 0, 2);
            context.Construction.TryClearTile(breach, TileLayer.Turf, Direction.North);
            sim.QueueCellRefresh(new TileCoord(map, 4, 2));
            for (int tick = 0; tick < 3; tick++) sim.Tick(AtmosConstants.TickInterval);
            Vector3 wind = AtmosWind.Sample(sim, inside);
            Assert.That(wind.x, Is.GreaterThan(AtmosWind.MinPressure));
            Assert.That(Mathf.Abs(wind.z), Is.LessThan(wind.x));
            TileMapTestUtilities.PlaceAirtightWall(context, breach);
            sim.UpdateCell(new TileCoord(map, 4, 2));
            Assert.That(AtmosWind.Sample(sim, inside).x, Is.LessThanOrEqualTo(0f));
        }

        [Test]
        public void EvacuatedCellStopsPulling_UnknownCellHasNoWind()
        {
            var context = TileMapTestUtilities.CreateContext(_objects);
            TileMapTestUtilities.PlacePlenum(context, new Vector3(2, 0, 2));
            using var sim = new AtmosSimulation(context.Query, context.Map.MapId, AtmosConstants.DefaultGasCount);
            sim.CreateChunk(new TileChunkRef { MapId = context.Map.MapId, ChunkKey = Vector2Int.zero, Origin = Vector3.zero });
            var cell = new TileCoord(context.Map.MapId, 2, 2);
            // Close three sides so pressure has one directed outlet.
            TileMapTestUtilities.PlaceAirtightWall(context, new Vector3(1, 0, 2));
            TileMapTestUtilities.PlaceAirtightWall(context, new Vector3(2, 0, 1));
            TileMapTestUtilities.PlaceAirtightWall(context, new Vector3(2, 0, 3));
            sim.UpdateCell(new TileCoord(context.Map.MapId, 1, 2));
            sim.UpdateCell(new TileCoord(context.Map.MapId, 2, 1));
            sim.UpdateCell(new TileCoord(context.Map.MapId, 2, 3));
            Assert.That(AtmosWind.Sample(sim, cell).x, Is.GreaterThan(AtmosWind.KnockdownPressure));
            Assert.That(AtmosWind.Sample(sim, new TileCoord(context.Map.MapId, 900, 900)), Is.EqualTo(Vector3.zero));
            for (int tick = 0; tick < 200; tick++) sim.Tick(AtmosConstants.TickInterval);
            Assert.That(AtmosWind.Sample(sim, cell).magnitude, Is.LessThan(AtmosWind.MinPressure));
        }

        [Test]
        public void GustAccelerationIsBounded()
        {
            Assert.That(AtmosWind.Acceleration(Vector3.right * 10000f).magnitude,
                Is.EqualTo(AtmosWind.MaxAcceleration).Within(0.001f));
            Assert.That(AtmosWind.Acceleration(Vector3.zero), Is.EqualTo(Vector3.zero));
        }
    }
}

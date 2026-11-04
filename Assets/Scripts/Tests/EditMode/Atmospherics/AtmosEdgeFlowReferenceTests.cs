using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SS3D.Systems.Atmospherics;
using SS3D.Systems.Atmospherics.ECS;
using SS3D.Systems.Tile;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EditorTests.Atmospherics
{
    /// <summary>
    /// Room-scale reference cases for the atmos edge-flow effort. Each case reports how a real
    /// <see cref="AtmosSimulation"/> behaves, so a change to gas transport shows up as a change in
    /// these numbers. See Documents/architecture/2026-11_atmos-edge-flow.md.
    /// </summary>
    public class AtmosEdgeFlowReferenceTests
    {
        private const float MaxSeconds = 1200f;
        private const float OnsetDropKilopascals = 1f;
        private const float BaselineTolerance = 5f;

        private readonly List<GameObject> _objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _objects) if (go != null) Object.DestroyImmediate(go);
            _objects.Clear();
        }

        [Test]
        public void Room10x10_OneTileHole()
        {
            Metrics metrics = RunBreach(10, 10, "room 10x10");
            AssertBaseline(metrics.SecondsToHalf, 19.8f, "time to half pressure");
            AssertBaseline(metrics.SecondsToTenth, 65.4f, "time to a tenth of pressure");
            AssertBaseline(metrics.FarOnset, 1.2f, "far-field onset");
            AssertBaseline(metrics.PeakHole, 5.09f, "peak wind in the hole");
            AssertBaseline(metrics.PeakNear, 3.01f, "peak wind beside the hole");
            AssertBaseline(metrics.PeakMiddle, 0.3f, "peak wind mid-room");
            AssertBaseline(metrics.PeakFar, 0.06f, "peak wind at the far wall");
        }

        [Test]
        public void Corridor40x8_OneTileHole()
        {
            Metrics metrics = RunBreach(40, 8, "corridor 40x8");
            AssertBaseline(metrics.SecondsToHalf, 77.8f, "time to half pressure");
            AssertBaseline(metrics.FarOnset, 10.6f, "far-field onset");
            AssertBaseline(metrics.PeakMiddle, 0.2f, "peak wind mid-corridor");
            AssertBaseline(metrics.PeakFar, 0.046f, "peak wind at the far end");
        }

        [Test]
        public void TwoRooms_DoorOpensOntoEvacuatedRoom()
        {
            const int roomSize = 6;
            var context = TileMapTestUtilities.CreateContext(_objects);
            using var sim = AtmosTestFixtures.CreateSealedRoomSimulation(context, roomSize * 2 + 1, roomSize, out int map);

            int dividerX = roomSize + 1;
            for (int z = 1; z <= roomSize; z++)
            {
                TileMapTestUtilities.PlaceAirtightWall(context, new Vector3(dividerX, 0, z));
                sim.UpdateCell(new TileCoord(map, dividerX, z));
            }

            List<TileCoord> full = Interior(map, 1, roomSize, roomSize);
            List<TileCoord> empty = Interior(map, dividerX + 1, roomSize, roomSize);
            foreach (TileCoord coord in empty)
            {
                sim.TryRemoveMoles(coord, AtmosConstants.Oxygen, float.MaxValue, out _, out _);
                sim.TryRemoveMoles(coord, AtmosConstants.Nitrogen, float.MaxValue, out _, out _);
            }

            int doorZ = roomSize / 2;
            var door = new TileCoord(map, dividerX, doorZ);
            context.Construction.TryClearTile(new Vector3(dividerX, 0, doorZ), TileLayer.Turf, Direction.North);
            sim.QueueCellRefresh(door);

            var farEmpty = new TileCoord(map, roomSize * 2 + 1, doorZ);
            var nearDoor = new TileCoord(map, dividerX - 1, doorZ);
            float settled = -1f, onset = -1f, peakWind = 0f;
            int ticks = Mathf.CeilToInt(MaxSeconds / AtmosConstants.TickInterval);
            for (int tick = 1; tick <= ticks && settled < 0f; tick++)
            {
                sim.Tick(AtmosConstants.TickInterval);
                float seconds = tick * AtmosConstants.TickInterval;
                peakWind = Mathf.Max(peakWind, WindSpeed(sim, nearDoor));
                if (onset < 0f && sim.GetCellPressure(farEmpty) >= OnsetDropKilopascals) onset = seconds;
                if (MeanPressure(sim, full) - MeanPressure(sim, empty) < 5f) settled = seconds;
            }

            TestContext.WriteLine($"[edge-flow-ref] two rooms: settled={Show(settled)} farOnset={Show(onset)} doorWind={peakWind:F2} m/s");
            AssertBaseline(settled, 9.8f, "time to equalise");
            AssertBaseline(onset, 0.6f, "far-field onset");
            AssertBaseline(peakWind, 3.52f, "peak wind at the door");
        }

        private Metrics RunBreach(int width, int depth, string label)
        {
            var context = TileMapTestUtilities.CreateContext(_objects);
            using var sim = AtmosTestFixtures.CreateSealedRoomSimulation(context, width, depth, out int map);

            int holeZ = depth / 2;
            var hole = new TileCoord(map, width + 1, holeZ);

            // Destruction clears only the wall's own layer, so a real hole is a floored air tile
            // between the room and space. That neck about doubles drain time over a bare vacuum edge.
            context.Construction.TryClearTile(new Vector3(width + 1, 0, holeZ), TileLayer.Turf, Direction.North);
            sim.QueueCellRefresh(hole);
            sim.Tick(AtmosConstants.TickInterval);
            Assert.That(sim.TryGetCellIndex(hole, out int holeIndex), Is.True);
            Assert.That(sim.CellMeta[holeIndex].State, Is.Not.EqualTo(AtmosCellState.Blocked), "hole stayed sealed");
            Assert.That(sim.TryGetCellIndex(new TileCoord(map, width + 2, holeZ), out int spaceIndex), Is.True);
            Assert.That(sim.CellMeta[spaceIndex].State, Is.EqualTo(AtmosCellState.Vacuum), "hole does not open onto space");

            List<TileCoord> room = Interior(map, 1, width, depth);
            float start = MeanPressure(sim, room);
            float startMoles = TotalMoles(sim, room);
            var near = new TileCoord(map, width, holeZ);
            var middle = new TileCoord(map, 1 + width / 2, holeZ);
            var far = new TileCoord(map, 1, holeZ);
            float farStart = sim.GetCellPressure(far);

            var metrics = new Metrics { SecondsToHalf = -1f, SecondsToTenth = -1f, SecondsToHalfMoles = -1f, FarOnset = -1f };
            int ticks = Mathf.CeilToInt(MaxSeconds / AtmosConstants.TickInterval);
            for (int tick = 1; tick <= ticks && metrics.SecondsToTenth < 0f; tick++)
            {
                sim.Tick(AtmosConstants.TickInterval);
                float seconds = tick * AtmosConstants.TickInterval;
                float pressure = MeanPressure(sim, room);
                if (metrics.SecondsToHalf < 0f && pressure <= start * 0.5f) metrics.SecondsToHalf = seconds;
                if (metrics.SecondsToHalfMoles < 0f && TotalMoles(sim, room) <= startMoles * 0.5f) metrics.SecondsToHalfMoles = seconds;
                if (metrics.SecondsToTenth < 0f && pressure <= start * 0.1f) metrics.SecondsToTenth = seconds;
                if (metrics.FarOnset < 0f && farStart - sim.GetCellPressure(far) >= OnsetDropKilopascals) metrics.FarOnset = seconds;
                metrics.PeakHole = Mathf.Max(metrics.PeakHole, WindSpeed(sim, hole));
                metrics.PeakNear = Mathf.Max(metrics.PeakNear, WindSpeed(sim, near));
                metrics.PeakMiddle = Mathf.Max(metrics.PeakMiddle, WindSpeed(sim, middle));
                metrics.PeakFar = Mathf.Max(metrics.PeakFar, WindSpeed(sim, far));
            }

            TestContext.WriteLine(
                $"[edge-flow-ref] {label}: half={Show(metrics.SecondsToHalf)} halfMoles={Show(metrics.SecondsToHalfMoles)} tenth={Show(metrics.SecondsToTenth)} " +
                $"farOnset={Show(metrics.FarOnset)} wind hole/near/mid/far={metrics.PeakHole:F2}/{metrics.PeakNear:F2}/{metrics.PeakMiddle:F2}/{metrics.PeakFar:F2} m/s");
            return metrics;
        }

        // Edge flow's untuned numbers. Tuning is meant to break these: phase 3 replaces them with
        // Decision 5's targets rather than widening the band.
        private static void AssertBaseline(float measured, float baseline, string what) =>
            Assert.That(measured, Is.EqualTo(baseline).Within(BaselineTolerance).Percent, what);

        private static float WindSpeed(AtmosSimulation sim, TileCoord coord) =>
            AtmosWind.Sample(sim, coord).magnitude;

        private static List<TileCoord> Interior(int map, int minX, int width, int depth)
        {
            var cells = new List<TileCoord>(width * depth);
            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < depth; z++)
                    cells.Add(new TileCoord(map, minX + x, 1 + z));
            }

            return cells;
        }

        private static float MeanPressure(AtmosSimulation sim, List<TileCoord> cells) =>
            cells.Sum(sim.GetCellPressure) / cells.Count;

        private static float TotalMoles(AtmosSimulation sim, List<TileCoord> cells)
        {
            float total = 0f;
            foreach (TileCoord coord in cells)
            {
                for (ushort gas = 0; gas < AtmosConstants.DefaultGasCount; gas++)
                    total += sim.DebugGetMoles(coord, new GasId(gas));
            }

            return total;
        }

        private static string Show(float seconds) => seconds < 0f ? $">{MaxSeconds:F0}s" : $"{seconds:F1}s";

        private struct Metrics
        {
            public float SecondsToHalf;
            public float SecondsToHalfMoles;
            public float SecondsToTenth;
            public float FarOnset;
            public float PeakHole;
            public float PeakNear;
            public float PeakMiddle;
            public float PeakFar;
        }
    }
}

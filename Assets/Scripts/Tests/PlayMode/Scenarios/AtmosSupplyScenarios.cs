using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SS3D.Core;
using SS3D.Systems.Atmospherics;
using SS3D.Systems.Atmospherics.Pipes;
using SS3D.Systems.Tile;
using SS3D.Systems.Tile.MapEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace SS3D.Tests
{
    /// <summary>
    /// Classic MiniStation's atmos room: the air chamber feeds the vents through the Mixed line, and
    /// scrubbers send what they take to the Waste line.
    /// </summary>
    [Category(TestCategories.Scenarios)]
    [Category(TestCategories.Environment)]
    public class AtmosSupplyScenarios : ScenarioFixture
    {
        private const float TargetRoomKpa = 5f;

        private static readonly Vector2Int Intake = new(2, -10);
        private static readonly Vector2Int Source = new(3, -10);
        private static readonly RectInt VentedRoom = new(16, 6, 6, 3);
        private static readonly Vector2Int RoomVent = new(17, 7);
        private static readonly Vector2Int RoomScrubber = new(20, 7);

        protected override string StationFixture => "Builds/Game/Data/StationTemplates/classic-ministation.json";

        protected override bool ChecksRoomAir => false;

        private static AtmosSubSystem Atmos => SubSystems.Get<AtmosSubSystem>();

        private static TileCoord Tile(Vector2Int cell) => new(SubSystems.Get<TileSubSystem>().CurrentMap.MapId, cell);

        private static T[] Devices<T>() where T : AtmosPortControllerBase =>
            Object.FindObjectsByType<T>(FindObjectsSortMode.None);

        private static T DeviceAt<T>(Vector2Int cell) where T : AtmosPortControllerBase =>
            Devices<T>().Single(device => device.OriginTile.Grid == cell);

        private static GasPipeNetworkId NetworkOf(AtmosPortControllerBase device)
        {
            Assert.IsTrue(device.TryGetConnectedNetwork(out GasPipeNetworkId network), $"{device.name} at {device.OriginTile.Grid} has a pipe network");
            return network;
        }

        // Guards: a loaded station starts with its distribution line filled, the chamber at its fixed mix, and every vent and intake on that one line while the room scrubbers stay off it (AtmosLineFill.ApplyAll, AtmosGasSource, AtmosDevicePipeResolver) · Not cheaper because: needs the importer's real template loaded through the tilemap, with devices spawned and ticking
        [UnityTest]
        public IEnumerator ClassicMiniStation_StartsWithChamberAndLinesWired()
        {
            yield return WaitFor(() => Atmos.Simulation.GetCellPressure(Tile(Source)) > AtmosPipeConstants.ChamberPressureKpa - 1f,
                "the source holds the chamber at pressure", 10f);
            yield return WaitFor(() => Devices<VentController>().Concat<AtmosPortControllerBase>(Devices<ScrubberController>())
                .All(device => device.TryGetConnectedNetwork(out _)), "every vent and scrubber joins a network", 10f);

            GasPipeNetworkId supply = NetworkOf(DeviceAt<ScrubberController>(Intake));
            Assert.That(Devices<VentController>().Select(NetworkOf), Is.All.EqualTo(supply), "All vents draw on the intake's line");
            GasPipeNetworkId[] waste = Devices<ScrubberController>().Where(scrubber => scrubber.OriginTile.Grid != Intake)
                .Select(NetworkOf).Distinct().ToArray();
            Assert.That(waste, Has.None.EqualTo(supply), "Room scrubbers stay off the distribution line");

            Assert.IsTrue(Atmos.PipeRegistry.TryGetNetwork(supply, out GasPipeNetworkRecord record));
            Assert.Greater(record.GetPressure(AtmosConstants.DefaultGasCount), AtmosConstants.StandardPressure,
                "The line starts above room pressure, so vents have air from the first moment");
            Assert.Greater(record.Moles[AtmosConstants.Nitrogen.Value], record.Moles[AtmosConstants.Oxygen.Value], "The line holds air");
        }

        // Guards: gas taken from the chamber by the intake reaches a depressurised room through its vent, with the line's starting fill out of the way (AtmosGasSource, ScrubberController, VentController, Mixed line attachment) · Not cheaper because: needs the turf sim, pipe network, powered devices and the shipped map's real run between them
        [UnityTest]
        public IEnumerator Chamber_RefillsADepressurisedRoomThroughItsVent()
        {
            ChangesRoom = true;
            ScrubberController intake = DeviceAt<ScrubberController>(Intake);
            VentController vent = DeviceAt<VentController>(RoomVent);
            yield return WaitFor(() => intake.TryGetConnectedNetwork(out _) && vent.TryGetConnectedNetwork(out _), "the intake and vent join their line", 10f);

            foreach (ScrubberController scrubber in Devices<ScrubberController>().Where(scrubber => scrubber != intake))
                scrubber.ServerSetEnabled(false);
            foreach (VentController other in Devices<VentController>().Where(other => other != vent))
                other.ServerSetEnabled(false);

            Assert.IsTrue(Atmos.PipeRegistry.TryGetNetwork(NetworkOf(intake), out GasPipeNetworkRecord line));
            System.Array.Clear(line.Moles, 0, line.Moles.Length);
            DrainRoom();
            Assert.Less(RoomPressure(), 1f, "The room starts empty");

            yield return WaitFor(() => RoomPressure() >= TargetRoomKpa, "the vent refills the room from the chamber", 45f);
            Assert.Greater(Atmos.Simulation.GetCellPressure(Tile(Source)), AtmosPipeConstants.ChamberPressureKpa - 1f, "The chamber never runs down");
            Assert.That(line.Moles[AtmosConstants.Oxygen.Value] / line.GetTotalMoles(AtmosConstants.DefaultGasCount),
                Is.EqualTo(0.2f).Within(0.02f), "The intake carries the chamber's mix, not whichever gas it reaches first");
        }

        // Guards: a room scrubber carries CO₂ off the turf into the Waste line and not the distribution line (line attachment on a tile with both lines) · Not cheaper because: needs a powered scrubber spawned on the shipped map's pipes
        [UnityTest]
        public IEnumerator Scrubber_MovesCarbonDioxideToTheWasteLine()
        {
            ChangesRoom = true;
            ScrubberController scrubber = DeviceAt<ScrubberController>(RoomScrubber);
            yield return WaitFor(() => scrubber.TryGetConnectedNetwork(out _), "the scrubber joins its line", 10f);
            GasPipeNetworkId waste = NetworkOf(scrubber);
            GasPipeNetworkId supply = NetworkOf(DeviceAt<VentController>(RoomVent));

            Atmos.Simulation.DebugAddMoles(Tile(RoomScrubber), AtmosConstants.CarbonDioxide, 20f);
            yield return WaitFor(() => Atmos.GetPipeNetworkMoles(waste, AtmosConstants.CarbonDioxide) >= 1f, "CO₂ reaches the waste line", 15f);
            Assert.AreEqual(0f, Atmos.GetPipeNetworkMoles(supply, AtmosConstants.CarbonDioxide), "None of it reaches the distribution line");
        }

        // Guards: map logic objects draw only while authoring (TileObjectSo.mapEditorOnly through PlacedTileObject visibility) · Not cheaper because: FishNet's host visibility pass re-enables renderers on a real spawn, which EditMode tiles never reach
        [UnityTest]
        public IEnumerator GasSource_IsHiddenInPlay_AndDrawnInTheMapEditor()
        {
            ChangesRoom = true;
            Renderer marker = Object.FindAnyObjectByType<AtmosGasSource>().GetComponentInChildren<Renderer>(true);
            MapEditorSubSystem editor = SubSystems.Get<MapEditorSubSystem>();
            Assert.IsTrue(marker.forceRenderingOff, "Players never see the source");

            SetEditorActive(editor, true);
            try
            {
                yield return Hold(0.2f);
                Assert.IsTrue(marker.enabled && !marker.forceRenderingOff, "A mapper can see the source");
                SetEditorActive(editor, false);
                yield return null;
                Assert.IsTrue(marker.forceRenderingOff, "Closing the editor hides it again");
            }
            finally
            {
                if (editor.IsActive)
                    SetEditorActive(editor, false);
            }
        }

        private static void SetEditorActive(MapEditorSubSystem editor, bool active) =>
            typeof(MapEditorSubSystem).GetMethod("SetActive", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(editor, new object[] { active });

        private static void DrainRoom()
        {
            AtmosSimulation simulation = Atmos.Simulation;
            foreach (Vector2Int cell in VentedRoom.allPositionsWithin)
            {
                simulation.TryRemoveMoles(Tile(cell), AtmosConstants.Oxygen, float.MaxValue, out _, out _);
                simulation.TryRemoveMoles(Tile(cell), AtmosConstants.Nitrogen, float.MaxValue, out _, out _);
            }
        }

        private static float RoomPressure()
        {
            float total = 0f;
            int cells = 0;
            foreach (Vector2Int cell in VentedRoom.allPositionsWithin)
            {
                total += Atmos.Simulation.GetCellPressure(Tile(cell));
                cells++;
            }

            return total / cells;
        }
    }
}

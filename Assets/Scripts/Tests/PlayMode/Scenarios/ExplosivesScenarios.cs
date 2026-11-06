using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SS3D.Core;
using SS3D.Systems.Atmospherics;
using SS3D.Systems.Atmospherics.Pipes;
using SS3D.Systems.Inventory.Items;
using SS3D.Systems.StructuralDamage;
using SS3D.Systems.Tile;
using UnityEngine;
using UnityEngine.TestTools;

namespace SS3D.Tests
{
    /// <summary>A real C4 set off in the middle of the test room, where its walls take too little force to break.</summary>
    [Category(TestCategories.Scenarios)]
    [Category(TestCategories.Explosives)]
    public class ExplosivesScenarios : ScenarioFixture
    {
        private const string C4 = "d5222e435d7aa2d45b959a73ebc874c9";
        private const float C4Yield = 180f;
        private const float C4Falloff = 45f;

        private static readonly Vector3 Epicenter = new(0f, 0f, -1f);

        private static TileSubSystem Tiles => SubSystems.Get<TileSubSystem>();

        // Guards: one C4 strips floors without venting an undamaged room, a second breaches the cracked hull, and plenums beside bare cells draw so space does not show under the neighbouring floor (explosives-destruction §3, TileUnderfloorVisibility) · Not cheaper because: needs the real charge, atmos, FishNet observers and renderer state on a live host together
        [UnityTest]
        public IEnumerator C4_CracksFreshHullWithoutVenting_ThenASecondBlastBreachesIt()
        {
            ChangesRoom = true;
            Teleport(Body, new Vector3(5f, Body.transform.position.y, 1f));
            var diagonalOnly = new Vector3(2f, 0f, 1f);
            var sideRoom = new Vector3(5f, 0f, -1f);
            TileCoord epicenter = Coord(Epicenter);

            Item charge = SubSystems.Get<ItemSubSystem>().SpawnItem(C4, Epicenter + Vector3.up * 0.2f, Quaternion.identity);
            yield return Hold(0.5f);
            Assert.IsFalse(IsDrawn(Plenum(diagonalOnly)), "A plenum under an intact room stays hidden");

            typeof(TimedCharge).GetMethod("Detonate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(charge.GetComponent<TimedCharge>(), null);
            Assert.IsTrue(IsDrawn(Plenum(diagonalOnly)), "The surrounding plenum shows at once: remote clients have no timed observer pass to fall back on");
            yield return Hold(3f);

            Assert.IsFalse(Tiles.QueryService.TryGetOccupant(epicenter, TileLayer.Turf, Direction.North, out _), "The floor at the charge is gone");
            Assert.AreEqual(StructuralIntegrityStage.Cracked, Plenum(Epicenter).IntegrityStage, "One charge cracks the plating");
            Assert.LessOrEqual(ScenarioAtmos.Read().MaxPressureDeviationKpa, 0.5f, "Cracked plating holds the room's air");
            Assert.IsTrue(Tiles.QueryService.TryGetOccupant(Coord(diagonalOnly), TileLayer.Turf, Direction.North, out _), "The corner keeps its floor");
            Assert.IsTrue(IsDrawn(Plenum(diagonalOnly)), "A diagonal bare cell keeps the plenum under this floor drawn");
            Assert.IsFalse(IsDrawn(Plenum(sideRoom)), "The side room stays fully covered");

            SubSystems.Get<StructuralDamageSubSystem>().ResolveBlast(epicenter, C4Yield, C4Falloff);

            Assert.IsTrue(Tiles.QueryService.TryGetOccupancy(epicenter, out TileOccupancy occupancy) && !occupancy.HasPlenum, "The second blast opens the hull");
            AtmosSimulation simulation = SubSystems.Get<AtmosSubSystem>().Simulation;
            yield return WaitFor(() => AtmosAreaSampler.TrySampleTile(Coord(diagonalOnly), simulation, out AtmosAreaSample corner)
                && corner.AveragePressureKpa < AtmosConstants.StandardPressure - 5f, "the room vents through the hole", 10f);
        }

        private static TileCoord Coord(Vector3 position) => Tiles.QueryService.WorldToTile(position, Tiles.CurrentMap.MapId);

        private static PlacedTileObject Plenum(Vector3 position)
        {
            Assert.IsTrue(Tiles.QueryService.TryGetOccupant(Coord(position), TileLayer.Plenum, Direction.North, out ITileOccupant occupant),
                $"A plenum exists at {position}");
            return (PlacedTileObject)occupant;
        }

        private static bool IsDrawn(PlacedTileObject tile) => tile.GetComponentsInChildren<Renderer>(true)
            .Any(renderer => renderer.enabled && !renderer.forceRenderingOff);
    }
}

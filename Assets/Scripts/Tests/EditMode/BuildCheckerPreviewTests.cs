using EditorTests;
using NUnit.Framework;
using SS3D.Systems.Tile;
using System.Collections.Generic;
using UnityEngine;

namespace SS3D.Tests.EditMode
{
    public sealed class BuildCheckerPreviewTests
    {
        private readonly List<GameObject> _instantiated = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = _instantiated.Count - 1; i >= 0; i--)
            {
                if (_instantiated[i] != null)
                    Object.DestroyImmediate(_instantiated[i]);
            }

            _instantiated.Clear();
        }

        [Test]
        public void Preview_FloorWithoutPlenum_FailsWithMissingPlenum()
        {
            TileMapTestUtilities.MapContext context = TileMapTestUtilities.CreateContext(_instantiated);
            TileObjectSo floor = TileMapTestUtilities.CreateTileSo(TileLayer.Turf, "SteelFloor");
            floor.genericType = TileObjectGenericType.Floor;

            PreviewResult preview = context.Construction.TryPreviewTile(
                floor, new Vector3(3, 0, 3), Direction.North, replaceExisting: false);

            Assert.IsFalse(preview.CanBuild);
            CollectionAssert.Contains(preview.Failures, BuildFailReason.MissingOrInvalidPlenum);
            Assert.AreEqual("Needs a plenum or catwalk underneath", preview.PrimaryMessage);
        }

        [Test]
        public void Preview_FloorWithPlenum_Succeeds()
        {
            TileMapTestUtilities.MapContext context = TileMapTestUtilities.CreateContext(_instantiated);
            Vector3 pos = new(4, 0, 4);
            TileMapTestUtilities.PlacePlenum(context, pos);

            TileObjectSo floor = TileMapTestUtilities.CreateTileSo(TileLayer.Turf, "SteelFloor");
            floor.genericType = TileObjectGenericType.Floor;

            PreviewResult preview = context.Construction.TryPreviewTile(floor, pos, Direction.North, replaceExisting: false);

            Assert.IsTrue(preview.CanBuild);
            Assert.IsEmpty(preview.Failures);
        }

        [Test]
        public void Preview_OccupiedLayerWithoutReplace_FailsWithLayerOccupied()
        {
            TileMapTestUtilities.MapContext context = TileMapTestUtilities.CreateContext(_instantiated);
            Vector3 pos = new(5, 0, 5);
            TileMapTestUtilities.PlacePlenum(context, pos);

            TileObjectSo floor = TileMapTestUtilities.CreateTileSo(TileLayer.Turf, "SteelFloor");
            floor.genericType = TileObjectGenericType.Floor;
            Assert.IsTrue(context.Construction.TryPlaceTile(floor, pos, Direction.North, replaceExisting: false).Success);

            TileObjectSo otherFloor = TileMapTestUtilities.CreateTileSo(TileLayer.Turf, "WoodFloor");
            otherFloor.genericType = TileObjectGenericType.Floor;

            PreviewResult preview = context.Construction.TryPreviewTile(
                otherFloor, pos, Direction.North, replaceExisting: false);

            Assert.IsFalse(preview.CanBuild);
            CollectionAssert.Contains(preview.Failures, BuildFailReason.LayerOccupied);
        }

        [Test]
        public void Preview_ReplaceOnOccupiedLayer_WithoutPlenumStillFails()
        {
            ITileLocation[] locations = CreateEmptyLocations();
            TileObjectSo floor = TileMapTestUtilities.CreateTileSo(TileLayer.Turf, "SteelFloor");
            floor.genericType = TileObjectGenericType.Floor;

            // Occupy turf without a valid plenum underneath.
            var turf = (SingleTileLocation)locations[(int)TileLayer.Turf];
            GameObject go = new("ExistingFloor");
            _instantiated.Add(go);
            turf.PlacedObject = go.AddComponent<PlacedTileObject>();

            BuildFailReason[] failures = BuildChecker.Evaluate(
                locations, floor, Direction.North, Vector3.zero, new PlacedTileObject[8], replaceExisting: true);

            CollectionAssert.Contains(failures, BuildFailReason.MissingOrInvalidPlenum);
            CollectionAssert.DoesNotContain(failures, BuildFailReason.LayerOccupied);
        }

        [Test]
        public void Format_KnownReasons_AreNonEmpty()
        {
            foreach (BuildFailReason reason in System.Enum.GetValues(typeof(BuildFailReason)))
                Assert.IsFalse(string.IsNullOrWhiteSpace(BuildFailMessages.Format(reason)));
        }

        private static ITileLocation[] CreateEmptyLocations()
        {
            TileLayer[] layers = TileHelper.GetTileLayers();
            var locations = new ITileLocation[layers.Length];
            foreach (TileLayer layer in layers)
                locations[(int)layer] = TileHelper.CreateTileLocation(layer, 0, 0);
            return locations;
        }
    }
}

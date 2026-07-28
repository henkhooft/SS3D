using NUnit.Framework;
using SS3D.Systems.Examine;
using SS3D.Systems.StructuralDamage;
using SS3D.Systems.Tile;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EditorTests
{
    public class StructuralIntegrityPresentationTests
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
        public void ResolveTint_DarkensDamagedAndCracked()
        {
            Color intact = StructuralIntegrityPresenter.ResolveTint(StructuralIntegrityStage.Intact);
            Color damaged = StructuralIntegrityPresenter.ResolveTint(StructuralIntegrityStage.Damaged);
            Color cracked = StructuralIntegrityPresenter.ResolveTint(StructuralIntegrityStage.Cracked);

            Assert.AreEqual(Color.white, intact);
            Assert.Less(damaged.r + damaged.g + damaged.b, intact.r + intact.g + intact.b);
            Assert.Less(cracked.r + cracked.g + cracked.b, damaged.r + damaged.g + damaged.b);
        }

        [Test]
        public void Presenter_Apply_SetsAppliedStage()
        {
            var go = new GameObject("WallPresenter");
            _instantiated.Add(go);
            go.AddComponent<MeshRenderer>();
            StructuralIntegrityPresenter presenter = go.AddComponent<StructuralIntegrityPresenter>();

            presenter.Apply(StructuralIntegrityStage.Cracked);
            Assert.AreEqual(StructuralIntegrityStage.Cracked, presenter.AppliedStage);

            presenter.Apply(StructuralIntegrityStage.Intact);
            Assert.AreEqual(StructuralIntegrityStage.Intact, presenter.AppliedStage);
        }

        [Test]
        public void Presenter_ApplyIntact_ClearsMaterialPropertyBlock()
        {
            var go = new GameObject("WallPresenterMpb");
            _instantiated.Add(go);
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            StructuralIntegrityPresenter presenter = go.AddComponent<StructuralIntegrityPresenter>();

            presenter.Apply(StructuralIntegrityStage.Damaged);
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            Assert.IsFalse(block.isEmpty, "Damaged stage should write a tint MPB");

            presenter.Apply(StructuralIntegrityStage.Intact);
            renderer.GetPropertyBlock(block);
            Assert.IsTrue(block.isEmpty, "Intact should clear MPB for SRP Batcher / GPU Instancing");
        }

        [Test]
        public void ExamineProvider_AppendsCrackedLine()
        {
            TileMapTestUtilities.MapContext context = TileMapTestUtilities.CreateContext(_instantiated);
            Vector3 wallPos = new Vector3(5, 0, 5);
            TileMapTestUtilities.PlacePlenum(context, wallPos);
            TileMapTestUtilities.PlaceAirtightWall(context, wallPos);

            Assert.IsTrue(context.Query.TryGetOccupant(
                context.Query.WorldToTile(wallPos), TileLayer.Turf, Direction.North, out ITileOccupant occupant));
            var placed = (PlacedTileObject)occupant;
            placed.ServerSetIntegrity(30f, StructuralIntegrityStage.Cracked);

            GameObject host = placed.gameObject;
            StructuralIntegrityExaminable examinable = host.AddComponent<StructuralIntegrityExaminable>();
            var sections = new List<ExamineSection>();
            examinable.AppendSections(examinable, sections);

            Assert.AreEqual(1, sections.Count);
            Assert.IsTrue(
                sections[0].Text.IndexOf("cracked", System.StringComparison.OrdinalIgnoreCase) >= 0,
                $"Expected cracked examine text, got: {sections[0].Text}");
        }
    }
}

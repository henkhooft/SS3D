using NUnit.Framework;
using SS3D.Systems.Tile;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AssetAudit
{
    public class TileMapTests
    {
        #region Tests
        [Test, TestCaseSource(nameof(AllTileObjectSo))]
        public void EveryTileObjectSoHasAName(TileObjectSo tileObjectSo)
        {
            Assert.IsTrue(tileObjectSo.NameString != "", $"TileObjectSo '{tileObjectSo.name}' does not have name set.\n");
        }

        [Test, TestCaseSource(nameof(AllItemObjectSo))]
        public void EveryItemObjectSoHasAName(ItemObjectSo itemObjectSo)
        {
            Assert.IsTrue(itemObjectSo.NameString != "", $"ItemObjectSo '{itemObjectSo.name}' does not have name set.\n");
        }

        /// <summary>
        /// Test if every TileObjectSo has a size between 1 and 5
        /// </summary>
        [Test, TestCaseSource(nameof(AllTileObjectSo))]
        public void EveryTileHasRealisticSize(TileObjectSo tile)
        {
            bool invalidSize = tile.height <= 0 || tile.height > 5 || tile.width <= 0 || tile.width > 5;
            Assert.IsFalse(invalidSize, $"TileObjectSo '{tile.name}' has an unrealistic size of { tile.width},{ tile.height}.\n");
        }

        /// <summary>
        /// A networked tile prefab must carry PlacedTileObject on its root. The runtime AddComponent
        /// fallback is server-only, so remote clients never learn the tile exists.
        /// Fix: SS3D.Systems.Tile.Editor.TilePlacedObjectPrefabSetup.SetupAll.
        /// </summary>
        [Test, TestCaseSource(nameof(AllTileObjectSo))]
        public void NetworkedTilePrefabsCarryPlacedTileObject(TileObjectSo tile)
        {
            GameObject prefab = SS3D.Data.Assets.Get<GameObject>(tile.PrefabAsset);
            if (prefab == null || prefab.GetComponent<FishNet.Object.NetworkObject>() == null)
                Assert.Ignore($"TileObjectSo '{tile.name}' has no networked prefab.");

            Assert.IsNotNull(prefab.GetComponent<PlacedTileObject>(),
                $"Tile prefab '{prefab.name}' ({tile.name}) lacks PlacedTileObject; run TilePlacedObjectPrefabSetup.SetupAll.\n");
        }
        #endregion

        #region Helper functions

        public static TileObjectSo[] AllTileObjectSo()
        {
            return AssetAuditUtilities.AllTileObjectSo();
        }

        public static ItemObjectSo[] AllItemObjectSo()
        {
            return AssetAuditUtilities.AllItemObjectSo();
        }
    }

    #endregion
}

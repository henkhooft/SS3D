using NUnit.Framework;
using SS3D.Systems.Area;
using SS3D.Systems.Tile;
using UnityEngine;

namespace EditorTests
{
    public class AreaFloorVisualCacheTests
    {
        [Test]
        public void TryGetAreaIdForWorldGrid_ResolvesPositiveChunkLocal()
        {
            var cache = new AreaFloorVisualCache();
            var ids = new ushort[TileConstants.ChunkSize * TileConstants.ChunkSize];
            ids[3 * TileConstants.ChunkSize + 5] = 7;
            cache.SetChunkAreaIds(new Vector2Int(0, 0), ids);

            Assert.IsTrue(cache.TryGetAreaIdForWorldGrid(new Vector2Int(5, 3), out ushort areaId));
            Assert.AreEqual(7, areaId);
        }

        [Test]
        public void TryGetAreaIdForWorldGrid_ResolvesNegativeChunk()
        {
            var cache = new AreaFloorVisualCache();
            var ids = new ushort[TileConstants.ChunkSize * TileConstants.ChunkSize];
            // world grid (-1, -1) → chunk (-1, -1), local (15, 15)
            ids[15 * TileConstants.ChunkSize + 15] = 42;
            cache.SetChunkAreaIds(new Vector2Int(-1, -1), ids);

            Assert.IsTrue(cache.TryGetAreaIdForWorldGrid(new Vector2Int(-1, -1), out ushort areaId));
            Assert.AreEqual(42, areaId);
        }

        [Test]
        public void TryGetAreaIdForWorldGrid_ReturnsFalseForUnassignedTile()
        {
            var cache = new AreaFloorVisualCache();
            var ids = new ushort[TileConstants.ChunkSize * TileConstants.ChunkSize];
            cache.SetChunkAreaIds(Vector2Int.zero, ids);

            Assert.IsFalse(cache.TryGetAreaIdForWorldGrid(Vector2Int.zero, out _));
        }
    }
}

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

        [Test]
        public void TryGetAmbienceTrackId_ReturnsFalseWhenNotSet()
        {
            var cache = new AreaFloorVisualCache();
            Assert.IsFalse(cache.TryGetAmbienceTrackId(1, out _));
        }

        [Test]
        public void SetAmbienceTrackId_ThenGet_RoundTrips()
        {
            var cache = new AreaFloorVisualCache();
            cache.SetAmbienceTrackId(3, "engineering_hum");

            Assert.IsTrue(cache.TryGetAmbienceTrackId(3, out string trackId));
            Assert.AreEqual("engineering_hum", trackId);
        }

        [Test]
        public void SetAmbienceTrackId_EmptyTrackId_Clears()
        {
            var cache = new AreaFloorVisualCache();
            cache.SetAmbienceTrackId(3, "engineering_hum");
            cache.SetAmbienceTrackId(3, string.Empty);

            Assert.IsFalse(cache.TryGetAmbienceTrackId(3, out _));
        }

        [Test]
        public void ReplaceAmbienceTrackIds_ReplacesPreviousEntries()
        {
            var cache = new AreaFloorVisualCache();
            cache.SetAmbienceTrackId(1, "stale_track");

            cache.ReplaceAmbienceTrackIds(new (ushort areaId, string trackId)[]
            {
                (2, "medbay_quiet"),
            });

            Assert.IsFalse(cache.TryGetAmbienceTrackId(1, out _));
            Assert.IsTrue(cache.TryGetAmbienceTrackId(2, out string trackId));
            Assert.AreEqual("medbay_quiet", trackId);
        }
    }
}

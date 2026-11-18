using NUnit.Framework;
using SS3D.Systems.Inventory.Items;

namespace EditorTests
{
    /// <summary>
    /// The item holder replicates as one packed long (tech-debt P4.4); a client unpacks it to find the container by
    /// NetworkObject id and component index, so the round trip must be exact.
    /// </summary>
    public class ItemHolderTests
    {
        [TestCase(0, (byte)0)]
        [TestCase(1, (byte)3)]
        [TestCase(32767, (byte)254)]
        [TestCase(int.MaxValue, (byte)255)]
        public void Container_RoundTrips(int objectId, byte componentIndex)
        {
            ItemHolder holder = ItemHolder.InContainer(objectId, componentIndex);
            ItemHolder back = ItemHolder.Unpack(holder.Pack());

            Assert.AreEqual(ItemHolderKind.Container, back.Kind);
            Assert.AreEqual(objectId, back.ObjectId);
            Assert.AreEqual(componentIndex, back.ComponentIndex);
            Assert.AreEqual(holder, back);
        }

        [Test]
        public void NoneAndTransit_RoundTrip_AndCarryNoContainer()
        {
            Assert.AreEqual(ItemHolder.None, ItemHolder.Unpack(ItemHolder.None.Pack()));
            Assert.AreEqual(ItemHolder.Transit, ItemHolder.Unpack(ItemHolder.Transit.Pack()));
            Assert.AreEqual(0L, ItemHolder.None.Pack(), "A fresh SyncVar (0) reads as no holder");
            Assert.AreEqual(0, new ItemHolder(ItemHolderKind.Transit, 12, 3).ObjectId, "Transit drops the container id");
        }

        [Test]
        public void AnUnknownKind_ReadsAsNone()
        {
            Assert.AreEqual(ItemHolder.None, ItemHolder.Unpack(9L << 40));
        }
    }
}

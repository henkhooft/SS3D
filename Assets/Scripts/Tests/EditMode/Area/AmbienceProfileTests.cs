using System.Linq;
using NUnit.Framework;
using SS3D.Systems.Area;
using SS3D.Systems.Audio;

namespace EditorTests
{
    public class AmbienceProfileTests
    {
        private static AmbienceProfileCatalog Catalog => AmbienceProfileCatalog.Instance;

        [Test]
        public void GenericProfileExistsAndHasABed()
        {
            Assert.IsTrue(Catalog.TryGet(AmbienceProfileCatalog.GenericProfileId, out AmbienceProfile profile));
            Assert.IsNotNull(profile.Bed);
        }

        [TestCase("no-such-profile")]
        [TestCase("")]
        [TestCase(null)]
        public void UnknownIdResolvesToTheGenericProfile(string id)
        {
            Assert.AreEqual(AmbienceProfileCatalog.GenericProfileId, Catalog.Resolve(id).Id);
        }

        [Test]
        public void LookupIgnoresCase()
        {
            Assert.IsTrue(Catalog.TryGet("STATION", out _));
        }

        [Test]
        public void EveryProfileHasAUniqueLowerCaseId()
        {
            string[] ids = Catalog.ProfileIds.ToArray();
            CollectionAssert.AllItemsAreUnique(ids);
            Assert.IsTrue(ids.All(id => id == id.ToLowerInvariant()), string.Join(", ", ids));
        }

        [Test]
        public void OverrideBeatsDepartment()
        {
            AreaRecord room = new() { ParentTag = "Medical", AmbienceProfileId = "morgue" };
            Assert.AreEqual("morgue", room.EffectiveAmbienceProfileId);
        }

        [Test]
        public void DepartmentNamesTheProfileWhenThereIsNoOverride()
        {
            AreaRecord room = new() { ParentTag = "Medical" };
            Assert.AreEqual("medical", room.EffectiveAmbienceProfileId);
        }

        [Test]
        public void RoomWithNoDepartmentAsksForNothing()
        {
            Assert.AreEqual(string.Empty, new AreaRecord().EffectiveAmbienceProfileId);
        }
    }
}

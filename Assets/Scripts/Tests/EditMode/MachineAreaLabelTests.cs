using NUnit.Framework;
using SS3D.Tests;
using SS3D.UI.MachineInterface;

namespace EditorTests
{
    /// <summary>
    /// Machine panels name the room the device is in instead of a hard-coded location.
    /// </summary>
    public class MachineAreaLabelTests : EditModeTest
    {
        [TestCase("Medbay", "Atmospheric Vent", "Medbay — Atmospheric Vent")]
        [TestCase("  Room 3 ", "Air Alarm", "Room 3 — Air Alarm")]
        [TestCase(null, "Atmospheric Scrubber", "Atmospheric Scrubber")]
        [TestCase("", "Pipe Pump", "Pipe Pump")]
        [TestCase("Bridge", "", "Bridge")]
        [TestCase(null, null, "")]
        public void Compose_PrefixesTheRoomName_OnlyWhenThereIsOne(string area, string kind, string expected)
        {
            Assert.AreEqual(expected, MachineAreaLabel.Compose(area, kind));
        }

        [Test]
        public void ApcMapper_UsesTheRoomAsSubtitle()
        {
            ApcInterfaceViewModel model = ApcInterfaceSnapshotMapper.ToViewModel(new ApcInterfaceSnapshot
            {
                Title = "APC · MEDBAY",
                AreaName = "Medbay",
            });

            Assert.AreEqual("APC · MEDBAY", model.Title);
            Assert.AreEqual("Medbay", model.Subtitle);
        }

        [Test]
        public void ApcMapper_SaysUnassigned_WithoutARoom()
        {
            ApcInterfaceViewModel model = ApcInterfaceSnapshotMapper.ToViewModel(new ApcInterfaceSnapshot { Title = "APC" });

            Assert.AreEqual(ApcInterfaceSnapshotMapper.UnassignedSubtitle, model.Subtitle);
        }

        [Test]
        public void AirAlarmMapper_NamesTheZoneFromTheRoom()
        {
            AirAlarmInterfaceViewModel withRoom = AirAlarmInterfaceSnapshotMapper.ToViewModel(new AirAlarmInterfaceSnapshot { AreaName = "Medbay" });
            AirAlarmInterfaceViewModel withoutRoom = AirAlarmInterfaceSnapshotMapper.ToViewModel(new AirAlarmInterfaceSnapshot());

            Assert.AreEqual("WIRED · ATMOS NET · ZONE MEDBAY", withRoom.ConnectionStatus);
            Assert.AreEqual("WIRED · ATMOS NET · NO ZONE", withoutRoom.ConnectionStatus);
        }
    }
}

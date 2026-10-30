using NUnit.Framework;
using SS3D.Systems.Rounds;

namespace EditorTests
{
    public class ServerSettingsTests
    {
        [TestCase(0, 90, false)]
        [TestCase(257, 90, false)]
        [TestCase(32, 0, false)]
        [TestCase(32, 1441, false)]
        [TestCase(1, 1, true)]
        [TestCase(256, 1440, true)]
        public void ValidatesServerResourceLimits(int cap, int minutes, bool expected)
        {
            ServerSettings settings = ServerSettings.Default;
            settings.PlayerCap = cap;
            settings.RoundLengthMinutes = minutes;
            Assert.AreEqual(expected, settings.IsValid);
        }
    }
}

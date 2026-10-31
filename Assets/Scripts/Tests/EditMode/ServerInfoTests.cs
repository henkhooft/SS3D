using NUnit.Framework;
using SS3D.Systems.Rounds;

namespace EditorTests
{
    public class ServerInfoTests
    {
        [Test]
        public void SanitizeName_EmptyFallsBackToDefault()
        {
            Assert.AreEqual(ServerInfoText.DefaultName, ServerInfoText.SanitizeName(null));
            Assert.AreEqual(ServerInfoText.DefaultName, ServerInfoText.SanitizeName(" \t\n "));
        }

        [Test]
        public void SanitizeName_IsSingleLineTrimmedAndClamped()
        {
            Assert.AreEqual("USS Horizon test", ServerInfoText.SanitizeName("  USS\nHorizon \u0007  test "));
            Assert.AreEqual(ServerInfoText.MaxNameLength, ServerInfoText.SanitizeName(new string('x', 200)).Length);
        }

        [Test]
        public void SanitizeMotd_KeepsParagraphsButCollapsesBlankRuns()
        {
            string motd = ServerInfoText.SanitizeMotd("\r\nWelcome   aboard.  \r\n\r\n\r\n\r\nRules:\tbe nice\u0000");
            Assert.AreEqual("Welcome aboard.\n\nRules: be nice", motd);
        }

        [Test]
        public void SanitizeMotd_ClampsLengthAndDefaultsWhenEmpty()
        {
            Assert.AreEqual(ServerInfoText.MaxMotdLength, ServerInfoText.SanitizeMotd(new string('m', 5000)).Length);
            Assert.AreEqual(ServerInfoText.DefaultMotd, ServerInfoText.SanitizeMotd(""));
        }

        [Test]
        public void BuildInfoParse_ReadsGeneratedJson()
        {
            BuildInfo.Data data = BuildInfo.Parse(
                "{\"commit\":\"007c4e9\",\"entries\":[{\"hash\":\"007c4e9\",\"date\":\"2026-10-31\",\"subject\":\"Lobby chat\"}]}");
            Assert.AreEqual("007c4e9", data.commit);
            Assert.AreEqual(1, data.entries.Length);
            Assert.AreEqual("Lobby chat", data.entries[0].subject);
        }

        [Test]
        public void BuildInfoParse_MissingOrBrokenFileIsEmpty()
        {
            Assert.IsEmpty(BuildInfo.Parse(null).entries);
            Assert.IsEmpty(BuildInfo.Parse("not json").entries);
            Assert.IsNull(BuildInfo.Parse("").commit);
        }
    }
}

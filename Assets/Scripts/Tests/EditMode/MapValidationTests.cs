using System;
using System.Linq;
using NUnit.Framework;
using SS3D.Systems.Tile;
using SS3D.Systems.Tile.SpawnPoints;
using SS3D.Systems.Tile.Validation;
using SS3D.Systems.Tile.MapEditor;
using SS3D.Systems.Tile.MapEditor.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace EditorTests
{
    public class MapValidationTests
    {
        private static SpawnPointRecord Job(string name) =>
            SpawnPointRecord.ForJob(name, Vector3.zero, Direction.North);

        private static SpawnPointRecord Operative => SpawnPointRecord.ForAntagonist(
            AntagonistSpawnCategory.NuclearOperatives, Vector3.one, Direction.North);

        [Test]
        public void ExtendedRequiresEveryOfferedJobEvenWithNoReadyPlayers()
        {
            MapValidationReport report = MapValidator.Validate("Extended", new[] { "Captain", "Assistant", "Captain" },
                new[] { Job("Assistant") }, false, false);
            Assert.IsFalse(report.CanStart);
            Assert.AreEqual(1, report.Errors.Count);
            StringAssert.Contains("Captain", report.Errors[0]);
        }

        [Test]
        public void ExtendedDoesNotRequireNuclearContent()
        {
            Assert.IsTrue(MapValidator.Validate("Extended", new[] { "Assistant" },
                new[] { Job("Assistant") }, false, false).CanStart);
        }

        [TestCase("Secret")]
        [TestCase("Nuclear Emergency")]
        public void NuclearModesListAllMissingRequirementsTogether(string mode)
        {
            MapValidationReport report = MapValidator.Validate(mode, new[] { "Assistant", "Captain" },
                Array.Empty<SpawnPointRecord>(), false, false);
            Assert.AreEqual(5, report.Errors.Count);
            Assert.IsFalse(report.CanStart);
            string message = report.Describe("Test station", mode, saved: false);
            foreach (string expected in new[] { "Assistant", "Captain", "Nuclear Operatives", "device", "disk", "save" })
                StringAssert.Contains(expected, message);
        }

        [TestCase(true, true, true, 0)]
        [TestCase(false, true, true, 1)]
        [TestCase(true, false, true, 1)]
        [TestCase(true, true, false, 1)]
        public void NuclearRequirementsAreIndependent(bool nuke, bool disk, bool operative, int errors)
        {
            SpawnPointRecord[] spawns = operative ? new[] { Job("Assistant"), Operative } : new[] { Job("Assistant") };
            MapValidationReport report = MapValidator.Validate("Nuclear Emergency", new[] { "Assistant" }, spawns, nuke, disk);
            Assert.AreEqual(errors, report.Errors.Count);
            Assert.AreEqual(errors == 0, report.CanStart);
        }

        [Test]
        public void WrongJobAndAntagonistTagsDoNotSatisfyRequirements()
        {
            MapValidationReport report = MapValidator.Validate("Nuclear Emergency", new[] { "Assistant" },
                new[] { Job("assistant"), SpawnPointRecord.ForAntagonist(AntagonistSpawnCategory.Traitor,
                    Vector3.one, Direction.North) }, true, true);
            Assert.AreEqual(2, report.Errors.Count);
            Assert.AreEqual(1, report.Warnings.Count);
        }

        [Test]
        public void UnknownJobWarningDoesNotBlockStart()
        {
            MapValidationReport report = MapValidator.Validate("Extended", new[] { "Assistant" },
                new[] { Job("Assistant"), Job("RetiredJob") }, false, false);
            Assert.IsTrue(report.CanStart);
            Assert.IsTrue(report.HasIssues);
            StringAssert.Contains("RetiredJob", report.Warnings.Single());
            StringAssert.Contains("Saved", report.Describe("draft", "Extended", saved: true));
        }

        [Test]
        public void UnconfiguredJobsAndUnknownModesFailClosed()
        {
            Assert.IsFalse(MapValidator.Validate("Extended", Array.Empty<string>(),
                new[] { Job("Assistant") }, true, true).CanStart);
            Assert.IsFalse(MapValidator.Validate("FutureMode", new[] { "Assistant" },
                new[] { Job("Assistant"), Operative }, true, true).CanStart);
        }

        [Test]
        public void RevalidationAfterRepairDoesNotRetainOldErrors()
        {
            Assert.IsFalse(MapValidator.Validate("Extended", new[] { "Assistant" },
                Array.Empty<SpawnPointRecord>(), false, false).CanStart);
            MapValidationReport repaired = MapValidator.Validate("Extended", new[] { "Assistant" },
                new[] { Job("Assistant") }, false, false);
            Assert.IsTrue(repaired.CanStart);
            Assert.IsFalse(repaired.HasIssues);
        }

        [Test]
        public void MissingMapIsAnExplainedFailure()
        {
            MapValidationReport report = MapValidationService.Validate(null, "Extended");
            Assert.IsFalse(report.CanStart);
            StringAssert.Contains("No station map", report.Errors.Single());
        }

        [Test]
        public void SaveReportIsScrollableAndDisplaysMapNamesLiterally()
        {
            MapEditorViewModel model = new()
            {
                OpenPopover = "validation",
                ValidationReport = "Saved <b>draft</b>.\n" + string.Join("\n", Enumerable.Repeat("Missing spawn", 40)),
            };
            MapEditorView view = new(null, null, model, new MapEditorCatalog(), null);
            try
            {
                VisualElement root = new();
                view.Build(root);
                ScrollView scroll = root.Q<ScrollView>("map-validation-report");
                Assert.IsNotNull(scroll);
                Assert.AreEqual(320f, scroll.style.maxHeight.value.value);
                Label report = scroll.Q<Label>();
                Assert.AreEqual(model.ValidationReport, report.text);
                Assert.IsFalse(report.enableRichText);
            }
            finally
            {
                view.Destroy();
            }
        }
    }
}

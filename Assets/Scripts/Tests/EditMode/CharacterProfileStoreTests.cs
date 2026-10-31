using System;
using System.IO;
using NUnit.Framework;
using SS3D.UI.Lobby;
using UnityEngine;
using UnityEngine.TestTools;

namespace EditorTests
{
    public class CharacterProfileStoreTests
    {
        private string _directory;
        private string ProfilePath => Path.Combine(_directory, "characters.json");

        [SetUp]
        public void SetUp() => _directory = Path.Combine(Path.GetTempPath(), "ss3d-profile-" + Guid.NewGuid());

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        [Test]
        public void NewStoreRestoresAllSlotsAndSelectedCharacter()
        {
            CharacterCreatorDraft first = new() { Id = "first", Name = "First", HairStyleIndex = 3, HairColorIndex = 4, OutfitIndex = 2, SkinColorIndex = 5 };
            first.SetMorph("fat", 0.73f);
            CharacterCreatorDraft selected = new() { Id = "second", Name = "Second", BeardStyleIndex = 2, Species = "Human", EyeColorIndex = 3, Eyebrows = "thick" };
            selected.SetMorph("height", 0.81f);
            Assert.That(new CharacterProfileStore(ProfilePath).Save(new CharacterCreatorDraft[] { first, selected }, selected.Id), Is.True);

            CharacterProfileStore.Profile loaded = new CharacterProfileStore(ProfilePath).Load();
            Assert.That(loaded.SelectedId, Is.EqualTo("second"));
            Assert.That(loaded.Characters.Length, Is.EqualTo(2));
            Assert.That(loaded.Characters[0].Name, Is.EqualTo("First"));
            Assert.That(loaded.Characters[0].HairStyleIndex, Is.EqualTo(3));
            Assert.That(loaded.Characters[0].HairColorIndex, Is.EqualTo(4));
            Assert.That(loaded.Characters[0].SkinColorIndex, Is.EqualTo(5));
            Assert.That(loaded.Characters[0].OutfitIndex, Is.EqualTo(2));
            Assert.That(loaded.Characters[0].GetMorph("fat"), Is.EqualTo(0.73f));
            Assert.That(loaded.Characters[1].GetMorph("height"), Is.EqualTo(0.81f));
            Assert.That(loaded.Characters[1].BeardStyleIndex, Is.EqualTo(2));
            Assert.That(loaded.Characters[1].EyeColorIndex, Is.EqualTo(3));
            Assert.That(loaded.Characters[1].Eyebrows, Is.EqualTo("thick"));
        }

        [Test]
        public void UnsavedEditsDoNotChangeDiskAndSecondSaveReplacesFile()
        {
            CharacterCreatorDraft draft = new() { Id = "slot", Name = "Saved" };
            CharacterProfileStore store = new(ProfilePath);
            Assert.That(store.Save(new CharacterCreatorDraft[] { draft }, draft.Id), Is.True);
            draft.Name = "Edited";
            Assert.That(new CharacterProfileStore(ProfilePath).Load().Characters[0].Name, Is.EqualTo("Saved"));
            Assert.That(store.Save(new CharacterCreatorDraft[] { draft }, draft.Id), Is.True);
            Assert.That(new CharacterProfileStore(ProfilePath).Load().Characters[0].Name, Is.EqualTo("Edited"));
            Assert.That(File.Exists(ProfilePath + ".tmp"), Is.False);
        }

        [TestCase("invalid json")]
        [TestCase("{\"Version\":2,\"Characters\":[]}")]
        [TestCase("{\"Version\":1,\"SelectedId\":\"missing\",\"Characters\":[]}")]
        public void UnreadableProfileIsPreservedWhenSavingDefaults(string original)
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(ProfilePath, original);
            CharacterProfileStore store = new(ProfilePath);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Could not load character presets"));
            Assert.That(store.Load(), Is.Null);
            Assert.That(store.Save(new CharacterCreatorDraft[] { new CharacterCreatorDraft() }, "default"), Is.False);
            Assert.That(File.ReadAllText(ProfilePath), Is.EqualTo(original));
        }

        [Test]
        public void MissingProfileUsesDefaultsWithoutCreatingAFile()
        {
            Assert.That(new CharacterProfileStore(ProfilePath).Load(), Is.Null);
            Assert.That(File.Exists(ProfilePath), Is.False);
        }
    }
}

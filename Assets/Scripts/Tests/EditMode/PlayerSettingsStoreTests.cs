using System;
using System.IO;
using NUnit.Framework;
using SS3D.Systems.Settings;
using UnityEngine;
using UnityEngine.TestTools;

namespace EditorTests
{
    public class PlayerSettingsStoreTests
    {
        private string _directory;
        private string SettingsPath => Path.Combine(_directory, "settings.json");

        [SetUp]
        public void SetUp() => _directory = Path.Combine(Path.GetTempPath(), "ss3d-settings-" + Guid.NewGuid());

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        [Test]
        public void SavedAudioSettingsSurviveARestart()
        {
            PlayerSettings settings = new();
            settings.Audio.Master = 0.35f;
            settings.Audio.Music = 0.1f;
            settings.Audio.Sfx = 0.95f;
            settings.Audio.Ambience = 0.4f;
            settings.Audio.Personal = 0.6f;
            settings.Audio.MuteAll = true;
            settings.Audio.MuteWhenUnfocused = false;
            Assert.That(new PlayerSettingsStore(SettingsPath).Save(settings), Is.True);

            PlayerAudioSettings loaded = new PlayerSettingsStore(SettingsPath).Load().Audio;
            Assert.That(loaded.Master, Is.EqualTo(0.35f));
            Assert.That(loaded.Music, Is.EqualTo(0.1f));
            Assert.That(loaded.Sfx, Is.EqualTo(0.95f));
            Assert.That(loaded.Ambience, Is.EqualTo(0.4f));
            Assert.That(loaded.Personal, Is.EqualTo(0.6f));
            Assert.That(loaded.MuteAll, Is.True);
            Assert.That(loaded.MuteWhenUnfocused, Is.False);
        }

        [Test]
        public void SavedFileRecordsItsFormatVersion()
        {
            new PlayerSettingsStore(SettingsPath).Save(new PlayerSettings());

            Assert.That(File.ReadAllText(SettingsPath), Does.Contain("\"Version\": 1"));
        }

        [TestCase("invalid json")]
        [TestCase("")]
        [TestCase("[1, 2, 3]")]
        public void CorruptFileIsKeptAsBackupAndDefaultsLoad(string corrupt)
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(SettingsPath, corrupt);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("settings"));

            PlayerSettings loaded = new PlayerSettingsStore(SettingsPath).Load();

            Assert.That(loaded.Audio.Master, Is.EqualTo(0.8f));
            Assert.That(loaded.Audio.MuteWhenUnfocused, Is.True);
            Assert.That(File.ReadAllText(SettingsPath + ".bak"), Is.EqualTo(corrupt));
        }

        [Test]
        public void KeysMissingFromTheFileKeepTheirDefaults()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(SettingsPath, "{\"Version\":1,\"Audio\":{\"Music\":0.25}}");

            PlayerAudioSettings loaded = new PlayerSettingsStore(SettingsPath).Load().Audio;

            Assert.That(loaded.Music, Is.EqualTo(0.25f));
            Assert.That(loaded.Master, Is.EqualTo(0.8f));
            Assert.That(loaded.MuteWhenUnfocused, Is.True);
        }

        [Test]
        public void UnknownKeysAreIgnored()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(SettingsPath,
                "{\"Version\":1,\"Brightness\":0.6,\"Audio\":{\"Voice\":0.1,\"Sfx\":0.3}}");

            Assert.That(new PlayerSettingsStore(SettingsPath).Load().Audio.Sfx, Is.EqualTo(0.3f));
        }

        [Test]
        public void MissingFileLoadsFirstLaunchDefaultsWithoutCreatingIt()
        {
            PlayerAudioSettings loaded = new PlayerSettingsStore(SettingsPath).Load().Audio;

            Assert.That(loaded.Master, Is.EqualTo(0.8f));
            Assert.That(loaded.Personal, Is.EqualTo(0.8f));
            Assert.That(loaded.MuteAll, Is.False);
            Assert.That(loaded.MuteWhenUnfocused, Is.True);
            Assert.That(File.Exists(SettingsPath), Is.False);
        }

        [Test]
        public void OutOfRangeVolumesAreClampedOnLoad()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(SettingsPath, "{\"Version\":1,\"Audio\":{\"Master\":3.5,\"Music\":-1}}");

            PlayerAudioSettings loaded = new PlayerSettingsStore(SettingsPath).Load().Audio;

            Assert.That(loaded.Master, Is.EqualTo(1f));
            Assert.That(loaded.Music, Is.EqualTo(0f));
        }
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SS3D.Systems.Rounds;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SS3D.Editor
{
    /// <summary>
    /// Writes <see cref="BuildInfo"/> (game version, commit id, recent commit subjects) from git into a
    /// gitignored Resources file: on editor load when it changed, and before every player build.
    /// Player builds also get the version as <see cref="PlayerSettings.bundleVersion"/> (restored
    /// right after, so ProjectSettings never churns). CI passes <c>-ss3dVersion X.Y.Z</c> to stamp
    /// the release version before its tag exists. Without git the existing file is kept (or none
    /// exists) and the lobby falls back. Rules: Documents/architecture/2026-10_game-versioning.md.
    /// </summary>
    [InitializeOnLoad]
    public sealed class BuildInfoGenerator : IPreprocessBuildWithReport
    {
        public const string AssetPath = "Assets/Content/Systems/UI/Lobby/Resources/" + BuildInfo.ResourceName + ".json";
        public const string VersionArgument = "-ss3dVersion";
        private const int EntryCount = 6;
        private const char Separator = '\u001f';

        static BuildInfoGenerator() => EditorApplication.delayCall += () => Generate();

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            // Without git (e.g. a CI container) the version argument still stamps the player.
            string version = Generate() ?? ArgumentVersion() ?? BuildInfo.Version;
            string original = PlayerSettings.bundleVersion;
            if (version == original) return;

            // Build runs synchronously on the main thread; delayCall fires once it returns (pass or fail).
            PlayerSettings.bundleVersion = version;
            EditorApplication.delayCall += () => PlayerSettings.bundleVersion = original;
            Debug.Log($"[BuildInfo] Stamped player version {version}");
        }

        [MenuItem("SS3D/Build/Regenerate Build Info")]
        private static void GenerateFromMenu() => Generate();

        /// <summary>Regenerates the file; returns the version, or null when git is unavailable.</summary>
        public static string Generate()
        {
            if (!TryGit($"log -{EntryCount} --format=%h{Separator}%cs{Separator}%s", out string log)) return null;

            BuildInfo.Entry[] entries = log.Split('\n')
                .Select(line => line.Split(Separator))
                .Where(parts => parts.Length == 3)
                .Select(parts => new BuildInfo.Entry { hash = parts[0], date = parts[1], subject = parts[2] })
                .ToArray();
            if (entries.Length == 0) return null;

            string version = ComputeVersion();
            string json = JsonUtility.ToJson(new BuildInfo.Data { version = version, commit = entries[0].hash, entries = entries }, true);
            if (File.Exists(AssetPath) && File.ReadAllText(AssetPath) == json) return version;
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath)!);
            File.WriteAllText(AssetPath, json);
            AssetDatabase.ImportAsset(AssetPath);
            return version;
        }

        private static string ArgumentVersion()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, VersionArgument);
            return index >= 0 && index + 1 < args.Length && !string.IsNullOrWhiteSpace(args[index + 1])
                ? args[index + 1].Trim()
                : null;
        }

        private static string ComputeVersion()
        {
            if (ArgumentVersion() is { } fromArgument) return fromArgument;

            bool dirty = TryGit("status --porcelain --untracked-files=no", out _);
            if (TryGit($"describe --tags --long --abbrev=7 --match \"{GameVersion.TagGlob}\" HEAD", out string describe)
                && GameVersion.FromDescribe(describe, dirty) is { } tagged)
            {
                return tagged;
            }

            return TryGit("rev-list --count HEAD", out string count) && int.TryParse(count, out int commits)
                && TryGit("rev-parse --short=7 HEAD", out string hash)
                ? GameVersion.Untagged(commits, hash, dirty)
                : null;
        }

        private static bool TryGit(string arguments, out string output)
        {
            output = null;
            try
            {
                using Process git = Process.Start(new ProcessStartInfo("git", arguments)
                {
                    WorkingDirectory = Path.GetDirectoryName(UnityEngine.Application.dataPath)!,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
                if (git == null) return false;
                output = git.StandardOutput.ReadToEnd().Trim();
                if (!git.WaitForExit(5000) || git.ExitCode != 0) return false;
                return output.Length > 0;
            }
            catch (Exception exception)
            {
                Debug.Log($"[BuildInfo] git unavailable; keeping existing build info ({exception.Message}).");
                return false;
            }
        }
    }
}

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
    /// Writes <see cref="BuildInfo"/> (commit id + recent commit subjects) from git into a
    /// gitignored Resources file: on editor load when HEAD changed, and before every player
    /// build. Without git the existing file is kept (or none exists) and the lobby falls back.
    /// </summary>
    [InitializeOnLoad]
    public sealed class BuildInfoGenerator : IPreprocessBuildWithReport
    {
        public const string AssetPath = "Assets/Content/Systems/UI/Lobby/Resources/" + BuildInfo.ResourceName + ".json";
        private const int EntryCount = 6;
        private const char Separator = '\u001f';

        static BuildInfoGenerator() => EditorApplication.delayCall += () => Generate();

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) => Generate();

        [MenuItem("SS3D/Build/Regenerate Build Info")]
        public static void Generate()
        {
            if (!TryGit($"log -{EntryCount} --format=%h{Separator}%cs{Separator}%s", out string log)) return;

            BuildInfo.Entry[] entries = log.Split('\n')
                .Select(line => line.Split(Separator))
                .Where(parts => parts.Length == 3)
                .Select(parts => new BuildInfo.Entry { hash = parts[0], date = parts[1], subject = parts[2] })
                .ToArray();
            if (entries.Length == 0) return;

            string json = JsonUtility.ToJson(new BuildInfo.Data { commit = entries[0].hash, entries = entries }, true);
            if (File.Exists(AssetPath) && File.ReadAllText(AssetPath) == json) return;
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath)!);
            File.WriteAllText(AssetPath, json);
            AssetDatabase.ImportAsset(AssetPath);
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

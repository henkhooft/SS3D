using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SS3D.Editor.Automation
{
    /// <summary>
    /// Non-interactive client build for agents: refuses on a dirty/polluted scene instead of letting
    /// BuildPlayer open a modal dialog, returns before the build blocks the main thread, and reports
    /// through artifacts/ui/client-build-result.json. Builds are stamped with
    /// Testing/ui/source_hash.py so stale clients are detected.
    /// <para>From MCP: <c>execute_code</c> with <c>SS3D.Editor.Automation.AutomationBuild.BuildClient();</c></para>
    /// </summary>
    public static class AutomationBuild
    {
        public const string ClientBinary = "Builds/Game/SS3D.x86_64";
        public const string HashFile = "Builds/Game/ss3d-build-hash.txt";
        public const string ResultFile = "artifacts/ui/client-build-result.json";

        [Serializable]
        private sealed class Result
        {
            public string status;
            public double seconds;
            public string error;
            public string sourceHash;
            public string startedUtc;
        }

        /// <summary>Validates, then schedules the build on the next editor tick. Poll <see cref="ResultFile"/>.</summary>
        public static void BuildClient()
        {
            Result result = new() { status = "building", startedUtc = DateTime.UtcNow.ToString("o") };
            try
            {
                if (EditorApplication.isCompiling || EditorUtility.scriptCompilationFailed)
                    throw new InvalidOperationException("Fix compilation errors before building the client.");
                AutomationPreflight.Require("client build");
            }
            catch (Exception exception)
            {
                result.status = "failed";
                result.error = exception.Message;
                Write(result);
                throw;
            }

            Write(result);
            // Not delayCall: an idle, unfocused editor can leave the delayCall queue unprocessed
            // indefinitely while update keeps ticking.
            void Once()
            {
                EditorApplication.update -= Once;
                Build(result);
            }
            EditorApplication.update += Once;
        }

        private static void Build(Result result)
        {
            Stopwatch watch = Stopwatch.StartNew();
            try
            {
                // Re-check: something may have dirtied a scene between the request and this tick.
                AutomationPreflight.Require("client build");
                ClientBuildScript.BuildClient(ClientBinary);
                // Hash after the build so files the build itself rewrites are part of the stamp.
                result.sourceHash = SourceHash();
                File.WriteAllText(HashFile, result.sourceHash + "\n");
                result.status = "succeeded";
            }
            catch (Exception exception)
            {
                result.status = "failed";
                result.error = exception.ToString();
            }
            result.seconds = watch.Elapsed.TotalSeconds;
            Write(result);
            Debug.Log($"[SS3D Automation] Client build {result.status} in {result.seconds:0.0}s");
        }

        /// <summary>Null when the client build matches the working tree, otherwise why it is stale.</summary>
        public static string ClientStaleReason()
        {
            if (!File.Exists(ClientBinary)) return $"no client build at {ClientBinary}";
            if (!File.Exists(HashFile)) return $"client build has no source stamp ({HashFile})";
            string built = File.ReadAllText(HashFile).Trim();
            string current = SourceHash();
            return built == current ? null : $"client build is stale (built from {built}, working tree is {current})";
        }

        public static string SourceHash()
        {
            ProcessStartInfo info = new("python3", "Testing/ui/source_hash.py")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = Path.GetFullPath("."),
            };
            using Process process = Process.Start(info);
            string output = process.StandardOutput.ReadToEnd().Trim();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0 || output.Length == 0)
                throw new InvalidOperationException("Testing/ui/source_hash.py failed: " + error);
            return output;
        }

        private static void Write(Result result)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResultFile));
            File.WriteAllText(ResultFile, JsonUtility.ToJson(result, true));
        }
    }
}

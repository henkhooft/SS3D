using System;
using UnityEngine;

namespace SS3D.Systems.Rounds
{
    /// <summary>
    /// Game version, commit id and recent commit subjects, generated from git into a Resources text
    /// asset by the editor (on load and before player builds). The file is gitignored; without it the
    /// label falls back to <see cref="UnityEngine.Application.version"/> and the change log is empty.
    /// Versioning rules: Documents/architecture/2026-10_game-versioning.md.
    /// </summary>
    public static class BuildInfo
    {
        public const string ResourceName = "BuildInfo";

        [Serializable]
        public sealed class Entry
        {
            public string hash;
            public string date;
            public string subject;
        }

        [Serializable]
        public sealed class Data
        {
            /// <summary>See <see cref="GameVersion"/>, e.g. "0.1.0" or "0.1.1-dev.12+5da58b9".</summary>
            public string version;
            public string commit;
            public Entry[] entries = Array.Empty<Entry>();
        }

        private static Data _data;

        public static Data Current => _data ??= Load();

        /// <summary>This process's game version; player builds also stamp it into <see cref="UnityEngine.Application.version"/>.</summary>
        public static string Version => string.IsNullOrEmpty(Current.version) ? UnityEngine.Application.version : Current.version;

        /// <summary>
        /// "0.1.1-dev.12+007c4e9" for dev builds (the version already names the commit), "0.1.0 · 007c4e9"
        /// for releases, or the bare version without generated build info.
        /// </summary>
        public static string Label => string.IsNullOrEmpty(Current.commit) || Version.Contains('+')
            ? Version
            : $"{Version} · {Current.commit}";

        public static Data Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new Data();
            try
            {
                Data data = JsonUtility.FromJson<Data>(json) ?? new Data();
                data.entries ??= Array.Empty<Entry>();
                return data;
            }
            catch (ArgumentException)
            {
                return new Data();
            }
        }

        private static Data Load()
        {
            TextAsset asset = Resources.Load<TextAsset>(ResourceName);
            return Parse(asset != null ? asset.text : null);
        }
    }
}

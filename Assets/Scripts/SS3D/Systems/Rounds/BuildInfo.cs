using System;
using UnityEngine;

namespace SS3D.Systems.Rounds
{
    /// <summary>
    /// Commit id and recent commit subjects, generated from git into a Resources text asset by
    /// the editor (on load and before player builds). The file is gitignored; without it the
    /// build label falls back to <see cref="UnityEngine.Application.version"/> and the change log is empty.
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
            public string commit;
            public Entry[] entries = Array.Empty<Entry>();
        }

        private static Data _data;

        public static Data Current => _data ??= Load();

        /// <summary>"0.1.0 · 007c4e9" style label for this process.</summary>
        public static string Label => string.IsNullOrEmpty(Current.commit)
            ? UnityEngine.Application.version
            : $"{UnityEngine.Application.version} · {Current.commit}";

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

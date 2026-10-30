using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SS3D.UI.Lobby
{
    /// <summary>Local character presets, independent of server-owned player meta and crew records.</summary>
    public sealed class CharacterProfileStore
    {
        [Serializable]
        private sealed class FileData
        {
            public int Version = 1;
            public string SelectedId;
            public Entry[] Characters;
        }

        [Serializable]
        private sealed class Entry
        {
            public CharacterCreatorDraft Draft;
            // JsonUtility cannot serialize the draft's dictionary. Store morphs by key, not UI order.
            public string[] MorphKeys;
            public float[] MorphValues;
        }

        public sealed class Profile
        {
            public string SelectedId;
            public CharacterCreatorDraft[] Characters;
        }

        public static string DefaultPath
        {
            get
            {
#if UNITY_EDITOR
                // Pilots save characters: use a fresh per-run profile instead of the owner's presets.
                foreach (string pilot in new string[] { "SS3D.LobbyPilot", "SS3D.LobbyLifecyclePilot" })
                    if (UnityEditor.SessionState.GetBool(pilot + ".Active", false))
                        return Path.Combine(UnityEditor.SessionState.GetString(pilot + ".Output", ""), "characters.json");
#endif
                return Path.Combine(Application.persistentDataPath, "Lobby", "characters.json");
            }
        }

        private readonly string _path;
        private bool _readOnly;

        public CharacterProfileStore(string path) => _path = path;

        public Profile Load()
        {
            if (!File.Exists(_path)) return null;
            try
            {
                FileData data = JsonUtility.FromJson<FileData>(File.ReadAllText(_path));
                if (data == null || data.Version != 1 || data.Characters == null)
                    throw new InvalidDataException("Unsupported or incomplete character profile.");
                List<CharacterCreatorDraft> characters = new();
                HashSet<string> ids = new();
                foreach (Entry entry in data.Characters)
                {
                    CharacterCreatorDraft draft = entry?.Draft;
                    if (draft == null || string.IsNullOrWhiteSpace(draft.Id) || !ids.Add(draft.Id))
                        throw new InvalidDataException("Invalid or duplicate character slot.");
                    if (entry.MorphKeys == null || entry.MorphValues == null || entry.MorphKeys.Length != entry.MorphValues.Length)
                        throw new InvalidDataException("Invalid character morph data.");
                    for (int i = 0; i < entry.MorphKeys.Length; i++)
                    {
                        string key = entry.MorphKeys[i];
                        if (key != null && draft.BodyMorphs.ContainsKey(key))
                        {
                            float value = entry.MorphValues[i];
                            draft.SetMorph(key, float.IsNaN(value) ? 0.5f : Mathf.Clamp01(value));
                        }
                    }
                    characters.Add(draft);
                }
                if (!characters.Any(c => c.Id == data.SelectedId))
                    throw new InvalidDataException("Selected character slot is missing.");
                return new Profile { SelectedId = data.SelectedId, Characters = characters.ToArray() };
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
            {
                // Preserve unreadable/newer files rather than replacing them with the default slots.
                _readOnly = true;
                Debug.LogWarning($"[Lobby] Could not load character presets; original file preserved: {exception.Message}");
                return null;
            }
        }

        public bool Save(IEnumerable<CharacterCreatorDraft> characters, string selectedId)
        {
            if (_readOnly) return false;
            string temporary = _path + ".tmp";
            try
            {
                FileData data = new()
                {
                    SelectedId = selectedId,
                    Characters = characters.Select(draft => new Entry
                    {
                        Draft = draft,
                        MorphKeys = draft.BodyMorphs.Keys.ToArray(),
                        MorphValues = draft.BodyMorphs.Values.ToArray(),
                    }).ToArray(),
                };
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                File.WriteAllText(temporary, JsonUtility.ToJson(data, true));
                if (File.Exists(_path)) File.Replace(temporary, _path, null);
                else File.Move(temporary, _path);
                return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Debug.LogWarning($"[Lobby] Could not save character presets: {exception.Message}");
                return false;
            }
        }
    }
}

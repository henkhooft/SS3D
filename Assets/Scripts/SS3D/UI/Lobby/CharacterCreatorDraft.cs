using System.Collections.Generic;

namespace SS3D.UI.Lobby
{
    /// <summary>In-memory Character Creator draft (local only; no network yet).</summary>
    public sealed class CharacterCreatorDraft
    {
        public string Name = LobbyMockData.CharacterName;

        /// <summary>Body slider keys → 0–1 values (see <see cref="CharacterCreatorMockData.BodySliders"/>).</summary>
        public Dictionary<string, float> BodyMorphs { get; } = CreateDefaultMorphs();

        public static Dictionary<string, float> CreateDefaultMorphs()
        {
            Dictionary<string, float> morphs = new();
            foreach (CharacterCreatorMockData.SliderDef slider in CharacterCreatorMockData.BodySliders)
            {
                morphs[slider.Key] = slider.DefaultValue;
            }

            return morphs;
        }

        public float GetMorph(string key, float fallback = 0.5f) =>
            BodyMorphs.TryGetValue(key, out float value) ? value : fallback;

        public void SetMorph(string key, float value) => BodyMorphs[key] = value;

        public void CopyMorphsFrom(IReadOnlyDictionary<string, float> source)
        {
            if (source == null)
            {
                return;
            }

            foreach (KeyValuePair<string, float> pair in source)
            {
                BodyMorphs[pair.Key] = pair.Value;
            }
        }
    }
}

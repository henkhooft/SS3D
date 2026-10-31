using System;
using UnityEngine;

namespace SS3D.Systems.Settings
{
    /// <summary>
    /// Client-local machine options (2026-10_player-settings). Plain data: <see cref="PlayerSettingsSubSystem"/>
    /// applies it and <see cref="PlayerSettingsStore"/> persists it. Field initializers are the first-launch
    /// defaults (Decision 14), and also fill keys a saved file lacks.
    /// </summary>
    [Serializable]
    public sealed class PlayerSettings
    {
        public const int CurrentVersion = 1;

        /// <summary>File format version (Decision 2). Bump with a migration when a field changes meaning.</summary>
        public int Version = CurrentVersion;
        public PlayerAudioSettings Audio = new();

        /// <summary>Pulls hand-edited or stale values back into range after a load.</summary>
        public void Sanitize()
        {
            Audio ??= new PlayerAudioSettings();
            Audio.Sanitize();
        }
    }

    /// <summary>The five volume sliders: Master plus the audio.md §7 categories.</summary>
    public enum PlayerVolume
    {
        Master,
        Music,
        Sfx,
        Ambience,
        Personal,
    }

    /// <summary>Linear 0..1 volumes per audio.md §7 category, plus the two mutes (Decision 4).</summary>
    [Serializable]
    public sealed class PlayerAudioSettings
    {
        // Placeholder levels; balancing them is out of scope (audio.md §10).
        public float Master = DefaultVolume;
        public float Music = DefaultVolume;
        public float Sfx = DefaultVolume;
        public float Ambience = DefaultVolume;
        public float Personal = DefaultVolume;
        public bool MuteAll;
        public bool MuteWhenUnfocused = true;

        public float Get(PlayerVolume volume) => volume switch
        {
            PlayerVolume.Master => Master,
            PlayerVolume.Music => Music,
            PlayerVolume.Sfx => Sfx,
            PlayerVolume.Ambience => Ambience,
            _ => Personal,
        };

        public void Set(PlayerVolume volume, float value)
        {
            value = Volume(value);
            switch (volume)
            {
                case PlayerVolume.Master: Master = value; break;
                case PlayerVolume.Music: Music = value; break;
                case PlayerVolume.Sfx: Sfx = value; break;
                case PlayerVolume.Ambience: Ambience = value; break;
                default: Personal = value; break;
            }
        }

        internal void Sanitize()
        {
            Master = Volume(Master);
            Music = Volume(Music);
            Sfx = Volume(Sfx);
            Ambience = Volume(Ambience);
            Personal = Volume(Personal);
        }

        private static float Volume(float value) => float.IsNaN(value) ? DefaultVolume : Mathf.Clamp01(value);

        private const float DefaultVolume = 0.8f;
    }
}

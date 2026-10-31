using System;
using Coimbra.Services.Events;
using Coimbra.Services.PlayerLoopEvents;
using SS3D.Core.Behaviours;
using SS3D.Systems.Audio;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;

namespace SS3D.Systems.Settings
{
    /// <summary>
    /// Client-local owner of <see cref="PlayerSettings"/> (2026-10_player-settings). Loads at boot, applies before
    /// the intro and lobby, and saves shortly after the last change. Views raise "set X" intents here and rebuild
    /// from <see cref="Settings"/> on <see cref="Changed"/>. Inert on a headless or dedicated server.
    /// </summary>
    public sealed class PlayerSettingsSubSystem : SubSystem
    {
        /// <summary>A slider drag fires many changes; write the file once it settles.</summary>
        private const float SaveDebounceSeconds = 0.5f;

        private PlayerSettingsStore _store;
        private bool _active;
        private bool _focused = true;
        private float _saveDueTime = -1f;

        /// <summary>Current values. Read-only for callers: change them through the Set methods.</summary>
        public PlayerSettings Settings { get; private set; } = new();

        public event Action Changed;

        public static bool IsHeadless =>
#if UNITY_SERVER
            true;
#else
            UnityEngine.Application.isBatchMode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;
#endif

        protected override void OnAwake()
        {
            base.OnAwake();
            if (IsHeadless) return;

            _active = true;
            _store = new PlayerSettingsStore(PlayerSettingsStore.DefaultPath);
            Settings = _store.Load();
            _focused = UnityEngine.Application.isFocused;
            UnityEngine.Application.focusChanged += HandleFocusChanged;
            AddHandle(UpdateEvent.AddListener(HandleUpdate));
        }

        protected override void OnStart()
        {
            base.OnStart();
            // AudioMixer.SetFloat is ignored when called from Awake, so the first apply waits for Start.
            if (_active) ApplyAudio();
        }

        protected override void OnDestroyed()
        {
            UnityEngine.Application.focusChanged -= HandleFocusChanged;
            if (_saveDueTime >= 0f) SaveNow();
            base.OnDestroyed();
        }

        public void SetVolume(PlayerVolume volume, float linear)
        {
            Settings.Audio.Set(volume, linear);
            CommitAudio();
        }

        public void SetMuteAll(bool muted)
        {
            Settings.Audio.MuteAll = muted;
            CommitAudio();
        }

        public void SetMuteWhenUnfocused(bool muted)
        {
            Settings.Audio.MuteWhenUnfocused = muted;
            CommitAudio();
        }

        /// <summary>The Audio tab's Reset to defaults (Decision 13).</summary>
        public void ResetAudio()
        {
            Settings.Audio = new PlayerAudioSettings();
            CommitAudio();
        }

        /// <summary>Writes pending changes now instead of after the debounce (quit, pilots).</summary>
        public bool SaveNow()
        {
            _saveDueTime = -1f;
            return _store != null && _store.Save(Settings);
        }

        private void CommitAudio()
        {
            ApplyAudio();
            ScheduleSave();
            Changed?.Invoke();
        }

        private void ScheduleSave()
        {
            if (_active) _saveDueTime = Time.unscaledTime + SaveDebounceSeconds;
        }

        private void ApplyAudio()
        {
            if (!_active) return;
            AudioMixer mixer = MainAudioMixer.Mixer;
            if (mixer == null)
            {
                Debug.LogWarning("[Settings] MainMixer not found in Resources; volume settings are not applied.");
                return;
            }

            PlayerAudioSettings audio = Settings.Audio;
            mixer.SetFloat(MainAudioMixer.MasterVolume, PlayerAudioMix.MasterDecibels(audio, _focused));
            // Mutes also cover any AudioSource that bypasses the mixer (no output group set).
            AudioListener.volume = PlayerAudioMix.IsMuted(audio, _focused) ? 0f : 1f;
            mixer.SetFloat(MainAudioMixer.MusicVolume, PlayerAudioMix.ToDecibels(audio.Music));
            mixer.SetFloat(MainAudioMixer.SfxVolume, PlayerAudioMix.ToDecibels(audio.Sfx));
            mixer.SetFloat(MainAudioMixer.AmbienceVolume, PlayerAudioMix.ToDecibels(audio.Ambience));
            mixer.SetFloat(MainAudioMixer.PersonalVolume, PlayerAudioMix.ToDecibels(audio.Personal));
        }

        private void HandleFocusChanged(bool focused)
        {
            _focused = focused;
            ApplyAudio();
        }

        private void HandleUpdate(ref EventContext context, in UpdateEvent updateEvent)
        {
            if (_saveDueTime >= 0f && Time.unscaledTime >= _saveDueTime) SaveNow();
        }
    }
}

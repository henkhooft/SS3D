using Coimbra.Services.Events;
using Coimbra.Services.PlayerLoopEvents;
using SS3D.Core.Behaviours;
using SS3D.Data;
using SS3D.Data.Generated;
using UnityEngine;

namespace SS3D.Systems.Audio
{
    /// <summary>
    /// Personal, internal audio cues (audio.md §4) — heartbeat, breathing, and future systemic cues
    /// (virology's symptom sound is the same category, per §4). Non-positional, no occlusion, heard
    /// only by whichever local owner is driving it — the diegetic-but-personal exception §1 carves
    /// out from ordinary positional SFX (§3). Self-bootstrapped by <c>SystemsBootstrap</c>
    /// (process-wide DDOL), same pattern as <c>ScreenEffectsSubSystem</c> / <c>AmbienceSubSystem</c>.
    /// Domain mappers (<c>HealthPersonalAudioMapper</c>, <c>StaminaPersonalAudioMapper</c>) push
    /// intensities in; this subsystem only owns playback. Breathing takes the max of stamina and
    /// health contributors so neither overwrites the other.
    /// </summary>
    public sealed class PersonalAudioSubSystem : SubSystem
    {
        private const float IntensityLerpPerSecond = 4f;
        private const float AlertCueVolumeScale = 0.8f;

        /// <summary>Minimum gap between alert cues — a burst of chips appearing together (or in
        /// quick succession) still reads as one restrained "ding," not a machine-gun (audio.md §6).</summary>
        private const float AlertCueCooldownSeconds = 1f;

        private AudioSource _heartbeatSource;
        private AudioSource _breathingSource;
        private AudioSource _alertCueSource;

        private float _heartbeatTarget;
        private float _staminaBreathingTarget;
        private float _healthBreathingTarget;
        private float _breathingTarget;
        private float _nextAlertCueTime;

        protected override void OnAwake()
        {
            base.OnAwake();

            _heartbeatSource = BuildPersonalSource("HeartbeatSource");
            _breathingSource = BuildPersonalSource("BreathingSource");
            _alertCueSource = BuildPersonalSource("AlertCueSource", loop: false);

            AddHandle(UpdateEvent.AddListener(HandleUpdate));
        }

        private AudioSource BuildPersonalSource(string name, bool loop = true)
        {
            GameObject host = new(name);
            host.transform.SetParent(Transform, false);

            AudioSource source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f; // Non-positional, no occlusion — audio.md §4.
            source.volume = loop ? 0f : 1f; // One-shot cue source keeps a fixed base volume.

            // TODO(audio-foundation Phase 0): route to the MainMixer "Personal" group once it
            // exists and a runtime-loadable mixer reference is available (same gap as Ambience).
            return source;
        }

        /// <summary>
        /// Sets the target heartbeat intensity (0 silent .. 1 full volume). The clip already loops a
        /// single beat/pair-of-beats cycle; intensity only scales volume, not tempo.
        /// </summary>
        public void SetHeartbeatIntensity(float intensity)
        {
            _heartbeatTarget = Mathf.Clamp01(intensity);
            EnsurePlayingIfAudible(_heartbeatSource, AudioTrackIds.Heartbeat, _heartbeatTarget);
        }

        /// <summary>
        /// Stamina-driven breathing contribution (0 silent/resting .. 1 fully exhausted).
        /// Combined with health via max — see <see cref="SetHealthBreathingIntensity"/>.
        /// </summary>
        public void SetStaminaBreathingIntensity(float intensity)
        {
            _staminaBreathingTarget = Mathf.Clamp01(intensity);
            RefreshBreathingTarget();
        }

        /// <summary>
        /// Health-driven labored breathing (oxy debt / critical). Combined with stamina via max.
        /// </summary>
        public void SetHealthBreathingIntensity(float intensity)
        {
            _healthBreathingTarget = Mathf.Clamp01(intensity);
            RefreshBreathingTarget();
        }

        /// <summary>
        /// Clears health breathing on ownership loss without touching stamina contribution.
        /// </summary>
        public void ClearHealthBreathing()
        {
            SetHealthBreathingIntensity(0f);
        }

        private void RefreshBreathingTarget()
        {
            _breathingTarget = Mathf.Max(_staminaBreathingTarget, _healthBreathingTarget);
            EnsurePlayingIfAudible(_breathingSource, AudioTrackIds.HeavyBreathing, _breathingTarget);
        }

        /// <summary>
        /// One-shot cue for a newly-appearing alert-stack chip or PDA notification (audio.md §6) —
        /// not a loop, cooldown-debounced so a burst of chips reads as one restrained "ding."
        /// </summary>
        public void PlayAlertCue()
        {
            if (Time.time < _nextAlertCueTime)
            {
                return;
            }

            if (!Assets.TryGet(AssetDatabases.Sounds, AudioTrackIds.AlertCue, out AudioClip clip))
            {
                return;
            }

            _nextAlertCueTime = Time.time + AlertCueCooldownSeconds;
            _alertCueSource.PlayOneShot(clip, AlertCueVolumeScale);
        }

        private static void EnsurePlayingIfAudible(AudioSource source, string trackId, float targetIntensity)
        {
            if (targetIntensity <= 0f || source.isPlaying)
            {
                return;
            }

            if (Assets.TryGet(AssetDatabases.Sounds, trackId, out AudioClip clip))
            {
                source.clip = clip;
                source.time = 0f;
                source.Play();
            }
        }

        private void HandleUpdate(ref EventContext context, in UpdateEvent updateEvent)
        {
            float step = IntensityLerpPerSecond * Time.deltaTime;

            _heartbeatSource.volume = Mathf.MoveTowards(_heartbeatSource.volume, _heartbeatTarget, step);
            _breathingSource.volume = Mathf.MoveTowards(_breathingSource.volume, _breathingTarget, step);

            StopIfSilent(_heartbeatSource, _heartbeatTarget);
            StopIfSilent(_breathingSource, _breathingTarget);
        }

        private static void StopIfSilent(AudioSource source, float target)
        {
            if (target <= 0f && source.volume <= 0f && source.isPlaying)
            {
                source.Stop();
            }
        }
    }
}

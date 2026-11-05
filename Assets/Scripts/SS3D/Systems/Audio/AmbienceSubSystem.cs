using Coimbra.Services.Events;
using Coimbra.Services.PlayerLoopEvents;
using SS3D.Core;
using SS3D.Core.Behaviours;
using SS3D.Systems.Area;
using SS3D.Systems.Entities.Events;
using UnityEngine;

namespace SS3D.Systems.Audio
{
    /// <summary>
    /// Client-local per-Area ambience crossfade (audio.md §2). Tracks the local player's current
    /// Area — the same one-tile lookup every Area-driven system uses
    /// (<see cref="AreaSubSystem.TryResolveAreaIdForWorldPosition"/>) — and crossfades between two
    /// non-positional 2D sources whenever the area's <see cref="AmbienceProfile"/> changes. Inside an
    /// area with no profile of its own that's the generic one; outside any area it's silence.
    /// Self-bootstrapped by <c>SystemsBootstrap</c> (process-wide DDOL), same as
    /// <c>ScreenEffectsSubSystem</c>.
    /// </summary>
    public sealed class AmbienceSubSystem : SubSystem
    {
        private const float PollInterval = 0.5f;
        private const float CrossfadeVolumePerSecond = 0.4f;

        private AudioSource _activeSource;
        private AudioSource _inactiveSource;

        private GameObject _listenerTarget;
        private AmbienceProfile _currentProfile;
        private float _nextPollTime;

        protected override void OnAwake()
        {
            base.OnAwake();

            _activeSource = BuildAmbienceSource("AmbienceSourceA");
            _inactiveSource = BuildAmbienceSource("AmbienceSourceB");

            AddHandle(LocalPlayerObjectChanged.AddListener(HandlePlayerObjectChanged));
            AddHandle(UpdateEvent.AddListener(HandleUpdate));
        }

        private AudioSource BuildAmbienceSource(string name)
        {
            GameObject host = new(name);
            host.transform.SetParent(Transform, false);

            AudioSource source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f; // Non-positional — audio.md §2, unlike diegetic SFX (§3).
            source.volume = 0f;
            source.outputAudioMixerGroup = MainAudioMixer.FindGroup(MainAudioMixer.AmbienceGroup);
            return source;
        }

        private void HandlePlayerObjectChanged(ref EventContext context, in LocalPlayerObjectChanged e)
        {
            _listenerTarget = e.PlayerHasObject ? e.PlayerObject : null;

            if (_listenerTarget == null)
            {
                // No body to be "in an area" — fade to silence. Mirrors HealthScreenEffectMapper.Clear
                // on HumanHealthController's own destroy/ownership loss: the driving consumer clears
                // the shared presentation state it was pushing.
                BeginCrossfade(null);
            }
        }

        private void HandleUpdate(ref EventContext context, in UpdateEvent updateEvent)
        {
            if (_listenerTarget != null && Time.time >= _nextPollTime)
            {
                _nextPollTime = Time.time + PollInterval;
                PollCurrentArea();
            }

            DriveCrossfade();
        }

        /// <summary>
        /// Resolves every poll rather than on area change, so a track authored while the player
        /// stands in the room applies without them leaving it.
        /// </summary>
        private void PollCurrentArea()
        {
            if (!SubSystems.TryGet(out AreaSubSystem areaSubSystem))
            {
                return;
            }

            if (!areaSubSystem.TryResolveAreaIdForWorldPosition(_listenerTarget.transform.position, out AreaId areaId))
            {
                BeginCrossfade(null);
                return;
            }

            areaSubSystem.TryGetAmbienceProfileId(areaId, out string profileId);
            BeginCrossfade(AmbienceProfileCatalog.Instance.Resolve(profileId));
        }

        private void BeginCrossfade(AmbienceProfile profile)
        {
            if (profile == _currentProfile)
            {
                return;
            }

            _currentProfile = profile;

            // The previously active source fades out into silence and becomes the next
            // inactive slot; the previously inactive source takes the new clip and fades in.
            AudioSource incoming = _inactiveSource;
            AudioSource outgoing = _activeSource;

            if (profile != null && profile.Bed != null)
            {
                incoming.clip = profile.Bed;
                incoming.time = 0f;
                incoming.Play();
            }
            else
            {
                incoming.Stop();
                incoming.clip = null;
            }

            _activeSource = incoming;
            _inactiveSource = outgoing;
        }

        private void DriveCrossfade()
        {
            float step = CrossfadeVolumePerSecond * Time.deltaTime;
            float activeTarget = _currentProfile != null ? _currentProfile.BedVolume : 0f;

            _activeSource.volume = Mathf.MoveTowards(_activeSource.volume, activeTarget, step);
            _inactiveSource.volume = Mathf.MoveTowards(_inactiveSource.volume, 0f, step);

            if (_inactiveSource.volume <= 0f && _inactiveSource.isPlaying)
            {
                _inactiveSource.Stop();
            }
        }
    }
}

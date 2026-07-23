using Coimbra.Services.Events;
using Coimbra.Services.PlayerLoopEvents;
using SS3D.Core;
using SS3D.Core.Behaviours;
using SS3D.Data;
using SS3D.Data.AssetDatabases;
using SS3D.Systems.Area;
using SS3D.Systems.Entities.Events;
using UnityEngine;

namespace SS3D.Systems.Audio
{
    /// <summary>
    /// Client-local per-Area ambience crossfade (audio.md §2). Tracks the local player's current
    /// Area — the same one-tile lookup every Area-driven system uses
    /// (<see cref="AreaSubSystem.TryResolveAreaIdForWorldPosition"/>) — and crossfades between two
    /// non-positional 2D sources whenever <see cref="AreaRecord.AmbienceTrackId"/> changes.
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
        private string _currentTrackId = string.Empty;
        private AreaId? _lastResolvedAreaId;
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

            // TODO(audio-foundation Phase 0): route to the MainMixer "Ambience" group once a
            // runtime-loadable mixer reference exists; currently outputs to Master like any
            // AudioSource with no explicit OutputAudioMixerGroup.
            return source;
        }

        private void HandlePlayerObjectChanged(ref EventContext context, in LocalPlayerObjectChanged e)
        {
            _listenerTarget = e.PlayerHasObject ? e.PlayerObject : null;
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

        private void PollCurrentArea()
        {
            if (!SubSystems.TryGet(out AreaSubSystem areaSubSystem))
            {
                return;
            }

            AreaId? resolvedAreaId = areaSubSystem.TryResolveAreaIdForWorldPosition(_listenerTarget.transform.position, out AreaId areaId)
                ? areaId
                : null;

            bool areaUnchanged = resolvedAreaId.HasValue == _lastResolvedAreaId.HasValue
                && (!resolvedAreaId.HasValue || resolvedAreaId.Value.Equals(_lastResolvedAreaId.Value));

            if (areaUnchanged)
            {
                return;
            }

            _lastResolvedAreaId = resolvedAreaId;

            string trackId = string.Empty;
            if (resolvedAreaId.HasValue && areaSubSystem.TryGetAmbienceTrackId(resolvedAreaId.Value, out string resolvedTrackId))
            {
                trackId = resolvedTrackId;
            }

            BeginCrossfade(trackId);
        }

        private void BeginCrossfade(string newTrackId)
        {
            if (newTrackId == _currentTrackId)
            {
                return;
            }

            _currentTrackId = newTrackId;

            // The previously active source fades out into silence and becomes the next
            // inactive slot; the previously inactive source takes the new clip and fades in.
            AudioSource incoming = _inactiveSource;
            AudioSource outgoing = _activeSource;

            if (!string.IsNullOrEmpty(newTrackId) && Assets.TryGet(AssetDatabases.Sounds, newTrackId, out AudioClip clip))
            {
                incoming.clip = clip;
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
            float activeTarget = string.IsNullOrEmpty(_currentTrackId) ? 0f : 1f;

            _activeSource.volume = Mathf.MoveTowards(_activeSource.volume, activeTarget, step);
            _inactiveSource.volume = Mathf.MoveTowards(_inactiveSource.volume, 0f, step);

            if (_inactiveSource.volume <= 0f && _inactiveSource.isPlaying)
            {
                _inactiveSource.Stop();
            }
        }
    }
}

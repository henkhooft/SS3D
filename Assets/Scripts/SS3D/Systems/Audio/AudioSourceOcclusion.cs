using SS3D.Utils;
using UnityEngine;

namespace SS3D.Systems.Audio
{
    /// <summary>
    /// Client-local occlusion for a pooled positional <see cref="AudioSource"/> — audio.md §3,
    /// the seventh consumer of the shared <see cref="LineOfSight"/> raycast (a wall blocks a
    /// sound exactly the same way it already blocks a shot, a subtitle bubble, or a wireless
    /// scan). Runs entirely client-side: occlusion depends on the local listener's position, so
    /// it cannot be baked into the server's play RPC.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioSourceOcclusion : MonoBehaviour
    {
        private static readonly LayerMask OcclusionMask = LayerMask.GetMask("Default");

        private const float SampleInterval = 0.15f;
        private const float LerpSpeed = 6f;

        private static Transform s_listener;

        private AudioSource _source;
        private AudioLowPassFilter _lowPass;
        private float _baseVolume = 1f;
        private float _cutoffCurrent = AudioOcclusionState.ClearCutoffHz;
        private float _volumeScaleCurrent = 1f;
        private float _nextSampleTime;
        private bool _isOccluded;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _lowPass = GetComponent<AudioLowPassFilter>();
            if (_lowPass == null)
            {
                _lowPass = gameObject.AddComponent<AudioLowPassFilter>();
            }

            _lowPass.cutoffFrequency = AudioOcclusionState.ClearCutoffHz;
        }

        /// <summary>
        /// Called by <see cref="AudioSubSystem"/> right before <see cref="AudioSource.Play"/> so a
        /// reused pooled source doesn't inherit the previous clip's muffle state.
        /// </summary>
        public void PrepareForPlayback(float volume)
        {
            _baseVolume = volume;
            _isOccluded = false;
            _cutoffCurrent = AudioOcclusionState.ClearCutoffHz;
            _volumeScaleCurrent = 1f;
            _nextSampleTime = 0f;
            _lowPass.cutoffFrequency = AudioOcclusionState.ClearCutoffHz;
            _source.volume = volume;
        }

        private void Update()
        {
            if (!_source.isPlaying)
            {
                return;
            }

            if (s_listener == null)
            {
                AudioListener listener = FindAnyObjectByType<AudioListener>();
                if (listener == null)
                {
                    return;
                }

                s_listener = listener.transform;
            }

            // Unity's own rolloff already silences sources past maxDistance — skip the raycast
            // and filter update entirely rather than sampling occlusion for inaudible sources.
            if (Vector3.Distance(s_listener.position, transform.position) > _source.maxDistance)
            {
                return;
            }

            if (Time.time >= _nextSampleTime)
            {
                _nextSampleTime = Time.time + SampleInterval;
                _isOccluded = !LineOfSight.HasLineOfSight(s_listener.position, transform.position, OcclusionMask, out _);
            }

            float lerpFactor = Time.deltaTime * LerpSpeed;
            _cutoffCurrent = AudioOcclusionState.LerpCutoffHz(_cutoffCurrent, _isOccluded, lerpFactor);
            _volumeScaleCurrent = AudioOcclusionState.LerpVolumeScale(_volumeScaleCurrent, _isOccluded, lerpFactor);

            _lowPass.cutoffFrequency = _cutoffCurrent;
            _source.volume = _baseVolume * _volumeScaleCurrent;
        }
    }
}

using SS3D.Systems.Audio;
using UnityEngine;

namespace SS3D.Systems.Health
{
    /// <summary>
    /// Maps a local player's <see cref="HealthSnapshot"/> onto the personal heartbeat cue
    /// (audio.md §4, worked example C). Deliberately diverges from
    /// <see cref="HealthScreenEffectMapper"/>'s cardiac-arrest handling: the screen vignette stays at
    /// full intensity through cardiac arrest (still dying, still worth showing), but the heartbeat
    /// sound represents an actual beating heart, so it goes silent the moment the heart stops and
    /// resumes only when a defib restarts it — "resuming on a successful defib" only makes sense if
    /// something was silenced first.
    /// </summary>
    public static class HealthPersonalAudioMapper
    {
        public static float ComputeHeartbeatIntensity(HealthSnapshot snapshot)
        {
            // IsCardiacArrest is the operative flag (mirrors HealthScreenEffectMapper.ComputeDyingCritical,
            // which checks it before State) — the heart has stopped, so there's nothing to beat until a
            // defib restarts it, regardless of what State currently reads.
            if (snapshot.IsCardiacArrest)
            {
                return 0f;
            }

            if (snapshot.State != HealthState.Critical)
            {
                // Dead: brain function reaching zero silences it entirely.
                // Healthy: no cue outside critical/dying (audio.md §4).
                return 0f;
            }

            float brainFactor = Mathf.InverseLerp(
                100f,
                HealthConstants.ConsciousnessBrainFunctionPercent,
                snapshot.BrainFunctionPercent);
            return Mathf.Clamp01(0.4f + 0.6f * brainFactor);
        }

        public static void Apply(HealthSnapshot snapshot, PersonalAudioSubSystem personalAudio)
        {
            personalAudio?.SetHeartbeatIntensity(ComputeHeartbeatIntensity(snapshot));
        }

        public static void Clear(PersonalAudioSubSystem personalAudio)
        {
            personalAudio?.SetHeartbeatIntensity(0f);
        }
    }
}

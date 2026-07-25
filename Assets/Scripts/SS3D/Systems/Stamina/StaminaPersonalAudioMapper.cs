using SS3D.Systems.Audio;
using UnityEngine;

namespace SS3D.Systems.Stamina
{
    /// <summary>
    /// Maps a local player's stamina ratio onto the personal heavy-breathing cue (audio.md §4,
    /// stamina.md §4 — "heavier breathing audio" as stamina depletes).
    /// </summary>
    public static class StaminaPersonalAudioMapper
    {
        /// <summary>Breathing starts becoming audible once stamina drops below this ratio.</summary>
        public const float AudibleThreshold = 0.6f;

        /// <param name="staminaRatio">Current stamina as a proportion of max (0..1) — matches
        /// <see cref="IStamina.Current"/> / <see cref="StaminaController.CurrentStamina"/>.</param>
        public static float ComputeBreathingIntensity(float staminaRatio)
        {
            return Mathf.Clamp01(Mathf.InverseLerp(AudibleThreshold, 0f, staminaRatio));
        }

        public static void Apply(float staminaRatio, PersonalAudioSubSystem personalAudio)
        {
            personalAudio?.SetStaminaBreathingIntensity(ComputeBreathingIntensity(staminaRatio));
        }
    }
}

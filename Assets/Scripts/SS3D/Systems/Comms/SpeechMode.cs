namespace SS3D.Systems.Comms
{
    /// <summary>
    /// How a speech event was uttered. Speak is the live local-speech path; Whisper/Shout/Emote
    /// are reserved for upcoming compose-input slices. Radio and Announcement are included so the
    /// subtitle view can already carry mode CSS — those channels still target the non-diegetic
    /// feed per comms.md §6–§8 when that slice lands.
    /// </summary>
    public enum SpeechMode : byte
    {
        Speak = 0,
        Whisper = 1,
        Shout = 2,
        Emote = 3,
        Radio = 4,
        Announcement = 5,
    }
}

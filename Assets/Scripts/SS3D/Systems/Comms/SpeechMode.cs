namespace SS3D.Systems.Comms
{
    /// <summary>
    /// How a speech event was uttered. Only Speak is used by the local-speech-bubble slice;
    /// Whisper and Shout are reserved so the wire struct and rendering pipeline don't need to
    /// change shape once those slices land (comms.md §10).
    /// </summary>
    public enum SpeechMode : byte
    {
        Speak = 0,
        Whisper = 1,
        Shout = 2,
    }
}

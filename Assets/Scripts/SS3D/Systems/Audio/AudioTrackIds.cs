namespace SS3D.Systems.Audio
{
    /// <summary>
    /// Fixed (non-content-authored) `AssetDatabases.Sounds` clip ids for personal audio (audio.md §4).
    /// Unlike ambience tracks (content, authored per-area), these are architecturally fixed cues —
    /// named here so a future content pass knows exactly which ids to register.
    /// </summary>
    public static class AudioTrackIds
    {
        public const string Heartbeat = "personal_heartbeat";
        public const string HeavyBreathing = "personal_heavy_breathing";
        public const string AlertCue = "personal_alert_cue";
    }
}

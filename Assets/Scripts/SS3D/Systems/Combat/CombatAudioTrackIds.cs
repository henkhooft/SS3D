namespace SS3D.Systems.Combat
{
    /// <summary>
    /// `AssetDatabases.Sounds` clip ids for combat SFX (audio.md §3 — a weapon already has a fire
    /// event; playing a positioned, occluded sound through the pool is a side effect of that event,
    /// not a new trigger mechanism). Registered from imported SS3D-Art content
    /// (`Assets/Art/Sound/Items/Weapons/Firearms/`) — see `Documents/art-asset-index.md`.
    /// </summary>
    public static class CombatAudioTrackIds
    {
        /// <summary>Generic gunshot report, alternated for variety — not AR-15-specific in the
        /// source pack (the AR-15 folder covers charging/dry-fire/selector/magazine foley only).</summary>
        public static readonly string[] GunFire = { "ba5fc758523340608d95c09378f951ad", "0503dda69f694341a3981a83b45b5eb1" };

        /// <summary>Empty magazine pulled free — plays when a reload begins (manual or auto-on-empty).</summary>
        public const string ReloadMagazineOut = "16b906b99fb5471fa72060f8c4c8cadd";
    }
}

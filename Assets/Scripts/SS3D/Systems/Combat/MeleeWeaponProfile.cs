using System;
using SS3D.Systems.Health;

namespace SS3D.Systems.Combat
{
    /// <summary>
    /// Melee timing and damage profile. Interim lethality — retune after armor (combat plan Phase 5).
    /// </summary>
    [Serializable]
    public struct MeleeWeaponProfile
    {
        public float BruteDamage;
        public float BurnDamage;
        public float WindupSeconds;
        public float RecoverySeconds;
        public float StaminaCost;
        public bool CanSever;

        public MeleeDamagePacket ToDamagePacket() => new(BruteDamage, BurnDamage, CanSever);

        public static MeleeWeaponProfile Fists => new()
        {
            BruteDamage = 8f,
            BurnDamage = 0f,
            WindupSeconds = 0.25f,
            RecoverySeconds = 0.7f,
            StaminaCost = 8f,
            CanSever = false,
        };

        /// <summary>Low-base fallback for any held item without a dedicated profile.</summary>
        public static MeleeWeaponProfile Improvised => new()
        {
            BruteDamage = 6f,
            BurnDamage = 0f,
            WindupSeconds = 0.3f,
            RecoverySeconds = 1.6f,
            StaminaCost = 10f,
            CanSever = false,
        };

        public static MeleeWeaponProfile Crowbar => new()
        {
            BruteDamage = 18f,
            BurnDamage = 0f,
            WindupSeconds = 0.35f,
            RecoverySeconds = 1.0f,
            StaminaCost = 12f,
            CanSever = false,
        };

        public static MeleeWeaponProfile Hatchet => new()
        {
            BruteDamage = 16f,
            BurnDamage = 0f,
            WindupSeconds = 0.3f,
            RecoverySeconds = 0.9f,
            StaminaCost = 11f,
            CanSever = true,
        };

        public static MeleeWeaponProfile KitchenKnife => new()
        {
            BruteDamage = 12f,
            BurnDamage = 0f,
            WindupSeconds = 0.2f,
            RecoverySeconds = 0.7f,
            StaminaCost = 9f,
            CanSever = true,
        };
    }
}

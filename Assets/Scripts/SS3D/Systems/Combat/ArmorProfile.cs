using System;
using SS3D.Systems.Health;

namespace SS3D.Systems.Combat
{
    /// <summary>
    /// Per-zone flat damage absorption profile for a worn armor piece (Documents/design/armor.md §2).
    /// Interim values — exact balancing is out of scope per armor.md §8.
    /// </summary>
    [Serializable]
    public struct ArmorProfile
    {
        public float BruteAbsorption;
        public float BurnAbsorption;
        public float MaxIntegrity;
        public BodyZoneMask CoveredZones;

        public static ArmorProfile SecurityJumpsuit => new()
        {
            BruteAbsorption = 5f,
            BurnAbsorption = 3f,
            MaxIntegrity = 80f,
            CoveredZones = BodyZoneMask.Chest | BodyZoneMask.LeftArm | BodyZoneMask.RightArm
                | BodyZoneMask.LeftLeg | BodyZoneMask.RightLeg | BodyZoneMask.Groin,
        };
    }
}

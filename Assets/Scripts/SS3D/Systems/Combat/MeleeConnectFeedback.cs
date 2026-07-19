using System;

namespace SS3D.Systems.Combat
{
    /// <summary>
    /// Client-local signal when a melee swing's connect frame applied damage.
    /// Used by Main HUD for a transient red reticle pulse (whiffs stay silent).
    /// </summary>
    public static class MeleeConnectFeedback
    {
        public static event Action LocalConnectHitLanded;

        public static void NotifyLocalConnectHitLanded()
        {
            LocalConnectHitLanded?.Invoke();
        }
    }
}

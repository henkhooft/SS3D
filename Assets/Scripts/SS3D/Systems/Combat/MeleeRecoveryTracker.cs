using UnityEngine;

namespace SS3D.Systems.Combat
{
    /// <summary>
    /// Blocks follow-up melee swings during the recovery window after a connect frame.
    /// ReadyProgress01 drives the Main HUD bracket lock-on recharge (design 2A).
    /// </summary>
    public sealed class MeleeRecoveryTracker : MonoBehaviour
    {
        private float _recoverUntil;
        private float _recoveryDuration;

        public bool IsRecovering => Time.time < _recoverUntil;

        /// <summary>
        /// 0 at recovery start (brackets fully receded), 1 when ready / idle (full lock-on).
        /// </summary>
        public float ReadyProgress01
        {
            get
            {
                if (_recoveryDuration <= 0f || Time.time >= _recoverUntil)
                {
                    return 1f;
                }

                float remaining = _recoverUntil - Time.time;
                float elapsed = _recoveryDuration - remaining;
                return Mathf.Clamp01(elapsed / _recoveryDuration);
            }
        }

        public void BeginRecovery(float recoverySeconds)
        {
            if (recoverySeconds <= 0f)
            {
                return;
            }

            _recoveryDuration = recoverySeconds;
            _recoverUntil = Time.time + recoverySeconds;
        }
    }
}

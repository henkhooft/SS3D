using SS3D.Interactions;
using SS3D.Interactions.Interfaces;
using System.Collections.Generic;
using UnityEngine;

namespace SS3D.Systems.Combat.Interactions
{
    /// <summary>
    /// Dedicated hitscan firearm on a held item. Mag/recoil/cooldown/reload are authoritative on
    /// the server instance of this component; owner clients mirror via InteractionController TargetRpcs.
    /// </summary>
    public class RangedWeaponItemExtension : MonoBehaviour, IInteractionSourceExtension
    {
        [SerializeField] private RangedWeaponProfile _profile = RangedWeaponProfile.M4;
        [Tooltip("Barrel tip — muzzle flash and future aim alignment. Wired by RangedPrefabSetup.")]
        [SerializeField] private Transform _muzzle;

        private int _rounds;
        private float _recoilStacks;
        private float _cooldownUntil;
        private float _reloadUntil;
        private float _lastRecoilDecayTime;
        private bool _initialized;

        public RangedWeaponProfile Profile => _profile;

        /// <summary>Barrel tip transform when the prefab recipe has wired one; otherwise null.</summary>
        public Transform Muzzle => _muzzle != null ? _muzzle : transform.Find("Muzzle");

        public int RoundsRemaining => _rounds;

        public int MagazineSize => Mathf.Max(1, _profile.MagazineSize);

        public bool IsReloading => _reloadUntil > 0f && Time.time < _reloadUntil;

        public bool IsOnCooldown => Time.time < _cooldownUntil;

        public float RecoilStacks => _recoilStacks;

        public float ReadyProgress01
        {
            get
            {
                float now = Time.time;
                float busyUntil = Mathf.Max(_cooldownUntil, _reloadUntil);
                if (busyUntil <= now)
                {
                    return 1f;
                }

                float duration = IsReloading
                    ? Mathf.Max(0.01f, _profile.ReloadSeconds)
                    : Mathf.Max(0.01f, _profile.FireCooldownSeconds);
                float remaining = busyUntil - now;
                float elapsed = duration - remaining;
                return Mathf.Clamp01(elapsed / duration);
            }
        }

        public bool IsBusy => IsReloading || IsOnCooldown;

        private void Awake()
        {
            EnsureInitialized();
        }

        public void GetSourceInteractions(IInteractionTarget[] targets, List<InteractionEntry> interactions, InteractionEvent context)
        {
            // Fire is Harm-primary on InteractionController — not a discovered Hit.
            if (!CanStartReload())
            {
                return;
            }

            interactions.Add(InteractionEntry.SourceOnly(new ReloadRangedInteraction(this)));
        }

        public bool CanStartFire()
        {
            EnsureInitialized();
            DecayRecoil();
            ServerCompleteReloadIfDue();
            return !IsBusy && _rounds > 0;
        }

        public bool CanStartReload()
        {
            EnsureInitialized();
            ServerCompleteReloadIfDue();
            return !IsReloading && _rounds < MagazineSize;
        }

        public void BeginLocalFireCooldown()
        {
            float cooldown = Mathf.Max(0.01f, _profile.FireCooldownSeconds);
            _cooldownUntil = Time.time + cooldown;
        }

        public void BeginLocalReload(float reloadSeconds)
        {
            _reloadUntil = Time.time + Mathf.Max(0.01f, reloadSeconds);
        }

        public void ClientSetRounds(int rounds)
        {
            EnsureInitialized();
            _rounds = Mathf.Clamp(rounds, 0, MagazineSize);
        }

        public void ClientSetRecoil(float recoilStacks)
        {
            _recoilStacks = Mathf.Max(0f, recoilStacks);
        }

        public bool ServerTryConsumeRound()
        {
            EnsureInitialized();
            DecayRecoil();
            ServerCompleteReloadIfDue();
            if (IsBusy || _rounds <= 0)
            {
                return false;
            }

            _rounds--;
            _recoilStacks += Mathf.Max(0f, _profile.RecoilPerShot);
            float cooldown = Mathf.Max(0.01f, _profile.FireCooldownSeconds);
            _cooldownUntil = Time.time + cooldown;
            return true;
        }

        public bool ServerTryBeginReload()
        {
            EnsureInitialized();
            ServerCompleteReloadIfDue();
            if (!CanStartReload())
            {
                return false;
            }

            _reloadUntil = Time.time + Mathf.Max(0.01f, _profile.ReloadSeconds);
            return true;
        }

        public void ServerCompleteReloadIfDue()
        {
            if (_reloadUntil <= 0f)
            {
                return;
            }

            if (Time.time < _reloadUntil)
            {
                return;
            }

            _rounds = MagazineSize;
            _reloadUntil = 0f;
        }

        public float CurrentSpreadDegrees(float horizontalSpeed, float aimDistanceMeters, float exertionPenalty = 0f)
        {
            EnsureInitialized();
            DecayRecoil();
            return AccuracyCone.ComputeSpreadDegrees(_profile, _recoilStacks, horizontalSpeed, aimDistanceMeters, exertionPenalty);
        }

        /// <summary>
        /// World pose for muzzle flash / presentation. Uses the wired socket when present;
        /// otherwise a short offset along the item forward so flash still reads without a recipe re-run.
        /// </summary>
        public void GetMuzzleWorldPose(out Vector3 position, out Vector3 forward)
        {
            Transform muzzle = Muzzle;
            if (muzzle != null && muzzle != transform)
            {
                position = muzzle.position;
                forward = muzzle.forward.sqrMagnitude > 0.0001f ? muzzle.forward.normalized : transform.forward;
                return;
            }

            forward = transform.forward.sqrMagnitude > 0.0001f ? transform.forward.normalized : Vector3.forward;
            position = transform.position + (forward * 0.35f);
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _rounds = MagazineSize;
            _lastRecoilDecayTime = Time.time;
            _initialized = true;
        }

        private void DecayRecoil()
        {
            float now = Time.time;
            float dt = now - _lastRecoilDecayTime;
            _lastRecoilDecayTime = now;
            if (dt <= 0f || _recoilStacks <= 0f)
            {
                return;
            }

            if (IsOnCooldown)
            {
                return;
            }

            _recoilStacks = Mathf.Max(0f, _recoilStacks - (dt * Mathf.Max(0f, _profile.RecoilDecayPerSecond)));
        }
    }
}

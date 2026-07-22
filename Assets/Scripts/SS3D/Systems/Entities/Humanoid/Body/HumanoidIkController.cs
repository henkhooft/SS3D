using SS3D.Systems.Entities.Humanoid.Body;
using UnityEngine;

namespace SS3D.Systems.Entities.Humanoid
{
    /// <summary>
    /// IK hooks for combat look-at and future foot placement (#937).
    /// </summary>
    public class HumanoidIkController : MonoBehaviour
    {
        private const int UpperBodyLayer = 1;
        private static readonly int[] AttackSwingStateHashes =
        {
            Animator.StringToHash("Attack Swing"),
            Animator.StringToHash("Attack Swing Downward"),
            Animator.StringToHash("Attack Swing Backhand"),
        };

        [SerializeField] private HumanoidRigReferences _rig;
        [SerializeField] private Transform _lookAtTarget;
        [SerializeField] private float _headLookWeight = 0.65f;
        [SerializeField] private float _torsoLookWeight = 0.45f;
        [SerializeField] private float _lookDistance = 4f;
        /// <summary>Matches combat body yaw so head look-at does not snap ahead of the torso.</summary>
        [SerializeField] private float _lookAtLerpMultiplier = 3.5f;
        [SerializeField] private float _meleeIkBlendLerp = 6f;
        [SerializeField] private float _blockedStandUpHeadroom = 1.2f;

        private bool _combatLookActive;
        private float _meleeAttackIkBlend;
        private float _aimYaw;
        private float _aimPitch;
        private bool _hasWorldAimPoint;
        private Vector3 _worldAimPoint;
        private Vector3 _smoothedLookTarget;
        private bool _hasSmoothedLookTarget;
        private Animator _animator;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (_rig == null)
            {
                _rig = GetComponent<HumanoidRigReferences>();
            }
        }

        public void SetCombatLookAt(bool active, float aimYaw, float aimPitch)
        {
            _combatLookActive = active;
            _aimYaw = aimYaw;
            _aimPitch = aimPitch;
            if (!active)
            {
                _hasWorldAimPoint = false;
                _hasSmoothedLookTarget = false;
                _meleeAttackIkBlend = 0f;
            }
        }

        /// <summary>
        /// Owner-side precise aim point (includes up/down). Remotes reconstruct from yaw/pitch.
        /// </summary>
        public void SetCombatAimPoint(Vector3 worldAimPoint)
        {
            _worldAimPoint = worldAimPoint;
            _hasWorldAimPoint = true;
        }

        /// <summary>
        /// Returns true if there is enough headroom to stand up from ragdoll.
        /// </summary>
        public bool HasStandUpHeadroom()
        {
            return !Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.up, _blockedStandUpHeadroom);
        }

        /// <summary>
        /// Suggested get-up variant when headroom is blocked.
        /// </summary>
        public bool ShouldUseLowGetUp() => !HasStandUpHeadroom();

        private void OnAnimatorIK(int layerIndex)
        {
            if (_animator == null || !_animator.isHuman)
            {
                return;
            }

            // Look-at is global; only apply once (base layer pass).
            if (layerIndex != 0)
            {
                return;
            }

            Transform head = _rig != null ? _rig.Head : null;
            if (head == null)
            {
                head = _animator.GetBoneTransform(HumanBodyBones.Head);
            }

            if (head == null)
            {
                return;
            }

            if (!_combatLookActive)
            {
                _animator.SetLookAtWeight(0f);
                return;
            }

            // Swing clip owns spine/chest (upper-body mask); suppress torso look-at while Attack Swing plays.
            float swingTarget = IsUpperBodyAttackSwing() ? 1f : 0f;
            _meleeAttackIkBlend = Mathf.MoveTowards(
                _meleeAttackIkBlend,
                swingTarget,
                Time.deltaTime * _meleeIkBlendLerp);

            Vector3 desiredTarget = _hasWorldAimPoint
                ? _worldAimPoint
                : head.position + AimDirection(_aimYaw, _aimPitch) * _lookDistance;

            if (!_hasSmoothedLookTarget)
            {
                // Seed from current facing so look-at does not pop when leaving a swing.
                _smoothedLookTarget = head.position + transform.forward * _lookDistance;
                _hasSmoothedLookTarget = true;
            }

            _smoothedLookTarget = Vector3.Lerp(
                _smoothedLookTarget,
                desiredTarget,
                Time.deltaTime * _lookAtLerpMultiplier);

            if (_lookAtTarget != null)
            {
                _lookAtTarget.position = _smoothedLookTarget;
            }

            // SetLookAtWeight(global, body, head). Head stays on aim; body weight eases out during swing.
            float bodyWeight = Mathf.Lerp(_torsoLookWeight, 0f, _meleeAttackIkBlend);
            _animator.SetLookAtWeight(1f, bodyWeight, _headLookWeight);
            _animator.SetLookAtPosition(_smoothedLookTarget);
        }

        private bool IsUpperBodyAttackSwing()
        {
            if (_animator.layerCount <= UpperBodyLayer)
            {
                return false;
            }

            if (IsAttackSwingState(_animator.GetCurrentAnimatorStateInfo(UpperBodyLayer)))
            {
                return true;
            }

            return _animator.IsInTransition(UpperBodyLayer)
                && IsAttackSwingState(_animator.GetNextAnimatorStateInfo(UpperBodyLayer));
        }

        private static bool IsAttackSwingState(AnimatorStateInfo info)
        {
            for (int i = 0; i < AttackSwingStateHashes.Length; i++)
            {
                if (info.shortNameHash == AttackSwingStateHashes[i])
                {
                    return true;
                }
            }

            return false;
        }

        private static Vector3 AimDirection(float yawDegrees, float pitchDegrees)
        {
            // pitchDegrees: positive = look up
            float yaw = yawDegrees * Mathf.Deg2Rad;
            float pitch = pitchDegrees * Mathf.Deg2Rad;
            float cosPitch = Mathf.Cos(pitch);
            return new Vector3(
                Mathf.Sin(yaw) * cosPitch,
                Mathf.Sin(pitch),
                Mathf.Cos(yaw) * cosPitch);
        }
    }
}

using SS3D.Systems.Entities.Humanoid.Body;
using SS3D.Systems.Health;
using SS3D.Systems.Stamina;
using UnityEngine;

namespace SS3D.Systems.Entities.Humanoid
{
    /// <summary>
    /// Controls the movement for living biped characters that use the same armature
    /// as the human model uses.
    /// </summary>
    [RequireComponent(typeof(AnimationOrchestrator))]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Animator))]
    public class HumanoidLivingController : HumanoidController
    {

        [Header("Components")]
        [SerializeField] private CharacterController _characterController;
        [SerializeField] private StaminaController _staminaController;
        private HumanHealthController _healthController;
        [SerializeField] private HumanoidPredictedMovement _predictedMovement;

        public bool IsDragging { get; set; }

        /// <summary>Planar coast while unsupported (no plenum). Used when predicted movement is disabled.</summary>
        private Vector3 _coastVelocity;

		public override void OnStartClient()
        {
            base.OnStartClient();
            _healthController = GetComponent<HumanHealthController>();
            if (_predictedMovement == null)
            {
                _predictedMovement = GetComponent<HumanoidPredictedMovement>();
            }
            if (!IsOwner)
            {
                return;
            }    
        }

        /// <summary>
        /// Executes the movement code and updates the IK targets
        /// </summary>
        protected override void ProcessCharacterMovement()
        {
            if (TryGetComponent(out Ragdoll ragdoll)
                && ragdoll.Presentation != BodyPresentationState.Locomotion)
            {
                _characterController.Move(Physics.gravity);
                MoveMovementTarget(Vector2.zero, 5);
                MovePlayer();
                return;
            }

            bool predictedOwnsLoco = _predictedMovement != null && _predictedMovement.enabled;
            bool unsupported = HumanoidSpaceSupport.IsUnsupportedAt(transform.position);
            // Avoid publishing walk Speed the same frame we enter space float (clears Floating via gait).
            ProcessPlayerInput(publishSpeed: !predictedOwnsLoco && !unsupported);

            if (predictedOwnsLoco)
            {
                return;
            }

            // Human.prefab ships PredictedMovement disabled — this path owns living space float.
            if (TryProcessSpaceFloat(unsupported))
            {
                return;
            }

            _characterController.Move(Physics.gravity);

            float gaitSpeed = FilterSpeed();
            if (Input.magnitude != 0)
            {
                MoveMovementTarget(Input);
                if (!IsDragging)
                {
                    if (IsCombatMode())
                    {
                        RotatePlayerToCombatAim();
                    }
                    else
                    {
                        RotatePlayerToMovement();
                    }
                }

                MovePlayer();
                PublishLocomotionVelocity(TargetMovement, gaitSpeed);
            }
            else
            {
                MovePlayer();
                MoveMovementTarget(Vector2.zero, 5);
                if (IsCombatMode() && !IsDragging)
                {
                    RotatePlayerToCombatAim();
                }

                PublishLocomotionVelocity(Vector3.zero, 0f);
            }
        }

        /// <summary>
        /// When no plenum underfoot: Floating anim, no gravity/WASD, coast last planar velocity.
        /// Returns true when space float consumed this frame.
        /// </summary>
        private bool TryProcessSpaceFloat(bool unsupported)
        {
            bool wasFloating = BodyStateMachine != null && BodyStateMachine.Snapshot.IsFloating;

            if (!unsupported)
            {
                if (wasFloating)
                {
                    BodyStateMachine.SetFloating(false);
                    if (Input.magnitude < 0.01f)
                    {
                        _coastVelocity = Vector3.zero;
                    }
                }

                return false;
            }

            if (!wasFloating)
            {
                _coastVelocity = CaptureLivingCoastVelocity();
                BodyStateMachine?.SetFloating(true);
            }

            if (_coastVelocity.sqrMagnitude > 0.0001f)
            {
                _characterController.Move(_coastVelocity * Time.deltaTime);
            }

            MoveMovementTarget(Vector2.zero, 5);
            if (IsCombatMode() && !IsDragging)
            {
                RotatePlayerToCombatAim();
            }

            PublishLocomotionVelocity(Vector3.zero, 0f);
            return true;
        }

        private Vector3 CaptureLivingCoastVelocity()
        {
            if (TargetMovement.sqrMagnitude < 0.0001f)
            {
                return Vector3.zero;
            }

            float healthMultiplier = _healthController != null
                ? _healthController.Snapshot.MovementSpeedMultiplier
                : 1f;
            float combatFactor = 1f;
            if (IsCombatMode())
            {
                combatFactor = IsRunning ? _combatRunSpeedFactor : _combatWalkSpeedFactor;
            }

            float exertionFactor = _staminaController != null
                ? Mathf.Lerp(1f, 0.55f, _staminaController.ExertionPenalty)
                : 1f;
            float gait = FilterSpeed();
            float speed = _movementSpeed * healthMultiplier * combatFactor * exertionFactor * gait;
            return TargetMovement.normalized * speed;
        }

        protected override float FilterSpeed()
        {
            // Exhaustion no longer hard-blocks run (stamina.md §3); ExertionPenalty slows MovePlayer.
            return IsRunning ? RunAnimatorValue : WalkAnimatorValue;
        }

        /// <summary>
        /// Moves the player to the target movement
        /// </summary>
        protected override void MovePlayer()
        {
            // Health rewrite: MovementSpeedMultiplier subsumes the legacy per-foot FeetHealthFactor
            // (it already derives from LeftLeg/RightLeg zone damage). Combat scaling is orthogonal.
            float healthMultiplier = _healthController != null
                ? _healthController.Snapshot.MovementSpeedMultiplier
                : 1f;
            float combatFactor = 1f;
            // Combat walk/run clips are authored at the same cadence across stances (melee + ranged),
            // so apply slow combat speed scaling for any combat mode.
            if (IsCombatMode())
            {
                combatFactor = IsRunning ? _combatRunSpeedFactor : _combatWalkSpeedFactor;
            }

            float exertionFactor = _staminaController != null
                ? Mathf.Lerp(1f, 0.55f, _staminaController.ExertionPenalty)
                : 1f;

            _characterController.Move(
                TargetMovement * (_movementSpeed * healthMultiplier * combatFactor * exertionFactor * Time.deltaTime));
        }
    }

}

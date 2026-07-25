using Coimbra.Services.Events;
using Coimbra.Services.PlayerLoopEvents;
using SS3D.Core.Behaviours;
using SS3D.Systems.Entities.Data;
using SS3D.Systems.Entities.Humanoid.Body;
using System;
using System.Text;
using FishNet.Object;
using UnityEngine;

namespace SS3D.Systems.Entities.Humanoid
{
    /// <summary>
    /// Single writer to the Unity Animator for humanoids.
    /// Maps body state snapshots to animator parameters and layer weights.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class AnimationOrchestrator : Actor
    {
        public event Action<AnimationTriggerId> OnTriggerFired;

        [SerializeField] private HumanoidController _movementController;
        [SerializeField] private HumanoidBodyStateMachine _bodyStateMachine;
        [SerializeField] private Animator _animator;
        [SerializeField] private float _lerpMultiplier = 2.4f;
        [SerializeField] private HumanoidIkController _ikController;

        private float _currentSpeed;
        private float _targetSpeed;
        private float _currentVelX;
        private float _currentVelZ;
        private float _currentTurn;
        private float _targetVelX;
        private float _targetVelZ;
        private float _targetTurn;
        private BodyAnimationSnapshot _lastSnapshot = BodyAnimationSnapshot.Default;
        private AnimationTriggerId _lastConsumedTrigger = AnimationTriggerId.None;
        private byte _lastTriggerSequence;
        private bool _ownerPredictedAttack;
        private float _upperBodyWeight;
        private float _upperBodyWeightTarget;
        private float _additiveWeight;
        private float _additiveWeightTarget;
        private bool _posingSuppressed;
        private int _nextAttackVariant;

        /// <summary>Horizontal / downward / backhand cycle for melee AttackSwing.</summary>
        private const int MeleeSwingVariantCount = 3;

        /// <summary>Leg brute at/above this → stumble idle on injured blend; prefer base over arm additive.</summary>
        private const float StumbleLegThreshold = 0.65f;

        /// <summary>
        /// Soft Additive weight while Staggered so Additive Flinch (gut) fades in/out.
        /// Do not slam to 1 — Empty Additive is remapped to hurting idle and reads as a snap.
        /// </summary>
        private const float StaggerAdditiveWeight = 0.75f;

        /// <summary>
        /// Soft fade when entering/leaving Melee stance (Upper Body layer on/off).
        /// Swing clip lifetime is Animator exit-time owned — do not add swing duration constants here.
        /// </summary>
        [SerializeField] private float _upperBodyWeightLerp = 6f;

        [SerializeField] private float _additiveWeightLerp = 8f;

        public Animator Animator => _animator;

        protected override void OnStart()
        {
            base.OnStart();
            if (_bodyStateMachine == null)
            {
                _bodyStateMachine = GetComponent<HumanoidBodyStateMachine>();
            }
            if (_movementController == null)
            {
                _movementController = GetComponent<HumanoidController>();
            }

            EnsureAnimator();

            LogMissingAnimatorParametersOnce();
            SubscribeToEvents();

            if (_bodyStateMachine != null)
            {
                _targetSpeed = _bodyStateMachine.Snapshot.MovementSpeed;
                _targetVelZ = _targetSpeed;
                ApplySnapshot(_bodyStateMachine.Snapshot);
            }
            else if (_animator != null)
            {
                _targetSpeed = 0f;
                _animator.SetFloat(Animations.Humanoid.MovementSpeed, 0f);
                _animator.SetFloat(Animations.Humanoid.VelX, 0f);
                _animator.SetFloat(Animations.Humanoid.VelZ, 0f);
                _animator.SetFloat(Animations.Humanoid.Turn, 0f);
            }
        }

        private void EnsureAnimator()
        {
            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
            }

            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>(true);
            }
        }

        private void LogMissingAnimatorParametersOnce()
        {
            if (_animator == null || _animator.runtimeAnimatorController == null)
            {
                UnityEngine.Debug.LogError(
                    $"[AnimationOrchestrator] No runtimeAnimatorController on '{name}'.",
                    this);
                return;
            }

            AnimatorControllerParameter[] parameters = _animator.parameters;
            bool Has(int hash)
            {
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (parameters[i].nameHash == hash)
                    {
                        return true;
                    }
                }

                return false;
            }

            (string Name, int Hash)[] required =
            {
                ("Speed", Animations.Humanoid.MovementSpeed),
                ("Floating", Animations.Humanoid.Floating),
                ("LimpSide", Animations.Humanoid.LimpSide),
                ("IsCrawling", Animations.Humanoid.IsCrawling),
                ("IsDragging", Animations.Humanoid.IsDragging),
                ("ArmHold", Animations.Humanoid.ArmHold),
                ("InjuredArmLeft", Animations.Humanoid.InjuredArmLeft),
                ("InjuredArmRight", Animations.Humanoid.InjuredArmRight),
                ("InjuredLeg", Animations.Humanoid.InjuredLeg),
                ("IsSeated", Animations.Humanoid.IsSeated),
                ("CombatMode", Animations.Humanoid.CombatMode),
                ("CombatStance", Animations.Humanoid.CombatStance),
                ("AimYaw", Animations.Humanoid.AimYaw),
                ("AimPitch", Animations.Humanoid.AimPitch),
                ("AttackVariant", Animations.Humanoid.AttackVariant),
                ("MirrorUpperBody", Animations.Humanoid.MirrorUpperBody),
                ("VelX", Animations.Humanoid.VelX),
                ("VelZ", Animations.Humanoid.VelZ),
                ("Turn", Animations.Humanoid.Turn),
            };

            StringBuilder missing = null;
            foreach ((string Name, int Hash) entry in required)
            {
                if (Has(entry.Hash))
                {
                    continue;
                }

                missing ??= new StringBuilder();
                missing.Append(missing.Length == 0 ? entry.Name : ", " + entry.Name);
            }

            if (missing == null)
            {
                return;
            }

            StringBuilder present = new StringBuilder();
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i > 0)
                {
                    present.Append(", ");
                }

                present.Append(parameters[i].name);
            }

            UnityEngine.Debug.LogError(
                $"[AnimationOrchestrator] Animator on '{name}' is missing parameters: {missing}. "
                + $"Controller '{_animator.runtimeAnimatorController.name}' currently has "
                + $"[{present}]. Run SS3D → Animation → Rebind Humanoid Animator Parameters.",
                this);
        }

        protected override void OnEnabled()
        {
            base.OnEnabled();
            AddHandle(UpdateEvent.AddListener(HandleUpdate));
        }

        protected override void OnDestroyed()
        {
            base.OnDestroyed();
            UnsubscribeFromEvents();
        }

        private void HandleUpdate(ref EventContext context, in UpdateEvent updateEvent)
        {
            // Coimbra UpdateEvent still fires after enabled=false — must guard or walk params keep writing.
            if (!isActiveAndEnabled || _posingSuppressed || _animator == null || !_animator.enabled)
            {
                return;
            }

            ApplyLocomotionVelocity();
            TickUpperBodyWeight();
            TickAdditiveWeight();
        }

        /// <summary>
        /// Ragdoll/collapse sets this so Coimbra listeners cannot keep driving walk cycles.
        /// </summary>
        public void SetPosingSuppressed(bool suppressed)
        {
            _posingSuppressed = suppressed;
            if (suppressed && _animator != null)
            {
                _animator.SetFloat(Animations.Humanoid.MovementSpeed, 0f);
                _animator.SetFloat(Animations.Humanoid.VelX, 0f);
                _animator.SetFloat(Animations.Humanoid.VelZ, 0f);
                _animator.enabled = false;
            }
        }

        private void SubscribeToEvents()
        {
            if (_movementController != null)
            {
                _movementController.OnSpeedChangeEvent += HandleSpeedChanged;
                _movementController.OnLocomotionVelocityChanged += HandleLocomotionVelocityChanged;
            }
            if (_bodyStateMachine != null)
            {
                _bodyStateMachine.OnSnapshotChanged += ApplySnapshot;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (_movementController != null)
            {
                _movementController.OnSpeedChangeEvent -= HandleSpeedChanged;
                _movementController.OnLocomotionVelocityChanged -= HandleLocomotionVelocityChanged;
            }
            if (_bodyStateMachine != null)
            {
                _bodyStateMachine.OnSnapshotChanged -= ApplySnapshot;
            }
        }

        private void HandleSpeedChanged(float speed)
        {
            _targetSpeed = speed;
            _bodyStateMachine?.SetLocomotionSpeed(speed);
        }

        private void HandleLocomotionVelocityChanged(float velX, float velZ, float turn)
        {
            _targetVelX = velX;
            _targetVelZ = velZ;
            _targetTurn = turn;
            _targetSpeed = new Vector2(velX, velZ).magnitude;
            _bodyStateMachine?.SetLocomotionSpeed(_targetSpeed);
        }

        /// <summary>
        /// Plays a one-shot locomotion pack trigger (Jump / Turn90). Local visual for now.
        /// </summary>
        public void PlayLocomotionTrigger(int triggerHash)
        {
            if (_animator != null && triggerHash != 0)
            {
                _animator.SetTrigger(triggerHash);
            }
        }

        /// <summary>
        /// Owner-side immediate attack playback. Returns the swing variant used (0–2) for AttackSwing.
        /// </summary>
        public byte PlayAttackTrigger(AnimationTriggerId trigger)
        {
            if (_animator == null)
            {
                return 0;
            }

            int hash = Animations.Humanoid.GetTriggerHash(trigger);
            if (hash == 0)
            {
                return 0;
            }

            byte variant = 0;
            if (trigger == AnimationTriggerId.AttackSwing)
            {
                variant = (byte)(_nextAttackVariant % MeleeSwingVariantCount);
                _nextAttackVariant = (_nextAttackVariant + 1) % MeleeSwingVariantCount;
                _animator.SetInteger(Animations.Humanoid.AttackVariant, variant);
            }

            _ownerPredictedAttack = true;
            _animator.ResetTrigger(hash);
            _animator.SetTrigger(hash);
            return variant;
        }

        private void TickUpperBodyWeight()
        {
            if (_animator == null || _animator.layerCount <= 1)
            {
                return;
            }

            _upperBodyWeight = Mathf.MoveTowards(
                _upperBodyWeight,
                _upperBodyWeightTarget,
                Time.deltaTime * _upperBodyWeightLerp);
            _animator.SetLayerWeight(1, _upperBodyWeight);
        }

        private void TickAdditiveWeight()
        {
            if (_animator == null || _animator.layerCount <= 2)
            {
                return;
            }

            _additiveWeight = Mathf.MoveTowards(
                _additiveWeight,
                _additiveWeightTarget,
                Time.deltaTime * _additiveWeightLerp);
            _animator.SetLayerWeight(2, _additiveWeight);
        }

        public void ApplySnapshot(BodyAnimationSnapshot snapshot)
        {
            EnsureAnimator();
            if (_posingSuppressed || _animator == null || !_animator.enabled)
            {
                return;
            }

            if (snapshot.State == BodyState.Ragdoll)
            {
                return;
            }

            _lastSnapshot = snapshot;
            if (!IsLocalMovementAuthority())
            {
                _targetSpeed = snapshot.MovementSpeed;
                // Remotes do not yet replicate strafe axes — approximate with forward gait.
                _targetVelX = 0f;
                _targetVelZ = snapshot.MovementSpeed;
                _targetTurn = 0f;
            }
            ApplyLocomotion(snapshot);
            ApplyUpperBody(snapshot);
            ApplyCombat(snapshot);
            ApplyInjuries(snapshot);
            ApplyFullBodyOverride(snapshot);

            if (_bodyStateMachine != null)
            {
                byte sequence = _bodyStateMachine.TriggerSequence;
                if (sequence != _lastTriggerSequence && snapshot.ActiveTrigger != AnimationTriggerId.None)
                {
                    _lastTriggerSequence = sequence;
                    // Owner already played predictively — skip duplicate SetTrigger on host/client echo.
                    if (_ownerPredictedAttack)
                    {
                        _ownerPredictedAttack = false;
                        _lastConsumedTrigger = snapshot.ActiveTrigger;
                        OnTriggerFired?.Invoke(snapshot.ActiveTrigger);
                    }
                    else
                    {
                        ConsumeTrigger(snapshot.ActiveTrigger, snapshot.AttackVariant);
                    }
                }
            }
        }

        private void ApplyLocomotionVelocity()
        {
            float currentMag = Mathf.Sqrt(_currentVelX * _currentVelX + _currentVelZ * _currentVelZ);
            float targetMag = Mathf.Sqrt(_targetVelX * _targetVelX + _targetVelZ * _targetVelZ);
            bool leavingIdle = currentMag < 0.05f && targetMag > currentMag;
            // Walk targets sit near 0.3; run is 1.0 — only snap the short idle→walk step.
            bool leavingIdleToWalk = leavingIdle && targetMag <= 0.45f;

            if (leavingIdleToWalk)
            {
                _currentVelX = _targetVelX;
                _currentVelZ = _targetVelZ;
                _currentTurn = _targetTurn;
                _currentSpeed = _targetSpeed;
            }
            else if (leavingIdle)
            {
                // Idle → run: begin at walk gait so acceleration passes through the blend tree.
                Vector2 target = new Vector2(_targetVelX, _targetVelZ);
                Vector2 walkSeed = target.normalized * 0.3f;
                _currentVelX = walkSeed.x;
                _currentVelZ = walkSeed.y;
                _currentTurn = _targetTurn;
                _currentSpeed = 0.3f;
            }
            else
            {
                bool slowingToIdle = targetMag < 0.05f;
                float lerp = Time.deltaTime * (slowingToIdle ? _lerpMultiplier * 3f : _lerpMultiplier);
                _currentVelX = Mathf.Lerp(_currentVelX, _targetVelX, lerp);
                _currentVelZ = Mathf.Lerp(_currentVelZ, _targetVelZ, lerp);
                _currentTurn = Mathf.Lerp(_currentTurn, _targetTurn, lerp);
                _currentSpeed = Mathf.Lerp(_currentSpeed, _targetSpeed, lerp);
            }

            _animator.SetFloat(Animations.Humanoid.VelX, _currentVelX);
            _animator.SetFloat(Animations.Humanoid.VelZ, _currentVelZ);
            _animator.SetFloat(Animations.Humanoid.Turn, _currentTurn);
            _animator.SetFloat(Animations.Humanoid.MovementSpeed, _currentSpeed);
        }

        private void ApplyLocomotion(BodyAnimationSnapshot snapshot)
        {
            _animator.SetBool(Animations.Humanoid.Floating, snapshot.IsFloating);
            _animator.SetBool(Animations.Humanoid.IsCrawling, snapshot.IsCrawling);
            _animator.SetInteger(Animations.Humanoid.LimpSide, (int)snapshot.LimpSide);
            _animator.SetBool(Animations.Humanoid.IsDragging, snapshot.IsDragging);
        }

        private void ApplyUpperBody(BodyAnimationSnapshot snapshot)
        {
            _animator.SetInteger(Animations.Humanoid.ArmHold, (int)snapshot.ArmHold);
            _animator.SetBool(Animations.Humanoid.IsSeated, snapshot.IsSeated);
            _animator.SetBool(Animations.Humanoid.MirrorUpperBody, snapshot.MirrorUpperBody);

            if (_animator.layerCount <= 1)
            {
                return;
            }

            // Peaceful / Ranged: full base locomotion (ranged pack already has rifle poses).
            // Melee: Upper Body stays at weight 1 (Hold Default when empty; AttackSwing via trigger).
            bool needsUpperBodyLayer = snapshot.State != BodyState.Ragdoll
                && snapshot.CombatMode == HumanoidCombatMode.Melee;
            _upperBodyWeightTarget = needsUpperBodyLayer ? 1f : 0f;
        }

        private void ApplyCombat(BodyAnimationSnapshot snapshot)
        {
            bool inCombat = snapshot.CombatMode.IsCombat();
            _animator.SetBool(Animations.Humanoid.CombatMode, inCombat);
            _animator.SetInteger(Animations.Humanoid.CombatStance, (int)snapshot.CombatMode);
            _animator.SetFloat(Animations.Humanoid.AimYaw, snapshot.AimYaw);
            _animator.SetFloat(Animations.Humanoid.AimPitch, snapshot.AimPitch);
            _ikController?.SetCombatLookAt(inCombat, snapshot.AimYaw, snapshot.AimPitch);
        }

        private void ApplyInjuries(BodyAnimationSnapshot snapshot)
        {
            _animator.SetFloat(Animations.Humanoid.InjuredArmLeft, snapshot.InjuredArmLeft);
            _animator.SetFloat(Animations.Humanoid.InjuredArmRight, snapshot.InjuredArmRight);
            _animator.SetFloat(Animations.Humanoid.InjuredLeg, snapshot.InjuredLeg);

            if (_animator.layerCount > 2)
            {
                float armMax = Mathf.Max(snapshot.InjuredArmLeft, snapshot.InjuredArmRight);
                float leg = snapshot.InjuredLeg;

                float injuryWeight;
                if (leg >= StumbleLegThreshold && leg >= armMax)
                {
                    // Stumble idle already reads on base; keep arm additive light.
                    injuryWeight = armMax * 0.25f;
                }
                else if (armMax > 0.01f)
                {
                    injuryWeight = Mathf.Lerp(0.15f, 0.55f, armMax);
                }
                else
                {
                    injuryWeight = 0f;
                }

                // Soft target only — TickAdditiveWeight lerps. Stagger raises Additive so unmuted
                // Additive Flinch (gut) shows; never slam Empty Additive (hurting idle) to 1.
                _additiveWeightTarget = snapshot.State == BodyState.Staggered
                    ? Mathf.Max(injuryWeight, StaggerAdditiveWeight)
                    : injuryWeight;
            }
        }

        private void ApplyFullBodyOverride(BodyAnimationSnapshot snapshot)
        {
            if (_animator.layerCount <= 3)
            {
                return;
            }

            // Staggered uses Base + Additive Flinch — do not half-weight Full Body Override.
            float overrideWeight = snapshot.State switch
            {
                BodyState.Seated => 1f,
                BodyState.Crawling => 1f,
                _ => 0f,
            };
            _animator.SetLayerWeight(3, overrideWeight);
        }

        private void ConsumeTrigger(AnimationTriggerId trigger, byte attackVariant = 0)
        {
            // Sequence already gates re-entry; allow the same trigger id to fire repeatedly (e.g. swing spam).
            if (trigger == AnimationTriggerId.None)
            {
                return;
            }

            _lastConsumedTrigger = trigger;
            if (trigger == AnimationTriggerId.AttackSwing)
            {
                _animator.SetInteger(Animations.Humanoid.AttackVariant, attackVariant & 0x3);
            }

            int hash = Animations.Humanoid.GetTriggerHash(trigger);
            if (hash != 0)
            {
                _animator.ResetTrigger(hash);
                _animator.SetTrigger(hash);
            }

            OnTriggerFired?.Invoke(trigger);
        }

        public void FireLocalTrigger(AnimationTriggerId trigger)
        {
            _bodyStateMachine?.CmdFireTrigger(trigger, 0);
        }

        private bool IsLocalMovementAuthority()
        {
            if (_movementController is NetworkBehaviour networkBehaviour)
            {
                return networkBehaviour.IsOwner;
            }

            return false;
        }
    }
}

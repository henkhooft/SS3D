using SS3D.Core.Behaviours;
using SS3D.Systems.Entities.Humanoid.Body;
using SS3D.Systems.Inputs;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SS3D.Systems.Entities.Humanoid
{
    /// <summary>
    /// Toggles peaceful / combat stance and drives strafe + aim behaviour (#1246).
    /// Combat subtype (Melee vs Ranged) comes from inventory via <see cref="HumanoidBodyStateBridge"/>.
    /// Melee swing telegraph is requested by combat Hit dispatch via <see cref="RequestAttack"/>.
    /// </summary>
    [RequireComponent(typeof(HumanoidBodyStateMachine))]
    public class HumanoidCombatController : NetworkActor
    {
        [SerializeField] private HumanoidBodyStateMachine _bodyStateMachine;
        [SerializeField] private AnimationOrchestrator _orchestrator;
        [SerializeField] private HumanoidBodyStateBridge _bodyStateBridge;

        protected override void OnAwake()
        {
            base.OnAwake();
            if (_bodyStateMachine == null)
            {
                _bodyStateMachine = GetComponent<HumanoidBodyStateMachine>();
            }

            if (_orchestrator == null)
            {
                _orchestrator = GetComponent<AnimationOrchestrator>();
            }

            if (_bodyStateBridge == null)
            {
                _bodyStateBridge = GetComponent<HumanoidBodyStateBridge>();
            }
        }

        protected override void OnEnabled()
        {
            base.OnEnabled();
            AddHandle(Coimbra.Services.PlayerLoopEvents.UpdateEvent.AddListener(HandleUpdate));
        }

        private void HandleUpdate(ref Coimbra.Services.Events.EventContext context, in Coimbra.Services.PlayerLoopEvents.UpdateEvent updateEvent)
        {
            if (!IsOwner)
            {
                return;
            }

            if (InputInterface.IsCapturingText)
            {
                return;
            }

            if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
            {
                if (_bodyStateMachine.CombatMode.IsCombat())
                {
                    _bodyStateMachine.CmdSetCombatMode(HumanoidCombatMode.Peaceful);
                }
                else
                {
                    HumanoidCombatMode stance = _bodyStateBridge != null
                        ? _bodyStateBridge.ResolveCombatStance()
                        : HumanoidCombatMode.Melee;
                    _bodyStateMachine.CmdSetCombatMode(stance);
                }
            }
        }

        /// <summary>
        /// Called by combat system when a hit lands on this humanoid.
        /// </summary>
        public void OnHitReceived(Vector3 knockbackDirection, float knockbackForce, float staggerDuration)
        {
            if (!IsServer)
            {
                return;
            }

            _bodyStateMachine.ApplyStagger(staggerDuration);
            if (knockbackForce > 0f)
            {
                _bodyStateMachine.ApplyKnockback(knockbackDirection, knockbackForce);
            }
        }

        public void RequestAttack(AnimationTriggerId attackType)
        {
            if (!_bodyStateMachine.CanPerformAction())
            {
                return;
            }

            // Play immediately on the owning client — don't wait for ServerRpc + SyncVar.
            byte variant = _orchestrator != null
                ? _orchestrator.PlayAttackTrigger(attackType)
                : (byte)0;
            _bodyStateMachine.CmdFireTrigger(attackType, variant);
        }
    }
}

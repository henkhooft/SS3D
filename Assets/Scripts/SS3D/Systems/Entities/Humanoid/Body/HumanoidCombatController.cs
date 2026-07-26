using SS3D.Core.Behaviours;
using SS3D.Systems.Entities.Humanoid.Body;
using SS3D.Systems.Inputs;
using SS3D.Systems.Interactions;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SS3D.Systems.Entities.Humanoid
{
    /// <summary>
    /// Drives combat stance presentation and attack telegraphs (#1246).
    /// <c>C</c> toggles Help/Harm intent (combat mode follows Harm via <see cref="InteractionController"/>).
    /// Combat subtype (Melee vs Ranged) comes from inventory via <see cref="HumanoidBodyStateBridge"/>.
    /// Melee swing / ranged fire / reload telegraphs are requested via <see cref="RequestAttack"/>.
    /// </summary>
    [RequireComponent(typeof(HumanoidBodyStateMachine))]
    public class HumanoidCombatController : NetworkActor
    {
        [SerializeField] private HumanoidBodyStateMachine _bodyStateMachine;
        [SerializeField] private AnimationOrchestrator _orchestrator;
        [SerializeField] private HumanoidBodyStateBridge _bodyStateBridge;
        [SerializeField] private InteractionController _interactionController;

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

            if (_interactionController == null)
            {
                _interactionController = GetComponent<InteractionController>();
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
                if (_interactionController != null)
                {
                    _interactionController.RequestToggleIntent();
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

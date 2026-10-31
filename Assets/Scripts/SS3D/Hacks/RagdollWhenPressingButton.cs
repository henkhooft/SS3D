using SS3D.Core.Debugging;
using FishNet.Connection;
using SS3D.Core;
using SS3D.Core.Behaviours;
using SS3D.Systems.Entities.Humanoid;
using SS3D.Systems.Inputs;
using UnityEngine;
using UnityEngine.InputSystem;
using InputSubSystem = SS3D.Systems.Inputs.InputSubSystem;

namespace SS3D.Hacks
{
    /// <summary>
    /// Currenytly using button Y to ragdoll, might change in the future.
    /// </summary>
    public class RagdollWhenPressingButton : NetworkActor
    {
        [SerializeField]
        private Ragdoll _ragdoll;

        [SerializeField]
        private float _timeRagdolled;

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);

            if (IsOwner)
            {
                SubscribeToInput();
            }
            else if (prevOwner.Equals(LocalConnection))
            {
                UnsubscribeFromInput();
            }
        }

        protected override void OnEnabled()
        {
            base.OnEnabled();

            if (IsOwner)
            {
                SubscribeToInput();
            }
        }

        protected override void OnDisabled()
        {
            base.OnDisabled();

            if (IsOwner)
            {
                UnsubscribeFromInput();
            }
        }

        private void SubscribeToInput()
        {
            DebugTools.Get(DebugToolNames.KnockDown).Invoked += HandleKnockdown;
        }

        /// <summary>
        /// Unsubscribes from input events
        /// </summary>
        private void UnsubscribeFromInput()
        {
            DebugTools.Get(DebugToolNames.KnockDown).Invoked -= HandleKnockdown;
        }

        private void HandleKnockdown()
        {
            _ragdoll.Knockdown(_timeRagdolled);
        }
    }
}
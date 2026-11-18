using SS3D.Data;
using SS3D.Data.Generated;
using SS3D.Interactions;
using SS3D.Interactions.Extensions;
using SS3D.Interactions.Interfaces;
using SS3D.Systems.Entities;
using SS3D.Systems.Entities.Dragging;
using SS3D.Systems.Entities.Humanoid;
using SS3D.Systems.Inventory.Containers;
using UnityEngine;

namespace SS3D.Systems.Furniture
{
    /// <summary>
    /// Interaction used to drag heavy stuff around the map.
    /// </summary>
    public class DragInteraction : IInteraction, IClientInteractionSource
    {
        public string Name;
        public Sprite Icon;
        /// <summary>
        /// If the interaction should be range limited
        /// </summary>
        public bool RangeCheck { get; set; } = true;

        public string GetName(InteractionEvent interactionEvent)
        {
            IDragTarget target = interactionEvent.Target.GetGameObject().GetComponent<IDragTarget>();
            return target == null ? null : target.Dragged ? "Drop" : "Drag";
        }

        public string GetGenericName() => "Drag";

        public Sprite GetIcon(InteractionEvent interactionEvent)
        {
            return Assets.Get<Sprite>(AssetDatabases.InteractionIcons, InteractionIcons.Discard);
        }

        public bool CanInteract(InteractionEvent interactionEvent)
        {
            if (RangeCheck && !InteractionExtensions.RangeCheck(interactionEvent))
            {
                return false;
            }

            if (!IsInFront(interactionEvent.Source.GetComponentInParent<Entity>().gameObject.transform, interactionEvent.Target.GetGameObject().transform, 20f))
            {
                return false;
            }

            if (interactionEvent.Source is not Hand)
                return false;

            return true;
        }

        public bool Start(InteractionEvent interactionEvent, InteractionReference reference)
        {
            IDragTarget target = interactionEvent.Target.GetGameObject().GetComponent<IDragTarget>();
            if (target == null)
            {
                return false;
            }

            Transform dragger = interactionEvent.Source.GetComponentInParent<Entity>().transform;
            // The controller reads the drag from the target's replicated dragger (DragRegistry), so a remote client's
            // drag starts when the SyncVar arrives and ends when the target goes away.
            target.ServerSetDrag(!target.Dragged, dragger);
            return false;
        }

        private bool IsInFront(Transform transformToCheck, Transform target, float toleranceAngle)
        {
            // Calculate direction from the transform to the target
            Vector3 directionToTarget = (target.position - transformToCheck.position).normalized;

            // Calculate the angle between the transform's forward direction and direction to target
            float angle = Vector3.Angle(transformToCheck.forward, directionToTarget);

            // Check if the angle is within the tolerance range
            return angle <= toleranceAngle;
        }
    }
}
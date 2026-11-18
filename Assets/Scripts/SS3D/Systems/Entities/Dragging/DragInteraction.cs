using SS3D.Data;
using SS3D.Data.Generated;
using SS3D.Interactions;
using SS3D.Interactions.Extensions;
using SS3D.Interactions.Interfaces;
using SS3D.Systems.Entities;
using SS3D.Systems.Entities.Humanoid;
using SS3D.Systems.Inventory.Containers;
using UnityEngine;

namespace SS3D.Systems.Entities.Dragging
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

        /// <summary>If the target must lie in the start cone in front of the dragger (the console turns this off).</summary>
        public bool ConeCheck { get; set; } = true;

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

        /// <summary>
        /// Starting needs an empty hand (Decision 10), range, the target in front (<see cref="IDragTarget.IsInStartCone"/>)
        /// and the target's own rules. Letting go of your own drag needs none of them.
        /// </summary>
        public bool CanInteract(InteractionEvent interactionEvent)
        {
            if (interactionEvent.Source is not Hand hand)
            {
                return false;
            }

            IDragTarget target = interactionEvent.Target?.GetGameObject()?.GetComponent<IDragTarget>();
            Entity entity = hand.GetComponentInParent<Entity>();
            if (target == null || entity == null)
            {
                return false;
            }

            if (target.Dragged)
            {
                return target.DraggerObject == entity.NetworkObject;
            }

            if (RangeCheck && !InteractionExtensions.RangeCheck(interactionEvent))
            {
                return false;
            }

            return hand.IsEmpty() && (!ConeCheck || target.IsInStartCone(entity.transform)) && target.CanBeDraggedBy(entity.transform);
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
            HandSide hand = interactionEvent.Source is Hand source ? source.Side : HandSide.Right;
            target.ServerSetDrag(!target.Dragged, dragger, hand);
            return false;
        }
    }
}
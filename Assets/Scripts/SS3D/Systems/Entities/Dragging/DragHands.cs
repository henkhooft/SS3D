using SS3D.Systems.Inventory.Containers;

namespace SS3D.Systems.Entities.Dragging
{
    /// <summary>
    /// The dragging hand, as the inventory and the HUD see it: it refuses items and shows the reserved badge, like the
    /// off-hand of a two-handed rifle (<c>TwoHandedWeaponRules</c>), but it can still be selected, so Drop lets go.
    /// </summary>
    public static class DragHands
    {
        /// <summary>The drag <paramref name="hand"/>'s body holds, if this is the hand holding it; otherwise null.</summary>
        public static IDragTarget TargetHeldBy(Hand hand)
        {
            Entity body = hand != null ? hand.GetComponentInParent<Entity>() : null;
            if (body == null || !DragRegistry.TryGetTarget(body.NetworkObject, out object held)
                || held is not IDragTarget target)
            {
                return null;
            }

            return DragRules.IsHandReservedByDrag(true, target.DraggerHand, hand.Side) ? target : null;
        }

        public static bool IsDraggingHand(Hand hand) => TargetHeldBy(hand) != null;
    }
}

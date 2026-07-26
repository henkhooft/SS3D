using SS3D.Data;
using System;
using SS3D.Data.Generated;
using SS3D.Interactions;
using SS3D.Interactions.Extensions;
using SS3D.Interactions.Interfaces;
using SS3D.Systems.Entities;
using SS3D.Systems.Inventory.Containers;
using SS3D.Utils;
using UnityEngine;

namespace SS3D.Systems.Inventory.Interactions
{
    // a drop interaction is when we remove an item from the hand
    [Serializable]
    public class DropInteraction : IInteraction, IClientInteractionSource
    {
        public string Name;
        public Sprite Icon;
        /// <summary>
        /// The maximum angle of surface the item will allow being dropped on
        /// </summary>
        private float _maxSurfaceAngle = 10;

        /// <summary>
        /// Only raycast the default layer for seeing if we are vision blocked
        /// </summary>
        private LayerMask _defaultMask = LayerMask.GetMask("Default");

        public int Priority => 5;

        public string GetName(InteractionEvent interactionEvent)
        {
            return "Drop";
        }

        public string GetGenericName() => "Drop";

        public Sprite GetIcon(InteractionEvent interactionEvent)
        {
            return Icon ? Icon : Assets.Get<Sprite>(AssetDatabases.InteractionIcons, InteractionIcons.Discard);
        }

        public bool CanInteract(InteractionEvent interactionEvent)
        {
            // If item is not in hand return false
            if (interactionEvent.Source.GetRootSource() is not Hand)
            {
                return false;
            }

            Entity entity = interactionEvent.Source.GetComponentInParent<Entity>();

            if (!entity)
            {
                return false;
            }

            if (!interactionEvent.HasPoint)
            {
                return false;
            }

            // Confirm the entities ViewPoint can see the drop point (shared LOS helper).
            Vector3 viewOrigin = entity.ViewPoint.transform.position;
            Vector3 direction = (interactionEvent.Point - viewOrigin).normalized;
            if (!LineOfSight.TryGetFirstHit(viewOrigin, direction, Mathf.Infinity, _defaultMask, out RaycastHit hit))
            {
                return false;
            }

            // Confirm raycasted hit point is near the interaction point.
            // This is necessary because interaction rays are casted from the camera, not from view point
            if (Vector3.Distance(interactionEvent.Point, hit.point) > 0.1)
            {
                return false;
            }

            // Consider if the surface is facing up
            float angle = Vector3.Angle(interactionEvent.Normal, Vector3.up);

            if (angle > _maxSurfaceAngle)
            {
                return false;
            }

            if (interactionEvent.Source.GetRootSource() is not Hand)
            {
                return false;
            }

            return InteractionExtensions.RangeCheck(interactionEvent);
        }

        public bool Start(InteractionEvent interactionEvent, InteractionReference reference)
        {
            // Face entity yaw while keeping the prefab's authored rest pitch/roll (side-lying guns/tools).
            Entity entity = interactionEvent.Source.GetComponentInParent<Entity>();
            Hand hand = interactionEvent.Source.GetRootSource() as Hand;
            Quaternion rotation = hand.ItemInHand.GetWorldFacing(entity.transform.eulerAngles.y);
            hand.PlaceHeldItemOutOfHand(interactionEvent.Point, rotation);

            return false;
        }
    }
}
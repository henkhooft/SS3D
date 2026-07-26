using Coimbra;
using SS3D.Systems.Inventory.Items;
using UnityEngine;
using UnityEngine.Assertions;

namespace SS3D.Systems.Inventory.Containers
{
    /// <summary>
    /// This allows control over the position of displayed items inside the container.
    /// It also allows to define multiple points where items can be displayed inside the container,
    /// and items placed in the container appears at those different points in the order defined. 
    /// Take for example a battery compartment, battery should appear side by side when placed inside the compartment container.
    /// Without this they would pile up in the same spot.
    /// </summary>
    public class ContainerItemDisplay : MonoBehaviour
    {
        public AttachedContainer attachedContainer;
        public bool Mirrored;

        /// <summary>
        /// The list of items displayed in the container;
        /// </summary>
        private Item[] _displayedItems;

        public void Awake()
        {
            Assert.IsNotNull(attachedContainer);
            
            _displayedItems = new Item[attachedContainer.Displays.Length];
            attachedContainer.OnItemAttached += ContainerOnItemAttached;
            attachedContainer.OnItemDetached += ContainerOnItemDetached;
        }

        public void OnDestroy()
        {
            attachedContainer.OnItemAttached -= ContainerOnItemAttached;
            attachedContainer.OnItemDetached -= ContainerOnItemDetached;
        }

        private void ContainerOnItemAttached(object sender, Item item)
        {
            // Defines the transform of the item to be the first available position.
            int index = -1;
            for (int i = 0; i < attachedContainer.Displays.Length; i++)
            {
                if (_displayedItems[i] == null)
                {
                    index = i;
                    break;
                }
            }

            if (index == -1)
            {
                return;
            }

            Transform itemTransform = item.transform;

            // Check if a custom attachment point should be used
            Transform attachmentPoint = item.AttachmentPoint;
            if (Mirrored && item.AttachmentPointAlt != null)
            {
                attachmentPoint = item.AttachmentPointAlt;
            }

            if (attachmentPoint != null)
            {
                // #1226 used attachmentPoint.root (the ITEM) * localRotation — mid-air tumble became the grip.
                // Absolute world (identity * att) freezes world axes so hand-local drifts with facing.
                // Use the hand display's character root: hold ≈ character.facing * Attachment (stable).
                GameObject temporaryPoint = new GameObject("TempPivotPoint");
                Transform display = attachedContainer.Displays[index].transform;
                Transform characterRoot = display.root;

                temporaryPoint.transform.SetParent(display, false);
                temporaryPoint.transform.localPosition = Vector3.zero;

                itemTransform.SetParent(temporaryPoint.transform, false);
                temporaryPoint.transform.rotation = characterRoot.rotation * attachmentPoint.localRotation;
                itemTransform.localRotation = Quaternion.identity;
                itemTransform.localPosition = -attachmentPoint.localPosition;
            }
            else
            {
                itemTransform.SetParent(attachedContainer.Displays[index].transform, false);
                itemTransform.localPosition = Vector3.zero;
                itemTransform.localRotation = Quaternion.identity;
            }

            _displayedItems[index] = item;
        }

        private void ContainerOnItemDetached(object sender, Item item)
        {
            int index = -1;
            for (int i = 0; i < attachedContainer.Displays.Length; i++)
            {
                if (_displayedItems[i] == item)
                {
                    index = i;
                    break;
                }
            }

            if (index == -1)
            {
                return;
            }

            Transform itemParent = item.transform.parent;
            if (itemParent != null && itemParent != attachedContainer.Displays[index])
            {
                item.transform.SetParent(null, true);
                // It's currently deleting the game object containing the item, why is this here ?
                itemParent.gameObject.Dispose(true);
            }

            _displayedItems[index] = null;
        }
    }
}

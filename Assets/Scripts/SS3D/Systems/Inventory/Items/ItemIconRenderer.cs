using System;
using System.Collections.Generic;
using Coimbra;
using SS3D.Logging;
using SS3D.Systems.Inventory.Containers;
using SS3D.Utils;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SS3D.Systems.Inventory.Items
{
    /// <summary>
    /// Renders an item's inventory icon: a preview clone without its stored items, in the item's icon rotation. Moved
    /// out of <see cref="Item"/> unchanged (tech-debt P4.4 made room there for the replicated holder).
    /// </summary>
    internal static class ItemIconRenderer
    {
        public static Sprite Render(Item item, bool useWornShapedForm)
        {
#if UNITY_SERVER
            // Icon generation renders a camera to produce a preview texture, which is unavailable and
            // unnecessary on a dedicated server (no shaders are included in the build for it to use).
            return null;
#else
            // Same for headless / -nographics clients (multiplayer harness): NullGfxDevice cannot
            // run RuntimePreviewGenerator without URP GraphicsBuffer/Blitter exceptions.
            if (UnityEngine.Application.isBatchMode
                || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                return null;
            }

            Quaternion previousPreviewRotation = RuntimePreviewGenerator.PreviewRotation;
            RuntimePreviewGenerator.PreviewRotation = item.IconRotation;

            // Find stored items
            AttachedContainer[] containers = item.GetComponentsInChildren<AttachedContainer>();
            // If stored items are found, temporarily set their parents to null,
            // so IconPreviewGenerator won't generate stored items
            Dictionary<Transform, Transform> storedItemsWithParents = new Dictionary<Transform, Transform>();
            Transform previewObject = null;
            Sprite icon = null;
            try
            {
                foreach (AttachedContainer attachedContainer in containers)
                {
                    IEnumerable<Item> storedItems = attachedContainer.Items;
                    foreach (Item storedItem in storedItems)
                    {
                        Transform itemTransform = storedItem.transform;
                        storedItemsWithParents.Add(itemTransform, itemTransform.parent);
                    }
                }
                foreach (Transform storedItem in storedItemsWithParents.Keys)
                {
                    storedItem.parent = null;
                }

                // Stay in a loaded scene until bake finishes. HideAndDontSave makes
                // scene.IsValid() false, so RuntimePreviewGenerator re-clones; ClothingItemPresentation.Awake
                // then ApplyWorldForm and a worn bake becomes the folded pile again.
                previewObject = Object.Instantiate(item.transform, null, false);
                RemoveInteractionOutlines(previewObject);
                Item previewItem = previewObject.GetComponent<Item>();
                if (previewItem.TryGetComponent(out ClothingItemPresentation presentation))
                {
                    presentation.PrepareIconPreview(useWornShapedForm);
                }
                if (useWornShapedForm && presentation != null)
                {
                    // Bypass SetVisibility — that re-applies world (folded) form.
                    presentation.ApplyWornShapedForm();
                    SetChildRenderersEnabled(previewObject, true);
                }
                else
                {
                    previewItem.SetVisibility(true);
                }

                try
                {
                    // Bright full-toon ObjectIcon — not live half-toon world mats.
                    Texture2D texture = IconPreviewGenerator.Generate(previewObject, 128, 128);
                    if (texture == null)
                    {
                        Log.Warning(item, "Can't generate icon for " + item.name + ".");
                        icon = null;
                    }
                    else
                    {
                        icon = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                            new Vector2(0.5f, 0.5f), 100);
                        icon.name = item.transform.name;
                    }
                }
                catch (NullReferenceException)
                {
                    Log.Warning(item, "Can't generate icon for " + item.name + ".");
                    icon = null;
                }
            }
            finally
            {
                RuntimePreviewGenerator.PreviewRotation = previousPreviewRotation;
                if (previewObject != null)
                {
                    previewObject.gameObject.Dispose(false);
                }
                foreach (KeyValuePair<Transform, Transform> storedItemWithParent in storedItemsWithParents)
                {
                    storedItemWithParent.Key.parent = storedItemWithParent.Value;
                }
            }

            return icon;
#endif
        }

        /// <summary>
        /// Removes any interaction outline shells (added by <see cref="SS3D.Systems.Interactions.InteractionOutlineView"/>)
        /// from a preview clone, so they can't leak into generated icons.
        /// Must DestroyImmediate: Coimbra Dispose → Destroy is end-of-frame, but the preview camera
        /// renders in this frame while hover shells are still enabled.
        /// </summary>
        private static void RemoveInteractionOutlines(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (child.name == SS3D.Systems.Interactions.InteractionOutlineView.OutlineObjectName)
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                    continue;
                }

                RemoveInteractionOutlines(child);
            }
        }

        private static void SetChildRenderersEnabled(Transform root, bool visible)
        {
            // includeInactive: worn-shaped clothing children start disabled; preview must enable them.
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer childRenderer in renderers)
            {
                if (SS3D.Systems.Interactions.InteractionOutlineView.IsOutlineRenderer(childRenderer))
                {
                    continue;
                }

                childRenderer.enabled = visible;
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace SS3D.Systems.Inputs
{
    /// <summary>
    /// Single authority for "is the pointer currently over an interface". Spans both input UI
    /// stacks: legacy uGUI (via the <see cref="EventSystem"/>) and UI Toolkit runtime panels
    /// (via panel picking). World-click gameplay gates must query this instead of talking to a
    /// single stack, otherwise clicks leak through UI Toolkit overlays (radial menu, machine
    /// interfaces) that the uGUI raycaster does not know about.
    /// Also tracks text-entry capture depth so world interactions/selection clear while a field
    /// holds <see cref="InputTextEntryScope"/> (compose, console fields, etc.).
    /// </summary>
    public static class InputInterface
    {
        private static readonly List<UIDocument> Documents = new();
        private static int _textCaptureDepth;

        /// <summary>
        /// True while any <see cref="InputTextEntryScope"/> is active. World click/hover gates
        /// should treat this like pointer-over-UI so typing cannot fire gameplay.
        /// </summary>
        public static bool IsCapturingText => _textCaptureDepth > 0;

        /// <summary>
        /// Registers a runtime UI Toolkit document so its panel participates in pointer queries.
        /// Safe to call multiple times; disabled documents are ignored while querying.
        /// </summary>
        public static void RegisterDocument(UIDocument document)
        {
            if (document == null || Documents.Contains(document))
            {
                return;
            }

            Documents.Add(document);
        }

        /// <summary>
        /// Unregisters a previously registered document. Call from the owner's teardown.
        /// </summary>
        public static void UnregisterDocument(UIDocument document)
        {
            if (document == null)
            {
                return;
            }

            Documents.Remove(document);
        }

        /// <summary>Pushed by <see cref="InputTextEntryScope"/>; nested scopes are reference-counted.</summary>
        public static void PushTextCapture()
        {
            _textCaptureDepth++;
        }

        /// <summary>Matched to <see cref="PushTextCapture"/>; never goes below zero.</summary>
        public static void PopTextCapture()
        {
            if (_textCaptureDepth > 0)
            {
                _textCaptureDepth--;
            }
        }

        /// <summary>
        /// True when the pointer is over any uGUI element or any registered, enabled UI Toolkit panel,
        /// or when text entry is capturing keyboard (compose / focused fields).
        /// </summary>
        public static bool IsPointerOverInterface()
        {
            if (IsCapturingText)
            {
                return true;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return true;
            }

            return IsPointerOverToolkitPanel();
        }

        /// <summary>
        /// Current pointer position in bottom-left screen pixels (Input System mouse, with legacy fallback).
        /// </summary>
        public static Vector2 GetPointerScreenPosition()
        {
            return Mouse.current != null
                ? Mouse.current.position.ReadValue()
                : (Vector2)Input.mousePosition;
        }

        private static bool IsPointerOverToolkitPanel()
        {
            if (Documents.Count == 0)
            {
                return false;
            }

            Vector2 screenPosition = GetPointerScreenPosition();

            for (int i = 0; i < Documents.Count; i++)
            {
                UIDocument document = Documents[i];
                if (document == null || !document.isActiveAndEnabled)
                {
                    continue;
                }

                VisualElement root = document.rootVisualElement;
                IPanel panel = root?.panel;
                if (panel == null)
                {
                    continue;
                }

                // ScreenToPanel expects bottom-left screen pixels (same as Mouse/Input.mousePosition).
                Vector2 panelPosition = RuntimePanelUtils.ScreenToPanel(panel, screenPosition);
                if (panel.Pick(panelPosition) != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

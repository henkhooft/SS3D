using SS3D.Core.Behaviours;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SS3D.Systems.Inputs
{
    /// <summary>
    /// Owns the player's <see cref="Controls"/> and exposes the input arbiter.
    /// </summary>
    /// <remarks>
    /// Input state is derived, never counted. Callers push an <see cref="InputContext"/> or a
    /// suppression and receive an <see cref="IInputHandle"/>; disposing the handle removes exactly
    /// that request. All resolution lives in <see cref="InputArbiter"/>, which writes
    /// InputAction.enabled from the live request set, so dead keys, leaked input, and enabled/refcount
    /// desync are structurally impossible.
    /// </remarks>
    public sealed class InputSubSystem : SubSystem
    {
        public Controls Inputs { get; private set; }

        public float MouseSensitivity { get; private set; }

        /// <summary>Escape while a modal UI (machine interface) is open. Bound in code, arbitrated.</summary>
        public InputAction UiCancel => _uiCancel;

        /// <summary>Held while inspecting (Shift). Bound in code, arbitrated by the Gameplay context.</summary>
        public InputAction DetailedExamine => _detailedExamine;

        /// <summary>Opens local-speech compose (T). Bound in code, arbitrated by the Gameplay context.</summary>
        public InputAction OpenLocalSpeechCompose => _openLocalSpeechCompose;

        /// <summary>
        /// Toggle the Alert Icon Stack debug panel (F4). Bound in code like <see cref="UiCancel"/> —
        /// avoid editing the generated <see cref="Controls"/> asset for one-off debug chords.
        /// F3 is reserved for <c>LocalSpeechDebugTrigger</c> (local chat test lines).
        /// </summary>
        public InputAction ToggleAlertStackDebug => _toggleAlertStackDebug;

        private InputActionMap _systemMap;
        private InputAction _uiCancel;
        private InputAction _detailedExamine;
        private InputAction _openLocalSpeechCompose;
        private InputAction _toggleAlertStackDebug;

        private InputArbiter _arbiter;

        protected override void OnAwake()
        {
            DontDestroyOnLoad(transform.gameObject);

            base.OnAwake();

            Setup();
        }

        private void Setup()
        {
            MouseSensitivity = 0.001f;
            Inputs = new Controls();

            // Legacy hold-to-show objectives (Fade) and debug Spawn Cans both bound Tab in the
            // generated Controls asset. Erase those bindings — Tab is compose-only now.
            EraseAllBindings(Inputs.Other.Fade);
            EraseAllBindings(Inputs.Other.SpawnCans);

            BuildSystemActions();

            List<InputAction> allActions = CollectAllActions();
            _arbiter = new InputArbiter(allActions, BuildContexts());

            // Always-on baseline; never released.
            _arbiter.PushContext(InputContext.Global);
        }

        private static void EraseAllBindings(InputAction action)
        {
            for (int i = action.bindings.Count - 1; i >= 0; i--)
            {
                action.ChangeBinding(i).Erase();
            }
        }

        #region Public API

        /// <summary>
        /// Pushes a context. While the returned handle is live it participates in resolving the active
        /// context (highest priority wins). Dispose to pop it.
        /// </summary>
        public IInputHandle PushContext(InputContext context) => _arbiter.PushContext(context);

        /// <summary>Suppresses every action in a map while the handle is live.</summary>
        public IInputHandle SuppressMap(InputActionMap map) => _arbiter.SuppressActions(map.actions);

        /// <summary>Suppresses a single action while the handle is live.</summary>
        public IInputHandle SuppressAction(InputAction action) => _arbiter.SuppressActions(new[] { action });

        /// <summary>
        /// Suppresses every action that contains a binding with the given control path (e.g.
        /// "&lt;Mouse&gt;/leftButton", "&lt;Mouse&gt;/scroll/y") while the handle is live.
        /// </summary>
        public IInputHandle SuppressBinding(string bindingPath) => _arbiter.SuppressBinding(bindingPath);

        #endregion

        #region Setup helpers

        private void BuildSystemActions()
        {
            _systemMap = new InputActionMap("System");
            _uiCancel = _systemMap.AddAction("Cancel", InputActionType.Button, "<Keyboard>/escape");
            _detailedExamine = _systemMap.AddAction("DetailedExamine", InputActionType.Button);
            _detailedExamine.AddBinding("<Keyboard>/leftShift");
            _detailedExamine.AddBinding("<Keyboard>/rightShift");
            _openLocalSpeechCompose = _systemMap.AddAction(
                "OpenLocalSpeechCompose", InputActionType.Button, "<Keyboard>/t");
            // F3 = LocalSpeechDebugTrigger; F2 = screen-effects debug (condemned uGUI).
            _toggleAlertStackDebug = _systemMap.AddAction(
                "ToggleAlertStackDebug", InputActionType.Button, "<Keyboard>/f4");
        }

        private List<InputAction> CollectAllActions()
        {
            List<InputAction> allActions = new();
            foreach (InputAction action in Inputs)
            {
                allActions.Add(action);
            }

            foreach (InputAction action in _systemMap.actions)
            {
                allActions.Add(action);
            }

            return allActions;
        }

        private Dictionary<InputContext, InputContextDefinition> BuildContexts()
        {
            InputActionMap camera = Inputs.Camera.Get();
            InputActionMap movement = Inputs.Movement.Get();
            InputActionMap console = Inputs.Console.Get();
            InputActionMap hotkeys = Inputs.Hotkeys.Get();
            InputActionMap other = Inputs.Other.Get();
            InputActionMap tile = Inputs.TileCreator.Get();
            InputActionMap interactions = Inputs.Interactions.Get();

            InputAction consoleOpen = Inputs.Console.Open;
            InputAction tileToggle = Inputs.TileCreator.ToggleMenu;

            return new Dictionary<InputContext, InputContextDefinition>
            {
                // System actions only: menu toggle (in Other), open console, open build menu,
                // plus code-defined debug toggles that share Other's availability.
                [InputContext.Global] = new InputContextDefinition(
                    new[] { other },
                    new[] { consoleOpen, tileToggle, _toggleAlertStackDebug }),

                [InputContext.Gameplay] = new InputContextDefinition(
                    new[] { movement, camera, interactions, hotkeys, other },
                    new[] { consoleOpen, tileToggle, _detailedExamine, _openLocalSpeechCompose, _toggleAlertStackDebug }),

                // Build menu: keep looking around and placing; drop world interactions/hotkeys.
                [InputContext.TileMenu] = new InputContextDefinition(
                    new[] { movement, camera, tile, other },
                    new[] { consoleOpen, _detailedExamine, _toggleAlertStackDebug }),

                // Map Editor: it polls Keyboard/Mouse directly for its own free-fly camera, so
                // Movement/Camera must be masked here too (unlike TileMenu) to avoid the normal
                // character/camera fighting it every frame.
                [InputContext.MapEditor] = new InputContextDefinition(
                    new[] { tile, other },
                    new[] { consoleOpen }),

                // Machine panel captures movement/camera; Escape closes via UiCancel (Other masked).
                [InputContext.MachineUI] = new InputContextDefinition(
                    new[] { hotkeys, interactions },
                    new[] { _uiCancel }),

                [InputContext.Console] = new InputContextDefinition(
                    new[] { console },
                    System.Array.Empty<InputAction>()),

                // Text field focused (local-speech compose): gameplay off; Tab is read from
                // Keyboard device in LocalSpeechBubbleController (UITK focus eats InputActions).
                [InputContext.TextEntry] = new InputContextDefinition(
                    System.Array.Empty<InputActionMap>(),
                    System.Array.Empty<InputAction>()),
            };
        }

        #endregion
    }
}

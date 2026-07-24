using System;
using System.Collections.Generic;
using SS3D.Systems.Examine;
using SS3D.UI.Examine.Components;
using SS3D.UI.MachineInterface.Components;
using SS3D.UI.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace SS3D.UI.Examine
{
    /// <summary>
    /// Persistent character-examine window — a paperdoll grid inside a <see cref="MachineWindow"/>.
    /// Unlike every other examine surface, it stays open until the × closes it (no auto-close on
    /// mouse-leave) — a deliberate departure from examine.md §4's hold-to-peek/no-click-lock rule,
    /// scoped to character examine only (see examine.md fork-deviations).
    /// <para>
    /// Holding a slot ~650ms shows a "taking" affordance and clears that slot's icon locally.
    /// FOLLOW-UP: this is a UI-only stub — no networked item transfer happens yet. Wiring a real
    /// take-from-another-character transfer (range/permission checks, a server-validated request) is
    /// out of scope for this pass.
    /// </para>
    /// </summary>
    public sealed class CharacterExamineWindowView : IUiSurface
    {
        private const float SlotSize = 64f;
        private const float HoldDurationSeconds = 0.65f;

        public event Action CloseRequested;

        private readonly StyleSheet _characterExamineStyle;
        private readonly StyleSheet _inventorySlotStyle;
        private readonly StyleSheet _diegeticTokensStyle;
        private readonly StyleSheet _machineWindowStyle;
        private readonly CharacterExamineIconSet _icons;

        private VisualElement _root;
        private MachineWindow _window;
        private CharacterPaperdollGrid _grid;
        private Label _holdHintLabel;

        private CharacterExamineSlot? _holdingSlot;
        private float _holdElapsed;

        public bool IsOpen { get; private set; }

        public CharacterExamineWindowView(
            StyleSheet characterExamineStyle,
            StyleSheet inventorySlotStyle,
            StyleSheet diegeticTokensStyle,
            StyleSheet machineWindowStyle,
            CharacterExamineIconSet icons)
        {
            _characterExamineStyle = characterExamineStyle;
            _inventorySlotStyle = inventorySlotStyle;
            _diegeticTokensStyle = diegeticTokensStyle;
            _machineWindowStyle = machineWindowStyle;
            _icons = icons;
        }

        public void Attach(VisualElement layerRoot)
        {
            _root = new VisualElement { name = "character-examine-window-surface" };
            _root.style.flexGrow = 1;
            _root.pickingMode = PickingMode.Ignore;

            if (_characterExamineStyle != null)
            {
                _root.styleSheets.Add(_characterExamineStyle);
            }

            if (_inventorySlotStyle != null)
            {
                _root.styleSheets.Add(_inventorySlotStyle);
            }

            if (_diegeticTokensStyle != null)
            {
                _root.styleSheets.Add(_diegeticTokensStyle);
            }

            layerRoot.Add(_root);

            BuildTree();
            SetVisible(false);
        }

        public void Detach()
        {
            _window = null;
            _grid = null;
            _holdHintLabel = null;
            _root?.RemoveFromHierarchy();
            _root = null;
        }

        public void Show(string title, IReadOnlyList<CharacterExamineSlotContent> slots)
        {
            if (_window == null)
            {
                return;
            }

            _window.Title = title;
            foreach (CharacterExamineSlotContent content in slots)
            {
                _grid.SetSlot(content);
            }

            CancelHold();
            IsOpen = true;
            SetVisible(true);
        }

        public void Hide()
        {
            IsOpen = false;
            CancelHold();
            SetVisible(false);
        }

        /// <summary>Advances the hold-to-take timer. Driven by the owning subsystem's Update.</summary>
        public void Tick(float deltaTime)
        {
            if (_holdingSlot == null)
            {
                return;
            }

            _holdElapsed += deltaTime;
            if (_holdElapsed < HoldDurationSeconds)
            {
                return;
            }

            CompleteHold(_holdingSlot.Value);
        }

        private void BuildTree()
        {
            _window = new MachineWindow();
            _window.CloseClicked += () => CloseRequested?.Invoke();
            _window.AddToClassList("character-examine-window");
            if (_machineWindowStyle != null)
            {
                _window.styleSheets.Add(_machineWindowStyle);
            }

            _grid = new CharacterPaperdollGrid(SlotSize, _icons);
            _window.Content.Add(_grid);

            foreach (CharacterExamineSlot slot in Enum.GetValues(typeof(CharacterExamineSlot)))
            {
                WireHoldGesture(slot, _grid.GetSlot(slot));
            }

            _holdHintLabel = new Label();
            _holdHintLabel.AddToClassList("character-examine-window__hint");
            _holdHintLabel.AddToClassList("font-arcade");
            _holdHintLabel.style.display = DisplayStyle.None;
            _window.Content.Add(_holdHintLabel);

            _root.Add(_window);
        }

        private void WireHoldGesture(CharacterExamineSlot slot, InventorySlot inventorySlot)
        {
            inventorySlot.RegisterCallback<PointerDownEvent>(_ => BeginHold(slot, inventorySlot));
            inventorySlot.RegisterCallback<PointerUpEvent>(_ => CancelHold());
            inventorySlot.RegisterCallback<PointerLeaveEvent>(_ => CancelHold());
        }

        private void BeginHold(CharacterExamineSlot slot, InventorySlot inventorySlot)
        {
            if (inventorySlot.ItemIcon == null)
            {
                return;
            }

            _holdingSlot = slot;
            _holdElapsed = 0f;
            inventorySlot.AddToClassList("inventory-slot--drop-target");
            _holdHintLabel.text = $"Hold to Take: {inventorySlot.SlotLabel}";
            _holdHintLabel.style.display = DisplayStyle.Flex;
        }

        private void CompleteHold(CharacterExamineSlot slot)
        {
            InventorySlot inventorySlot = _grid.GetSlot(slot);
            // FOLLOW-UP: local-only — clears the displayed icon but does not move the item anywhere.
            inventorySlot.ItemIcon = null;
            CancelHold();
        }

        private void CancelHold()
        {
            if (_holdingSlot != null)
            {
                _grid.GetSlot(_holdingSlot.Value).RemoveFromClassList("inventory-slot--drop-target");
            }

            _holdingSlot = null;
            _holdElapsed = 0f;
            _holdHintLabel.style.display = DisplayStyle.None;
        }

        private void SetVisible(bool visible)
        {
            if (_window == null)
            {
                return;
            }

            _window.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}

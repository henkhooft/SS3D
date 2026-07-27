using System;
using System.Collections.Generic;
using SS3D.Systems.Examine;
using SS3D.Systems.Inputs;
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
    /// Hold-to-take: PointerDown on a filled slot (when <see cref="TakeAllowed"/>) raises
    /// <see cref="TakeHoldStarted"/>; the owning overlay starts a server delayed interaction.
    /// Progress is drawn via <see cref="BeginTakeProgress"/> after the server accepts.
    /// </para>
    /// </summary>
    public sealed class CharacterExamineWindowView : IUiSurface
    {
        private const float SlotSize = 64f;
        private const float OpenOffsetX = 24f;
        private const float OpenOffsetY = 24f;

        public event Action CloseRequested;
        public event Action<CharacterExamineSlot> TakeHoldStarted;
        public event Action TakeHoldCancelled;

        private readonly StyleSheet _examineStyle;
        private readonly StyleSheet _inventorySlotStyle;
        private readonly StyleSheet _diegeticTokensStyle;
        private readonly StyleSheet _machineWindowStyle;
        private readonly CharacterExamineIconSet _icons;

        private VisualElement _root;
        private MachineWindow _window;
        private CharacterPaperdollGrid _grid;
        private Label _holdHintLabel;

        private CharacterExamineSlot? _pendingHoldSlot;
        private CharacterExamineSlot? _progressSlot;
        private float _progressDelay;
        private float _progressElapsed;

        public bool IsOpen { get; private set; }

        /// <summary>
        /// When false, pointer holds on slots do not start a take (living conscious examine-only).
        /// </summary>
        public bool TakeAllowed { get; set; }

        public CharacterExamineWindowView(
            StyleSheet examineStyle,
            StyleSheet inventorySlotStyle,
            StyleSheet diegeticTokensStyle,
            StyleSheet machineWindowStyle,
            CharacterExamineIconSet icons)
        {
            _examineStyle = examineStyle;
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

            if (_examineStyle != null)
            {
                _root.styleSheets.Add(_examineStyle);
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

        /// <param name="screenPosition">
        /// Bottom-left screen pixels (mouse). Window opens near the cursor via
        /// <see cref="InputInterface.ScreenToPanel"/> — same panel-space rule as hover examine.
        /// </param>
        public void Show(
            string title,
            IReadOnlyList<CharacterExamineSlotContent> slots,
            bool takeAllowed,
            Vector2 screenPosition)
        {
            if (_window == null)
            {
                return;
            }

            TakeAllowed = takeAllowed;
            ApplySlots(title, slots);
            CancelLocalHoldGesture();
            ClearTakeProgress();
            IsOpen = true;
            SetVisible(true);
            PositionNearCursor(screenPosition);
            UpdateHoldHintIdle();
        }

        private void PositionNearCursor(Vector2 screenPositionBottomLeft)
        {
            if (_window == null)
            {
                return;
            }

            Vector2 panelPos = screenPositionBottomLeft;
            IPanel panel = _window.panel;
            if (panel != null)
            {
                panelPos = InputInterface.ScreenToPanel(panel, screenPositionBottomLeft);
            }

            // Pixel left/top (clear any percent/translate defaults) so drag ConvertToPixelPosition works.
            _window.style.translate = new Translate(0, 0);
            _window.style.left = panelPos.x + OpenOffsetX;
            _window.style.top = panelPos.y + OpenOffsetY;
            _window.style.right = StyleKeyword.Auto;
            _window.style.bottom = StyleKeyword.Auto;
        }

        public void RefreshSlots(string title, IReadOnlyList<CharacterExamineSlotContent> slots)
        {
            if (_window == null || !IsOpen)
            {
                return;
            }

            ApplySlots(title, slots);
        }

        public void Hide()
        {
            IsOpen = false;
            CancelLocalHoldGesture();
            ClearTakeProgress();
            SetVisible(false);
        }

        /// <summary>Starts the slot spinner after the server accepts the take windup.</summary>
        public void BeginTakeProgress(CharacterExamineSlot slot, float delaySeconds)
        {
            ClearTakeProgressVisualOnly();
            _progressSlot = slot;
            _progressDelay = Mathf.Max(0.01f, delaySeconds);
            _progressElapsed = 0f;
            _grid.GetSlot(slot).SetTakeProgress(0.01f);
            _holdHintLabel.text = $"Taking: {_grid.GetSlot(slot).SlotLabel}";
            _holdHintLabel.style.display = DisplayStyle.Flex;
        }

        public void ClearTakeProgress()
        {
            ClearTakeProgressVisualOnly();
            _progressSlot = null;
            _progressDelay = 0f;
            _progressElapsed = 0f;
            UpdateHoldHintIdle();
        }

        /// <summary>Advances the confirmed take spinner. Driven by the owning subsystem's Update.</summary>
        public void Tick(float deltaTime)
        {
            if (_progressSlot == null)
            {
                return;
            }

            _progressElapsed += deltaTime;
            float progress = Mathf.Clamp01(_progressElapsed / _progressDelay);
            _grid.GetSlot(_progressSlot.Value).SetTakeProgress(Mathf.Max(0.01f, progress));
        }

        private void ApplySlots(string title, IReadOnlyList<CharacterExamineSlotContent> slots)
        {
            _window.Title = title;
            foreach (CharacterExamineSlotContent content in slots)
            {
                _grid.SetSlot(content);
            }
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
            inventorySlot.RegisterCallback<PointerDownEvent>(_ => BeginHoldGesture(slot, inventorySlot));
            inventorySlot.RegisterCallback<PointerUpEvent>(_ => CancelHoldGesture());
            inventorySlot.RegisterCallback<PointerLeaveEvent>(_ => CancelHoldGesture());
        }

        private void BeginHoldGesture(CharacterExamineSlot slot, InventorySlot inventorySlot)
        {
            if (!TakeAllowed || inventorySlot.ItemIcon == null)
            {
                return;
            }

            _pendingHoldSlot = slot;
            inventorySlot.AddToClassList("inventory-slot--drop-target");
            _holdHintLabel.text = $"Hold to Take: {inventorySlot.SlotLabel}";
            _holdHintLabel.style.display = DisplayStyle.Flex;
            TakeHoldStarted?.Invoke(slot);
        }

        private void CancelHoldGesture()
        {
            if (_pendingHoldSlot == null && _progressSlot == null)
            {
                return;
            }

            CancelLocalHoldGesture();
            TakeHoldCancelled?.Invoke();
        }

        private void CancelLocalHoldGesture()
        {
            if (_pendingHoldSlot != null)
            {
                _grid.GetSlot(_pendingHoldSlot.Value).RemoveFromClassList("inventory-slot--drop-target");
            }

            _pendingHoldSlot = null;
        }

        private void ClearTakeProgressVisualOnly()
        {
            if (_progressSlot != null)
            {
                InventorySlot slot = _grid.GetSlot(_progressSlot.Value);
                slot.ClearTakeProgress();
                slot.RemoveFromClassList("inventory-slot--drop-target");
            }
        }

        private void UpdateHoldHintIdle()
        {
            if (_holdHintLabel == null)
            {
                return;
            }

            if (TakeAllowed && IsOpen && _progressSlot == null)
            {
                _holdHintLabel.text = "Hold a slot to take";
                _holdHintLabel.style.display = DisplayStyle.Flex;
                return;
            }

            if (_progressSlot == null)
            {
                _holdHintLabel.style.display = DisplayStyle.None;
            }
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

using System.Collections.Generic;
using UnityEngine.UIElements;

namespace SS3D.Systems.Comms.UI
{
    /// <summary>
    /// UI Toolkit view for local speech bubbles and the crowd-cap overflow chip. Mirrors
    /// RadialInteractionMenuView's Attach/Detach shape and pooling approach - a plain C# class
    /// (not a MonoBehaviour), owned and driven by LocalSpeechBubbleController.
    /// </summary>
    public sealed class LocalSpeechBubbleView
    {
        private readonly StyleSheet _bubbleStyleSheet;
        private readonly List<VisualElement> _bubblePool = new();

        private VisualElement _overlayRoot;
        private VisualElement _overflowChip;
        private Label _overflowLabel;

        public LocalSpeechBubbleView(StyleSheet bubbleStyleSheet)
        {
            _bubbleStyleSheet = bubbleStyleSheet;
        }

        public void Attach(VisualElement overlayRoot)
        {
            _overlayRoot = overlayRoot;
            _overlayRoot.style.flexGrow = 1;
            _overlayRoot.pickingMode = PickingMode.Ignore;

            if (_bubbleStyleSheet != null)
            {
                _overlayRoot.styleSheets.Add(_bubbleStyleSheet);
            }

            _overflowChip = new VisualElement();
            _overflowChip.AddToClassList("comms-overflow-chip");
            _overflowChip.pickingMode = PickingMode.Ignore;
            _overflowChip.style.display = DisplayStyle.None;

            _overflowLabel = new Label();
            _overflowLabel.AddToClassList("comms-overflow-chip__label");
            _overflowChip.Add(_overflowLabel);

            _overlayRoot.Add(_overflowChip);
        }

        public void Detach()
        {
            foreach (VisualElement bubble in _bubblePool)
            {
                bubble.RemoveFromHierarchy();
            }

            _bubblePool.Clear();

            _overflowChip?.RemoveFromHierarchy();
            _overflowChip = null;
            _overflowLabel = null;
            _overlayRoot = null;
        }

        /// <summary>
        /// Positions and fills a pooled bubble at the given screen position. <paramref name="left"/>
        /// and <paramref name="bottom"/> are raw Unity screen-space coordinates (origin bottom-left),
        /// matching RadialInteractionMenuView's convention of using style.left/style.bottom directly
        /// so no manual Y-flip against the UI Toolkit panel is needed.
        /// </summary>
        public void ShowBubble(int poolIndex, float left, float bottom, string text, AudibilityTier tier, float opacity)
        {
            VisualElement bubble = GetOrCreateBubble(poolIndex);
            bubble.style.display = DisplayStyle.Flex;
            bubble.style.left = left;
            bubble.style.bottom = bottom;
            bubble.style.opacity = opacity;

            bubble.EnableInClassList("comms-bubble--clear", tier == AudibilityTier.Clear);
            bubble.EnableInClassList("comms-bubble--muffled", tier == AudibilityTier.Muffled);

            Label label = (Label)bubble.userData;
            label.text = text;
        }

        public void HideBubble(int poolIndex)
        {
            if (poolIndex < 0 || poolIndex >= _bubblePool.Count)
            {
                return;
            }

            _bubblePool[poolIndex].style.display = DisplayStyle.None;
        }

        /// <summary>
        /// Hides every pooled bubble at or beyond <paramref name="fromIndex"/> - the tail of the
        /// pool left over from a previous frame that had more visible bubbles than this one.
        /// </summary>
        public void HideBubblesFrom(int fromIndex)
        {
            for (int i = fromIndex; i < _bubblePool.Count; i++)
            {
                _bubblePool[i].style.display = DisplayStyle.None;
            }
        }

        public void ShowOverflowChip(int hiddenCount)
        {
            if (_overflowChip == null)
            {
                return;
            }

            _overflowChip.style.display = DisplayStyle.Flex;
            _overflowLabel.text = $"+{hiddenCount} more talking nearby";
        }

        public void HideOverflowChip()
        {
            if (_overflowChip != null)
            {
                _overflowChip.style.display = DisplayStyle.None;
            }
        }

        private VisualElement GetOrCreateBubble(int index)
        {
            while (_bubblePool.Count <= index)
            {
                VisualElement bubble = new();
                bubble.AddToClassList("comms-bubble");
                bubble.pickingMode = PickingMode.Ignore;
                bubble.style.position = Position.Absolute;
                bubble.style.display = DisplayStyle.None;

                Label label = new();
                label.AddToClassList("comms-bubble__label");
                label.pickingMode = PickingMode.Ignore;
                bubble.Add(label);
                bubble.userData = label;

                _overlayRoot.Add(bubble);
                _bubblePool.Add(bubble);
            }

            return _bubblePool[index];
        }
    }
}

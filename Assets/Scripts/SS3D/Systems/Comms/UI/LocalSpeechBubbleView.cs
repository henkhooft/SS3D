using System.Collections.Generic;
using UnityEngine.UIElements;

namespace SS3D.Systems.Comms.UI
{
    /// <summary>
    /// UI Toolkit view for floating local-speech subtitle chips and the crowd-cap overflow chip.
    /// Visual language follows the Claude Design "weighted chips" mock (option 1a).
    /// </summary>
    public sealed class LocalSpeechBubbleView
    {
        private static readonly string[] ModeClasses =
        {
            "comms-subtitle--speak",
            "comms-subtitle--whisper",
            "comms-subtitle--shout",
            "comms-subtitle--emote",
            "comms-subtitle--radio",
            "comms-subtitle--announce",
        };

        private readonly StyleSheet _bubbleStyleSheet;
        private readonly List<VisualElement> _subtitlePool = new();

        private VisualElement _overlayRoot;
        private VisualElement _overflowChip;
        private Label _overflowLabel;

        private sealed class SubtitleLabels
        {
            public Label Name;
            public Label Line;
        }

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
            _overflowLabel.AddToClassList("font-body");
            _overflowLabel.AddToClassList("comms-overflow-chip__label");
            _overflowChip.Add(_overflowLabel);

            _overlayRoot.Add(_overflowChip);
        }

        public void Detach()
        {
            foreach (VisualElement subtitle in _subtitlePool)
            {
                subtitle.RemoveFromHierarchy();
            }

            _subtitlePool.Clear();

            _overflowChip?.RemoveFromHierarchy();
            _overflowChip = null;
            _overflowLabel = null;
            _overlayRoot = null;
        }

        /// <summary>
        /// Positions and fills a pooled subtitle. Screen coords are bottom-left origin.
        /// <paramref name="stackAge"/> is 0 for the newest line, 1+ for older stacked lines.
        /// </summary>
        public void ShowBubble(
            int poolIndex,
            float left,
            float bottom,
            string speakerName,
            string displayText,
            SpeechMode mode,
            AudibilityTier tier,
            float opacity,
            int stackAge)
        {
            VisualElement subtitle = GetOrCreateSubtitle(poolIndex);
            subtitle.style.display = DisplayStyle.Flex;
            subtitle.style.left = left;
            subtitle.style.bottom = bottom;
            subtitle.style.opacity = opacity;

            subtitle.EnableInClassList("comms-subtitle--clear", tier == AudibilityTier.Clear);
            subtitle.EnableInClassList("comms-subtitle--muffled", tier == AudibilityTier.Muffled);
            subtitle.EnableInClassList("comms-subtitle--aged", stackAge > 0);
            subtitle.EnableInClassList("comms-subtitle--aged-far", stackAge > 1);
            ApplyModeClass(subtitle, mode);

            SubtitleLabels labels = (SubtitleLabels)subtitle.userData;
            bool showName = !string.IsNullOrEmpty(speakerName);
            labels.Name.style.display = showName ? DisplayStyle.Flex : DisplayStyle.None;
            labels.Name.text = showName ? speakerName.ToUpperInvariant() : string.Empty;
            labels.Line.text = displayText;
        }

        public void HideBubblesFrom(int fromIndex)
        {
            for (int i = fromIndex; i < _subtitlePool.Count; i++)
            {
                _subtitlePool[i].style.display = DisplayStyle.None;
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

        private static void ApplyModeClass(VisualElement subtitle, SpeechMode mode)
        {
            for (int i = 0; i < ModeClasses.Length; i++)
            {
                subtitle.EnableInClassList(ModeClasses[i], false);
            }

            string modeClass = mode switch
            {
                SpeechMode.Whisper => "comms-subtitle--whisper",
                SpeechMode.Shout => "comms-subtitle--shout",
                SpeechMode.Emote => "comms-subtitle--emote",
                SpeechMode.Radio => "comms-subtitle--radio",
                SpeechMode.Announcement => "comms-subtitle--announce",
                _ => "comms-subtitle--speak",
            };

            subtitle.EnableInClassList(modeClass, true);
        }

        private VisualElement GetOrCreateSubtitle(int index)
        {
            while (_subtitlePool.Count <= index)
            {
                VisualElement subtitle = new();
                subtitle.AddToClassList("comms-subtitle");
                subtitle.AddToClassList("comms-subtitle--speak");
                subtitle.pickingMode = PickingMode.Ignore;
                subtitle.style.position = Position.Absolute;
                subtitle.style.display = DisplayStyle.None;

                Label nameLabel = new();
                nameLabel.AddToClassList("font-titling");
                nameLabel.AddToClassList("comms-subtitle__name");
                nameLabel.pickingMode = PickingMode.Ignore;
                subtitle.Add(nameLabel);

                Label lineLabel = new();
                lineLabel.AddToClassList("font-body");
                lineLabel.AddToClassList("comms-subtitle__line");
                lineLabel.pickingMode = PickingMode.Ignore;
                subtitle.Add(lineLabel);

                subtitle.userData = new SubtitleLabels
                {
                    Name = nameLabel,
                    Line = lineLabel,
                };

                _overlayRoot.Add(subtitle);
                _subtitlePool.Add(subtitle);
            }

            return _subtitlePool[index];
        }
    }
}

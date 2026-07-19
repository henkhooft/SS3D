using System.Collections.Generic;
using UnityEngine;
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
            string nameText = showName ? speakerName.ToUpperInvariant() : string.Empty;
            labels.Name.style.display = showName ? DisplayStyle.Flex : DisplayStyle.None;
            labels.Name.text = nameText;
            labels.Line.text = displayText;
            labels.Line.style.unityFontStyleAndWeight = mode switch
            {
                SpeechMode.Shout => FontStyle.Bold,
                SpeechMode.Whisper or SpeechMode.Emote => FontStyle.Italic,
                _ => FontStyle.Normal,
            };

            FitChipWidth(subtitle, labels, nameText, displayText, mode, stackAge, showName);
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

        /// <summary>
        /// Hug content for short lines; only pin the chip to max-width when text must wrap.
        /// Never shrink-fit via binary search — that under-measured (esp. with letter-spacing)
        /// and forced breaks like "NEED / OXYGEN".
        /// </summary>
        private static void FitChipWidth(
            VisualElement subtitle,
            SubtitleLabels labels,
            string nameText,
            string lineText,
            SpeechMode mode,
            int stackAge,
            bool showName)
        {
            float maxChip = mode switch
            {
                SpeechMode.Whisper => 300f,
                SpeechMode.Shout => 480f,
                SpeechMode.Announcement => 520f,
                _ => 420f,
            };

            float padX = stackAge > 0
                ? 24f
                : mode switch
                {
                    SpeechMode.Whisper => 20f,
                    SpeechMode.Shout => 32f,
                    SpeechMode.Emote => 24f,
                    _ => 28f,
                };

            float maxContent = Mathf.Max(8f, maxChip - padX);
            float letterSpacing = mode switch
            {
                SpeechMode.Whisper => 1f,
                SpeechMode.Shout => 1f,
                _ => 0f,
            };

            // Clear prior constraints so Undefined measure is truly unconstrained.
            labels.Line.style.maxWidth = StyleKeyword.None;
            labels.Name.style.maxWidth = StyleKeyword.None;

            Vector2 naturalLine = labels.Line.MeasureTextSize(
                lineText, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined);

            labels.Line.style.maxWidth = maxContent;
            labels.Name.style.maxWidth = maxContent;
            subtitle.style.maxWidth = maxChip;

            // Failed metrics → let Yoga size; never pin a tiny width.
            if (!IsPlausibleTextWidth(lineText, naturalLine.x))
            {
                labels.Line.style.whiteSpace = WhiteSpace.Normal;
                subtitle.style.width = StyleKeyword.Auto;
                return;
            }

            float contentWidth = naturalLine.x + LetterSpacingExtra(lineText, letterSpacing);
            if (showName)
            {
                Vector2 nameSize = labels.Name.MeasureTextSize(
                    nameText, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined);
                if (IsPlausibleTextWidth(nameText, nameSize.x))
                {
                    // Names always use letter-spacing: 1px in USS.
                    contentWidth = Mathf.Max(
                        contentWidth,
                        nameSize.x + LetterSpacingExtra(nameText, 1f));
                }
            }

            // Slack for measure vs render rounding — without this the last word wraps.
            const float MeasureSlack = 10f;

            if (contentWidth + MeasureSlack <= maxContent)
            {
                // Hard-stop premature wraps from Yoga / measure slack.
                labels.Line.style.whiteSpace = WhiteSpace.NoWrap;
                subtitle.style.width = StyleKeyword.Auto;
                return;
            }

            // Truly long: allow wrap at maxContent, chip fills max.
            labels.Line.style.whiteSpace = WhiteSpace.Normal;
            subtitle.style.width = maxChip;
        }

        private static bool IsPlausibleTextWidth(string text, float measuredWidth)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            float minExpected = Mathf.Max(12f, text.Length * 3.5f);
            return measuredWidth >= minExpected;
        }

        private static float LetterSpacingExtra(string text, float letterSpacingPx)
        {
            if (letterSpacingPx <= 0f || string.IsNullOrEmpty(text) || text.Length < 2)
            {
                return 0f;
            }

            return letterSpacingPx * (text.Length - 1);
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
                subtitle.style.flexGrow = 0;
                subtitle.style.flexShrink = 0;

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

using DG.Tweening;
using SS3D.UI.MainHud;
using UnityEngine;
using UnityEngine.UIElements;

namespace SS3D.UI.MainHud.Components
{
    /// <summary>
    /// The twelve hazards the main HUD design doc (§9) and the "Alert Icon Stack" mockup wire up: fire,
    /// ambient heat, ambient cold, low/high pressure, radiation, hunger, thirst, pulling, restrained, low
    /// oxygen and dying. Backend trackers only exist for none of these yet - hunger/thirst/restrained/
    /// pressure/radiation/pulling/low-oxygen/dying are all debug/console-only until their systems exist
    /// (see <see cref="SS3D.UI.MainHud.MainHudSubSystem"/>'s debug override).
    /// </summary>
    public enum AlertHazard
    {
        Fire,
        Hot,
        Cold,
        LowPressure,
        HighPressure,
        Radiation,
        Hunger,
        Thirst,
        Pulling,
        Restrained,
        LowOxygen,
        Dying,
    }

    /// <summary>
    /// How urgently a hazard reads: hidden, a plain amber warning, or a pulsing red critical.
    /// <see cref="AlertHazard.Dying"/> never uses <see cref="Warning"/> - the mockup treats it as a state
    /// with no lesser tier, it either isn't happening or it's critical.
    /// </summary>
    public enum AlertSeverity
    {
        None,
        Warning,
        Critical,
    }

    /// <summary>
    /// Which hazards are currently active on the local player, and at what severity. Every field defaults to
    /// <see cref="AlertSeverity.None"/> (a healthy, unencumbered character shows an empty stack) - none of
    /// these hazards have real trackers in <c>SS3D.Systems</c> yet, so <see cref="MainHudSubSystem"/> only
    /// ever sets these via its debug override. Wire real trackers in here once they exist instead of adding a
    /// parallel state model.
    /// </summary>
    public struct AlertStackState
    {
        public AlertSeverity Fire;
        public AlertSeverity Hot;
        public AlertSeverity Cold;
        public AlertSeverity LowPressure;
        public AlertSeverity HighPressure;
        public AlertSeverity Radiation;
        public AlertSeverity Hunger;
        public AlertSeverity Thirst;
        public AlertSeverity Pulling;
        public AlertSeverity Restrained;
        public AlertSeverity LowOxygen;
        public AlertSeverity Dying;

        public AlertSeverity this[AlertHazard hazard] => hazard switch
        {
            AlertHazard.Fire => Fire,
            AlertHazard.Hot => Hot,
            AlertHazard.Cold => Cold,
            AlertHazard.LowPressure => LowPressure,
            AlertHazard.HighPressure => HighPressure,
            AlertHazard.Radiation => Radiation,
            AlertHazard.Hunger => Hunger,
            AlertHazard.Thirst => Thirst,
            AlertHazard.Pulling => Pulling,
            AlertHazard.Restrained => Restrained,
            AlertHazard.LowOxygen => LowOxygen,
            AlertHazard.Dying => Dying,
            _ => AlertSeverity.None,
        };
    }

    /// <summary>
    /// Top-right icon-only hazard stack. Only active hazards render; hovering any icon reveals its label.
    /// Each chip re-styles between the plain "warning" border and the pulsing "critical" glow based on its
    /// live <see cref="AlertSeverity"/>, matching the Main HUD mockup.
    /// </summary>
    [UxmlElement]
    public partial class AlertIconStack : VisualElement
    {
        private static readonly (AlertHazard Hazard, string Label)[] Chips =
        {
            (AlertHazard.Fire, "Fire"),
            (AlertHazard.Hot, "Hot"),
            (AlertHazard.Cold, "Cold"),
            (AlertHazard.LowPressure, "Low Pressure"),
            (AlertHazard.HighPressure, "High Pressure"),
            (AlertHazard.Radiation, "Radiation"),
            (AlertHazard.Hunger, "Hunger"),
            (AlertHazard.Thirst, "Thirst"),
            (AlertHazard.Pulling, "Pulling"),
            (AlertHazard.Restrained, "Restrained"),
            (AlertHazard.LowOxygen, "Low Oxygen"),
            (AlertHazard.Dying, "Dying / Critical"),
        };

        private readonly AlertChip[] _chips;

        // Parameterless ctor required by [UxmlElement]; real construction happens via the icon-set overload,
        // called from MainHudView once the catalog's AlertIconSet is available.
        public AlertIconStack()
            : this(default)
        {
        }

        public AlertIconStack(AlertIconSet icons)
        {
            AddToClassList("alert-icon-stack");

            _chips = new AlertChip[Chips.Length];
            for (int i = 0; i < Chips.Length; i++)
            {
                (AlertHazard hazard, string label) = Chips[i];
                AlertChip chip = new(hazard, label, icons[hazard]);
                _chips[i] = chip;
                Add(chip);
            }
        }

        public void SetState(AlertStackState state)
        {
            foreach (AlertChip chip in _chips)
            {
                chip.SetSeverity(state[chip.Hazard]);
            }
        }

        private sealed class AlertChip : VisualElement
        {
            private const float GlowPulseDuration = 0.55f;

            public AlertHazard Hazard { get; }

            private readonly VisualElement _box;
            private readonly VisualElement _glow;
            private Tween _glowTween;
            private AlertSeverity _severity;

            public AlertChip(AlertHazard hazard, string label, Sprite icon)
            {
                Hazard = hazard;
                AddToClassList("alert-chip");
                style.display = DisplayStyle.None;

                _box = new VisualElement();
                _box.AddToClassList("alert-chip__box");

                _glow = new VisualElement();
                _glow.AddToClassList("alert-chip__glow");
                _glow.pickingMode = PickingMode.Ignore;
                _glow.style.opacity = 0f;

                VisualElement glyph = new();
                glyph.AddToClassList("alert-chip__glyph");
                glyph.pickingMode = PickingMode.Ignore;
                if (icon != null)
                {
                    glyph.style.backgroundImage = new StyleBackground(icon);
                }

                _box.Add(_glow);
                _box.Add(glyph);

                Label chipLabel = new(label);
                chipLabel.AddToClassList("alert-chip__label");
                chipLabel.AddToClassList("font-arcade");
                chipLabel.pickingMode = PickingMode.Ignore;

                Add(_box);
                Add(chipLabel);

                RegisterCallback<PointerEnterEvent>(_ => AddToClassList("alert-chip--hovered"));
                RegisterCallback<PointerLeaveEvent>(_ => RemoveFromClassList("alert-chip--hovered"));
            }

            public void SetSeverity(AlertSeverity severity)
            {
                if (_severity == severity)
                {
                    return;
                }

                _severity = severity;
                style.display = severity == AlertSeverity.None ? DisplayStyle.None : DisplayStyle.Flex;
                EnableInClassList("alert-chip--critical", severity == AlertSeverity.Critical);

                if (severity == AlertSeverity.Critical)
                {
                    StartGlow();
                }
                else
                {
                    StopGlow();
                }
            }

            // Pulses a glow overlay's opacity rather than the whole chip's, so the icon itself stays fully
            // legible while the critical border breathes - UI Toolkit has no CSS keyframe/box-shadow
            // equivalent to animate directly, so DOTween drives it the same way MainHudView drives show/hide.
            private void StartGlow()
            {
                StopGlow();
                _glowTween = DOTween.To(
                        () => _glow.style.opacity.value,
                        value => _glow.style.opacity = value,
                        1f,
                        GlowPulseDuration)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine);
            }

            private void StopGlow()
            {
                _glowTween?.Kill();
                _glowTween = null;
                _glow.style.opacity = 0f;
            }
        }
    }
}

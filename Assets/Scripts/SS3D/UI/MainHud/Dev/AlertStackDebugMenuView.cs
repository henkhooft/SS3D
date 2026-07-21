using System;
using SS3D.Core;
using SS3D.Core.Behaviours;
using SS3D.Systems.Inputs;
using SS3D.UI.MainHud.Components;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace SS3D.UI.MainHud.Dev
{
    /// <summary>
    /// Dev-only panel to force every Alert Icon Stack hazard through None/Warning/Critical, without needing
    /// the hunger/thirst/pressure/radiation/pulling/restrained/low-oxygen/dying trackers that don't exist yet
    /// (see <see cref="MainHudSubSystem.SetDebugAlertOverride"/>). Toggle with F3 via
    /// <see cref="InputSubSystem.ToggleAlertStackDebug"/> (F2 is the condemned screen-effects uGUI menu).
    /// <para>
    /// Built entirely at runtime via UI Toolkit with its own throwaway <see cref="PanelSettings"/> - no
    /// prefab/scene/stylesheet dependency, and deliberately not a copy of
    /// <c>ScreenEffectsDebugMenuView</c>'s uGUI shape: that panel is explicitly condemned (see
    /// Documents/architecture/systems/screen-effects.md), so a new debug tool shouldn't extend it.
    /// The document registers with <see cref="InputInterface"/> so pointer-over-panel blocks world clicks.
    /// </para>
    /// <para>
    /// Namespace is <c>Dev</c> (not <c>Debug</c>) so it cannot shadow <see cref="UnityEngine.Debug"/> inside
    /// <c>SS3D.UI.MainHud</c>.
    /// </para>
    /// </summary>
    public sealed class AlertStackDebugMenuView : View
    {
        private static readonly AlertHazard[] Hazards = (AlertHazard[])Enum.GetValues(typeof(AlertHazard));

        private static bool s_bootstrapped;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (s_bootstrapped)
            {
                return;
            }

            s_bootstrapped = true;

            GameObject host = new(nameof(AlertStackDebugMenuView));
            DontDestroyOnLoad(host);
            host.AddComponent<AlertStackDebugMenuView>();
        }

        private UIDocument _document;
        private VisualElement _panel;
        private AlertStackState _state;
        private bool _visible;
        private InputSubSystem _inputSystem;
        private bool _toggleBound;

        protected override void OnStart()
        {
            TryBindToggle();
        }

        protected override void OnEnabled()
        {
            base.OnEnabled();
            TryBindToggle();
        }

        protected override void OnDisabled()
        {
            UnbindToggle();
            base.OnDisabled();
        }

        protected override void OnDestroyed()
        {
            UnbindToggle();

            if (_document != null)
            {
                InputInterface.UnregisterDocument(_document);
            }

            base.OnDestroyed();
        }

        private void TryBindToggle()
        {
            if (_toggleBound)
            {
                return;
            }

            if (!SubSystems.TryGet(out _inputSystem) || _inputSystem.ToggleAlertStackDebug == null)
            {
                return;
            }

            _inputSystem.ToggleAlertStackDebug.performed += HandleToggle;
            _toggleBound = true;
        }

        private void UnbindToggle()
        {
            if (!_toggleBound || _inputSystem == null)
            {
                return;
            }

            _inputSystem.ToggleAlertStackDebug.performed -= HandleToggle;
            _toggleBound = false;
            _inputSystem = null;
        }

        private void HandleToggle(InputAction.CallbackContext context)
        {
            if (_document == null)
            {
                BuildUi();
                _visible = false;
                _panel.style.display = DisplayStyle.None;
            }

            _visible = !_visible;
            _panel.style.display = _visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void BuildUi()
        {
            _state = SubSystems.Get<MainHudSubSystem>()?.DebugAlertOverride ?? default;

            PanelSettings settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;

            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = settings;
            _document.sortingOrder = 2000f;

            _panel = new VisualElement();
            // Qualify UITK Position: Actor exposes a Vector3 Position property that would otherwise win.
            _panel.style.position = UnityEngine.UIElements.Position.Absolute;
            _panel.style.top = 16f;
            _panel.style.right = 16f;
            _panel.style.width = 260f;
            _panel.style.paddingLeft = 12f;
            _panel.style.paddingRight = 12f;
            _panel.style.paddingTop = 12f;
            _panel.style.paddingBottom = 12f;
            _panel.style.backgroundColor = new Color(0.05f, 0.05f, 0.06f, 0.9f);
            _panel.style.borderTopLeftRadius = 6f;
            _panel.style.borderTopRightRadius = 6f;
            _panel.style.borderBottomLeftRadius = 6f;
            _panel.style.borderBottomRightRadius = 6f;

            _panel.Add(BuildLabel("Alert Stack (F3)", 16, FontStyle.Bold));

            foreach (AlertHazard hazard in Hazards)
            {
                _panel.Add(BuildRow(hazard));
            }

            _panel.Add(BuildActionRow("Clear All", () =>
            {
                _state = default;
                SubSystems.Get<MainHudSubSystem>()?.ClearDebugAlertOverride();
                RebuildRowLabels();
            }));

            _document.rootVisualElement.Add(_panel);
            InputInterface.RegisterDocument(_document);
        }

        private VisualElement BuildRow(AlertHazard hazard)
        {
            VisualElement row = new() { name = $"row-{hazard}" };
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 4f;
            row.style.paddingLeft = 8f;
            row.style.paddingRight = 8f;
            row.style.paddingTop = 4f;
            row.style.paddingBottom = 4f;
            row.style.backgroundColor = new Color(0.15f, 0.15f, 0.17f, 1f);
            row.style.borderTopLeftRadius = 4f;
            row.style.borderTopRightRadius = 4f;
            row.style.borderBottomLeftRadius = 4f;
            row.style.borderBottomRightRadius = 4f;

            row.Add(BuildLabel(hazard.ToString(), 12, FontStyle.Normal));

            Label severityLabel = BuildLabel(SeverityText(_state[hazard]), 12, FontStyle.Bold);
            severityLabel.name = $"severity-{hazard}";
            severityLabel.style.color = SeverityColor(_state[hazard]);
            row.Add(severityLabel);

            row.RegisterCallback<ClickEvent>(_ =>
            {
                CycleSeverity(hazard);
                severityLabel.text = SeverityText(_state[hazard]);
                severityLabel.style.color = SeverityColor(_state[hazard]);
                SubSystems.Get<MainHudSubSystem>()?.SetDebugAlertOverride(_state);
            });

            return row;
        }

        private VisualElement BuildActionRow(string text, Action onClick)
        {
            VisualElement row = new();
            row.style.alignItems = Align.Center;
            row.style.marginTop = 8f;
            row.style.paddingTop = 6f;
            row.style.paddingBottom = 6f;
            row.style.backgroundColor = new Color(0.35f, 0.15f, 0.15f, 1f);
            row.style.borderTopLeftRadius = 4f;
            row.style.borderTopRightRadius = 4f;
            row.style.borderBottomLeftRadius = 4f;
            row.style.borderBottomRightRadius = 4f;

            row.Add(BuildLabel(text, 13, FontStyle.Bold));
            row.RegisterCallback<ClickEvent>(_ => onClick());
            return row;
        }

        private void RebuildRowLabels()
        {
            foreach (AlertHazard hazard in Hazards)
            {
                Label severityLabel = _panel.Q<Label>($"severity-{hazard}");
                if (severityLabel == null)
                {
                    continue;
                }

                severityLabel.text = SeverityText(_state[hazard]);
                severityLabel.style.color = SeverityColor(_state[hazard]);
            }
        }

        private void CycleSeverity(AlertHazard hazard)
        {
            AlertSeverity next = _state[hazard] switch
            {
                AlertSeverity.None => AlertSeverity.Warning,
                AlertSeverity.Warning => AlertSeverity.Critical,
                _ => AlertSeverity.None,
            };

            // Dying has no warning tier in the design (main-hud.md §9) - skip straight to Critical.
            if (hazard == AlertHazard.Dying && next == AlertSeverity.Warning)
            {
                next = AlertSeverity.Critical;
            }

            SetSeverity(hazard, next);
        }

        private void SetSeverity(AlertHazard hazard, AlertSeverity severity)
        {
            switch (hazard)
            {
                case AlertHazard.Fire: _state.Fire = severity; break;
                case AlertHazard.Hot: _state.Hot = severity; break;
                case AlertHazard.Cold: _state.Cold = severity; break;
                case AlertHazard.LowPressure: _state.LowPressure = severity; break;
                case AlertHazard.HighPressure: _state.HighPressure = severity; break;
                case AlertHazard.Radiation: _state.Radiation = severity; break;
                case AlertHazard.Hunger: _state.Hunger = severity; break;
                case AlertHazard.Thirst: _state.Thirst = severity; break;
                case AlertHazard.Pulling: _state.Pulling = severity; break;
                case AlertHazard.Restrained: _state.Restrained = severity; break;
                case AlertHazard.LowOxygen: _state.LowOxygen = severity; break;
                case AlertHazard.Dying: _state.Dying = severity; break;
            }
        }

        private static string SeverityText(AlertSeverity severity) => severity switch
        {
            AlertSeverity.Warning => "WARNING",
            AlertSeverity.Critical => "CRITICAL",
            _ => "off",
        };

        private static Color SeverityColor(AlertSeverity severity) => severity switch
        {
            AlertSeverity.Warning => new Color(0.85f, 0.65f, 0.25f),
            AlertSeverity.Critical => new Color(0.85f, 0.3f, 0.3f),
            _ => new Color(0.6f, 0.6f, 0.6f),
        };

        private static Label BuildLabel(string text, int fontSize, FontStyle fontStyle)
        {
            Label label = new(text);
            label.style.color = Color.white;
            label.style.fontSize = fontSize;
            label.style.unityFontStyleAndWeight = fontStyle;
            return label;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using SS3D.Systems.Rounds;
using UnityEngine.UIElements;

namespace SS3D.UI.Lobby
{
    public sealed partial class LobbyShellView
    {
        private StationLoadState _stationLoadState;
        private string _stationName = "";
        private Label _stationLoadLabel;
        private Label _headerMapLabel;

        public void SetStationLoadState(StationLoadState state, string stationName)
        {
            _stationLoadState = state;
            _stationName = stationName ?? "";
            if (_headerMapLabel != null) _headerMapLabel.text = string.IsNullOrEmpty(_stationName) ? "No map loaded" : _stationName;
            Label infoMap = _root?.Q<Label>("server-info-map");
            if (infoMap != null) infoMap.text = string.IsNullOrEmpty(_stationName) ? "No map loaded" : _stationName;
            UpdateStationIndicator(_stationLoadLabel);
            UpdateStationIndicator(_tabContent?.Q<Label>("server-map-load-status"));
        }

        private void UpdateStationIndicator(Label label)
        {
            if (label == null) return;
            label.text = _stationLoadState switch
            {
                StationLoadState.Loading => $"Loading map: {_stationName}…",
                StationLoadState.Initializing => $"Initializing station: {_stationName}…",
                StationLoadState.Ready => $"Map ready: {_stationName}",
                StationLoadState.Failed => "Map load failed — select a map and retry",
                _ => "No map loaded",
            };
            label.EnableInClassList("lobby-shell__map-load-status--loading",
                _stationLoadState is StationLoadState.Loading or StationLoadState.Initializing);
            label.EnableInClassList("lobby-shell__map-load-status--ready", _stationLoadState == StationLoadState.Ready);
            label.EnableInClassList("lobby-shell__map-load-status--failed", _stationLoadState == StationLoadState.Failed);
        }

        private ServerSettings _serverSettings = ServerSettings.Default;
        private string[] _serverMaps = Array.Empty<string>();
        private bool _hasServerSettings;
        private string _serverSettingsStatus = "Loading server settings…";
        public event Action<ServerSetting, ServerSettings> ServerSettingsChanged;
        public event Action<RoundAdminAction> RoundAdminRequested;

        public void SetServerSettings(ServerSettings settings, string[] maps, string status)
        {
            _serverSettings = settings;
            _serverMaps = maps ?? Array.Empty<string>();
            _hasServerSettings = true;
            if (!string.IsNullOrEmpty(status)) _serverSettingsStatus = status;
            else if (_serverSettingsStatus == "Loading server settings…") _serverSettingsStatus = "";
            RefreshServerSettings();
        }

        private void BuildServerSettingsTab(VisualElement parent)
        {
            ScrollView scroll = new() { name = "server-settings-scroll" };
            scroll.AddToClassList("lobby-shell__server-settings-scroll");
            parent.Add(scroll);
            VisualElement form = new() { name = "server-settings-form" };
            form.AddToClassList("lobby-shell__server-settings");
            scroll.Add(form);

            VisualElement card = new();
            card.AddToClassList("lobby-shell__round-card");
            Label heading = new("●   ROUND STATE");
            heading.AddToClassList("lobby-shell__round-card-heading");
            heading.AddToClassList("font-titling");
            card.Add(heading);
            VisualElement body = new();
            body.AddToClassList("lobby-shell__round-card-body");
            VisualElement state = new();
            Label title = new() { name = "server-round-state" };
            title.AddToClassList("lobby-shell__round-state");
            title.AddToClassList("font-titling");
            state.Add(title);
            Label detail = new() { name = "server-round-detail" };
            detail.AddToClassList("lobby-shell__round-detail");
            detail.AddToClassList("font-terminal");
            state.Add(detail);
            body.Add(state);
            VisualElement ready = new();
            ready.Add(new Label("READY"));
            Label ratio = new() { name = "server-ready-ratio" };
            ratio.AddToClassList("lobby-shell__ready-ratio");
            ratio.AddToClassList("font-terminal");
            ready.Add(ratio);
            body.Add(ready);
            card.Add(body);
            form.Add(card);

            form.Add(SectionTitle("Round Controls"));
            VisualElement controls = new();
            controls.AddToClassList("lobby-shell__admin-controls");
            AddRoundAction(controls, "Start Round", "server-start", RoundAdminAction.Start);
            AddRoundAction(controls, "Force Start", "server-force", RoundAdminAction.ForceStart, true);
            AddRoundAction(controls, "Extend Timer", "server-extend", RoundAdminAction.ExtendTimer, true);
            AddRoundAction(controls, "Cancel Countdown", "server-cancel", RoundAdminAction.CancelCountdown, true);
            // Stop is an in-round extension to the pre-round reference.
            AddRoundAction(controls, "Stop Round", "server-stop", RoundAdminAction.Stop, true);
            form.Add(controls);

            form.Add(SectionTitle("Map & Mode"));
            DropdownField map = new("Map") { name = "server-map" };
            map.AddToClassList("lobby-shell__server-select");
            map.AddToClassList("lobby-shell__server-map");
            Label swatch = new("M") { name = "server-map-initial" };
            swatch.AddToClassList("lobby-shell__server-map-swatch");
            swatch.pickingMode = PickingMode.Ignore;
            map.Add(swatch);
            map.RegisterValueChangedCallback(e =>
            {
                _serverSettings.Map = e.newValue;
                SaveServerSettings(ServerSetting.Map);
            });
            form.Add(map);
            Label mapStatus = new() { name = "server-map-load-status" };
            mapStatus.AddToClassList("lobby-shell__map-load-status");
            form.Add(mapStatus);
            UpdateStationIndicator(mapStatus);
            DropdownField mode = new("Game Mode", new List<string> { "Secret", "Extended", "Nuclear Emergency" }, 0)
                { name = "server-mode" };
            mode.AddToClassList("lobby-shell__server-select");
            mode.RegisterValueChangedCallback(e => { _serverSettings.Mode = e.newValue; SaveServerSettings(ServerSetting.Mode); });
            form.Add(mode);

            VisualElement numbers = new();
            numbers.AddToClassList("lobby-shell__server-numbers");
            AddServerNumber(numbers, "Player Cap", "server-player-cap", ServerSetting.PlayerCap, value => _serverSettings.PlayerCap = value);
            AddServerNumber(numbers, "Round Length (min)", "server-round-length", ServerSetting.RoundLengthMinutes, value => _serverSettings.RoundLengthMinutes = value);
            form.Add(numbers);
            form.Add(SectionTitle("Round Rules"));
            Toggle late = ServerRule("Allow Late Join", "server-late-join", true, "");
            late.RegisterValueChangedCallback(e => { _serverSettings.AllowLateJoin = e.newValue; SaveServerSettings(ServerSetting.AllowLateJoin); });
            form.Add(late);
            form.Add(ServerRule("Random Events", "server-random-events", false, "Random events are not implemented yet."));
            form.Add(ServerRule("Friendly Fire", "server-friendly-fire", false, "Combat has no friendly-fire policy yet."));
            form.Add(ServerRule("Allow Respawn (Cloning)", "server-respawn", false, "Cloning is not implemented yet."));
            Label status = new() { name = "server-settings-status" };
            status.AddToClassList("lobby-shell__server-status");
            form.Add(status);
            RefreshServerSettings();
        }

        private void SaveServerSettings(ServerSetting field)
        {
            if (!_hasServerSettings) return;
            if (!_serverSettings.IsValid)
            {
                _serverSettingsStatus = "Player cap must be 1–256; round length must be 1–1440 minutes.";
                _tabContent.Q<Label>("server-settings-status").text = _serverSettingsStatus;
                return;
            }
            ServerSettingsChanged?.Invoke(field, _serverSettings);
        }

        private void AddRoundAction(VisualElement parent, string text, string name, RoundAdminAction action, bool secondary = false)
        {
            Button button = AdminButton(text, () => RoundAdminRequested?.Invoke(action));
            button.name = name;
            button.AddToClassList(secondary ? "lobby-shell__admin-btn--outline" : "lobby-shell__admin-btn--start");
            parent.Add(button);
        }

        private void AddServerNumber(VisualElement parent, string label, string name, ServerSetting field, Action<int> update)
        {
            VisualElement column = new();
            column.AddToClassList("lobby-shell__server-number");
            column.Add(new Label(label.ToUpperInvariant()));
            IntegerField input = new() { name = name, isDelayed = true };
            input.AddToClassList("font-terminal");
            input.RegisterValueChangedCallback(e => { update(e.newValue); SaveServerSettings(field); });
            input.RegisterCallback<FocusInEvent>(_ => _textEntry.Enter());
            input.RegisterCallback<FocusOutEvent>(_ => _textEntry.Exit());
            column.Add(input);
            parent.Add(column);
        }

        private static Toggle ServerRule(string text, string name, bool supported, string reason)
        {
            Toggle toggle = new() { name = name, text = text, tooltip = reason };
            toggle.AddToClassList("lobby-shell__settings-toggle");
            toggle.SetValueWithoutNotify(name == "server-friendly-fire");
            toggle.SetEnabled(supported);
            return toggle;
        }

        private void RefreshServerSettings()
        {
            VisualElement form = _tabContent?.Q("server-settings-form");
            if (form == null) return;
            bool idle = _roundState is RoundState.Stopped or RoundState.Ended;
            bool warming = _roundState == RoundState.WarmingUp;
            form.Q<Label>("server-round-state").text = warming ? "STARTING IN" : _roundState switch
            {
                RoundState.Preparing => "LOADING STATION", RoundState.Ongoing => "ROUND IN PROGRESS",
                RoundState.Ending => "ENDING ROUND", _ => "WAITING FOR PLAYERS",
            };
            string detailText = "Please wait…";
            if (warming) detailText = $"{_secondsLeft / 60:00}:{_secondsLeft % 60:00}";
            else if (idle) detailText = "Manual start";
            else if (_roundState == RoundState.Ongoing) detailText = $"{_secondsLeft / 60:00}:{_secondsLeft % 60:00} elapsed";
            form.Q<Label>("server-round-detail").text = detailText;
            form.Q<Label>("server-ready-ratio").text = $"{_players.Count(p => p.Ready)}/{_players.Length}";
            Button start = form.Q<Button>("server-start");
            start.style.display = idle ? DisplayStyle.Flex : DisplayStyle.None;
            start.SetEnabled(idle && _hasServerSettings && _serverMaps.Contains(_serverSettings.Map));
            foreach (string name in new string[] { "server-force", "server-extend", "server-cancel" })
            {
                Button action = form.Q<Button>(name);
                action.style.display = idle || warming ? DisplayStyle.Flex : DisplayStyle.None;
                action.SetEnabled(warming);
            }
            Button stop = form.Q<Button>("server-stop");
            stop.style.display = _roundState == RoundState.Ongoing ? DisplayStyle.Flex : DisplayStyle.None;
            stop.SetEnabled(_roundState == RoundState.Ongoing);
            DropdownField map = form.Q<DropdownField>("server-map");
            map.choices = _serverMaps.ToList();
            map.SetValueWithoutNotify(_serverSettings.Map ?? "");
            form.Q<Label>("server-map-initial").text = string.IsNullOrEmpty(_serverSettings.Map)
                ? "M" : _serverSettings.Map.Substring(0, 1).ToUpperInvariant();
            map.SetEnabled(idle && _hasServerSettings);
            DropdownField mode = form.Q<DropdownField>("server-mode");
            mode.SetValueWithoutNotify(_serverSettings.Mode);
            mode.SetEnabled(idle && _hasServerSettings);
            IntegerField cap = form.Q<IntegerField>("server-player-cap");
            IntegerField length = form.Q<IntegerField>("server-round-length");
            // Do not replace text while an admin is typing in a delayed field.
            if (cap.panel?.focusController.focusedElement is not VisualElement focus || !cap.Contains(focus))
                cap.SetValueWithoutNotify(_serverSettings.PlayerCap);
            if (length.panel?.focusController.focusedElement is not VisualElement lengthFocus || !length.Contains(lengthFocus))
                length.SetValueWithoutNotify(_serverSettings.RoundLengthMinutes);
            cap.SetEnabled(idle && _hasServerSettings);
            length.SetEnabled(idle && _hasServerSettings);
            Toggle late = form.Q<Toggle>("server-late-join");
            late.SetValueWithoutNotify(_serverSettings.AllowLateJoin);
            late.SetEnabled(idle && _hasServerSettings);
            form.Q<Label>("server-settings-status").text = _serverSettingsStatus;
        }
    }
}

using System;
using SS3D.Systems.Rounds;
using UnityEngine;
using UnityEngine.UIElements;

namespace SS3D.UI.Lobby
{
    public sealed partial class LobbyShellView
    {
        private VisualElement _mapStopPrompt;
        private VisualElement _mapStopCard;
        private Label _mapStopText;
        private Label _mapStopError;
        private TextField _mapStopName;
        private int _mapStopToken;
        public event Action<int, MapStopChoice, string> MapStopResolved;

        /// <summary>Shows the admin's Stop confirmation while the map editor has unsaved edits (token 0 hides it).</summary>
        public void SetMapStopPrompt(int token, string editingMap, string error)
        {
            if (_mapStopPrompt == null) return;
            bool changed = token != _mapStopToken;
            _mapStopToken = token;
            _mapStopPrompt.style.display = token == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            if (token == 0) return;
            _mapStopCard.SetEnabled(true);
            bool unnamed = string.IsNullOrEmpty(editingMap);
            _mapStopText.text = unnamed
                ? "This new map has unsaved changes. Stopping the round unloads the station. Save and stop saves it under a new name first; Discard loses these edits."
                : $"{editingMap} has unsaved changes. Stopping the round unloads the station. Save and stop overwrites {editingMap} first; Discard loses these edits.";
            _mapStopName.style.display = unnamed ? DisplayStyle.Flex : DisplayStyle.None;
            if (changed) _mapStopName.SetValueWithoutNotify(unnamed ? "" : editingMap);
            _mapStopError.text = error ?? "";
            if (changed) _mapStopPrompt.Q<Button>("map-stop-cancel").Focus();
        }

        private VisualElement BuildMapStopPrompt()
        {
            _mapStopPrompt = new VisualElement { name = "map-stop-prompt" };
            _mapStopPrompt.AddToClassList("lobby-shell__display-prompt");
            _mapStopPrompt.style.display = DisplayStyle.None;
            _mapStopCard = new VisualElement();
            _mapStopCard.AddToClassList("lobby-shell__display-prompt-card");
            Label title = MakeLabel("UNSAVED MAP CHANGES", "font-titling");
            title.AddToClassList("lobby-shell__display-prompt-text");
            _mapStopCard.Add(title);
            _mapStopText = MakeLabel("", "font-body");
            _mapStopText.AddToClassList("lobby-shell__display-prompt-text");
            _mapStopCard.Add(_mapStopText);
            _mapStopName = new TextField("New map name") { name = "map-stop-name", maxLength = 80 };
            _mapStopName.AddToClassList("lobby-shell__server-text");
            _mapStopName.AddToClassList("font-body");
            StyleDarkTextField(_mapStopName);
            HoldTextEntry(_mapStopName);
            _mapStopCard.Add(_mapStopName);
            _mapStopError = MakeLabel("", "font-body");
            _mapStopError.AddToClassList("lobby-shell__display-prompt-text");
            _mapStopCard.Add(_mapStopError);
            foreach ((string name, string label, MapStopChoice choice) in new (string, string, MapStopChoice)[]
            {
                ("map-stop-save", "Save and stop", MapStopChoice.SaveAndStop),
                ("map-stop-discard", "Discard and stop", MapStopChoice.DiscardAndStop),
                ("map-stop-cancel", "Cancel", MapStopChoice.Cancel),
            })
            {
                Button button = AdminButton(label, () => ResolveMapStop(choice));
                button.name = name;
                _mapStopCard.Add(button);
            }
            _mapStopPrompt.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode != KeyCode.Escape) return;
                ResolveMapStop(MapStopChoice.Cancel);
                e.StopPropagation();
            }, TrickleDown.TrickleDown);
            _mapStopPrompt.Add(_mapStopCard);
            return _mapStopPrompt;
        }

        private void ResolveMapStop(MapStopChoice choice)
        {
            if (_mapStopToken == 0 || !_mapStopCard.enabledSelf) return;
            _mapStopName.Blur();
            _mapStopCard.SetEnabled(false);
            MapStopResolved?.Invoke(_mapStopToken, choice, _mapStopName.value);
        }
    }
}

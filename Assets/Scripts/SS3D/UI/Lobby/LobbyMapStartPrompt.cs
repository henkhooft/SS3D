using System;
using SS3D.Systems.Rounds;
using UnityEngine;
using UnityEngine.UIElements;

namespace SS3D.UI.Lobby
{
    public sealed partial class LobbyShellView
    {
        private VisualElement _mapStartPrompt;
        private VisualElement _mapStartCard;
        private Label _mapStartText;
        private Label _mapStartError;
        private TextField _mapStartName;
        private int _mapStartToken;
        public event Action<int, MapStartChoice, string> MapStartResolved;

        public void SetMapStartPrompt(int token, string selectedMap, string editingMap, string error)
        {
            if (_mapStartPrompt == null) return;
            bool changed = token != _mapStartToken;
            _mapStartToken = token;
            _mapStartPrompt.style.display = token == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            if (token == 0) return;
            _mapStartCard.SetEnabled(true);
            bool unnamed = string.IsNullOrEmpty(editingMap);
            _mapStartText.text = unnamed
                ? $"This new map has unsaved changes. Save and start will play the new map. Discard will load {selectedMap} and lose these edits."
                : $"{editingMap} has unsaved changes. Save and start will overwrite its saved template, then load {selectedMap}. Discard will load {selectedMap} without saving these edits.";
            _mapStartName.style.display = unnamed ? DisplayStyle.Flex : DisplayStyle.None;
            if (changed) _mapStartName.SetValueWithoutNotify(unnamed ? "" : editingMap);
            _mapStartError.text = error ?? "";
            if (changed) _mapStartPrompt.Q<Button>("map-start-cancel").Focus();
        }

        private VisualElement BuildMapStartPrompt()
        {
            _mapStartPrompt = new VisualElement { name = "map-start-prompt" };
            _mapStartPrompt.AddToClassList("lobby-shell__display-prompt");
            _mapStartPrompt.style.display = DisplayStyle.None;
            _mapStartCard = new VisualElement();
            _mapStartCard.AddToClassList("lobby-shell__display-prompt-card");
            Label title = MakeLabel("UNSAVED MAP CHANGES", "font-titling");
            title.AddToClassList("lobby-shell__display-prompt-text");
            _mapStartCard.Add(title);
            _mapStartText = MakeLabel("", "font-body");
            _mapStartText.AddToClassList("lobby-shell__display-prompt-text");
            _mapStartCard.Add(_mapStartText);
            _mapStartName = new TextField("New map name") { name = "map-start-name", maxLength = 80 };
            _mapStartName.AddToClassList("lobby-shell__server-text");
            _mapStartName.AddToClassList("font-body");
            StyleDarkTextField(_mapStartName);
            HoldTextEntry(_mapStartName);
            _mapStartCard.Add(_mapStartName);
            _mapStartError = MakeLabel("", "font-body");
            _mapStartError.AddToClassList("lobby-shell__display-prompt-text");
            _mapStartCard.Add(_mapStartError);
            foreach ((string name, string label, MapStartChoice choice) in new[]
            {
                ("map-start-save", "Save and start", MapStartChoice.SaveAndStart),
                ("map-start-discard", "Discard", MapStartChoice.DiscardAndStart),
                ("map-start-cancel", "Cancel", MapStartChoice.Cancel),
            })
            {
                Button button = AdminButton(label, () => ResolveMapStart(choice));
                button.name = name;
                _mapStartCard.Add(button);
            }
            _mapStartPrompt.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode != KeyCode.Escape) return;
                ResolveMapStart(MapStartChoice.Cancel);
                e.StopPropagation();
            }, TrickleDown.TrickleDown);
            _mapStartPrompt.Add(_mapStartCard);
            return _mapStartPrompt;
        }

        private void ResolveMapStart(MapStartChoice choice)
        {
            if (_mapStartToken == 0 || !_mapStartCard.enabledSelf) return;
            _mapStartName.Blur();
            _mapStartCard.SetEnabled(false);
            MapStartResolved?.Invoke(_mapStartToken, choice, _mapStartName.value);
        }
    }
}

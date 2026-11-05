using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace SS3D.UI.MainHud.Components
{
    /// <summary>
    /// Hidden until a tank and a mask or sealed helmet are worn. Shows whether internals are on, and
    /// a click toggles them (Documents/design/armor.md §6).
    /// </summary>
    public sealed class InternalsChip : VisualElement
    {
        private readonly Label _label;
        private bool _available;
        private bool _on;

        public event Action Clicked;

        public InternalsChip(Sprite icon)
        {
            AddToClassList("alert-chip");
            AddToClassList("internals-chip");
            style.display = DisplayStyle.None;

            VisualElement box = new();
            box.AddToClassList("alert-chip__box");

            VisualElement glyph = new();
            glyph.AddToClassList("alert-chip__glyph");
            glyph.pickingMode = PickingMode.Ignore;
            if (icon != null)
            {
                glyph.style.backgroundImage = new StyleBackground(icon);
            }

            box.Add(glyph);

            _label = new Label();
            _label.AddToClassList("alert-chip__label");
            _label.AddToClassList("font-arcade");
            _label.pickingMode = PickingMode.Ignore;

            Add(box);
            Add(_label);

            RegisterCallback<ClickEvent>(_ => Clicked?.Invoke());
            RegisterCallback<PointerEnterEvent>(_ => AddToClassList("alert-chip--hovered"));
            RegisterCallback<PointerLeaveEvent>(_ => RemoveFromClassList("alert-chip--hovered"));
            Refresh();
        }

        public void SetState(bool available, bool on)
        {
            if (_available == available && _on == on)
            {
                return;
            }

            _available = available;
            _on = on;
            Refresh();
        }

        private void Refresh()
        {
            style.display = _available ? DisplayStyle.Flex : DisplayStyle.None;
            EnableInClassList("internals-chip--on", _on);
            _label.text = _on ? "Internals On" : "Internals Off";
        }
    }
}

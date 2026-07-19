using UnityEngine;
using UnityEngine.UIElements;

namespace SS3D.UI.MainHud.Components
{
    /// <summary>
    /// Cursor-following crosshair + zone-label chip (main-hud §6). Shown only while Harm intent
    /// is active; chip appears when hovering a valid limb zone.
    /// </summary>
    public sealed class ZoneTargetReticle
    {
        private const float ReticleSize = 22f;
        private const float ChipOffsetY = 22f;

        private VisualElement _root;
        private VisualElement _reticle;
        private VisualElement _chip;
        private Label _chipLabel;

        public VisualElement Root => _root;

        public ZoneTargetReticle()
        {
            BuildTree();
            SetChipVisible(false);
        }

        public void UpdateCursorPosition(Vector2 screenPosition)
        {
            if (_reticle == null)
            {
                return;
            }

            float half = ReticleSize * 0.5f;
            _reticle.style.left = screenPosition.x - half;
            _reticle.style.bottom = screenPosition.y - half;

            _chip.style.left = screenPosition.x;
            _chip.style.bottom = screenPosition.y - ChipOffsetY;
            _chip.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);
        }

        public void SetZoneHover(bool hasZone, string zoneLabel)
        {
            _reticle.EnableInClassList("zone-target-reticle--active", hasZone);
            if (!hasZone)
            {
                SetChipVisible(false);
                return;
            }

            _chipLabel.text = zoneLabel;
            SetChipVisible(true);
        }

        private void BuildTree()
        {
            _root = new VisualElement();
            _root.AddToClassList("zone-target-overlay");
            _root.pickingMode = PickingMode.Ignore;

            _reticle = new VisualElement();
            _reticle.AddToClassList("zone-target-reticle");
            _reticle.pickingMode = PickingMode.Ignore;

            VisualElement ring = new();
            ring.AddToClassList("zone-target-reticle__ring");
            ring.pickingMode = PickingMode.Ignore;

            foreach (string tickClass in new[]
                     {
                         "zone-target-reticle__tick--top",
                         "zone-target-reticle__tick--bottom",
                         "zone-target-reticle__tick--left",
                         "zone-target-reticle__tick--right",
                     })
            {
                VisualElement tick = new();
                tick.AddToClassList("zone-target-reticle__tick");
                tick.AddToClassList(tickClass);
                tick.pickingMode = PickingMode.Ignore;
                _reticle.Add(tick);
            }

            _reticle.Add(ring);

            _chip = new VisualElement();
            _chip.AddToClassList("zone-target-chip");
            _chip.pickingMode = PickingMode.Ignore;

            _chipLabel = new Label();
            _chipLabel.AddToClassList("zone-target-chip__label");
            _chipLabel.AddToClassList("font-terminal");
            _chipLabel.pickingMode = PickingMode.Ignore;
            _chip.Add(_chipLabel);

            _root.Add(_reticle);
            _root.Add(_chip);
        }

        private void SetChipVisible(bool visible)
        {
            if (_chip == null)
            {
                return;
            }

            _chip.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}

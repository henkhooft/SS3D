using System.Collections.Generic;
using SS3D.Systems.Health;
using UnityEngine;
using UnityEngine.UIElements;

namespace SS3D.UI.MachineInterface.Components
{
    /// <summary>
    /// Interactive body-zone silhouette for the vitals scanner: hover/click a zone for a brute/burn readout.
    /// Zone shapes are drawn with rounded <see cref="VisualElement"/>s (no imported silhouette art yet —
    /// see health system map for the follow-up to swap in the design's Harm icon set).
    /// </summary>
    [UxmlElement]
    public partial class AnatomyBodyMap : VisualElement
    {
        private static readonly Color BaseZoneColor = new Color32(0x3a, 0x3a, 0x3f, 0xff);
        private static readonly Color WarningColor = new Color32(0xee, 0x90, 0x37, 0xff);
        private static readonly Color DangerColor = new Color32(0xb9, 0x48, 0x48, 0xff);

        private readonly VisualElement _canvas;
        private readonly Dictionary<BodyZone, VisualElement> _zoneElements = new();
        private readonly Dictionary<BodyZone, string> _tooltips = new();
        private readonly Label _tooltip;

        private BodyZone? _hoveredZone;
        private BodyZone? _pinnedZone;

        public AnatomyBodyMap()
        {
            AddToClassList("anatomy-body-map");

            _canvas = new VisualElement();
            _canvas.AddToClassList("anatomy-body-map__canvas");

            VisualElement silhouette = new();
            silhouette.AddToClassList("anatomy-body-map__silhouette");
            silhouette.pickingMode = PickingMode.Ignore;
            _canvas.Add(silhouette);

            CreateZone(BodyZone.Head, "head");
            CreateZone(BodyZone.Chest, "chest");
            CreateZone(BodyZone.LeftArm, "left-arm");
            CreateZone(BodyZone.RightArm, "right-arm");
            CreateZone(BodyZone.Groin, "groin");
            CreateZone(BodyZone.LeftLeg, "left-leg");
            CreateZone(BodyZone.RightLeg, "right-leg");

            _tooltip = new Label();
            _tooltip.AddToClassList("anatomy-body-map__tooltip");
            _tooltip.pickingMode = PickingMode.Ignore;
            _tooltip.style.display = DisplayStyle.None;
            _canvas.Add(_tooltip);

            Add(_canvas);
        }

        public void SetZone(BodyZone zone, AnatomyZoneVisual visual)
        {
            if (!_zoneElements.TryGetValue(zone, out VisualElement element))
            {
                return;
            }

            Color toneColor = ToneToColor(visual.Tone);
            float blend = visual.Severity01 <= 0f ? 0f : Mathf.Clamp01(0.35f + (visual.Severity01 * 0.65f));
            element.style.backgroundColor = Color.Lerp(BaseZoneColor, toneColor, blend);

            element.RemoveFromClassList("anatomy-body-map__zone--severed");
            if (visual.Severed)
            {
                element.AddToClassList("anatomy-body-map__zone--severed");
            }

            _tooltips[zone] = visual.Tooltip;

            if (_pinnedZone == zone || _hoveredZone == zone)
            {
                RefreshTooltip();
            }
        }

        public void ClearSelection()
        {
            _hoveredZone = null;
            _pinnedZone = null;
            RefreshTooltip();
        }

        private void CreateZone(BodyZone zone, string modifier)
        {
            VisualElement element = new();
            element.AddToClassList("anatomy-body-map__zone");
            element.AddToClassList($"anatomy-body-map__zone--{modifier}");
            element.style.backgroundColor = BaseZoneColor;

            element.RegisterCallback<PointerEnterEvent>(_ => SetHovered(zone));
            element.RegisterCallback<PointerLeaveEvent>(_ => ClearHovered(zone));
            element.RegisterCallback<ClickEvent>(_ => TogglePinned(zone));

            _zoneElements[zone] = element;
            _tooltips[zone] = string.Empty;
            _canvas.Add(element);
        }

        private void SetHovered(BodyZone zone)
        {
            _hoveredZone = zone;
            RefreshTooltip();
        }

        private void ClearHovered(BodyZone zone)
        {
            if (_hoveredZone == zone)
            {
                _hoveredZone = null;
            }

            RefreshTooltip();
        }

        private void TogglePinned(BodyZone zone)
        {
            _pinnedZone = _pinnedZone == zone ? null : zone;
            RefreshTooltip();
        }

        private void RefreshTooltip()
        {
            BodyZone? active = _pinnedZone ?? _hoveredZone;
            if (active == null || !_zoneElements.TryGetValue(active.Value, out VisualElement element))
            {
                _tooltip.style.display = DisplayStyle.None;
                return;
            }

            _tooltip.text = _tooltips.TryGetValue(active.Value, out string text) ? text : string.Empty;
            _tooltip.style.display = DisplayStyle.Flex;
            _tooltip.style.left = element.layout.x + (element.layout.width * 0.5f);
            _tooltip.style.top = element.layout.y;
        }

        private static Color ToneToColor(StatusTone tone)
        {
            return tone switch
            {
                StatusTone.Danger => DangerColor,
                StatusTone.Warning => WarningColor,
                _ => BaseZoneColor,
            };
        }
    }
}

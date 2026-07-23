using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SS3D.UI.MainHud.Components
{
    /// <summary>
    /// Corner-bracket aim reticle + terminal zone label (main-hud §6).
    /// Dumb view: paints only from <see cref="ZoneReticleFrame"/> produced by
    /// <see cref="ZoneReticleDriver"/>. Do not toggle color classes from multiple setters.
    /// </summary>
    public sealed class ZoneTargetReticle
    {
        private const float ReticleSize = 52f;
        private const float ReticleBloomExtraPx = 28f;
        private const float ChipGapAboveReticle = 14f;
        private const float CornerMinPx = 4f;
        private const float CornerMaxPx = 16f;
        private const float CrossFlashPopFraction = 0.15f;

        private VisualElement _root;
        private VisualElement _reticle;
        private VisualElement _shakeHost;
        private VisualElement _crossFlash;
        private readonly List<VisualElement> _corners = new(4);
        private Label _chipLabel;

        public VisualElement Root => _root;

        public ZoneTargetReticle()
        {
            BuildTree();
            Apply(new ZoneReticleFrame(
                visible: false,
                cursorScreen: Vector2.zero,
                zoneLabel: string.Empty,
                color: ZoneReticleColorMode.Idle,
                bracketReady01: 1f,
                crossFlashT: -1f,
                shakeOffset: Vector2.zero));
        }

        public void Apply(in ZoneReticleFrame frame)
        {
            if (_root == null)
            {
                return;
            }

            _root.style.display = frame.Visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!frame.Visible)
            {
                return;
            }

            float size = ReticleSize + (frame.Bloom01 * ReticleBloomExtraPx);
            float half = size * 0.5f;
            _reticle.style.width = size;
            _reticle.style.height = size;
            _reticle.style.left = frame.CursorScreen.x - half;
            _reticle.style.bottom = frame.CursorScreen.y - half;

            _chipLabel.style.left = frame.CursorScreen.x;
            _chipLabel.style.bottom = frame.CursorScreen.y + half + ChipGapAboveReticle;
            _chipLabel.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);

            // Exactly one color modifier — clear all, then set the composed mode.
            _reticle.EnableInClassList("zone-target-reticle--valid", frame.Color == ZoneReticleColorMode.Valid);
            _reticle.EnableInClassList("zone-target-reticle--recharging", frame.Color == ZoneReticleColorMode.Recharging);
            _chipLabel.EnableInClassList("zone-target-label--valid", frame.Color == ZoneReticleColorMode.Valid);
            _chipLabel.EnableInClassList("zone-target-label--recharging", frame.Color == ZoneReticleColorMode.Recharging);

            float cornerPx = Mathf.Lerp(CornerMinPx, CornerMaxPx, Mathf.Clamp01(frame.BracketReady01));
            for (int i = 0; i < _corners.Count; i++)
            {
                _corners[i].style.width = cornerPx;
                _corners[i].style.height = cornerPx;
            }

            bool showLabel = !string.IsNullOrEmpty(frame.ZoneLabel);
            _chipLabel.text = showLabel ? frame.ZoneLabel : string.Empty;
            _chipLabel.style.display = showLabel ? DisplayStyle.Flex : DisplayStyle.None;

            ApplyCrossFlash(frame.CrossFlashT);
            _shakeHost.style.translate = new Translate(frame.ShakeOffset.x, frame.ShakeOffset.y);
        }

        private void ApplyCrossFlash(float t)
        {
            if (t < 0f)
            {
                _crossFlash.style.opacity = 0f;
                _crossFlash.style.scale = new Scale(new Vector2(0.5f, 0.5f));
                _crossFlash.style.display = DisplayStyle.None;
                return;
            }

            float scale = 0.5f + (t * 0.7f);
            float opacity = t < CrossFlashPopFraction
                ? t / CrossFlashPopFraction
                : 1f - ((t - CrossFlashPopFraction) / (1f - CrossFlashPopFraction));

            _crossFlash.style.display = DisplayStyle.Flex;
            _crossFlash.style.opacity = Mathf.Clamp01(opacity);
            _crossFlash.style.scale = new Scale(new Vector2(scale, scale));
        }

        private void BuildTree()
        {
            _root = new VisualElement();
            _root.AddToClassList("zone-target-overlay");
            _root.pickingMode = PickingMode.Ignore;

            _reticle = new VisualElement();
            _reticle.AddToClassList("zone-target-reticle");
            _reticle.pickingMode = PickingMode.Ignore;

            _shakeHost = new VisualElement();
            _shakeHost.AddToClassList("zone-target-reticle__shake");
            _shakeHost.pickingMode = PickingMode.Ignore;

            foreach (string corner in new[]
                     {
                         "zone-target-reticle__corner--tl",
                         "zone-target-reticle__corner--tr",
                         "zone-target-reticle__corner--bl",
                         "zone-target-reticle__corner--br",
                     })
            {
                VisualElement cornerRoot = new();
                cornerRoot.AddToClassList("zone-target-reticle__corner");
                cornerRoot.AddToClassList(corner);
                cornerRoot.pickingMode = PickingMode.Ignore;

                VisualElement h = new();
                h.AddToClassList("zone-target-reticle__arm");
                h.AddToClassList("zone-target-reticle__arm--h");
                h.pickingMode = PickingMode.Ignore;

                VisualElement v = new();
                v.AddToClassList("zone-target-reticle__arm");
                v.AddToClassList("zone-target-reticle__arm--v");
                v.pickingMode = PickingMode.Ignore;

                cornerRoot.Add(h);
                cornerRoot.Add(v);
                _shakeHost.Add(cornerRoot);
                _corners.Add(cornerRoot);
            }

            VisualElement dot = new();
            dot.AddToClassList("zone-target-reticle__dot");
            dot.pickingMode = PickingMode.Ignore;
            _shakeHost.Add(dot);

            _crossFlash = BuildCrossFlash();
            _shakeHost.Add(_crossFlash);

            _reticle.Add(_shakeHost);

            _chipLabel = new Label();
            _chipLabel.AddToClassList("zone-target-label");
            _chipLabel.AddToClassList("font-terminal");
            _chipLabel.pickingMode = PickingMode.Ignore;

            _root.Add(_reticle);
            _root.Add(_chipLabel);
        }

        private static VisualElement BuildCrossFlash()
        {
            VisualElement root = new();
            root.AddToClassList("zone-target-reticle__cross-flash");
            root.pickingMode = PickingMode.Ignore;

            float[] angles = { 45f, 135f, 225f, 315f };
            for (int i = 0; i < angles.Length; i++)
            {
                VisualElement arm = new();
                arm.AddToClassList("zone-target-reticle__cross-arm");
                arm.pickingMode = PickingMode.Ignore;
                arm.style.rotate = new Rotate(Angle.Degrees(angles[i]));
                arm.style.translate = new Translate(11f, 0f);
                root.Add(arm);
            }

            return root;
        }
    }
}

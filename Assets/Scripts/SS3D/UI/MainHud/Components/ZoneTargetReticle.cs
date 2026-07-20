using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SS3D.UI.MainHud.Components
{
    public enum ZoneReticleAimState
    {
        /// <summary>Harm aiming — grey. Optional zone label when hovering out of range.</summary>
        Idle = 0,
        /// <summary>Zone under cursor and in melee range — blue.</summary>
        Valid = 1,
        /// <summary>Deprecated: connect hit uses cross flash; kept so callers compile.</summary>
        Hit = 2,
    }

    /// <summary>
    /// Corner-bracket aim reticle + terminal zone label (main-hud §6). Harm intent only.
    /// Grey idle, blue valid target; red lock-on recharge during melee recovery (design 2A);
    /// white cross flash on successful connect (whiffs stay silent).
    /// </summary>
    public sealed class ZoneTargetReticle
    {
        private const float ReticleSize = 52f;
        private const float ChipGapAboveReticle = 14f;
        private const float CornerMinPx = 4f;
        private const float CornerMaxPx = 16f;
        private const float CrossFlashSeconds = 0.5f;
        private const float CrossFlashPopFraction = 0.15f;
        private const float CrossFlashShakeFraction = 0.4f;
        private const float CrossFlashShakePx = 2.5f;

        private VisualElement _root;
        private VisualElement _reticle;
        private VisualElement _shakeHost;
        private VisualElement _crossFlash;
        private readonly List<VisualElement> _corners = new(4);
        private Label _chipLabel;
        private float _crossFlashStartedAt = -1f;
        private Vector2 _cursorScreenPosition;

        public VisualElement Root => _root;

        public ZoneTargetReticle()
        {
            BuildTree();
            SetAimState(ZoneReticleAimState.Idle, string.Empty);
            SetLockProgress(1f, recharging: false);
            SetCrossFlashProgress(-1f);
            ApplyShake(Vector2.zero);
        }

        public void UpdateCursorPosition(Vector2 screenPosition)
        {
            if (_reticle == null)
            {
                return;
            }

            _cursorScreenPosition = screenPosition;
            float half = ReticleSize * 0.5f;
            _reticle.style.left = screenPosition.x - half;
            _reticle.style.bottom = screenPosition.y - half;

            _chipLabel.style.left = screenPosition.x;
            _chipLabel.style.bottom = screenPosition.y + half + ChipGapAboveReticle;
            _chipLabel.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);
        }

        public void SetAimState(ZoneReticleAimState state, string zoneLabel)
        {
            // Hit no longer recolors brackets — cross flash is the connect confirm.
            _reticle.EnableInClassList("zone-target-reticle--valid", state == ZoneReticleAimState.Valid);
            _reticle.EnableInClassList("zone-target-reticle--hit", false);
            _chipLabel.EnableInClassList("zone-target-label--valid", state == ZoneReticleAimState.Valid);
            _chipLabel.EnableInClassList("zone-target-label--hit", false);

            bool showLabel = !string.IsNullOrEmpty(zoneLabel);
            _chipLabel.text = showLabel ? zoneLabel : string.Empty;
            _chipLabel.style.display = showLabel ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>
        /// Design 2A lock-on recharge: <paramref name="readyProgress01"/> 0 = brackets receded at
        /// recovery start, 1 = full lock. Red while <paramref name="recharging"/>.
        /// </summary>
        public void SetLockProgress(float readyProgress01, bool recharging)
        {
            float progress = Mathf.Clamp01(readyProgress01);
            float cornerPx = Mathf.Lerp(CornerMinPx, CornerMaxPx, progress);

            for (int i = 0; i < _corners.Count; i++)
            {
                _corners[i].style.width = cornerPx;
                _corners[i].style.height = cornerPx;
            }

            _reticle.EnableInClassList("zone-target-reticle--recharging", recharging);
            _chipLabel.EnableInClassList("zone-target-label--recharging", recharging);
        }

        public void PlayCrossFlash()
        {
            _crossFlashStartedAt = Time.unscaledTime;
        }

        /// <summary>
        /// Advances cross-flash scale/opacity and shakes the whole aim frame (brackets + flash).
        /// </summary>
        public void TickCrossFlash()
        {
            if (_crossFlashStartedAt < 0f)
            {
                SetCrossFlashProgress(-1f);
                ApplyShake(Vector2.zero);
                return;
            }

            float elapsed = Time.unscaledTime - _crossFlashStartedAt;
            if (elapsed >= CrossFlashSeconds)
            {
                _crossFlashStartedAt = -1f;
                SetCrossFlashProgress(-1f);
                ApplyShake(Vector2.zero);
                return;
            }

            float t = elapsed / CrossFlashSeconds;
            SetCrossFlashProgress(t);

            Vector2 shake = Vector2.zero;
            if (t < CrossFlashShakeFraction)
            {
                // Deterministic jitter matching design ss3d-hit-shake keyframes.
                float shakeT = t / CrossFlashShakeFraction;
                float x = shakeT < 0.2f ? -1f
                    : shakeT < 0.4f ? 1f
                    : shakeT < 0.6f ? -0.67f
                    : shakeT < 0.8f ? 0.67f
                    : 0f;
                float y = shakeT < 0.2f ? 0.67f
                    : shakeT < 0.4f ? -0.67f
                    : shakeT < 0.6f ? 0.67f
                    : shakeT < 0.8f ? -0.33f
                    : 0f;
                shake = new Vector2(x, y) * CrossFlashShakePx;
            }

            ApplyShake(shake);
        }

        private void ApplyShake(Vector2 shakeOffset)
        {
            _shakeHost.style.translate = new Translate(shakeOffset.x, shakeOffset.y);
        }

        private void SetCrossFlashProgress(float t)
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

            // Shake host wraps brackets + center + cross so hit feedback jolts the whole frame.
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

            // Four diagonal capsules pointing at center (design 2A option B).
            // Rotate then translate along local +X so arms form an open X.
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

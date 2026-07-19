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
        /// <summary>Melee windup or recovery in progress — red.</summary>
        Hit = 2,
    }

    /// <summary>
    /// Corner-bracket aim reticle + terminal zone label (main-hud §6). Harm intent only.
    /// Grey idle, blue valid target, red while hitting.
    /// </summary>
    public sealed class ZoneTargetReticle
    {
        private const float ReticleSize = 52f;
        private const float ChipGapAboveReticle = 14f;

        private VisualElement _root;
        private VisualElement _reticle;
        private Label _chipLabel;

        public VisualElement Root => _root;

        public ZoneTargetReticle()
        {
            BuildTree();
            SetAimState(ZoneReticleAimState.Idle, string.Empty);
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

            _chipLabel.style.left = screenPosition.x;
            _chipLabel.style.bottom = screenPosition.y + half + ChipGapAboveReticle;
            _chipLabel.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);
        }

        public void SetAimState(ZoneReticleAimState state, string zoneLabel)
        {
            _reticle.EnableInClassList("zone-target-reticle--valid", state == ZoneReticleAimState.Valid);
            _reticle.EnableInClassList("zone-target-reticle--hit", state == ZoneReticleAimState.Hit);
            _chipLabel.EnableInClassList("zone-target-label--valid", state == ZoneReticleAimState.Valid);
            _chipLabel.EnableInClassList("zone-target-label--hit", state == ZoneReticleAimState.Hit);

            bool showLabel = !string.IsNullOrEmpty(zoneLabel);
            _chipLabel.text = showLabel ? zoneLabel : string.Empty;
            _chipLabel.style.display = showLabel ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void BuildTree()
        {
            _root = new VisualElement();
            _root.AddToClassList("zone-target-overlay");
            _root.pickingMode = PickingMode.Ignore;

            _reticle = new VisualElement();
            _reticle.AddToClassList("zone-target-reticle");
            _reticle.pickingMode = PickingMode.Ignore;

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
                _reticle.Add(cornerRoot);
            }

            VisualElement dot = new();
            dot.AddToClassList("zone-target-reticle__dot");
            dot.pickingMode = PickingMode.Ignore;
            _reticle.Add(dot);

            _chipLabel = new Label();
            _chipLabel.AddToClassList("zone-target-label");
            _chipLabel.AddToClassList("font-terminal");
            _chipLabel.pickingMode = PickingMode.Ignore;

            _root.Add(_reticle);
            _root.Add(_chipLabel);
        }
    }
}

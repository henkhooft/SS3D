using UnityEngine;
using UnityEngine.UIElements;

namespace SS3D.UI.MachineInterface.Components
{
    [UxmlElement]
    public partial class InventorySlot : VisualElement
    {
        private readonly VisualElement _well;
        private readonly VisualElement _labelHost;
        private readonly Label _label;
        private readonly Label _unknownGlyph;
        private readonly Image _icon;
        private readonly VisualElement _takeProgress;
        private readonly VisualElement _reservedBadge;
        private bool _unknown = true;
        private bool _reserved;
        private float _size = 48f;
        private Sprite _emptyIcon;
        private Sprite _itemIcon;

        public InventorySlot()
        {
            AddToClassList("inventory-slot");

            _well = new VisualElement();
            _well.AddToClassList("inventory-slot__well");

            _unknownGlyph = new Label("?");
            _unknownGlyph.AddToClassList("inventory-slot__unknown-glyph");
            _unknownGlyph.AddToClassList("font-titling");
            _well.Add(_unknownGlyph);

            _icon = new Image();
            _icon.AddToClassList("inventory-slot__icon");
            _icon.pickingMode = PickingMode.Ignore;
            _well.Add(_icon);

            _takeProgress = new VisualElement();
            _takeProgress.AddToClassList("inventory-slot__take-progress");
            _takeProgress.pickingMode = PickingMode.Ignore;
            _takeProgress.style.display = DisplayStyle.None;
            _well.Add(_takeProgress);

            _reservedBadge = new VisualElement();
            _reservedBadge.AddToClassList("inventory-slot__reserved-badge");
            _reservedBadge.pickingMode = PickingMode.Ignore;
            VisualElement reservedRing = new();
            reservedRing.AddToClassList("inventory-slot__reserved-ring");
            reservedRing.pickingMode = PickingMode.Ignore;
            VisualElement reservedSlash = new();
            reservedSlash.AddToClassList("inventory-slot__reserved-slash");
            reservedSlash.pickingMode = PickingMode.Ignore;
            _reservedBadge.Add(reservedRing);
            _reservedBadge.Add(reservedSlash);
            _well.Add(_reservedBadge);

            // Host sizes to the slot and centers the chip; avoids UITK translate:-50% sticking to the
            // previous text width when SlotLabel changes (e.g. Head → Trucker Cap).
            _labelHost = new VisualElement();
            _labelHost.AddToClassList("inventory-slot__label-host");
            _labelHost.pickingMode = PickingMode.Ignore;

            _label = new Label();
            _label.AddToClassList("inventory-slot__label");
            _label.AddToClassList("font-body");
            _label.pickingMode = PickingMode.Ignore;
            _labelHost.Add(_label);

            Add(_well);
            Add(_labelHost);

            ApplySize(_size);
            ApplyUnknown(_unknown);
            ApplyReserved(_reserved);
            ApplyIcon();
        }

        [UxmlAttribute]
        public bool Unknown
        {
            get => _unknown;
            set
            {
                _unknown = value;
                ApplyUnknown(value);
                ApplyIcon();
            }
        }

        /// <summary>
        /// Silhouette shown while the slot is empty, dimmed - matches the design system's InventorySlot
        /// "empty" treatment. Ignored while <see cref="Unknown"/> is true.
        /// </summary>
        public Sprite EmptyIcon
        {
            get => _emptyIcon;
            set
            {
                _emptyIcon = value;
                ApplyIcon();
            }
        }

        /// <summary>
        /// Full-color icon shown once the slot holds an item. Takes priority over <see cref="EmptyIcon"/>
        /// while set, and is ignored while <see cref="Unknown"/> is true.
        /// </summary>
        public Sprite ItemIcon
        {
            get => _itemIcon;
            set
            {
                _itemIcon = value;
                ApplyIcon();
            }
        }

        [UxmlAttribute]
        public float Size
        {
            get => _size;
            set
            {
                _size = value;
                ApplySize(value);
            }
        }

        [UxmlAttribute]
        public string SlotLabel
        {
            get => _label.text;
            set => _label.text = value;
        }

        /// <summary>
        /// 0–1 take windup progress drawn as a spinner overlay on the well. Values ≤ 0 hide it.
        /// </summary>
        public void SetTakeProgress(float progress01)
        {
            if (progress01 <= 0f)
            {
                ClearTakeProgress();
                return;
            }

            float clamped = Mathf.Clamp01(progress01);
            _takeProgress.style.display = DisplayStyle.Flex;
            _takeProgress.style.rotate = new Rotate(Angle.Degrees(clamped * 360f));
            EnableInClassList("inventory-slot--taking", true);
        }

        public void ClearTakeProgress()
        {
            _takeProgress.style.display = DisplayStyle.None;
            _takeProgress.style.rotate = new Rotate(Angle.Degrees(0f));
            EnableInClassList("inventory-slot--taking", false);
        }

        /// <summary>
        /// Off-hand reserved by a two-hand firearm — dims the well and shows a ban badge.
        /// </summary>
        public bool Reserved
        {
            get => _reserved;
            set
            {
                if (_reserved == value)
                {
                    return;
                }

                _reserved = value;
                ApplyReserved(value);
            }
        }

        private void ApplySize(float size)
        {
            _well.style.width = size;
            _well.style.height = size;
            _well.style.minWidth = size;
            _well.style.minHeight = size;
        }

        private void ApplyUnknown(bool unknown)
        {
            EnableInClassList("inventory-slot--unknown", unknown);
            _unknownGlyph.style.display = unknown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void ApplyReserved(bool reserved)
        {
            EnableInClassList("inventory-slot--reserved", reserved);
            _reservedBadge.style.display = reserved ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void ApplyIcon()
        {
            if (_unknown)
            {
                _icon.style.display = DisplayStyle.None;
                return;
            }

            bool hasItem = _itemIcon != null;
            Sprite sprite = hasItem ? _itemIcon : _emptyIcon;
            if (sprite == null)
            {
                _icon.style.display = DisplayStyle.None;
                return;
            }

            _icon.sprite = sprite;
            _icon.style.display = DisplayStyle.Flex;
            _icon.EnableInClassList("inventory-slot__icon--placeholder", !hasItem);
        }
    }
}

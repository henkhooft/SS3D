using SS3D.Systems.Examine;
using SS3D.UI.MachineInterface.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace SS3D.UI.Examine.Components
{
    /// <summary>
    /// Shared equipment-slot grid for the character-examine paperdoll — one 3-column-per-row layout
    /// (Head / Eyes-Face-Ears / Suit-Shirt-Gloves / Back-Feet-Belt / IdCard-Pocket) plus a hand row,
    /// reused at two sizes: the hover quick-look preview (<see cref="CharacterQuickLookView"/>, 40px)
    /// and the full examine window (<see cref="CharacterExamineWindowView"/>, 64px).
    /// </summary>
    public sealed class CharacterPaperdollGrid : VisualElement
    {
        private readonly InventorySlot[] _slots = new InventorySlot[SlotOrder.Length];

        private static readonly CharacterExamineSlot[] SlotOrder =
        {
            CharacterExamineSlot.Head,
            CharacterExamineSlot.Eyes,
            CharacterExamineSlot.Face,
            CharacterExamineSlot.Ears,
            CharacterExamineSlot.Suit,
            CharacterExamineSlot.Shirt,
            CharacterExamineSlot.Gloves,
            CharacterExamineSlot.Back,
            CharacterExamineSlot.Feet,
            CharacterExamineSlot.Belt,
            CharacterExamineSlot.IdCard,
            CharacterExamineSlot.Pocket,
            CharacterExamineSlot.HandLeft,
            CharacterExamineSlot.HandRight,
        };

        public CharacterPaperdollGrid(float slotSize, CharacterExamineIconSet icons)
        {
            AddToClassList("character-paperdoll-grid");

            InventorySlot head = CreateSlot(CharacterExamineSlot.Head, "Head", icons.Head, slotSize);
            InventorySlot eyes = CreateSlot(CharacterExamineSlot.Eyes, "Eyes", icons.Eyes, slotSize);
            InventorySlot face = CreateSlot(CharacterExamineSlot.Face, "Face Cover", icons.Face, slotSize);
            InventorySlot ears = CreateSlot(CharacterExamineSlot.Ears, "Ears", icons.Ears, slotSize);
            InventorySlot suit = CreateSlot(CharacterExamineSlot.Suit, "Suit", icons.Suit, slotSize);
            InventorySlot shirt = CreateSlot(CharacterExamineSlot.Shirt, "Shirt", icons.Shirt, slotSize);
            InventorySlot gloves = CreateSlot(CharacterExamineSlot.Gloves, "Gloves", icons.Gloves, slotSize);
            InventorySlot back = CreateSlot(CharacterExamineSlot.Back, "Back", icons.Back, slotSize);
            InventorySlot feet = CreateSlot(CharacterExamineSlot.Feet, "Feet", icons.Feet, slotSize);
            InventorySlot belt = CreateSlot(CharacterExamineSlot.Belt, "Belt", icons.Belt, slotSize);
            InventorySlot idCard = CreateSlot(CharacterExamineSlot.IdCard, "ID Card", icons.IdCard, slotSize);
            InventorySlot pocket = CreateSlot(CharacterExamineSlot.Pocket, "Pocket", icons.Pocket, slotSize);
            InventorySlot handLeft = CreateSlot(CharacterExamineSlot.HandLeft, "Left Hand", icons.HandLeft, slotSize);
            InventorySlot handRight = CreateSlot(CharacterExamineSlot.HandRight, "Right Hand", icons.HandRight, slotSize);

            Add(BuildRow(BuildSpacer(slotSize), head, BuildSpacer(slotSize)));
            Add(BuildRow(eyes, face, ears));
            Add(BuildRow(suit, shirt, gloves));
            Add(BuildRow(back, feet, belt));
            Add(BuildRow(idCard, pocket, BuildSpacer(slotSize)));

            VisualElement handRow = BuildRow(handLeft, handRight);
            handRow.AddToClassList("character-paperdoll-grid__hand-row");
            Add(handRow);
        }

        public InventorySlot GetSlot(CharacterExamineSlot slot) => _slots[(int)slot];

        public void SetSlot(CharacterExamineSlotContent content)
        {
            InventorySlot slot = GetSlot(content.Slot);
            if (slot == null)
            {
                return;
            }

            slot.ItemIcon = content.ItemIcon;
        }

        private InventorySlot CreateSlot(CharacterExamineSlot slot, string label, Sprite emptyIcon, float size)
        {
            InventorySlot inventorySlot = new()
            {
                Unknown = false,
                EmptyIcon = emptyIcon,
                Size = size,
                SlotLabel = label,
            };
            _slots[(int)slot] = inventorySlot;
            return inventorySlot;
        }

        private static VisualElement BuildRow(params VisualElement[] children)
        {
            VisualElement row = new();
            row.AddToClassList("character-paperdoll-grid__row");
            foreach (VisualElement child in children)
            {
                row.Add(child);
            }

            return row;
        }

        private static VisualElement BuildSpacer(float size)
        {
            VisualElement spacer = new();
            spacer.AddToClassList("character-paperdoll-grid__spacer");
            spacer.style.width = size;
            return spacer;
        }
    }
}

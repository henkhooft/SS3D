using SS3D.Core.Behaviours;
using SS3D.Systems.Inventory.Containers;
using SS3D.Systems.Selection;
using UnityEngine;

namespace SS3D.Systems.Examine
{
    /// <summary>
    /// Marks a character root as examinable and exposes its <see cref="HumanInventory"/> for the
    /// character-examine paperdoll (Documents/architecture/systems/examine.md § character examine).
    /// Attached to <c>Human.prefab</c>'s root by the
    /// <c>CharacterExaminePrefabSetup</c> Editor recipe, alongside the <see cref="Selectable"/> this
    /// requires.
    /// </summary>
    [RequireComponent(typeof(Selectable))]
    [RequireComponent(typeof(HumanInventory))]
    public sealed class CharacterExaminable : Actor, IExaminable
    {
        [SerializeField] private ExamineData _data;

        private HumanInventory _inventory;

        public HumanInventory Inventory => _inventory != null ? _inventory : _inventory = GetComponent<HumanInventory>();

        public ExamineData GetData() => _data;
    }
}

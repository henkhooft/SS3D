using SS3D.Core;
using SS3D.Interactions;
using SS3D.Interactions.Interfaces;
using SS3D.Systems.IdAccess;
using SS3D.Systems.Furniture.Disposal;
using SS3D.Systems.Inventory.Items;
using SS3D.Systems.Selection;
using UnityEngine;

namespace SS3D.Systems.Furniture
{
    /// <summary>
    /// A disposal chute. Offers the Combine-tier drop-in interaction (design doc §2) that captures a
    /// held item into the disposal network below it.
    /// </summary>
    [RequireComponent(typeof(Selectable))]
    public class DisposalBin : MonoBehaviour, IDisposalElement, IInteractionTarget
    {
        [SerializeField]
        [Tooltip("Optional. If present, dropping an item in requires matching access, same pattern as AirLockAccessGate.")]
        private AirLockAccessGate _accessGate;

        [SerializeField]
        [Tooltip("Largest item SizeClass this chute accepts. Mirrors AttachedContainer MaxSizeClass (inventory-storage.md §4).")]
        private SizeClass _maxSizeClass = SizeClass.Huge;

        public GameObject GameObject => gameObject;

        public AirLockAccessGate AccessGate => _accessGate;

        public SizeClass MaxSizeClass => _maxSizeClass;

        /// <summary>
        /// Size-class chute accept check (design disposal.md §2 / inventory-storage.md §4).
        /// </summary>
        public bool AcceptsSize(SizeClass size) => size <= _maxSizeClass;

        public IInteraction[] CreateTargetInteractions(InteractionEvent interactionEvent)
        {
            return new IInteraction[] { new DisposalDropInInteraction(this), new DisposalTaggerInteraction() };
        }

        /// <summary>
        /// Routes <paramref name="item"/> into whatever disposal network this bin's pipe belongs to.
        /// </summary>
        public bool TryEnterDisposalNetwork(Item item, Department destinationTag)
        {
            if (!SubSystems.TryGet(out DisposalSubSystem disposalSubSystem))
            {
                return false;
            }

            return disposalSubSystem.TryEnterNetwork(this, item, destinationTag);
        }
    }
}

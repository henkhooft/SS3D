using FishNet.Object;
using FishNet.Object.Synchronizing;
using SS3D.Core.Behaviours;
using SS3D.Systems.Combat;
using SS3D.Systems.Inventory.Containers;
using SS3D.Systems.Inventory.Items;

namespace SS3D.Systems.Health
{
    /// <summary>
    /// The body's EVA switches (Documents/design/armor.md §3, §6). Internals need a tank plus a mask
    /// or sealed helmet, and the server turns them off the moment either goes away.
    /// </summary>
    public class EvaGearController : NetworkActor
    {
        /// <summary>Where a tank can feed internals from, best first. Bags do not count.</summary>
        private static readonly ContainerType[] TankSlots =
        {
            ContainerType.SuitStorage,
            ContainerType.Pocket,
            ContainerType.Hand,
        };

        [SyncVar]
        private bool _internalsAvailable;

        [SyncVar]
        private bool _internalsOn;

        private HumanInventory _inventory;

        public bool InternalsAvailable => _internalsAvailable;

        public bool InternalsOn => _internalsOn;

        public override void OnStartServer()
        {
            base.OnStartServer();
            _inventory = GetComponent<HumanInventory>();
            _inventory.OnContainerContentChanged += HandleContainerContentChanged;
            ServerRefresh();
        }

        protected override void OnDestroyed()
        {
            if (_inventory != null)
            {
                _inventory.OnContainerContentChanged -= HandleContainerContentChanged;
            }

            base.OnDestroyed();
        }

        public void ClientToggleInternals() => CmdToggleInternals();

        [ServerRpc]
        private void CmdToggleInternals()
        {
            if (TryGetComponent(out HumanHealthController health) && health.Snapshot.IsConscious)
            {
                ServerSetInternals(!_internalsOn);
            }
        }

        [Server]
        public void ServerSetInternals(bool on)
        {
            ServerRefresh();
            _internalsOn = on && _internalsAvailable;
        }

        /// <summary>The tank being breathed from, or null while internals are off.</summary>
        [Server]
        public GasTankItemExtension ServerBreathingTank()
        {
            // A severed hand removes its container without a content change, so check every breath.
            ServerRefresh();
            return _internalsOn ? FindTank() : null;
        }

        private void HandleContainerContentChanged(AttachedContainer container, Item oldItem, Item newItem, ContainerChangeType type)
        {
            ServerRefresh();
        }

        [Server]
        private void ServerRefresh()
        {
            _internalsAvailable = FindTank() != null && HasBreathingGear();
            _internalsOn = _internalsOn && _internalsAvailable;
        }

        private GasTankItemExtension FindTank()
        {
            foreach (ContainerType slot in TankSlots)
            {
                for (int i = 0; _inventory.TryGetTypeContainer(slot, i, out AttachedContainer container); i++)
                {
                    foreach (Item item in container.Items)
                    {
                        if (item.TryGetComponent(out GasTankItemExtension tank))
                        {
                            return tank;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>A sealed helmet counts as a mask even when breached (armor.md §3).</summary>
        private bool HasBreathingGear()
        {
            return Wears<BreathMaskItemExtension>(ContainerType.Mask) || Wears<SealedGearItemExtension>(ContainerType.Head);
        }

        private bool Wears<T>(ContainerType slot)
        {
            if (!_inventory.TryGetTypeContainer(slot, 0, out AttachedContainer container))
            {
                return false;
            }

            foreach (Item item in container.Items)
            {
                if (item.TryGetComponent(out T _))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

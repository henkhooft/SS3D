using System.Collections.Generic;
using FishNet.Component.Transforming;
using FishNet.Managing;
using FishNet.Object;
using SS3D.Core;
using SS3D.Systems.Inventory.Containers;
using SS3D.Systems.Tile;
using UnityEngine;

namespace SS3D.Systems.Inventory.Items
{
    /// <summary>
    /// The one routine that puts an item where its <see cref="ItemHolder"/> says, on every peer (tech-debt P4.4):
    /// parent, visibility, colliders and physics, NetworkTransform sync, and the container's attach / detach events.
    /// It replaced <c>AttachedContainer.handleItemAdded</c> / <c>handleItemRemoved</c>, which ran from each container's
    /// SyncList callback. A move between two containers then arrived as two callbacks in either order and needed the
    /// per-peer <c>Item.LocalHolder</c> guard (henkhooft/SS3D#208); now each peer applies the item's one replicated value.
    /// <para>
    /// The server applies when it sets the holder (<see cref="Item.SetContainer"/>, <see cref="Item.ServerSetInTransit"/>);
    /// a client applies from the holder's SyncVar callback and when the item or its container starts on that client.
    /// Applying is idempotent: it compares against what this peer last applied, so a repeat does nothing.
    /// </para>
    /// </summary>
    public static class ItemPlacementApplier
    {
        /// <summary>Items whose container this client has not spawned yet, by the container's NetworkObject id.</summary>
        private static readonly Dictionary<int, HashSet<Item>> PendingByObjectId = new();

        /// <summary>
        /// Applies <paramref name="item"/>'s current holder on this peer. Only for spawned items: EditMode containers
        /// never ran their SyncList callbacks, and the applier keeps that (tests call <see cref="Apply"/> directly).
        /// </summary>
        /// <param name="startedContainer">A container that just started on this client, to resolve without a lookup.</param>
        public static void Refresh(Item item, AttachedContainer startedContainer = null)
        {
            if (item == null || !item.IsSceneInstance || item.NetworkObject == null || !item.NetworkObject.IsSpawned)
            {
                return;
            }

            ItemHolder holder = item.Holder;
            AttachedContainer target = null;
            bool hidden = false;
            Forget(item);

            switch (holder.Kind)
            {
                case ItemHolderKind.Container:
                    target = IsAuthority(item) ? item.Container : Resolve(item, holder, startedContainer);
                    if (target == null)
                    {
                        // Not spawned here yet: keep the item out of sight where it is, and finish when the container starts.
                        hidden = true;
                        AddPending(item, holder.ObjectId);
                    }

                    break;
                case ItemHolderKind.Transit:
                    hidden = true;
                    break;
            }

            if (!IsAuthority(item))
            {
                item.SetResolvedContainer(target);
            }

            Apply(item, target, hidden);
        }

        /// <summary>A container started on this client: apply every item that was waiting for it.</summary>
        public static void ContainerStarted(AttachedContainer container)
        {
            if (container == null || container.NetworkObject == null
                || !PendingByObjectId.TryGetValue(container.NetworkObject.ObjectId, out HashSet<Item> waiting))
            {
                return;
            }

            foreach (Item item in new List<Item>(waiting))
            {
                Refresh(item, container);
            }
        }

        /// <summary>Drops <paramref name="item"/> from the pending list (it stopped, or its holder changed).</summary>
        public static void Forget(Item item)
        {
            if (item == null || !item.PendingContainerObjectId.HasValue)
            {
                return;
            }

            int objectId = item.PendingContainerObjectId.Value;
            item.PendingContainerObjectId = null;
            if (PendingByObjectId.TryGetValue(objectId, out HashSet<Item> waiting) && waiting.Remove(item) && waiting.Count == 0)
            {
                PendingByObjectId.Remove(objectId);
            }
        }

        /// <summary>
        /// Moves <paramref name="item"/> from what this peer last applied to <paramref name="target"/> (null: loose, or
        /// hidden and frozen when <paramref name="hidden"/>). The steps and their order are those of the old container
        /// callbacks: leaving shows, unparents, raises the old container's detach, restores the rest scale; entering
        /// freezes, untracks it from the map, hides and attaches; only a loose item unfreezes.
        /// </summary>
        public static void Apply(Item item, AttachedContainer target, bool hidden)
        {
            if (item == null || !item.IsSceneInstance)
            {
                return;
            }

            AttachedContainer from = item.AppliedHolder;
            bool fromHidden = item.AppliedHidden;
            hidden &= target is null;
            if (ReferenceEquals(from, target) && fromHidden == hidden)
            {
                return;
            }

            item.AppliedHolder = target;
            item.AppliedHidden = hidden;

            if (!(from is null))
            {
                Leave(item, from);
            }
            else if (fromHidden)
            {
                item.SetVisibility(true);
                item.ApplyRestScale();
            }

            if (!(target is null))
            {
                Enter(item, target);
            }
            else if (hidden)
            {
                item.Freeze();
                item.SetVisibility(false);
            }
            else
            {
                item.Unfreeze();
            }

            SetTransformSync(item, target is null);
        }

        private static void Leave(Item item, AttachedContainer from)
        {
            // A destroyed container (despawned with the item still pointing at it) has nothing left to detach from.
            if (from != null)
            {
                if (from.HideItems)
                {
                    item.SetVisibility(true);
                }

                if (item.transform.parent == from.transform)
                {
                    item.transform.SetParent(null, true);
                }

                if (ReferenceEquals(item.AttachEventPendingTo, from))
                {
                    // Its attach was never raised here, so there is nothing to detach from.
                    item.AttachEventPendingTo = null;
                }
                else if (WaitsForList(item) && from.ContainsItem(item))
                {
                    item.DetachEventPendingFrom = from;
                }
                else
                {
                    from.ProcessItemDetached(item);
                }
            }

            item.ApplyRestScale();
        }

        private static void Enter(Item item, AttachedContainer target)
        {
            item.Freeze();

            // Items in containers are not map items: drop the PlacedItemObject so it isn't saved twice.
            if (item.TryGetComponent(out PlacedItemObject placedItemObject))
            {
                TileMap tileMap = SubSystems.GetOrNull<TileSubSystem>()?.CurrentMap;
                if (tileMap != null)
                {
                    tileMap.RemovePlacedItemFromTracking(placedItemObject);
                }

                UnityEngine.Object.DestroyImmediate(placedItemObject);
            }

            if (target.HideItems)
            {
                item.SetVisibility(false);
            }

            if (target.AttachItems)
            {
                if (!target.HasCustomDisplay)
                {
                    Transform itemTransform = item.transform;
                    itemTransform.SetParent(target.transform, false);
                    itemTransform.localPosition = target.AttachmentOffset;
                }

                if (WaitsForList(item) && !target.ContainsItem(item))
                {
                    item.AttachEventPendingTo = target;
                }
                else
                {
                    target.ProcessItemAttached(item);
                }
            }
        }

        /// <summary>
        /// Client, from a container's list callback: raises the attach / detach event the applier held back. FishNet
        /// writes SyncVars before SyncObjects, so a client usually gets the item's holder before the container's list;
        /// listeners such as <c>HumanoidBodyStateBridge</c> read <c>Hand.ItemInHand</c> from that list, so the event
        /// waits until the list agrees (the order the old list callback gave them).
        /// </summary>
        public static void ListCaughtUp(AttachedContainer container, Item item)
        {
            if (item == null || container == null)
            {
                return;
            }

            if (ReferenceEquals(item.DetachEventPendingFrom, container) && !container.ContainsItem(item))
            {
                item.DetachEventPendingFrom = null;
                container.ProcessItemDetached(item);
            }

            if (ReferenceEquals(item.AttachEventPendingTo, container) && container.ContainsItem(item))
            {
                item.AttachEventPendingTo = null;
                container.ProcessItemAttached(item);
            }
        }

        /// <summary>Only a spawned client copy waits: the server changes the list first, and EditMode has no list callbacks.</summary>
        private static bool WaitsForList(Item item) =>
            !IsAuthority(item) && item.NetworkObject != null && item.NetworkObject.IsSpawned;

        private static void SetTransformSync(Item item, bool synchronize)
        {
            if (item.TryGetComponent(out NetworkTransform networkTransform))
            {
                networkTransform.SetSynchronizePosition(synchronize);
                networkTransform.SetSynchronizeRotation(synchronize);
            }
        }

        /// <summary>True where this peer runs the server for the item: the server's own reference is the truth there.</summary>
        private static bool IsAuthority(Item item) => item.NetworkObject != null && item.NetworkObject.IsServer;

        private static AttachedContainer Resolve(Item item, ItemHolder holder, AttachedContainer startedContainer)
        {
            if (startedContainer != null && startedContainer.NetworkObject != null
                && startedContainer.NetworkObject.ObjectId == holder.ObjectId
                && startedContainer.ComponentIndex == holder.ComponentIndex)
            {
                return startedContainer;
            }

            NetworkManager networkManager = item.NetworkManager;
            if (networkManager == null)
            {
                return null;
            }

            NetworkObject nob = null;
            if (networkManager.IsClient)
            {
                networkManager.ClientManager.Objects.Spawned.TryGetValue(holder.ObjectId, out nob);
            }

            if (nob == null && networkManager.IsServer)
            {
                networkManager.ServerManager.Objects.Spawned.TryGetValue(holder.ObjectId, out nob);
            }

            if (nob == null || holder.ComponentIndex >= nob.NetworkBehaviours.Length)
            {
                return null;
            }

            return nob.NetworkBehaviours[holder.ComponentIndex] as AttachedContainer;
        }

        private static void AddPending(Item item, int objectId)
        {
            if (!PendingByObjectId.TryGetValue(objectId, out HashSet<Item> waiting))
            {
                waiting = new HashSet<Item>();
                PendingByObjectId.Add(objectId, waiting);
            }

            waiting.Add(item);
            item.PendingContainerObjectId = objectId;
        }
    }
}

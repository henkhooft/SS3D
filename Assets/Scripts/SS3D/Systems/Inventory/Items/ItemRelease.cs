using SS3D.Core;
using SS3D.Systems.Inventory.Containers;
using SS3D.Systems.Tile;
using UnityEngine;

namespace SS3D.Systems.Inventory.Items
{
    /// <summary>How an item comes to rest when it leaves its container for the world.</summary>
    public enum ItemReleaseKind
    {
        /// <summary>Put on a surface at the given pose, lifted so its colliders rest on the point (click drop, place).</summary>
        Placed,

        /// <summary>Let go where it is and left to fall (drop key, losing an arm, collapsing).</summary>
        Dropped,

        /// <summary>Lifted like <see cref="Placed"/> at the release point; the caller then launches it.</summary>
        Thrown,

        /// <summary>Held at exactly the given pose, with no lift (a charge stuck to a wall).</summary>
        Mounted,
    }

    /// <summary>
    /// The one server path that takes an item out of its container and into the world (tech-debt P4.4). Drop, place,
    /// throw, wall mount and the hand dump on collapse all go through here, so every released item gives up its
    /// owner, leaves its container and is tracked by the tile map the same way. Before, three copies differed:
    /// the dump skipped map tracking, so a dropped item was missing from the map's item list.
    /// </summary>
    public static class ItemRelease
    {
        /// <summary>
        /// Server only. Removes <paramref name="item"/> from its container (if any), poses it per <paramref name="kind"/>
        /// and registers it with the current tile map. <paramref name="position"/> and <paramref name="rotation"/> are
        /// ignored for <see cref="ItemReleaseKind.Dropped"/>, which keeps the pose the item had in the hand.
        /// </summary>
        /// <returns>False when the item could not leave its container (a storage condition refused).</returns>
        public static bool ServerReleaseToWorld(Item item, Vector3 position, Quaternion rotation, ItemReleaseKind kind)
        {
            if (item == null)
            {
                return false;
            }

            AttachedContainer container = item.Container;
            if (container != null && !container.CanRemoveItem(item))
            {
                return false;
            }

            // Ownership goes with the hand, before the item leaves it (the order the old release paths used).
            // Unspawned items (EditMode, prefab previews) have no owner to drop.
            if (item.NetworkObject != null && item.NetworkObject.IsSpawned)
            {
                item.GiveOwnership(null);
            }

            if (container != null)
            {
                container.RemoveItem(item);
            }

            Transform itemTransform = item.transform;
            switch (kind)
            {
                case ItemReleaseKind.Placed:
                case ItemReleaseKind.Thrown:
                    ItemUtility.Place(item, position, rotation);
                    break;
                case ItemReleaseKind.Mounted:
                    ItemUtility.SetWorldPose(itemTransform, position, rotation);
                    break;
                case ItemReleaseKind.Dropped:
                    break;
            }

            TrackOnMap(item);
            return true;
        }

        private static void TrackOnMap(Item item)
        {
            if (!SubSystems.TryGet(out TileSubSystem tile) || item.Asset == null)
            {
                return;
            }

            if (tile.GetAsset(item.Asset) is ItemObjectSo itemObjectSo)
            {
                // Pass the item's resulting pose; ItemUtility.Place lifts it off the raw surface point.
                tile.Construction.TryPlaceItem(itemObjectSo, item.transform.position, item.transform.rotation, item.gameObject);
            }
        }
    }
}

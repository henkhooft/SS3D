using System;

namespace SS3D.Systems.Inventory.Items
{
    /// <summary>Where an item is, as far as placement (parent, visibility, physics) is concerned.</summary>
    public enum ItemHolderKind : byte
    {
        /// <summary>Loose in the world: shown, physical, synced by its NetworkTransform.</summary>
        None = 0,

        /// <summary>Stored in an <see cref="Containers.AttachedContainer"/>.</summary>
        Container = 1,

        /// <summary>Riding the disposal pipes: hidden and frozen, in no container.</summary>
        Transit = 2,
    }

    /// <summary>
    /// The replicated holder of an <see cref="Item"/> (tech-debt P4.4). The container is kept as its NetworkObject id
    /// and component index, the same pair FishNet writes for a NetworkBehaviour reference, but resolved by
    /// <see cref="ItemPlacementApplier"/> rather than at read time: a client that has not spawned the container yet
    /// (a late joiner, or one outside its AOI) would read a plain reference as null and never retry.
    /// Packed into one <c>long</c> so the SyncVar needs no generated serializer.
    /// </summary>
    public readonly struct ItemHolder : IEquatable<ItemHolder>
    {
        public static readonly ItemHolder None = default;

        public static readonly ItemHolder Transit = new(ItemHolderKind.Transit, 0, 0);

        public ItemHolder(ItemHolderKind kind, int objectId, byte componentIndex)
        {
            Kind = kind;
            ObjectId = kind == ItemHolderKind.Container ? objectId : 0;
            ComponentIndex = kind == ItemHolderKind.Container ? componentIndex : (byte)0;
        }

        public ItemHolderKind Kind { get; }

        /// <summary>The container's NetworkObject id; 0 unless <see cref="Kind"/> is Container.</summary>
        public int ObjectId { get; }

        /// <summary>The container's index in its NetworkObject's behaviour list; 0 unless <see cref="Kind"/> is Container.</summary>
        public byte ComponentIndex { get; }

        public static ItemHolder InContainer(int objectId, byte componentIndex) =>
            new(ItemHolderKind.Container, objectId, componentIndex);

        /// <summary>Bits 0–31 object id, 32–39 component index, 40–47 kind.</summary>
        public long Pack() => ((long)Kind << 40) | ((long)ComponentIndex << 32) | (uint)ObjectId;

        public static ItemHolder Unpack(long packed)
        {
            ItemHolderKind kind = (ItemHolderKind)(byte)((packed >> 40) & 0xFF);
            if (!Enum.IsDefined(typeof(ItemHolderKind), kind))
            {
                return None;
            }

            return new ItemHolder(kind, unchecked((int)(packed & 0xFFFFFFFFL)), (byte)((packed >> 32) & 0xFF));
        }

        public bool Equals(ItemHolder other) =>
            Kind == other.Kind && ObjectId == other.ObjectId && ComponentIndex == other.ComponentIndex;

        public override bool Equals(object obj) => obj is ItemHolder other && Equals(other);

        public override int GetHashCode() => HashCode.Combine((int)Kind, ObjectId, ComponentIndex);

        public static bool operator ==(ItemHolder a, ItemHolder b) => a.Equals(b);

        public static bool operator !=(ItemHolder a, ItemHolder b) => !a.Equals(b);

        public override string ToString() => Kind == ItemHolderKind.Container
            ? $"Container({ObjectId}:{ComponentIndex})"
            : Kind.ToString();
    }
}

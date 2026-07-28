using SS3D.Systems.Health;
using UnityEngine;

namespace SS3D.Systems.Inventory.Containers
{
    /// <summary>
    /// Shared gate for taking items from another character's worn/held gear
    /// (Documents/design/inventory-storage.md §9; examine hold-to-take).
    /// Restrained/grabbed is deferred until that system exists.
    /// </summary>
    public static class CharacterLootUtility
    {
        /// <summary>
        /// True when the character is dead or unconscious and may be looted.
        /// </summary>
        public static bool IsLootable(HumanInventory inventory)
        {
            if (inventory == null)
            {
                return false;
            }

            HumanHealthController health = inventory.GetComponent<HumanHealthController>();
            if (health == null)
            {
                health = inventory.GetComponentInParent<HumanHealthController>();
            }

            if (health == null)
            {
                return false;
            }

            HealthSnapshot snapshot = health.Snapshot;
            return snapshot.State == HealthState.Dead || !snapshot.IsConscious;
        }

        /// <summary>
        /// True when <paramref name="takerInventory"/> is a different character than
        /// <paramref name="victimInventory"/>.
        /// </summary>
        public static bool IsOtherCharacter(HumanInventory takerInventory, HumanInventory victimInventory)
        {
            if (takerInventory == null || victimInventory == null)
            {
                return false;
            }

            return takerInventory != victimInventory
                && takerInventory.transform.root != victimInventory.transform.root;
        }
    }
}

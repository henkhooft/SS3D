using SS3D.Systems.Combat.Interactions;
using SS3D.Systems.Inventory.Containers;
using SS3D.Systems.Inventory.Items;
using UnityEngine;

namespace SS3D.Systems.Combat
{
    /// <summary>
    /// Equip / fire / stance rules for firearms that occupy both hands (M4: right-only, left reserved).
    /// </summary>
    public static class TwoHandedWeaponRules
    {
        public static bool RequiresBothHands(Item item, out HandSide requiredHand)
        {
            requiredHand = HandSide.Right;
            if (item == null || !item.TryGetComponent(out RangedWeaponItemExtension weapon))
            {
                return false;
            }

            if (!weapon.Profile.RequiresBothHands)
            {
                return false;
            }

            requiredHand = weapon.Profile.RequiredHand;
            return true;
        }

        /// <summary>
        /// Whether <paramref name="item"/> may enter <paramref name="targetHand"/>.
        /// Two-hand weapons must land in <see cref="RangedWeaponProfile.RequiredHand"/> with the
        /// off-hand empty; a reserved off-hand rejects any new item while the rifle is wielded.
        /// </summary>
        public static bool CanPlaceInHand(Item item, Hand targetHand, Hands hands)
        {
            if (item == null || targetHand == null)
            {
                return true;
            }

            if (RequiresBothHands(item, out HandSide requiredHand))
            {
                if (targetHand.Side != requiredHand)
                {
                    return false;
                }

                Hand offHand = FindOtherHand(hands, targetHand);
                if (offHand != null && !offHand.IsEmpty())
                {
                    return false;
                }
            }

            return !IsHandReserved(targetHand, hands);
        }

        /// <summary>
        /// True when the other hand holds a two-hand weapon in its required hand — this hand is blocked.
        /// </summary>
        public static bool IsHandReserved(Hand hand, Hands hands)
        {
            Hand other = FindOtherHand(hands, hand);
            if (other == null)
            {
                return false;
            }

            Item otherItem = other.ItemInHand;
            if (!RequiresBothHands(otherItem, out HandSide requiredHand))
            {
                return false;
            }

            return other.Side == requiredHand;
        }

        /// <summary>
        /// Held firearm for fire/reload. Two-hand rifles resolve from their required hand even when
        /// the other hand is selected for UI; one-hand guns use the selected hand only.
        /// </summary>
        public static bool TryGetWieldedRangedWeapon(
            Hands hands,
            out Hand holdingHand,
            out RangedWeaponItemExtension weapon)
        {
            holdingHand = null;
            weapon = null;
            if (hands?.PlayerHands == null)
            {
                return false;
            }

            foreach (Hand hand in hands.PlayerHands)
            {
                if (hand == null)
                {
                    continue;
                }

                Item item = hand.ItemInHand;
                if (item == null || !item.TryGetComponent(out RangedWeaponItemExtension candidate))
                {
                    continue;
                }

                if (!candidate.Profile.RequiresBothHands)
                {
                    continue;
                }

                if (hand.Side != candidate.Profile.RequiredHand)
                {
                    continue;
                }

                holdingHand = hand;
                weapon = candidate;
                return true;
            }

            Hand selected = hands.SelectedHand;
            Item selectedItem = selected?.ItemInHand;
            if (selectedItem != null && selectedItem.TryGetComponent(out weapon))
            {
                if (weapon.Profile.RequiresBothHands && selected.Side != weapon.Profile.RequiredHand)
                {
                    weapon = null;
                    return false;
                }

                holdingHand = selected;
                return true;
            }

            weapon = null;
            return false;
        }

        /// <summary>Item that drives combat stance / arm-hold while a two-hand rifle is wielded.</summary>
        public static Item ResolvePresentationItem(Hands hands)
        {
            if (TryGetWieldedRangedWeapon(hands, out Hand holdingHand, out _))
            {
                return holdingHand.ItemInHand;
            }

            return hands?.SelectedHand?.ItemInHand;
        }

        /// <summary>Two-hand rifles stay right-posed; otherwise mirror follows the selected hand.</summary>
        public static bool ShouldMirrorUpperBody(Hands hands)
        {
            if (TryGetWieldedRangedWeapon(hands, out _, out RangedWeaponItemExtension weapon)
                && weapon.Profile.RequiresBothHands)
            {
                return false;
            }

            Hand active = hands?.SelectedHand;
            return active != null && active.Side == HandSide.Left;
        }

        private static Hand FindOtherHand(Hands hands, Hand hand)
        {
            if (hands?.PlayerHands == null || hand == null)
            {
                return null;
            }

            foreach (Hand candidate in hands.PlayerHands)
            {
                if (candidate != null && candidate != hand)
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}

using SS3D.Interactions;
using SS3D.Interactions.Interfaces;
using SS3D.Systems.Inventory.Containers;
using System.Collections.Generic;
using UnityEngine;

namespace SS3D.Systems.Combat.Interactions
{
    /// <summary>
    /// Empty-hand fists melee from hand sources.
    /// </summary>
    public class HandMeleeExtension : MonoBehaviour, IInteractionSourceExtension
    {
        public void GetSourceInteractions(IInteractionTarget[] targets, List<InteractionEntry> interactions)
        {
            if (!TryGetComponent(out Hand hand))
            {
                return;
            }

            var interaction = new MeleeHitInteraction(MeleeWeaponProfile.Fists);
            foreach (IInteractionTarget target in targets)
            {
                if (interaction.CanInteract(new InteractionEvent(hand, target)))
                {
                    interactions.Add(new InteractionEntry(target, interaction));
                }
            }
        }
    }
}

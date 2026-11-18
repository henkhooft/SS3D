using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using NUnit.Framework;
using SS3D.Interactions;
using SS3D.Interactions.Interfaces;
using SS3D.Systems.Construction;
using SS3D.Systems.Entities.Humanoid.Body;

namespace SS3D.Tests.EditMode
{
    /// <summary>
    /// Tech-debt P4.3: interactions declare their body animation (<see cref="IInteraction.AnimationHint"/>) instead of
    /// <c>InteractionAnimation.ChooseClip</c> matching generic-name strings. The compiler makes every interaction
    /// declare one; these tests pin that the switch changed no clip: <see cref="LegacyHint"/> is the old name table,
    /// copied as it stood, and every interaction type and construction rule must still land on the same clip.
    /// </summary>
    public class InteractionAnimationHintTests
    {
        /// <summary>The generic-name table ChooseClip used before P4.3 (the light switch was "Toggle" on a LightSwitchController).</summary>
        private static InteractionAnimationHint LegacyHint(string genericName) => genericName switch
        {
            "Pickup" => InteractionAnimationHint.Pickup,
            "Drop" => InteractionAnimationHint.PutDown,
            "OpenLocker" => InteractionAnimationHint.LockerDoor,
            "LockLocker" or "UnlockLocker" => InteractionAnimationHint.LockerTap,
            "OpenAirlock" or "AttachCharge" or "NukeLoadDisk" or "NukeArm" => InteractionAnimationHint.DoorReach,
            "DefuseC4" or "Perform CPR" or "ConstructionPlacePlating" or "ConstructionPlaceFloorTile" or "ConstructionPryTile"
                or "ConstructionRepairPlating" or "ConstructionClearWreckage" => InteractionAnimationHint.Kneel,
            "ConstructionRepairStructure" or "ConstructionPlaceGirder" or "ConstructionPlateGirder" or "ConstructionWeldSeams"
                or "ConstructionGlazeGirder" or "ConstructionCutSeams" or "ConstructionPullPlate" or "ConstructionLiftPane"
                or "ConstructionDismantleGirder" or "NukeOpenPanel" or "NukeCutWires" => InteractionAnimationHint.WallWork,
            _ => InteractionAnimationHint.None,
        };

        [Test]
        public void EveryInteractionType_KeepsTheClipItsNameUsedToPick()
        {
            List<string> mismatches = new();
            int checkedTypes = 0;
            int animated = 0;

            foreach (Type type in InteractionTypes())
            {
                // Constant hints and names read fine from an unconstructed instance; ones built from fields throw and are
                // covered elsewhere (construction rules below, the light switch toggle in its own test).
                IInteraction interaction = (IInteraction)FormatterServices.GetUninitializedObject(type);
                string genericName;
                InteractionAnimationHint hint;
                try
                {
                    genericName = interaction.GetGenericName();
                    hint = interaction.AnimationHint;
                }
                catch (Exception)
                {
                    continue;
                }

                checkedTypes++;
                if (hint != InteractionAnimationHint.None)
                {
                    animated++;
                }

                InteractionAnimationHint legacy = LegacyHint(genericName);
                if (InteractionAnimation.ChooseClip(hint, 1f) != InteractionAnimation.ChooseClip(legacy, 1f)
                    || InteractionAnimation.ChooseClip(hint, 0f) != InteractionAnimation.ChooseClip(legacy, 0f))
                {
                    mismatches.Add($"{type.FullName} (\"{genericName}\"): {hint}, was {legacy}");
                }
            }

            Assert.That(checkedTypes, Is.GreaterThan(40), "The scan found too few interaction types; check InteractionTypes.");
            Assert.That(animated, Is.GreaterThanOrEqualTo(14), "Pickup, drop, lockers, doors, charges, the nuke and CPR declare a hint");
            Assert.IsEmpty(mismatches, "These interactions now play a different clip:\n" + string.Join("\n", mismatches));
        }

        [Test]
        public void EveryConstructionRule_KeepsTheClipItsNameUsedToPick()
        {
            foreach (ConstructionRule rule in ConstructionRecipes.Rules)
            {
                Assert.AreEqual(LegacyHint(rule.GenericName), rule.AnimationHint, rule.GenericName);
            }
        }

        [Test]
        public void AToggle_PlaysNothing_UnlessItIsALightSwitch()
        {
            Assert.AreEqual(InteractionAnimationHint.None, new ToggleInteraction().AnimationHint);
            Assert.AreEqual(InteractionAnimation.LightSwitch,
                InteractionAnimation.ChooseClip(new ToggleInteraction { AnimationHint = InteractionAnimationHint.LightSwitch }.AnimationHint));
        }

        private static IEnumerable<Type> InteractionTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => assembly.GetName().Name.StartsWith("SS3D") && !assembly.GetName().Name.StartsWith("SS3D.Tests"))
                .SelectMany(SafeTypes)
                .Where(type => type.IsClass && !type.IsAbstract && !type.ContainsGenericParameters
                    && typeof(IInteraction).IsAssignableFrom(type));
        }

        private static IEnumerable<Type> SafeTypes(System.Reflection.Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (System.Reflection.ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(type => type != null);
            }
        }
    }
}

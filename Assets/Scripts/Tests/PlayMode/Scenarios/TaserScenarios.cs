using System.Collections;
using NUnit.Framework;
using SS3D.Core;
using SS3D.Systems.Combat;
using SS3D.Systems.Entities;
using SS3D.Systems.Entities.Humanoid;
using SS3D.Systems.Health;
using UnityEngine;
using UnityEngine.TestTools;

namespace SS3D.Tests
{
    // Guards: a taser hit floors a combat dummy for a knockdown (combat.md §5), the dummy stands up again still frozen, and a second hit keeps it down without stacking · Not cheaper because: the unowned ragdoll, its server-side physics authority and the get-up clip only run on a spawned body
    [Category(TestCategories.Scenarios)]
    [Category(TestCategories.Combat)]
    public class TaserScenarios : ScenarioFixture
    {
        [UnityTest]
        public IEnumerator TaserHit_KnocksDummyDown_ThenItStandsFrozen()
        {
            // Open floor in the main room; "two metres ahead" of the fresh body is inside its north wall.
            var spawn = new Vector3(0f, Body.transform.position.y, ScenarioAtmos.MainRoom.yMin + 1f);
            Entity dummy = SubSystems.Get<EntitySubSystem>().ServerSpawnCombatDummy(spawn, Quaternion.identity);
            yield return Hold(0.5f);
            Ragdoll ragdoll = dummy.GetComponent<Ragdoll>();
            HumanHealthController health = dummy.GetComponentInChildren<HumanHealthController>();
            RangedWeaponProfile taser = RangedWeaponProfile.Taser;

            CombatInteractionNetwork.ServerApplyLivingHit(taser, health, BodyZone.Chest);
            Assert.IsTrue(ragdoll.IsKnockedDown, "The taser floors the dummy");

            yield return Hold(1f);
            CombatInteractionNetwork.ServerApplyLivingHit(taser, health, BodyZone.Chest);
            yield return Hold(taser.KnockdownSeconds - 0.3f);
            Assert.IsTrue(ragdoll.IsKnockedDown, "A second hit refreshes the knockdown");

            yield return WaitFor(() => !ragdoll.IsKnockedDown, "The dummy gets up", timeout: 1f);
            Assert.That(ragdoll.Presentation, Is.EqualTo(BodyPresentationState.Locomotion));
            yield return WaitFor(() => !ragdoll.IsGettingUp, "The get-up finishes", timeout: 5f);
            yield return Hold(0.2f);
            Assert.IsFalse(dummy.GetComponent<HumanoidLivingController>().enabled, "Standing up leaves the dummy frozen");
            Assert.That(dummy.transform.position.y, Is.EqualTo(spawn.y).Within(0.1f), "It stands on the deck");
        }
    }
}

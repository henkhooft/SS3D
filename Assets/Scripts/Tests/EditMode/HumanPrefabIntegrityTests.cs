using System.Linq;
using NUnit.Framework;
using SS3D.Hacks;
using UnityEditor;
using UnityEngine;

namespace EditorTests
{
    /// <summary>
    /// Guards TECH_DEBT.md §1.1 "enforcement is convention only" — catches drift that hand-editing
    /// Human.prefab's YAML would otherwise let through silently. See
    /// 2026-07_human-prefab-decomposition.md Phase 0.
    /// </summary>
    public class HumanPrefabIntegrityTests
    {
        private const string HumanPrefabPath = "Assets/Content/WorldObjects/Entities/Humanoids/Human/Human.prefab";
        private const string TestHumanPrefabPath = "Assets/Content/WorldObjects/Entities/Humanoids/Human/TestHuman.prefab";

        [TestCase(HumanPrefabPath)]
        [TestCase(TestHumanPrefabPath)]
        public void Prefab_HasNoMissingScripts(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(prefab, $"Could not load prefab at {prefabPath}");

            // A missing-script component surfaces as a Unity "fake null" MonoBehaviour reference.
            int missingCount = prefab.GetComponentsInChildren<MonoBehaviour>(true).Count(behaviour => behaviour == null);

            Assert.AreEqual(0, missingCount, $"{prefabPath} has {missingCount} component(s) with a missing script.");
        }

        [Test]
        [Ignore("RagdollWhenPressingButton still present on Human.prefab — run SS3D/Entities/Remove Dev-Only " +
            "Hacks From Human Prefab in the Editor, verify in Play Mode, then re-enable this test. See " +
            "2026-07_human-prefab-decomposition.md Phase 0.")]
        public void HumanPrefab_HasNoDevOnlyHackComponents()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HumanPrefabPath);
            Assert.IsNotNull(prefab, $"Could not load prefab at {HumanPrefabPath}");

            RagdollWhenPressingButton[] hacks = prefab.GetComponentsInChildren<RagdollWhenPressingButton>(true);

            Assert.AreEqual(0, hacks.Length, "Human.prefab must not ship SS3D.Hacks debug components to players.");
        }
    }
}

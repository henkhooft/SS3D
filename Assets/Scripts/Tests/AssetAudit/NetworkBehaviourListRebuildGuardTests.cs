using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace SS3D.Tests.AssetAudit
{
    /// <summary>
    /// TECH_DEBT §1.32: recipes rewrite a prefab's FishNet behaviour list in one place,
    /// <c>SS3D.Systems.PrefabRecipes.NetworkBehaviourListRebuild</c>. Eight copies had drifted apart (two walks, two
    /// sets of written fields), and forgetting the rewrite fails silently (combat.md Pitfalls). Any other game or
    /// editor source naming FishNet's private <c>_networkBehaviours</c> list is a new copy: call the helper instead.
    /// Tests may still read the field to check a prefab.
    /// </summary>
    public sealed class NetworkBehaviourListRebuildGuardTests
    {
        private const string FieldLiteral = "\"_networkBehaviours\"";

        private const string HelperPath = "Systems/PrefabRecipes/NetworkBehaviourListRebuild.cs";

        private static string ScriptsRoot => Path.Combine(Application.dataPath, "Scripts", "SS3D");

        [Test]
        public void OnlyTheSharedHelperRewritesTheBehaviourList()
        {
            string[] files = Directory.GetFiles(ScriptsRoot, "*.cs", SearchOption.AllDirectories);
            Assert.That(files.Length, Is.GreaterThan(100), "The scan found almost no scripts; check ScriptsRoot.");
            Assert.That(File.Exists(Path.Combine(ScriptsRoot, HelperPath)), "The shared helper moved; update HelperPath.");

            List<string> copies = files
                .Where(path => File.ReadAllText(path).Contains(FieldLiteral))
                .Select(path => path.Substring(ScriptsRoot.Length + 1).Replace('\\', '/'))
                .Where(relative => relative != HelperPath)
                .ToList();

            Assert.IsEmpty(copies,
                "These files touch FishNet's _networkBehaviours list directly. Call NetworkBehaviourListRebuild.ForPrefab " +
                "(or ForNestedPrefab on Human) instead:\n" + string.Join("\n", copies));
        }
    }
}

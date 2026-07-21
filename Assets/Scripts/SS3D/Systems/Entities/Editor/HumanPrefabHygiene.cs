#if UNITY_EDITOR
using System.Collections.Generic;
using FishNet.Object;
using SS3D.Hacks;
using UnityEditor;
using UnityEngine;

namespace SS3D.Systems.Entities.Editor
{
    /// <summary>
    /// Removes dev-only test components from <c>Human.prefab</c> via <see cref="PrefabUtility"/> instead of
    /// hand-edited YAML. First recipe tool for the follow-on (d) "Entity prefab setup / recipes" convention
    /// named in 2026-07_agent-first-composition.md / 2026-07_human-prefab-decomposition.md.
    /// </summary>
    /// <remarks>
    /// <c>Human.prefab</c>'s root <see cref="NetworkObject"/> flattens NetworkBehaviours across nested
    /// PrefabInstance boundaries (HumanTorso, HumanHead, each limb — every body part carries its own
    /// <see cref="NetworkObject"/> with <see cref="NetworkObject.IsNested"/> set, not a separate spawn root).
    /// <see cref="StorageContainerPrefabSetup"/>'s collection helper stops at the first child with a
    /// <see cref="NetworkObject"/>, which is correct for flat prefabs (backpacks, lockers) but would silently
    /// drop every nested body-part behaviour if reused here. <see cref="CollectNetworkBehaviours"/> below only
    /// stops at a child Nob when it is <b>not</b> nested (a genuine separate spawn root) — see
    /// entities.md § Pitfalls.
    /// </remarks>
    public static class HumanPrefabHygiene
    {
        private const string HumanPrefabPath = "Assets/Content/WorldObjects/Entities/Humanoids/Human/Human.prefab";

        [MenuItem("SS3D/Entities/Remove Dev-Only Hacks From Human Prefab")]
        public static void RemoveDevHacksMenu()
        {
            int removed = RemoveDevHacks();
            EditorUtility.DisplayDialog(
                "Human Prefab Hygiene",
                removed > 0
                    ? $"Removed {removed} dev-only component(s) from Human.prefab."
                    : "No dev-only components found on Human.prefab.",
                "OK");
        }

        /// <summary>BatchMode entry: <c>-executeMethod SS3D.Systems.Entities.Editor.HumanPrefabHygiene.RemoveDevHacksBatch</c></summary>
        public static void RemoveDevHacksBatch()
        {
            int removed = RemoveDevHacks();
            Debug.Log($"[HumanPrefabHygiene] Removed {removed} dev-only component(s) from Human.prefab.");
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }

        public static int RemoveDevHacks()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(HumanPrefabPath);
            if (prefabRoot == null)
            {
                Debug.LogError($"[HumanPrefabHygiene] Missing prefab: {HumanPrefabPath}");
                return 0;
            }

            try
            {
                if (!prefabRoot.TryGetComponent(out NetworkObject rootNetworkObject))
                {
                    Debug.LogError($"[HumanPrefabHygiene] No root NetworkObject on {HumanPrefabPath}");
                    return 0;
                }

                RagdollWhenPressingButton[] hacks = prefabRoot.GetComponentsInChildren<RagdollWhenPressingButton>(true);
                foreach (RagdollWhenPressingButton hack in hacks)
                {
                    Object.DestroyImmediate(hack, true);
                }

                if (hacks.Length > 0)
                {
                    RebuildNetworkBehaviours(rootNetworkObject);
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, HumanPrefabPath);
                }

                return hacks.Length;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        /// <summary>
        /// Rewrites the root <see cref="NetworkObject"/>'s <c>_networkBehaviours</c> list and each
        /// behaviour's <c>_componentIndexCache</c> after removing a component, mirroring FishNet's own
        /// prefab-processing step. Unlike <c>StorageContainerPrefabSetup.RebuildNetworkBehaviours</c>, this
        /// walk does not stop at a nested body-part's own <see cref="NetworkObject"/> — see class remarks.
        /// </summary>
        private static void RebuildNetworkBehaviours(NetworkObject rootNetworkObject)
        {
            List<NetworkBehaviour> behaviours = new();
            CollectNetworkBehaviours(rootNetworkObject.transform, behaviours);

            SerializedObject nobSo = new(rootNetworkObject);
            SerializedProperty listProp = nobSo.FindProperty("_networkBehaviours");
            listProp.arraySize = behaviours.Count;
            for (int i = 0; i < behaviours.Count; i++)
            {
                listProp.GetArrayElementAtIndex(i).objectReferenceValue = behaviours[i];

                SerializedObject behaviourSo = new(behaviours[i]);
                behaviourSo.FindProperty("_componentIndexCache").intValue = i;
                behaviourSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(behaviours[i]);
            }

            nobSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rootNetworkObject);
        }

        private static void CollectNetworkBehaviours(Transform transform, List<NetworkBehaviour> behaviours)
        {
            behaviours.AddRange(transform.GetComponents<NetworkBehaviour>());
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);

                // Only a non-nested NetworkObject marks a genuine separate spawn root; every body-part
                // Nob under Human.prefab is nested and must still be walked into.
                if (child.TryGetComponent(out NetworkObject childNob) && !childNob.IsNested)
                {
                    continue;
                }

                CollectNetworkBehaviours(child, behaviours);
            }
        }
    }
}
#endif

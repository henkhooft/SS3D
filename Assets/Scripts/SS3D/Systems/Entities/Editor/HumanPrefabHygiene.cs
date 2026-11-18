#if UNITY_EDITOR
using System.Collections.Generic;
using FishNet.Object;
using SS3D.Systems.Comms;
using SS3D.Systems.PrefabRecipes;
using UnityEditor;
using UnityEngine;

namespace SS3D.Systems.Entities.Editor
{
    /// <summary>
    /// Removes dev-only test components from <c>Human.prefab</c> via <see cref="PrefabUtility"/> instead of
    /// hand-edited YAML. First recipe tool for the follow-on (d) "Entity prefab setup / recipes" convention
    /// named in 2026-07_agent-first-composition.md / Documents/architecture/systems/entities-prefab-recipes.md.
    /// Individual MenuItems removed — run via <see cref="HumanPrefabRecipes"/> (tier B).
    /// </summary>
    /// <remarks>
    /// <c>Human.prefab</c>'s root <see cref="NetworkObject"/> flattens NetworkBehaviours across nested
    /// PrefabInstance boundaries (HumanTorso, HumanHead, each limb — every body part carries its own
    /// <see cref="NetworkObject"/> with <see cref="NetworkObject.IsNested"/> set, not a separate spawn root).
    /// <see cref="NetworkBehaviourListRebuild.ForPrefab"/> stops at the first child with a
    /// <see cref="NetworkObject"/>, which is correct for flat prefabs (backpacks, lockers) but would silently
    /// drop every nested body-part behaviour here. Recipes on Human therefore call
    /// <see cref="NetworkBehaviourListRebuild.ForNestedPrefab"/>, which only stops at a child Nob when it is
    /// <b>not</b> nested (a genuine separate spawn root) — see entities.md § Pitfalls.
    /// </remarks>
    public static class HumanPrefabHygiene
    {
        private const string HumanPrefabPath = "Assets/Content/WorldObjects/Entities/Humanoids/Human/Human.prefab";

        /// <summary>BatchMode: <c>-executeMethod SS3D.Systems.Entities.Editor.HumanPrefabHygiene.RemoveDevHacksBatch</c>
        /// Prefer <see cref="HumanPrefabRecipes.RunAllBatch"/>.</summary>
        public static void RemoveDevHacksBatch()
        {
            int removed = RemoveDevHacks();
            UnityEngine.Debug.Log($"[HumanPrefabHygiene] Removed {removed} dev-only component(s) from Human.prefab.");
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
                UnityEngine.Debug.LogError($"[HumanPrefabHygiene] Missing prefab: {HumanPrefabPath}");
                return 0;
            }

            try
            {
                if (!prefabRoot.TryGetComponent(out NetworkObject rootNetworkObject))
                {
                    UnityEngine.Debug.LogError($"[HumanPrefabHygiene] No root NetworkObject on {HumanPrefabPath}");
                    return 0;
                }

                List<MonoBehaviour> hacks = new();
                foreach (MonoBehaviour behaviour in prefabRoot.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (IsDevHack(behaviour))
                    {
                        hacks.Add(behaviour);
                    }
                }

                foreach (MonoBehaviour hack in hacks)
                {
                    UnityEngine.Object.DestroyImmediate(hack, true);
                }

                if (hacks.Count > 0)
                {
                    NetworkBehaviourListRebuild.ForNestedPrefab(rootNetworkObject, writeNetworkObjectReferences: false);
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, HumanPrefabPath);
                }

                return hacks.Count;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        /// <summary>
        /// Strips components whose script was deleted (e.g. <c>HumanoidPredictedMovement</c>, tech-debt P3.2) from
        /// <c>Human.prefab</c> and rebuilds the root behaviour list, so a retired script is removed by rerunning the
        /// recipes rather than by hand-editing YAML. Returns how many were removed.
        /// </summary>
        public static int RemoveMissingScripts()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(HumanPrefabPath);
            if (prefabRoot == null)
            {
                UnityEngine.Debug.LogError($"[HumanPrefabHygiene] Missing prefab: {HumanPrefabPath}");
                return 0;
            }

            try
            {
                if (!prefabRoot.TryGetComponent(out NetworkObject rootNetworkObject))
                {
                    UnityEngine.Debug.LogError($"[HumanPrefabHygiene] No root NetworkObject on {HumanPrefabPath}");
                    return 0;
                }

                int removed = 0;
                foreach (Transform transform in prefabRoot.GetComponentsInChildren<Transform>(true))
                {
                    removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
                }

                if (removed > 0)
                {
                    NetworkBehaviourListRebuild.ForNestedPrefab(rootNetworkObject, writeNetworkObjectReferences: false);
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, HumanPrefabPath);
                }

                return removed;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        /// <summary>A debug component from the <c>SS3D.Hacks</c> assembly, which must never ship on Human.</summary>
        public static bool IsDevHack(MonoBehaviour behaviour) =>
            behaviour != null && behaviour.GetType().Assembly.GetName().Name == "SS3D.Hacks";

        /// <summary>
        /// Reloads <c>Human.prefab</c> against the current on-disk state of its nested body-part prefabs
        /// and rewrites the root <see cref="NetworkObject"/>'s behaviour list to match, then resaves.
        /// Needed because destroying a <see cref="NetworkBehaviour"/> directly on a nested prefab asset
        /// (e.g. <c>BodyPartContainerInteractiveStrip</c> editing <c>HumanHead.prefab</c>/`HumanTorso.prefab`
        /// on their own) does not retroactively update <c>Human.prefab</c>'s own stripped mirror of that
        /// instance — that mirror only refreshes the next time <c>Human.prefab</c> itself is reloaded and
        /// resaved. Always run this after any recipe that removes a component from a body-part prefab. See
        /// entities.md § Pitfalls. Invoked by <see cref="HumanPrefabRecipes"/> (no standalone MenuItem).
        /// </summary>
        public static void ResyncNestedPrefabInstances()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(HumanPrefabPath);
            if (prefabRoot == null)
            {
                UnityEngine.Debug.LogError($"[HumanPrefabHygiene] Missing prefab: {HumanPrefabPath}");
                return;
            }

            try
            {
                if (!prefabRoot.TryGetComponent(out NetworkObject rootNetworkObject))
                {
                    UnityEngine.Debug.LogError($"[HumanPrefabHygiene] No root NetworkObject on {HumanPrefabPath}");
                    return;
                }

                NetworkBehaviourListRebuild.ForNestedPrefab(rootNetworkObject, writeNetworkObjectReferences: false);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, HumanPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        /// <summary>
        /// Ensures <see cref="LocalSpeechEmitter"/> on <c>Human.prefab</c>. Returns true if added.
        /// </summary>
        public static bool EnsureLocalSpeechEmitter()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(HumanPrefabPath);
            if (prefabRoot == null)
            {
                UnityEngine.Debug.LogError($"[HumanPrefabHygiene] Missing prefab: {HumanPrefabPath}");
                return false;
            }

            try
            {
                if (prefabRoot.GetComponent<LocalSpeechEmitter>() != null)
                {
                    return false;
                }

                if (!prefabRoot.TryGetComponent(out NetworkObject rootNetworkObject))
                {
                    UnityEngine.Debug.LogError($"[HumanPrefabHygiene] No root NetworkObject on {HumanPrefabPath}");
                    return false;
                }

                prefabRoot.AddComponent<LocalSpeechEmitter>();
                NetworkBehaviourListRebuild.ForNestedPrefab(rootNetworkObject, writeNetworkObjectReferences: false);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, HumanPrefabPath);
                UnityEngine.Debug.Log($"[HumanPrefabHygiene] Added LocalSpeechEmitter to {HumanPrefabPath}");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
    }
}
#endif

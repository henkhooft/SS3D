#if UNITY_EDITOR
using FishNet.Object;
using SS3D.Systems.PrefabRecipes;
using UnityEditor;
using UnityEngine;

namespace SS3D.Systems.Construction.Editor
{
    /// <summary>
    /// Tier-B recipe: puts <see cref="ConstructionPlacementSubSystem"/> on the NetworkSystemsHub prefab and
    /// rewrites the hub NetworkObject's behaviour list, so the new behaviour is part of the spawn. Appended
    /// last, existing component indices are unchanged. The full hub rebuild
    /// (SS3D → Bootstrap → Rebuild NetworkSystemsHub Prefab) lists the type too. Idempotent.
    /// </summary>
    public static class ConstructionHubRecipe
    {
        public const string HubPath = "Assets/Resources/NetworkSystemsHub.prefab";

        public static bool SetupAll()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(HubPath);
            if (root == null)
            {
                Debug.LogError($"[ConstructionHubRecipe] Missing {HubPath}");
                return false;
            }

            try
            {
                if (root.GetComponent<ConstructionPlacementSubSystem>() != null)
                {
                    return false;
                }

                root.AddComponent<ConstructionPlacementSubSystem>();
                NetworkObject networkObject = root.GetComponent<NetworkObject>();
                if (networkObject != null)
                {
                    NetworkBehaviourListRebuild.ForPrefab(networkObject);
                }

                PrefabUtility.SaveAsPrefabAsset(root, HubPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
#endif

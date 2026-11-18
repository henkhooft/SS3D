#if UNITY_EDITOR
using FishNet.Object;
using UnityEditor;
using UnityEngine;

namespace SS3D.Systems.Inventory.Containers.Editor
{
    /// <summary>
    /// Tier-B recipe (registered on <see cref="InventoryContentPrefabRecipes"/>): makes the three lockers draggable
    /// (Documents/plans/drag-and-zero-g.plan.md, M13: "a crew member drags a locker"). Adds <see cref="Draggable"/> to
    /// each root and rebuilds its NetworkObject's behaviour list. Idempotent.
    /// </summary>
    public static class LockerDragPrefabSetup
    {
        public static readonly string[] LockerPaths =
        {
            "Assets/Content/WorldObjects/Furniture/Storage/Lockers/Locker.prefab",
            "Assets/Content/WorldObjects/Furniture/Storage/Lockers/LockerSecure.prefab",
            "Assets/Content/WorldObjects/Furniture/Storage/Lockers/SecurityLocker.prefab",
        };

        /// <summary>How many lockers it changed.</summary>
        public static int SetupAll()
        {
            int changed = 0;
            foreach (string path in LockerPaths)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.GetComponent<Draggable>() != null)
                    {
                        continue;
                    }

                    root.AddComponent<Draggable>();
                    StorageContainerPrefabSetup.RebuildNetworkBehaviours(root.GetComponent<NetworkObject>());
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changed++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            return changed;
        }
    }
}
#endif

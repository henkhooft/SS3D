#if UNITY_EDITOR
using FishNet.Object;
using SS3D.Systems.Entities.Humanoid;
using UnityEditor;
using UnityEngine;

namespace SS3D.Systems.Entities.Editor
{
    /// <summary>
    /// Tier-B recipe (registered on <see cref="HumanPrefabRecipes"/>): puts <see cref="BodyDraggable"/> on the
    /// Human.prefab root so Downed, Critical and dead bodies can be dragged (Documents/plans/drag-and-zero-g.plan.md,
    /// M13 Decision 25), and rebuilds the root NetworkObject's behaviour list. Idempotent: a second run changes nothing.
    /// </summary>
    public static class BodyDraggablePrefabSetup
    {
        public const string HumanPrefabPath = "Assets/Content/WorldObjects/Entities/Humanoids/Human/Human.prefab";

        /// <summary>True when it added the component.</summary>
        public static bool Wire()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(HumanPrefabPath);
            try
            {
                if (root.GetComponent<BodyDraggable>() != null)
                {
                    return false;
                }

                root.AddComponent<BodyDraggable>();
                HumanPrefabHygiene.RebuildNetworkBehaviours(root.GetComponent<NetworkObject>());
                PrefabUtility.SaveAsPrefabAsset(root, HumanPrefabPath);
                UnityEngine.Debug.Log("[BodyDraggablePrefabSetup] Added BodyDraggable to Human.prefab");
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

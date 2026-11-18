#if UNITY_EDITOR
using System.Collections.Generic;
using FishNet.Object;
using UnityEditor;
using UnityEngine;

namespace SS3D.Systems.PrefabRecipes
{
    /// <summary>
    /// The one place recipes rewrite a prefab's FishNet behaviour list after adding or removing a
    /// <see cref="NetworkBehaviour"/>. <c>PrefabUtility.SaveAsPrefabAsset</c> leaves the new behaviour at
    /// <c>_componentIndexCache: 255</c> and the root's <c>_networkBehaviours</c> unchanged, so its SyncVars
    /// and RPCs never run, with no error (combat.md Pitfalls). Call one of these after every
    /// <c>AddComponent</c> / <c>DestroyImmediate</c> of a NetworkBehaviour in a recipe. Lives in
    /// <c>SS3D.Systems</c> (editor-only) so the hub recipes compiled there can reach it too.
    /// </summary>
    public static class NetworkBehaviourListRebuild
    {
        /// <summary>
        /// FishNet's own rule (<c>NetworkObject.UpdateNetworkBehaviours</c>): collect behaviours under the
        /// root, stop at any child with its own <see cref="NetworkObject"/>, and point each collected
        /// behaviour back at the root. Use for every prefab except Human and its body parts.
        /// </summary>
        public static void ForPrefab(NetworkObject root)
        {
            Rebuild(root, includeNestedNetworkObjects: false, writeNetworkObjectReferences: true);
        }

        /// <summary>
        /// Human and its body parts: each body part is a nested <see cref="NetworkObject"/>
        /// (<c>IsNested</c>), and the root's list flattens across them (entities.md Pitfalls). The walk only
        /// stops at a child <see cref="NetworkObject"/> that is not nested.
        /// </summary>
        /// <param name="writeNetworkObjectReferences">
        /// Also point each behaviour's <c>_addedNetworkObject</c> / <c>_networkObjectCache</c> at the root.
        /// <c>HumanPrefabHygiene</c> and <c>BodyPartContainerInteractiveStrip</c> pass false (index only);
        /// <c>CombatInteractionNetworkPrefabSetup</c> passes true, which matches what Human.prefab holds today.
        /// Kept as a choice so this consolidation changes no prefab; see TECH_DEBT §6 (1.32).
        /// </param>
        public static void ForNestedPrefab(NetworkObject root, bool writeNetworkObjectReferences)
        {
            Rebuild(root, includeNestedNetworkObjects: true, writeNetworkObjectReferences);
        }

        /// <summary>The behaviours <see cref="ForPrefab"/> or <see cref="ForNestedPrefab"/> would list, in order.</summary>
        public static List<NetworkBehaviour> Collect(NetworkObject root, bool includeNestedNetworkObjects)
        {
            List<NetworkBehaviour> behaviours = new();
            CollectNetworkBehaviours(root.transform, behaviours, includeNestedNetworkObjects);
            return behaviours;
        }

        private static void Rebuild(NetworkObject root, bool includeNestedNetworkObjects, bool writeNetworkObjectReferences)
        {
            List<NetworkBehaviour> behaviours = Collect(root, includeNestedNetworkObjects);

            SerializedObject nobSo = new(root);
            SerializedProperty listProp = nobSo.FindProperty("_networkBehaviours");
            listProp.arraySize = behaviours.Count;
            for (int i = 0; i < behaviours.Count; i++)
            {
                listProp.GetArrayElementAtIndex(i).objectReferenceValue = behaviours[i];

                SerializedObject behaviourSo = new(behaviours[i]);
                if (writeNetworkObjectReferences)
                {
                    SetReference(behaviourSo, "_addedNetworkObject", root);
                    SetReference(behaviourSo, "_networkObjectCache", root);
                }

                SerializedProperty index = behaviourSo.FindProperty("_componentIndexCache");
                if (index != null)
                {
                    index.intValue = i;
                }

                behaviourSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(behaviours[i]);
            }

            nobSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(root);
        }

        private static void SetReference(SerializedObject behaviourSo, string propertyName, NetworkObject root)
        {
            SerializedProperty property = behaviourSo.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = root;
            }
        }

        private static void CollectNetworkBehaviours(Transform transform, List<NetworkBehaviour> behaviours, bool includeNestedNetworkObjects)
        {
            behaviours.AddRange(transform.GetComponents<NetworkBehaviour>());
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.TryGetComponent(out NetworkObject childNob) && (!includeNestedNetworkObjects || !childNob.IsNested))
                {
                    continue;
                }

                CollectNetworkBehaviours(child, behaviours, includeNestedNetworkObjects);
            }
        }
    }
}
#endif

using System.Linq;
using FishNet.Object;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EditorTests
{
    /// <summary>
    /// M13 (Documents/plans/drag-and-zero-g.plan.md): lockers can be dragged. The recipe
    /// <c>LockerDragPrefabSetup</c> puts <see cref="Draggable"/> on each locker root and lists it on its NetworkObject.
    /// </summary>
    public sealed class LockerDragPrefabTests
    {
        [TestCase("Assets/Content/WorldObjects/Furniture/Storage/Lockers/Locker.prefab")]
        [TestCase("Assets/Content/WorldObjects/Furniture/Storage/Lockers/LockerSecure.prefab")]
        [TestCase("Assets/Content/WorldObjects/Furniture/Storage/Lockers/SecurityLocker.prefab")]
        public void Locker_IsDraggable_AndItsNetworkObjectListsTheDrag(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, path);
            Assert.IsTrue(prefab.TryGetComponent(out Draggable draggable), $"{path} has Draggable on its root (Run Inventory Content Prefab Recipes)");
            Assert.IsTrue(prefab.GetComponent<NetworkObject>().NetworkBehaviours.Contains(draggable), "Its NetworkObject lists the Draggable");
        }
    }
}

using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SS3D.Systems;
using SS3D.Systems.Inventory.Containers;
using SS3D.Systems.Inventory.Items;
using UnityEngine;

namespace EditorTests
{
    /// <summary>
    /// Storage items only nest into strictly larger storage items (Documents/design/inventory-storage.md §7),
    /// so a toolbox can't hold another toolbox and so on forever (playtest bug: "matrioska toolboxes").
    /// </summary>
    public class ContainerNestingTests
    {
        private readonly List<GameObject> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
            {
                UnityEngine.Object.DestroyImmediate(go);
            }

            _spawned.Clear();
        }

        [Test]
        public void StorageItemRejectsAnotherStorageItemOfItsOwnSize()
        {
            AttachedContainer toolbox = CreateStorageItem(SizeClass.Bulky, SizeClass.Bulky);
            Item otherToolbox = CreateStorageItem(SizeClass.Bulky, SizeClass.Bulky).GetComponent<Item>();

            Assert.IsFalse(toolbox.CanContainItem(otherToolbox));
        }

        [Test]
        public void StorageItemAcceptsSmallerStorageItem()
        {
            AttachedContainer toolbox = CreateStorageItem(SizeClass.Bulky, SizeClass.Bulky);
            Item medkit = CreateStorageItem(SizeClass.Normal, SizeClass.Normal).GetComponent<Item>();

            Assert.IsTrue(toolbox.CanContainItem(medkit));
        }

        [Test]
        public void StorageItemStillAcceptsPlainItemOfItsOwnSize()
        {
            AttachedContainer toolbox = CreateStorageItem(SizeClass.Bulky, SizeClass.Bulky);
            Item extinguisher = CreateItem(SizeClass.Bulky);

            Assert.IsTrue(toolbox.CanContainItem(extinguisher));
        }

        [Test]
        public void NonStorageSlotOnAnItemAcceptsStorageItemOfAnySize()
        {
            // Hand and worn slots live on body-part items but have no storage panel.
            AttachedContainer hand = CreateContainerOnItem(SizeClass.Normal, SizeClass.Huge, hasUi: false);
            Item toolbox = CreateStorageItem(SizeClass.Bulky, SizeClass.Bulky).GetComponent<Item>();

            Assert.IsTrue(hand.CanContainItem(toolbox));
        }

        [Test]
        public void FreestandingStorageAcceptsStorageItems()
        {
            GameObject lockerObject = Track(new GameObject("Locker"));
            AttachedContainer locker = lockerObject.AddComponent<AttachedContainer>();
            locker.Init(new Vector2Int(4, 4), null);
            SetField(locker, "_hasUi", true);
            Item toolbox = CreateStorageItem(SizeClass.Bulky, SizeClass.Bulky).GetComponent<Item>();

            Assert.IsTrue(locker.CanContainItem(toolbox));
        }

        private AttachedContainer CreateStorageItem(SizeClass itemSize, SizeClass accepts)
        {
            return CreateContainerOnItem(itemSize, accepts, hasUi: true);
        }

        private AttachedContainer CreateContainerOnItem(SizeClass itemSize, SizeClass accepts, bool hasUi)
        {
            Item item = CreateItem(itemSize);
            AttachedContainer container = item.gameObject.AddComponent<AttachedContainer>();
            container.Init(new Vector2Int(4, 2), null);
            SetField(container, "_hasUi", hasUi);
            SetField(container, "_maxSizeClass", accepts);
            return container;
        }

        private Item CreateItem(SizeClass size)
        {
            Item item = Track(new GameObject("Item")).AddComponent<Item>();
            item.Init("TestItem", 1f, new List<Trait>());
            SetField(item, "_sizeClass", size);
            return item;
        }

        private GameObject Track(GameObject go)
        {
            _spawned.Add(go);
            return go;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"{target.GetType().Name}.{fieldName}");
            field.SetValue(target, value);
        }
    }
}

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
    /// A move between two containers (pocket to toolbox) used to sync as two container SyncList callbacks that a client
    /// could run in either order; a late remove re-showed the item under the new container (playtest gas mask,
    /// henkhooft/SS3D#208). Placement now follows one replicated holder per item (tech-debt P4.4), applied by
    /// <see cref="ItemPlacementApplier"/>. These cases drive the applier directly: a move is one transition, and a repeat
    /// (a late callback refreshing the same holder) changes nothing.
    /// </summary>
    public class ContainerMoveOrderTests
    {
        private readonly List<GameObject> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
            {
                Object.DestroyImmediate(go);
            }

            _spawned.Clear();
        }

        [Test]
        public void MoveToAnotherContainer_EndsInTheNewOne()
        {
            AttachedContainer pocket = CreateContainer("Pocket", attachItems: false);
            AttachedContainer toolbox = CreateContainer("Toolbox", attachItems: true);
            Item mask = CreateItem(out MeshRenderer renderer, out BoxCollider collider);

            ItemPlacementApplier.Apply(mask, pocket, hidden: false);
            ItemPlacementApplier.Apply(mask, toolbox, hidden: false);

            Assert.IsFalse(renderer.enabled, "Hidden inside the toolbox");
            Assert.IsFalse(collider.enabled, "Frozen inside the toolbox");
            Assert.AreEqual(toolbox.transform, mask.transform.parent, "Parented to the toolbox");
            Assert.AreEqual(toolbox, mask.AppliedHolder);
        }

        [Test]
        public void RepeatingTheSameHolder_ChangesNothing()
        {
            AttachedContainer pocket = CreateContainer("Pocket", attachItems: false);
            AttachedContainer toolbox = CreateContainer("Toolbox", attachItems: true);
            Item mask = CreateItem(out MeshRenderer renderer, out BoxCollider collider);
            int detachedFromPocket = 0;
            int attachedToToolbox = 0;
            pocket.OnItemDetached += (_, _) => detachedFromPocket++;
            toolbox.OnItemAttached += (_, _) => attachedToToolbox++;

            ItemPlacementApplier.Apply(mask, pocket, hidden: false);
            ItemPlacementApplier.Apply(mask, toolbox, hidden: false);
            ItemPlacementApplier.Apply(mask, toolbox, hidden: false);

            Assert.AreEqual(1, detachedFromPocket, "The pocket lets go once");
            Assert.AreEqual(1, attachedToToolbox, "The toolbox takes it once");
            Assert.IsFalse(renderer.enabled);
            Assert.IsFalse(collider.enabled);
            Assert.AreEqual(toolbox.transform, mask.transform.parent);
        }

        [Test]
        public void LeavingIntoTheWorld_ShowsAndReleasesTheItem()
        {
            AttachedContainer toolbox = CreateContainer("Toolbox", attachItems: true);
            Item mask = CreateItem(out MeshRenderer renderer, out BoxCollider collider);

            ItemPlacementApplier.Apply(mask, toolbox, hidden: false);
            ItemPlacementApplier.Apply(mask, null, hidden: false);

            Assert.IsTrue(renderer.enabled);
            Assert.IsTrue(collider.enabled);
            Assert.IsNull(mask.transform.parent);
            Assert.IsNull(mask.AppliedHolder);
        }

        [Test]
        public void Transit_HidesAndFreezesOutsideAnyContainer_ThenReleases()
        {
            Item mask = CreateItem(out MeshRenderer renderer, out BoxCollider collider);

            ItemPlacementApplier.Apply(mask, null, hidden: true);
            Assert.IsFalse(renderer.enabled, "Hidden in the pipes");
            Assert.IsFalse(collider.enabled, "Frozen in the pipes");
            Assert.IsNull(mask.transform.parent);

            ItemPlacementApplier.Apply(mask, null, hidden: false);
            Assert.IsTrue(renderer.enabled, "Shown at the outlet");
            Assert.IsTrue(collider.enabled, "Physical at the outlet");
        }

        // A late joiner can spawn the item before its container: the item waits hidden, then goes in when the
        // container starts.
        [Test]
        public void WaitingForTheContainer_ThenEntering_EndsInTheContainer()
        {
            AttachedContainer toolbox = CreateContainer("Toolbox", attachItems: true);
            Item mask = CreateItem(out MeshRenderer renderer, out BoxCollider collider);

            ItemPlacementApplier.Apply(mask, null, hidden: true);
            ItemPlacementApplier.Apply(mask, toolbox, hidden: false);

            Assert.IsFalse(renderer.enabled);
            Assert.IsFalse(collider.enabled);
            Assert.AreEqual(toolbox.transform, mask.transform.parent);
            Assert.AreEqual(toolbox, mask.AppliedHolder);
        }

        [Test]
        public void ALooseItemThatWasNeverPlaced_IsLeftAlone()
        {
            Item charge = CreateItem(out MeshRenderer renderer, out BoxCollider collider);
            collider.enabled = false; // a mounted or anchored item manages its own physics

            ItemPlacementApplier.Apply(charge, null, hidden: false);

            Assert.IsFalse(collider.enabled, "No unfreeze for an item no container ever held");
            Assert.IsTrue(renderer.enabled);
        }

        private AttachedContainer CreateContainer(string name, bool attachItems)
        {
            GameObject go = Track(new GameObject(name));
            AttachedContainer container = go.AddComponent<AttachedContainer>();
            container.Init(new Vector2Int(4, 2), null);
            SetField(container, "_attachItems", attachItems);
            return container;
        }

        private Item CreateItem(out MeshRenderer renderer, out BoxCollider collider)
        {
            GameObject go = Track(new GameObject("GasMask"));
            renderer = go.AddComponent<MeshRenderer>();
            collider = go.AddComponent<BoxCollider>();
            Item item = go.AddComponent<Item>();
            item.Init("GasMask", 1f, new List<Trait>());
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

using System.Collections;
using System.Linq;
using NUnit.Framework;
using SS3D.Core;
using SS3D.Systems.Combat;
using SS3D.Systems.Entities;
using SS3D.Systems.Entities.Humanoid;
using SS3D.Systems.Entities.Humanoid.Body;
using SS3D.Systems.Furniture;
using SS3D.Systems.Health;
using SS3D.Systems.Interactions;
using SS3D.Systems.Inventory.Containers;
using SS3D.Systems.Inventory.Items;
using SS3D.Systems.Tile;
using SS3D.Interactions;
using UnityEngine;
using UnityEngine.TestTools;

namespace SS3D.Tests
{
    // Guards: dragging on a rope (Documents/plans/drag-and-zero-g.plan.md, M13): a Downed body trails and holds still with
    // the hand reserved, a corpse slides whole, a locker trails, stops at a wall and snags, and a dragged corpse holds an
    // airlock open · Not cheaper because: the interaction's server path, the movers on the body's physics authority, the
    // furniture sweep against tile colliders and the airlock's doorway check only meet on spawned, networked objects
    [Category(TestCategories.Scenarios)]
    [Category(TestCategories.Drag)]
    public class DragScenarios : ScenarioFixture
    {
        private static readonly BodyZone[] CenterMass = { BodyZone.Chest, BodyZone.Groin };
        private const string WrenchAsset = "772da386eb7b01745913050fee544c32";

        private static TileSubSystem Tiles => SubSystems.Get<TileSubSystem>();

        [UnityTest]
        public IEnumerator DownedBody_TrailsOnTheRope_HoldsStill_AndLetsGo()
        {
            Entity dummy = SpawnDummy(new Vector3(0f, Body.transform.position.y, -2f));
            yield return Undress(dummy);
            BodyDraggable drag = dummy.GetComponent<BodyDraggable>();
            Assert.IsNotNull(drag, "Human.prefab carries BodyDraggable");
            Ragdoll ragdoll = dummy.GetComponent<Ragdoll>();
            HumanoidLivingController controller = Body.GetComponent<HumanoidLivingController>();
            Hand hand = Body.GetComponent<Hands>().SelectedHand;

            StandBeside(drag, 1f);
            Assert.IsNotNull(Run(dummy.gameObject, "Drag"), "A standing body offers no drag");

            HumanHealthController health = dummy.GetComponent<HumanHealthController>();
            for (int i = 0; i < 7; i++)
            {
                CombatInteractionNetwork.ServerApplyLivingHit(RangedWeaponProfile.M4, health, CenterMass[i % 2]);
            }

            yield return WaitFor(() => ragdoll.IsDowned && !ragdoll.IsFallingDowned, "the dummy lies Downed", 12f);
            StandBeside(drag, 1f);
            yield return Hold(0.2f);

            Assert.IsNull(Run(dummy.gameObject, "Drag"), "An empty hand drags a Downed body");
            yield return WaitFor(() => drag.Dragged, "the drag starts");
            Assert.IsTrue(controller.IsDragging, "The dragger is dragging");
            Assert.IsFalse(controller.IsRunning, "No sprint while dragging");
            yield return WaitFor(() => dummy.GetComponent<HumanoidBodyStateMachine>().Snapshot.IsBeingDragged, "the body knows it's dragged");
            Item wrench = SubSystems.Get<ItemSubSystem>().SpawnItem(WrenchAsset, Body.transform.position, Quaternion.identity);
            Assert.IsFalse(hand.Container.CanContainItem(wrench), "The dragging hand stays empty");

            Vector3 start = drag.BodyPosition;
            yield return WalkTo(Body.transform.position + Away(drag) * 1.6f);
            yield return Hold(1f);
            Assert.That(Planar(drag.BodyPosition - start), Is.GreaterThan(0.8f), "The body follows on the rope");
            Assert.That(Planar(drag.BodyPosition - Body.transform.position), Is.LessThan(1.4f), "It trails about a metre behind");
            Assert.IsTrue(ragdoll.IsDowned && !ragdoll.IsKnockedDown, "It is still Downed, dragged on its controller, not ragdolled");

            controller.CmdReleaseDrag();
            yield return WaitFor(() => !drag.Dragged, "Drop on the dragging hand lets go");
            Assert.IsFalse(controller.IsDragging);
            Assert.IsTrue(hand.Container.CanContainItem(wrench), "The hand is free again");
            yield return WaitFor(() => !dummy.GetComponent<HumanoidBodyStateMachine>().Snapshot.IsBeingDragged, "the body is let go");
            wrench.Delete();
        }

        [UnityTest]
        public IEnumerator Corpse_SlidesWholeOnTheRope()
        {
            Entity dummy = SpawnDummy(new Vector3(0f, Body.transform.position.y, -2f));
            yield return Hold(2f); // killing a body the frame it spawns throws (entities Pitfalls)
            dummy.GetComponent<Human>().Kill();
            Ragdoll ragdoll = dummy.GetComponent<Ragdoll>();
            yield return WaitFor(() => ragdoll.Presentation == BodyPresentationState.Dead, "the dummy dies");
            yield return Hold(1.5f);
            BodyDraggable drag = dummy.GetComponent<BodyDraggable>();

            StandBeside(drag, 1f);
            yield return Hold(0.2f);
            Assert.IsNull(Run(dummy.gameObject, "Drag"), "A corpse drags");
            yield return WaitFor(() => drag.Dragged, "the drag starts");

            Vector3 start = drag.BodyPosition;
            yield return WalkTo(Body.transform.position + Away(drag) * 1.6f);
            yield return Hold(1.5f);
            Assert.That(Planar(drag.BodyPosition - start), Is.GreaterThan(0.8f), "The corpse follows on the rope");

            Transform hips = drag.Hips;
            float spread = dummy.GetComponentsInChildren<RagdollPart>().Max(part => Vector3.Distance(part.transform.position, hips.position));
            Assert.That(spread, Is.LessThan(1.2f), "No bone is stretched away from the hips");
            Body.GetComponent<HumanoidLivingController>().CmdReleaseDrag();
            yield return WaitFor(() => !drag.Dragged, "the dragger lets go");
        }

        [UnityTest]
        public IEnumerator Locker_TrailsOnTheRope_StopsAtAWall_AndSnags()
        {
            ChangesRoom = true;
            var lockerSo = (TileObjectSo)Tiles.GetAsset("Locker");
            Assert.IsTrue(Tiles.CurrentMap.PlaceTileObject(lockerSo, new Vector3(1f, 0f, -2f), Direction.North, true, false, false,
                out GameObject locker), "A locker places in the room");
            Draggable draggable = locker.GetComponent<Draggable>();
            Assert.IsNotNull(draggable, "Lockers are draggable");
            yield return Hold(0.3f);

            Teleport(Body, new Vector3(1f, Body.transform.position.y, -1f));
            Face(locker.transform.position);
            yield return Hold(0.2f);
            Assert.IsNull(Run(locker, "Drag"), "An empty hand drags the locker");
            yield return WaitFor(() => draggable.Dragged, "the drag starts");

            // A wall goes up on the locker's way west: the rope pulls it, the sweep stops it there.
            var wallSo = (TileObjectSo)Tiles.GetAsset("SteelWall");
            Assert.IsTrue(Tiles.CurrentMap.PlaceTileObject(wallSo, new Vector3(0f, 0f, -2f), Direction.North, true, true, false, out _),
                "A wall goes up in the locker's way");
            yield return Hold(0.3f);

            float westmost = locker.transform.position.x;
            Vector3 lockerStart = locker.transform.position;
            yield return WalkTo(new Vector3(-1.9f, 0f, -1f), during: () => westmost = Mathf.Min(westmost, locker.transform.position.x));
            Assert.That(Planar(locker.transform.position - lockerStart), Is.GreaterThan(0.05f), "The locker follows until the wall");
            Assert.That(westmost, Is.GreaterThan(0.5f), "It never enters the wall's tile");
            yield return WaitFor(() => !draggable.Dragged, "held back more than 2.5 m for a second, the drag snags", 3f);
            Assert.IsFalse(Body.GetComponent<HumanoidLivingController>().IsDragging);
        }

        [UnityTest]
        public IEnumerator DraggedCorpse_InTheDoorway_HoldsTheAirlockOpen()
        {
            AirLockOpener door = Object.FindObjectsByType<AirLockOpener>(FindObjectsSortMode.None).SingleOrDefault();
            Assert.IsNotNull(door, "The test room has one airlock");
            yield return WaitFor(() => door.IsPowered, "the door's APC powers it");
            Assert.IsTrue(door.ServerDebugHoldOpen(), "The door opens");

            // The door joins the main room (west) and the side room (east) along x.
            Vector3 doorway = door.transform.position;
            float east = Mathf.Sign(ScenarioAtmos.SideRoom.center.x - doorway.x);
            Entity dummy = SpawnDummy(new Vector3(doorway.x - east * 1.4f, Body.transform.position.y, doorway.z));
            yield return Hold(2f);
            dummy.GetComponent<Human>().Kill();
            Ragdoll ragdoll = dummy.GetComponent<Ragdoll>();
            yield return WaitFor(() => ragdoll.Presentation == BodyPresentationState.Dead, "the dummy dies in the main room");
            yield return Hold(1.5f);
            BodyDraggable drag = dummy.GetComponent<BodyDraggable>();

            Teleport(Body, new Vector3(drag.BodyPosition.x - east * 1f, Body.transform.position.y, doorway.z));
            Face(drag.BodyPosition);
            yield return Hold(0.2f);
            Assert.IsNull(Run(dummy.gameObject, "Drag"), "The corpse drags");
            yield return WaitFor(() => drag.Dragged, "the drag starts");

            // Through the door: the corpse trails a metre behind, so it ends up lying in the doorway.
            yield return WalkTo(new Vector3(doorway.x + east * 1.5f, 0f, doorway.z));
            yield return Hold(1.5f);
            Assert.That(Mathf.Abs(drag.BodyPosition.x - doorway.x), Is.LessThan(0.65f), "The corpse lies in the doorway");

            door.ServerCloseFromInteraction();
            yield return Hold(1f);
            Assert.IsTrue(door.IsOpen, "The door does not close on the dragged corpse in its doorway");

            yield return WalkTo(new Vector3(doorway.x + east * 3.3f, 0f, doorway.z));
            yield return WaitFor(() => !door.IsOpen, "pulled clear of the doorway, the door closes", 4f);
            Body.GetComponent<HumanoidLivingController>().CmdReleaseDrag();
        }

        private Entity SpawnDummy(Vector3 at) => SubSystems.Get<EntitySubSystem>().ServerSpawnCombatDummy(at, Quaternion.identity);

        /// <summary>Takes off the dummy's security jumpsuit, which is armor (ShockScenarios does the same).</summary>
        private static IEnumerator Undress(Entity dummy)
        {
            yield return Hold(0.5f);
            foreach (Item worn in dummy.GetComponent<HumanInventory>().Containers
                .Where(container => container.ContainerType == ContainerType.Jumpsuit)
                .SelectMany(container => container.Items).ToList())
            {
                worn.Delete();
            }

            yield return Hold(0.3f);
        }

        /// <summary>The room's centre side of the target, so the walk away from it stays inside the room.</summary>
        private static Vector3 Away(BodyDraggable target)
        {
            Vector3 toCentre = new Vector3(ScenarioAtmos.MainRoom.center.x, 0f, ScenarioAtmos.MainRoom.center.y) - target.BodyPosition;
            toCentre.y = 0f;
            return toCentre.sqrMagnitude > 0.01f ? toCentre.normalized : Vector3.forward;
        }

        /// <summary>Puts the host <paramref name="distance"/> from the target on its room-centre side, facing it.</summary>
        private void StandBeside(BodyDraggable target, float distance)
        {
            Vector3 at = target.BodyPosition + Away(target) * distance;
            Teleport(Body, new Vector3(at.x, Body.transform.position.y, at.z));
            Face(target.BodyPosition);
        }

        private void Face(Vector3 point)
        {
            Vector3 look = point - Body.transform.position;
            look.y = 0f;
            Body.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
        }

        /// <summary>Moves the host at walking pace with small teleports, so the rope is pulled as a walk would.</summary>
        private IEnumerator WalkTo(Vector3 to, System.Action during = null)
        {
            Vector3 from = Body.transform.position;
            to.y = from.y;
            float length = Vector3.Distance(from, to);
            for (float walked = 0f; walked < length; walked += 1.4f * Time.deltaTime)
            {
                Teleport(Body, Vector3.Lerp(from, to, walked / length));
                during?.Invoke();
                yield return null;
            }

            Teleport(Body, to);
            during?.Invoke();
        }

        private static float Planar(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        private string Run(GameObject target, string genericName) =>
            Body.GetComponent<InteractionController>()
                .RunInteractionForTesting(target, target.transform.position, genericName, IntentType.Help);
    }
}

using System.Collections;
using FishNet;
using NUnit.Framework;
using SS3D.Core;
using SS3D.Core.Behaviours;
using SS3D.Systems.Combat;
using SS3D.Systems.Entities;
using SS3D.Systems.Entities.Humanoid;
using SS3D.Systems.Entities.Humanoid.Body;
using SS3D.Systems.Inventory.Containers;
using SS3D.Systems.Inventory.Items;
using SS3D.Systems.Screens;
using SS3D.Systems.Tile;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace SS3D.Tests
{
    /// <summary>
    /// Moving in zero gravity (Documents/plans/drag-and-zero-g.plan.md, M13): a knockdown over open space gets up floating,
    /// wind pushes a floating body but never floors it, a floating body pushes off a structure back onto the deck, a
    /// throw pushes the thrower, and station structures count as reach while lockers, items and bodies don't.
    /// </summary>
    public partial class EnvironmentScenarios
    {
        /// <summary>Open space well away from the test room (AirlockFloorScenarios uses the same spot and clears it).</summary>
        private static readonly Vector3 OpenSpace = new(40f, 0f, 40f);

        private const string WrenchAsset = "772da386eb7b01745913050fee544c32";

        // Guards: a wind/blast knockdown that runs out over open space gets up into Floating instead of ragdolling forever (R2; Ragdoll.MayRecoverFromKnockdown, ZeroGravityRules.WindKnockdownRecovery) · Not cheaper because: needs the server's knockdown timer, the owner's ragdoll and the living controller's float entry on one live body
        [UnityTest]
        public IEnumerator KnockdownOverOpenSpace_GetsUpFloating()
        {
            Ragdoll ragdoll = Body.GetComponent<Ragdoll>();
            HumanoidBodyStateMachine state = Body.GetComponent<HumanoidBodyStateMachine>();
            yield return FloatAt(OpenSpace);

            ragdoll.ServerApplyBlast(Vector3.right * 0.5f, knockdown: true);
            yield return WaitFor(() => ragdoll.Presentation == BodyPresentationState.Collapsed, "the blast floors the floating body");
            yield return WaitFor(() => ragdoll.Presentation == BodyPresentationState.Locomotion,
                "the knockdown runs out over open space and the body gets up", 10f);
            yield return WaitFor(() => !ragdoll.IsGettingUp && state.Snapshot.IsFloating, "it floats upright", 6f);
            Assert.AreEqual(HumanoidSupportState.Unsupported, HumanoidSpaceSupport.GetSupportAt(Body.transform.position),
                "It is still over open space");
        }

        // Guards: while Floating, wind adds to the coast and never knocks down (R3; Ragdoll.ServerApplyWind, Ragdoll.TickWind → AddCoastImpulse) · Not cheaper because: wind reaches the owner as a TargetRpc and moves it through the living controller's coast
        [UnityTest]
        public IEnumerator FloatingBody_WindPushesItAlong_NeverFloorsIt()
        {
            Ragdoll ragdoll = Body.GetComponent<Ragdoll>();
            HumanoidLivingController controller = Body.GetComponent<HumanoidLivingController>();
            yield return FloatAt(OpenSpace);

            Vector3 start = Body.transform.position;
            bool floored = false;
            float nextGust = 0f;
            for (float elapsed = 0f; elapsed < 1f; elapsed += Time.deltaTime)
            {
                if (elapsed >= nextGust)
                {
                    ragdoll.ServerApplyWind(Vector3.right * 30f);
                    nextGust += SS3D.Systems.Atmospherics.AtmosConstants.TickInterval;
                }

                floored |= ragdoll.IsKnockedDown;
                yield return null;
            }

            Assert.IsFalse(floored, "A violent gust never floors a floating body");
            Assert.That(Body.transform.position.x, Is.GreaterThan(start.x + 0.3f), "The wind carries it along");
            Assert.That(controller.CoastVelocity.x, Is.GreaterThan(0.5f), "It keeps drifting once the gust passes");
        }

        // Guards: a floating body within reach of a structure pushes off with low traction, crosses back onto the deck and stops floating; letting go of the keys there brakes (R4, Decision 8; ZeroGravityRules.TractionStep, ZeroGravityReach) · Not cheaper because: only real key input through the living controller's float branch and the tile colliders show it
        [UnityTest]
        public IEnumerator FloatingBody_PushesOffAStructure_BackOntoTheDeck()
        {
            HumanoidBodyStateMachine state = Body.GetComponent<HumanoidBodyStateMachine>();
            HumanoidLivingController controller = Body.GetComponent<HumanoidLivingController>();
            // W pushes along the camera's forward; put the island that way so the test needs no camera of its own.
            Vector3 forward = CameraForwardOnDeck();
            Vector3 island = OpenSpace;
            InputSettings.BackgroundBehavior background = InputSystem.settings.backgroundBehavior;
            InputSettings.EditorInputBehaviorInPlayMode editor = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                PlaceTile("Plenum", island);
                PlaceTile("TileGrey", island);
                yield return FloatAt(island - forward * 1.3f);
                Assert.IsTrue(ZeroGravityReach.IsStructureInReach(Body.transform.position + Vector3.up),
                    "The island's edge is within reach");

                float elapsed = 0f;
                while (state.Snapshot.IsFloating && elapsed < 6f)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Assert.IsFalse(state.Snapshot.IsFloating, "Pushing off reaches the deck and the body stops floating");

                // Back over open space, coasting: no keys within reach brakes it to a stop.
                yield return FloatAt(island - forward * 1.3f);
                controller.AddCoastImpulse(-forward * 1f);
                yield return Hold(0.1f);
                Assert.That(controller.CoastVelocity.magnitude, Is.GreaterThan(0.2f), "The body coasts away from the island");
                yield return WaitFor(() => controller.CoastVelocity.sqrMagnitude < 0.0001f, "letting go of the keys in reach brakes it", 2f);
            }
            finally
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = background;
                InputSystem.settings.editorInputBehaviorInPlayMode = editor;
                ClearTile(island, TileLayer.Plenum);
            }
        }

        // Guards: throwing while floating pushes the thrower the other way at the release frame (R6, Decision 20; CombatInteractionNetwork.ServerPushFloatingThrower → TargetThrowRecoil) · Not cheaper because: the push rides the live throw's release timing and a TargetRpc to the owner
        [UnityTest]
        public IEnumerator FloatingThrower_IsPushedTheOtherWay()
        {
            HumanoidLivingController controller = Body.GetComponent<HumanoidLivingController>();
            EmptyHands(Body);
            Hand hand = Body.GetComponent<Hands>().SelectedHand;
            Item wrench = SubSystems.Get<ItemSubSystem>().SpawnItem(WrenchAsset, Body.transform.position, Quaternion.identity);
            Assert.IsTrue(hand.Container.AddItem(wrench), "The wrench goes in the active hand");
            yield return FloatAt(OpenSpace + new Vector3(20f, 0f, 0f));
            Assert.That(controller.CoastVelocity.magnitude, Is.LessThan(0.05f), "It floats still before the throw");

            Vector3 aim = Body.transform.position + new Vector3(5f, 0f, 0f);
            Assert.IsTrue(Body.GetComponent<CombatInteractionNetwork>().TryThrowAt(aim), "A floating body can throw");
            yield return WaitFor(() => hand.ItemInHand == null, "the wrench leaves the hand", 3f);
            yield return Hold(0.2f);

            float expected = ZeroGravityRules.ThrowDeltaV(ThrowRules.ThrownWeight(wrench));
            Assert.That(controller.CoastVelocity.x, Is.LessThan(-0.5f), "The throw pushes the thrower back");
            Assert.AreEqual(expected, controller.CoastVelocity.magnitude, 0.3f, "By the weight's Δv");
            wrench.Delete();
        }

        // Guards: station structures count as reach and things that move don't (R5, Decision 46; ZeroGravityReach on the Default and Walls layers) · Not cheaper because: the layers and PlacedTileObject parents come from the real tile prefabs
        [UnityTest]
        public IEnumerator Structures_CountAsReach_LockersItemsAndBodiesDoNot()
        {
            string[] structures = { "SteelWall", "SteelWindow", "SteelGirder", "CivillianAirlock", "Lattice", "Plenum" };
            Vector3[] spots = new Vector3[structures.Length];
            Vector3 lockerSpot = OpenSpace + new Vector3(0f, 0f, 12f);
            Item wrench = null;
            try
            {
                for (int i = 0; i < structures.Length; i++)
                {
                    spots[i] = OpenSpace + new Vector3(i * 4f, 0f, 6f);
                    if (structures[i] != "Plenum" && structures[i] != "Lattice")
                    {
                        PlaceTile("Plenum", spots[i]);
                    }

                    PlaceTile(structures[i], spots[i]);
                }

                PlaceTile("Locker", lockerSpot);
                wrench = SubSystems.Get<ItemSubSystem>().SpawnItem(WrenchAsset, OpenSpace + new Vector3(0f, 0.2f, 18f), Quaternion.identity);
                Teleport(Body, OpenSpace + new Vector3(0f, Body.transform.position.y, 24f));
                yield return Hold(0.5f);

                for (int i = 0; i < structures.Length; i++)
                {
                    // Beside the structure's tile, over the next (empty) tile, at body-centre height.
                    Assert.IsTrue(ZeroGravityReach.IsStructureInReach(spots[i] + new Vector3(1.2f, 0.9f, 0f)),
                        $"{structures[i]} is within reach 1.2 m away");
                    Assert.IsFalse(ZeroGravityReach.IsStructureInReach(spots[i] + new Vector3(0f, 0.9f, -3f)),
                        $"{structures[i]} is out of reach 3 m away");
                }

                Assert.IsFalse(ZeroGravityReach.IsStructureInReach(lockerSpot + new Vector3(1.2f, 0.9f, 0f)), "A locker is not reach");
                Assert.IsFalse(ZeroGravityReach.IsStructureInReach(wrench.transform.position + new Vector3(0.5f, 0.5f, 0f)), "An item is not reach");
                Assert.IsFalse(ZeroGravityReach.IsStructureInReach(Body.transform.position + Vector3.up), "A body (its own) is not reach");
            }
            finally
            {
                if (wrench != null)
                {
                    wrench.Delete();
                }

                ClearTile(lockerSpot, TileLayer.Plenum);
                Tiles.CurrentMap.ClearTileObject(lockerSpot, ((TileObjectSo)Tiles.GetAsset("Locker")).layer, Direction.North);
                foreach (Vector3 spot in spots)
                {
                    ClearTile(spot, TileLayer.Plenum);
                }
            }
        }

        private static TileSubSystem Tiles => SubSystems.Get<TileSubSystem>();

        /// <summary>Puts back the invincibility <see cref="FloatAt"/> suspended.</summary>
        private System.Action _invincibilityRestore;

        [TearDown]
        public void RestoreSuspendedInvincibility()
        {
            _invincibilityRestore?.Invoke();
            _invincibilityRestore = null;
        }

        /// <summary>Moves the body over <paramref name="position"/> (deck height kept) and waits for it to float there.</summary>
        private IEnumerator FloatAt(Vector3 position)
        {
            HumanoidBodyStateMachine state = Body.GetComponent<HumanoidBodyStateMachine>();
            // Invincible walkers never float. A map-editor scenario earlier in the category (GasMarker_…) leaves the
            // builder's session invincibility on, and a later one counts on it, so suspend it here and restore it after.
            if (_invincibilityRestore == null)
            {
                int clientId = Body.Owner.ClientId;
                bool server = EntityInvincibility.IsInvincible(Body) && InstanceFinder.IsServer;
                bool local = EntityInvincibility.LocalPlayerInvincible;
                _invincibilityRestore = () =>
                {
                    if (server)
                    {
                        EntityInvincibility.SetServer(clientId, true);
                    }

                    EntityInvincibility.LocalPlayerInvincible = local;
                };
            }

            EntityInvincibility.ClearServer(Body.Owner.ClientId);
            EntityInvincibility.LocalPlayerInvincible = false;
            Teleport(Body, new Vector3(position.x, Body.transform.position.y, position.z));
            for (float waited = 0f; !state.Snapshot.IsFloating && waited < 3f; waited += Time.deltaTime)
            {
                yield return null;
            }

            HumanoidLivingController controller = Body.GetComponent<HumanoidLivingController>();
            Assert.IsTrue(state.Snapshot.IsFloating, "The body floats over open space; it is at "
                + $"{Body.transform.position}, support {HumanoidSpaceSupport.GetSupportFor(controller, Body.transform.position)}, "
                + $"controller enabled {controller.enabled}, can move {state.Capabilities.CanMove}, owner {controller.IsOwner}, "
                + $"presentation {Body.GetComponent<Ragdoll>().Presentation}, locomotion {state.Snapshot.Locomotion}");
            yield return Hold(0.2f);
        }

        private static Vector3 CameraForwardOnDeck()
        {
            Actor camera = SubSystems.TryGet(out CameraSubSystem cameras) ? cameras.PlayerCamera : null;
            Vector3 forward = camera != null ? Vector3.Cross(camera.Right, Vector3.up).normalized : Vector3.forward;
            return forward.sqrMagnitude > 0.5f ? forward : Vector3.forward;
        }

        private static void PlaceTile(string assetName, Vector3 position)
        {
            var tile = (TileObjectSo)Tiles.GetAsset(assetName);
            Assert.IsNotNull(tile, $"{assetName} is in the tile catalog");
            Assert.IsTrue(Tiles.CurrentMap.PlaceTileObject(tile, position, Direction.North, true, false, false, out _),
                $"{assetName} places at {position}");
        }

        /// <summary>Clearing a plenum takes everything above it along.</summary>
        private static void ClearTile(Vector3 position, TileLayer layer) =>
            Tiles.CurrentMap.ClearTileObject(position, layer, Direction.North);
    }
}

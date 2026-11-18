using System;
using System.Collections;
using FishNet;
using SS3D.Core;
using SS3D.Systems.Entities;
using SS3D.Systems.Entities.Humanoid;
using SS3D.Systems.Entities.Humanoid.Body;
using SS3D.Systems.Combat;
using SS3D.Systems.Furniture;
using SS3D.Systems.Health;
using SS3D.Systems.IngameConsoleSystem.Commands;
using UnityEngine;

namespace SS3D.Systems.Testing
{
    /// <summary>
    /// Automation steps for M13's remote proof (Documents/plans/drag-and-zero-g.plan.md rig-proof): mppm:drag and the
    /// drag smoke line drag a remote client's Downed body through an airlock, mppm:drag also drops the owner mid-drag,
    /// and mppm:float floats a remote body and blows wind at it. The dragger is a server-side combat dummy, so the
    /// same server steps run on an MPPM host and on a dedicated server.
    /// </summary>
    public static class DragAutomation
    {
        /// <summary>Walking pace of the dummy dragger.</summary>
        private const float DraggerSpeed = 1.2f;

        /// <summary>How far from the door's centre the bodies start and end, on either side.</summary>
        private const float DoorStandOff = 1.8f;

        public static bool Handles(string opcode) => opcode is "stand_by_airlock" or "drag_client_through_airlock"
            or "drag_client_until_disconnect" or "assert_dragged" or "float_self" or "assert_floating_wind";

        public static IEnumerator Run(string opcode, float timeoutSeconds) => opcode switch
        {
            "stand_by_airlock" => StandByAirlock(),
            "drag_client_through_airlock" => DragRemoteThroughAirlock(timeoutSeconds),
            "drag_client_until_disconnect" => DragRemoteUntilOwnerLeaves(timeoutSeconds),
            "assert_dragged" => AssertOwnBodyDragged(timeoutSeconds),
            "float_self" => FloatOwnBody(timeoutSeconds),
            "assert_floating_wind" => AssertFloatingWind(timeoutSeconds),
            _ => throw new InvalidOperationException($"DragAutomation has no step {opcode}"),
        };

        /// <summary>Client: moves the owned body beside the nearest powered inner airlock, on the side it stands.</summary>
        private static IEnumerator StandByAirlock()
        {
            Entity own = OwnBody();
            AirLockOpener door = NearestInnerDoor(own.transform.position, "stand_by_airlock");
            Vector3 axis = TraversalAxis(door, own.transform.position);
            Vector3 spot = door.transform.position + axis * DoorStandOff;
            Teleport(own.transform, new Vector3(spot.x, own.transform.position.y, spot.z));
            yield return new WaitForSeconds(1f);
        }

        /// <summary>
        /// Server: a dummy drags the first remote body (already Downed) from its side of the nearest airlock to the other,
        /// holding the door open; fails if the body ragdolls, the drag ends early or the body doesn't come through.
        /// </summary>
        private static IEnumerator DragRemoteThroughAirlock(float timeoutSeconds)
        {
            BodyDraggable body = RemoteBody("drag_client_through_airlock");
            Ragdoll ragdoll = body.GetComponent<Ragdoll>();
            yield return Wait(() => ragdoll.IsDowned && !ragdoll.IsFallingDowned, timeoutSeconds, "the remote body lying Downed");

            AirLockOpener door = NearestInnerDoor(body.BodyPosition, "drag_client_through_airlock");
            Vector3 axis = TraversalAxis(door, body.BodyPosition);
            Vector3 doorway = door.transform.position;
            Entity dragger = null;
            yield return SpawnDragger(body, doorway + axis * (DoorStandOff - 1f), spawned => dragger = spawned);
            try
            {
                Vector3 farSide = doorway - axis * (DoorStandOff + 1f);
                yield return WalkDragger(dragger, body, farSide, () => door.ServerDebugHoldOpen(), timeoutSeconds);
                yield return HoldDown(body, 2f);

                float side = Vector3.Dot(body.BodyPosition - doorway, axis);
                if (side > -0.3f)
                {
                    throw new InvalidOperationException($"drag_client_through_airlock: the body stayed {side:F2} m on its own side of the door");
                }

                if (ragdoll.IsKnockedDown || !ragdoll.IsDowned)
                {
                    throw new InvalidOperationException($"drag_client_through_airlock: the body is {ragdoll.Presentation}, not Downed");
                }

                Debug.Log($"[DragAutomation] dragged the remote body through {door.name}; it lies {-side:F2} m past the door");
                TestSignal.Emit(dragger, "DraggedThroughAirlock");
            }
            finally
            {
                Release(body, dragger);
            }
        }

        /// <summary>
        /// Server: a dummy starts dragging the first remote body, the owner disconnects (its script does), and the drag
        /// carries on with the server moving the orphaned body (Decision 22).
        /// </summary>
        private static IEnumerator DragRemoteUntilOwnerLeaves(float timeoutSeconds)
        {
            BodyDraggable body = RemoteBody("drag_client_until_disconnect");
            Ragdoll ragdoll = body.GetComponent<Ragdoll>();
            if (!ragdoll.IsDowned)
            {
                yield return DownedAutomation.DownRemoteBody(timeoutSeconds);
            }

            yield return Wait(() => ragdoll.IsDowned && !ragdoll.IsFallingDowned, timeoutSeconds, "the remote body lying Downed");

            AirLockOpener door = NearestInnerDoor(body.BodyPosition, "drag_client_until_disconnect");
            Vector3 axis = TraversalAxis(door, body.BodyPosition);
            Entity dragger = null;
            yield return SpawnDragger(body, body.BodyPosition - axis * 1f, spawned => dragger = spawned);
            try
            {
                TestSignal.Emit(dragger, "DragStarted");
                yield return Wait(() => { KeepDown(body); return !body.Owner.IsValid; }, timeoutSeconds, "the dragged body's owner to disconnect");
                if (!body.Dragged)
                {
                    throw new InvalidOperationException("drag_client_until_disconnect: the drag ended when the owner left");
                }

                Vector3 start = body.BodyPosition;
                yield return WalkDragger(dragger, body, door.transform.position, () => door.ServerDebugHoldOpen(), timeoutSeconds);
                yield return HoldDown(body, 2f);
                float moved = Planar(body.BodyPosition - start);
                if (moved < 0.4f)
                {
                    throw new InvalidOperationException($"drag_client_until_disconnect: the orphaned body moved only {moved:F2} m");
                }

                Debug.Log($"[DragAutomation] the server dragged the orphaned body {moved:F2} m");
                TestSignal.Emit(dragger, "DragSurvivedDisconnect");
            }
            finally
            {
                Release(body, dragger);
            }
        }

        /// <summary>Client: the owned body is dragged (the server's bit arrives), moves with it, and stays Downed, not ragdolled.</summary>
        private static IEnumerator AssertOwnBodyDragged(float timeoutSeconds)
        {
            Entity own = OwnBody();
            HumanoidBodyStateMachine state = own.GetComponent<HumanoidBodyStateMachine>();
            Ragdoll ragdoll = own.GetComponent<Ragdoll>();
            yield return Wait(() => state.Snapshot.IsBeingDragged, timeoutSeconds, "assert_dragged: the body being dragged");

            Vector3 start = own.transform.position;
            float moved = 0f;
            bool ragdolled = false;
            float began = Time.time;
            while (state.Snapshot.IsBeingDragged && Time.time - began < timeoutSeconds)
            {
                moved = Mathf.Max(moved, Planar(own.transform.position - start));
                ragdolled |= ragdoll.IsKnockedDown;
                yield return null;
            }

            if (ragdolled)
            {
                throw new InvalidOperationException("assert_dragged: the dragged Downed body ragdolled on its owner");
            }

            if (moved < 1.5f)
            {
                throw new InvalidOperationException($"assert_dragged: the owner saw its body move only {moved:F2} m while dragged");
            }

            Debug.Log($"[DragAutomation] the owner's body moved {moved:F2} m while dragged");
            TestSignal.Emit(own, "DragSeen");
        }

        /// <summary>Client: moves the owned body far out over open space and waits for the server's Floating to arrive.</summary>
        private static IEnumerator FloatOwnBody(float timeoutSeconds)
        {
            Entity own = OwnBody();
            HumanoidBodyStateMachine state = own.GetComponent<HumanoidBodyStateMachine>();
            Teleport(own.transform, own.transform.position + new Vector3(300f, 0f, 300f));
            yield return Wait(() => state.Snapshot.IsFloating, timeoutSeconds, "float_self: the body floating");
            TestSignal.Emit(own, "Floating");
        }

        /// <summary>Client: the server's wind carries the floating owned body along and never floors it (R3).</summary>
        private static IEnumerator AssertFloatingWind(float timeoutSeconds)
        {
            Entity own = OwnBody();
            Ragdoll ragdoll = own.GetComponent<Ragdoll>();
            HumanoidLivingController controller = own.GetComponent<HumanoidLivingController>();
            Vector3 start = own.transform.position;
            bool floored = false;
            float moved = 0f;
            float began = Time.time;
            while (moved < 1f && Time.time - began < timeoutSeconds)
            {
                floored |= ragdoll.IsKnockedDown;
                moved = Planar(own.transform.position - start);
                yield return null;
            }

            if (floored)
            {
                throw new InvalidOperationException("assert_floating_wind: wind floored a floating body");
            }

            if (moved < 1f)
            {
                throw new InvalidOperationException($"assert_floating_wind: the wind moved the floating body only {moved:F2} m");
            }

            Debug.Log($"[DragAutomation] wind carried the floating body {moved:F2} m, coast {controller.CoastVelocity.magnitude:F2} m/s");
            TestSignal.Emit(own, "FloatingWindFelt");
        }

        private static readonly BodyZone[] TopUpZones =
            { BodyZone.LeftArm, BodyZone.RightArm, BodyZone.LeftLeg, BodyZone.RightLeg, BodyZone.Groin, BodyZone.Chest };

        private static int _topUps;

        /// <summary>
        /// Holds a Downed body down for the test: down_client stops at the Downed line, and shock decays to the stand line
        /// within seconds, which ends a drag by its own rule (TargetStood). One round to a rotating zone tops it up.
        /// </summary>
        private static void KeepDown(BodyDraggable body)
        {
            if (body == null || !body.TryGetComponent(out HumanHealthController health))
            {
                return;
            }

            HealthSnapshot snapshot = health.Snapshot;
            if (snapshot.State == HealthState.Downed && snapshot.Shock + snapshot.IncomingPain < HealthConstants.ShockDownedThreshold - 2f)
            {
                CombatInteractionNetwork.ServerApplyLivingHit(RangedWeaponProfile.M4, health, TopUpZones[_topUps++ % TopUpZones.Length]);
            }
        }

        /// <summary>Waits <paramref name="seconds"/>, keeping <paramref name="body"/> down.</summary>
        private static IEnumerator HoldDown(BodyDraggable body, float seconds)
        {
            for (float waited = 0f; waited < seconds; waited += Time.deltaTime)
            {
                KeepDown(body);
                yield return null;
            }
        }

        private static IEnumerator SpawnDragger(BodyDraggable body, Vector3 at, Action<Entity> spawned)
        {
            at.y = body.transform.position.y;
            Vector3 look = body.BodyPosition - at;
            look.y = 0f;
            Entity dragger = SubSystems.Get<EntitySubSystem>().ServerSpawnCombatDummy(at,
                look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity);
            spawned(dragger);
            yield return HoldDown(body, 2f);
            string refusal = DragCommand.TryStartDrag(dragger.GetComponent<HumanoidLivingController>(), body);
            if (refusal != null)
            {
                throw new InvalidOperationException($"the dummy can't drag the remote body: {refusal}");
            }
        }

        /// <summary>Moves the unowned dummy to <paramref name="to"/> at walking pace; the server owns its transform.</summary>
        private static IEnumerator WalkDragger(Entity dragger, BodyDraggable body, Vector3 to, Action each, float timeoutSeconds)
        {
            Vector3 from = dragger.transform.position;
            to.y = from.y;
            float length = Vector3.Distance(from, to);
            float began = Time.time;
            for (float walked = 0f; walked < length; walked += DraggerSpeed * Time.deltaTime)
            {
                if (!body.Dragged)
                {
                    throw new InvalidOperationException($"the drag ended {walked:F1} m into the walk (see the server log's 'Drag of' line)");
                }

                if (Time.time - began > timeoutSeconds)
                {
                    throw new TimeoutException("the dummy's walk timed out");
                }

                each?.Invoke();
                KeepDown(body);
                dragger.transform.position = Vector3.Lerp(from, to, walked / length);
                yield return null;
            }

            dragger.transform.position = to;
        }

        private static void Release(BodyDraggable body, Entity dragger)
        {
            if (body != null && body.Dragged && dragger != null)
            {
                body.ServerSetDrag(false, dragger.transform);
            }

            if (dragger != null && dragger.IsSpawned)
            {
                InstanceFinder.ServerManager.Despawn(dragger.gameObject);
            }
        }

        /// <summary>The nearest powered airlock that opens on approach: external ones lead to space.</summary>
        private static AirLockOpener NearestInnerDoor(Vector3 near, string step)
        {
            AirLockOpener best = null;
            float bestDistance = float.MaxValue;
            foreach (AirLockOpener door in UnityEngine.Object.FindObjectsByType<AirLockOpener>(FindObjectsSortMode.None))
            {
                float distance = Planar(door.transform.position - near);
                if (door.OpensOnApproach && door.IsPowered && distance < bestDistance && HasFloorBothSides(door))
                {
                    best = door;
                    bestDistance = distance;
                }
            }

            return best != null ? best : throw new InvalidOperationException($"{step}: no powered inner airlock with floor on both sides");
        }

        /// <summary>
        /// The planar axis through the doorway, pointing to <paramref name="side"/>'s side: of the door's two horizontal
        /// axes, the one without wall on either side (the wall line runs along the other).
        /// </summary>
        private static Vector3 TraversalAxis(AirLockOpener door, Vector3 side)
        {
            Vector3 forward = Flat(door.transform.forward);
            Vector3 right = Flat(door.transform.right);
            Vector3 axis = WallsAlong(door, right) >= WallsAlong(door, forward) ? forward : right;
            return Vector3.Dot(side - door.transform.position, axis) >= 0f ? axis : -axis;
        }

        private static int WallsAlong(AirLockOpener door, Vector3 direction)
        {
            int walls = 0;
            foreach (float sign in new[] { 1f, -1f })
            {
                Vector3 probe = door.transform.position + direction * sign + Vector3.up;
                foreach (Collider hit in Physics.OverlapSphere(probe, 0.3f, ZeroGravityReach.Mask, QueryTriggerInteraction.Ignore))
                {
                    if (!hit.transform.IsChildOf(door.transform))
                    {
                        walls++;
                        break;
                    }
                }
            }

            return walls;
        }

        private static bool HasFloorBothSides(AirLockOpener door)
        {
            Vector3 forward = Flat(door.transform.forward);
            Vector3 right = Flat(door.transform.right);
            Vector3 axis = WallsAlong(door, right) >= WallsAlong(door, forward) ? forward : right;
            return HasFloor(door.transform.position + axis * DoorStandOff) && HasFloor(door.transform.position - axis * (DoorStandOff + 1f));
        }

        private static bool HasFloor(Vector3 at) =>
            Physics.Raycast(at + Vector3.up, Vector3.down, 2f, ZeroGravityReach.Mask, QueryTriggerInteraction.Ignore);

        private static BodyDraggable RemoteBody(string step)
        {
            foreach (Entity body in SubSystems.Get<EntitySubSystem>().SpawnedPlayers)
            {
                if (body != null && !body.IsOwner && body.TryGetComponent(out BodyDraggable drag))
                {
                    return drag;
                }
            }

            throw new InvalidOperationException($"{step}: no remote body");
        }

        private static Entity OwnBody()
        {
            if (!SubSystems.Get<EntitySubSystem>().TryGetOwnedEntity(InstanceFinder.ClientManager.Connection, out Entity entity))
            {
                throw new InvalidOperationException("No owned body");
            }

            return entity;
        }

        private static void Teleport(Transform body, Vector3 position)
        {
            CharacterController controller = body.GetComponent<CharacterController>();
            bool wasEnabled = controller != null && controller.enabled;
            if (wasEnabled)
            {
                controller.enabled = false;
            }

            body.position = position;
            if (wasEnabled)
            {
                controller.enabled = true;
            }
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.normalized;
        }

        private static float Planar(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        private static IEnumerator Wait(Func<bool> condition, float timeoutSeconds, string label)
        {
            float start = Time.time;
            while (!condition())
            {
                if (Time.time - start > timeoutSeconds)
                {
                    throw new TimeoutException($"Timed out after {timeoutSeconds}s waiting for {label}");
                }

                yield return null;
            }
        }
    }
}

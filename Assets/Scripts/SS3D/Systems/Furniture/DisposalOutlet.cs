using Coimbra;
using FishNet;
using SS3D.Systems.IdAccess;
using SS3D.Systems.Inventory.Items;
using System.Collections.Generic;
using UnityEngine;

namespace SS3D.Systems.Furniture
{
    /// <summary>
    /// Disposal network terminal. The untagged/main outlet (<see cref="TargetDepartment"/> ==
    /// <see cref="Department.None"/>) ejects unclaimed arrivals into space after a grace window unless
    /// swept via <see cref="IDisposalSweepable"/> (design doc §6). Tagged department outlets just hold
    /// arrivals for pickup, no timer.
    /// </summary>
    public class DisposalOutlet : MonoBehaviour, IDisposalElement
    {
        private const float ClaimProximityMeters = 2.5f;

        [SerializeField]
        [Tooltip("Department.None marks this as the main/untagged outlet — the one with space ejection.")]
        private Department _targetDepartment = Department.None;

        [SerializeField]
        [Tooltip("How long an unclaimed item sits at the main outlet before ejection. Balancing value, left unset for now (design doc §12).")]
        private float _graceWindowSeconds = 30f;

        [SerializeField]
        [Tooltip("How far in front of the outlet (along its facing) arrivals are spat out.")]
        private float _spitDistance = 0.85f;

        [SerializeField]
        private Transform _spaceEjectionPoint;

        private readonly List<PendingArrival> _pendingArrivals = new();

        public GameObject GameObject => gameObject;

        public Department TargetDepartment => _targetDepartment;

        public bool IsMainOutlet => _targetDepartment == Department.None;

        private void Update()
        {
            if (!IsMainOutlet || _pendingArrivals.Count == 0)
            {
                return;
            }

            for (int i = _pendingArrivals.Count - 1; i >= 0; i--)
            {
                PendingArrival arrival = _pendingArrivals[i];
                arrival.RemainingSeconds -= Time.deltaTime;

                if (arrival.RemainingSeconds > 0f)
                {
                    _pendingArrivals[i] = arrival;
                    continue;
                }

                _pendingArrivals.RemoveAt(i);
                ResolveUnclaimed(arrival.Item);
            }
        }

        /// <summary>
        /// Called by the disposal subsystem when a capsule arrives at this outlet.
        /// </summary>
        public void OnItemArrived(Item item)
        {
            if (item == null)
            {
                return;
            }

            item.Unfreeze();
            SpitItemOut(item);

            if (IsMainOutlet)
            {
                _pendingArrivals.Add(new PendingArrival(item, _graceWindowSeconds));
            }

            // Tagged department outlets: item sits in front of the outlet for pickup, no timer (§6).
        }

        /// <summary>
        /// Places the item just in front of the outlet along its facing direction (tile rotation).
        /// </summary>
        private void SpitItemOut(Item item)
        {
            Vector3 spitPosition = transform.position + GetFacingDirection() * _spitDistance;
            Quaternion spitRotation = Quaternion.LookRotation(GetFacingDirection(), Vector3.up);
            item.transform.SetPositionAndRotation(spitPosition, spitRotation);
        }

        private Vector3 GetFacingDirection()
        {
            // Tile objects are yaw-rotated to their Direction; flatten in case of slight pitch.
            Vector3 facing = transform.forward;
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.0001f)
            {
                return Vector3.forward;
            }

            return facing.normalized;
        }

        private void ResolveUnclaimed(Item item)
        {
            if (!StillUnclaimedAtOutlet(item))
            {
                return;
            }

            foreach (IDisposalSweepable sweepable in GetComponents<IDisposalSweepable>())
            {
                if (sweepable.TrySweep(this, item))
                {
                    return;
                }
            }

            EjectIntoSpace(item);
        }

        /// <summary>
        /// True while the arrival is still a world item near this outlet. Picked-up / moved items
        /// must not be teleported or despawned when the grace window expires.
        /// </summary>
        private bool StillUnclaimedAtOutlet(Item item)
        {
            if (item == null)
            {
                return false;
            }

            if (item.IsInContainer())
            {
                return false;
            }

            float distance = Vector3.Distance(item.transform.position, transform.position);
            return distance <= ClaimProximityMeters;
        }

        private void EjectIntoSpace(Item item)
        {
            if (_spaceEjectionPoint != null)
            {
                item.transform.SetPositionAndRotation(_spaceEjectionPoint.position, item.transform.rotation);
            }

            // Honest discard: the item is gone, not quietly left at an offset (design §6).
            // Item.Delete assumes a container; world arrivals despawn/dispose directly.
            if (InstanceFinder.ServerManager != null)
            {
                InstanceFinder.ServerManager.Despawn(item.GameObject);
            }
            else
            {
                item.GameObject.Dispose(true);
            }
        }

        private struct PendingArrival
        {
            public readonly Item Item;
            public float RemainingSeconds;

            public PendingArrival(Item item, float remainingSeconds)
            {
                Item = item;
                RemainingSeconds = remainingSeconds;
            }
        }
    }
}

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
        [SerializeField]
        [Tooltip("Department.None marks this as the main/untagged outlet — the one with space ejection.")]
        private Department _targetDepartment = Department.None;

        [SerializeField]
        [Tooltip("How long an unclaimed item sits at the main outlet before ejection. Balancing value, left unset for now (design doc §12).")]
        private float _graceWindowSeconds = 30f;

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
            item.transform.SetPositionAndRotation(transform.position, transform.rotation);

            if (IsMainOutlet)
            {
                _pendingArrivals.Add(new PendingArrival(item, _graceWindowSeconds));
            }

            // Tagged department outlets: item just sits here for pickup, no timer (§6).
        }

        private void ResolveUnclaimed(Item item)
        {
            if (item == null)
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

        private void EjectIntoSpace(Item item)
        {
            Vector3 target = _spaceEjectionPoint != null ? _spaceEjectionPoint.position : transform.position;
            item.transform.SetPositionAndRotation(target, item.transform.rotation);
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

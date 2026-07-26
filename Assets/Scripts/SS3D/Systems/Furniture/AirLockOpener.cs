using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using SS3D.Core;
using SS3D.Systems.Electricity;
using SS3D.Systems.Entities;
using SS3D.Systems.Inventory.Containers;
using SS3D.Systems.Tile;
using UnityEngine;

namespace SS3D.Systems.Furniture
{
    /// <summary>
    /// Opens when an authorized character is in the door volume, stays open while occupied,
    /// and closes after a delay when empty.
    /// </summary>
    /// <remarks>
    /// Proximity uses spawned entity transforms against the door trigger (with padding), not
    /// CharacterController physics triggers. Remotes are NetworkTransformed on the server;
    /// CC.Move never runs for them, so OnTriggerEnter / OverlapBox against the CC miss them.
    /// </remarks>
    public class AirLockOpener : NetworkBehaviour, IDynamicTileOccupant
    {
        /// <summary>
        /// Seconds before the door starts closing once the volume is empty.
        /// </summary>
        private const float DoorWaitCloseTime = 2.0f;

        /// <summary>
        /// Sample slightly above the feet so the point sits near the door volume centerline.
        /// </summary>
        private const float OccupantSampleHeight = 0.5f;

        /// <summary>
        /// Extra reach beyond the trigger collider. Closed door solids stop characters ~5–10cm
        /// outside the OBB, so a strict contains check never authorizes.
        /// </summary>
        private const float ProximityPadding = 0.85f;

        private static readonly int OpenId = Animator.StringToHash("Open");

        [SerializeField]
        private Animator _animator;

        [SerializeField]
        private BasicPowerConsumer _powerConsumer;

        [SerializeField]
        private LayerMask doorTriggerLayers = -1;

        /// <summary>Optional; if unset, first trigger collider under this object is used.</summary>
        [SerializeField]
        private Collider _proximityVolume;

        [SerializeField]
        private List<MeshRenderer> _meshesToColor;

        [SerializeField]
        private List<SkinnedMeshRenderer> _skinnedMeshesToColor;

        [SyncVar(OnChange = nameof(OnOpenChanged))]
        private bool _isOpen;

        private readonly HashSet<HumanInventory> _authorizedOccupants = new();

        private AirLockAccessGate _accessGate;
        private Coroutine _closeTimer;

        public ReadOnlyCollection<MeshRenderer> MeshesToColor => _meshesToColor.AsReadOnly();

        public ReadOnlyCollection<SkinnedMeshRenderer> SkinnedMeshesToColor => _skinnedMeshesToColor.AsReadOnly();

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (_proximityVolume == null)
            {
                Collider[] colliders = GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < colliders.Length; i++)
                {
                    if (colliders[i] != null && colliders[i].isTrigger)
                    {
                        _proximityVolume = colliders[i];
                        break;
                    }
                }
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            _accessGate = GetComponent<AirLockAccessGate>();

            if (_powerConsumer == null)
            {
                TryGetComponent(out _powerConsumer);
            }

            if (_powerConsumer != null)
            {
                _powerConsumer.OnPowerStatusUpdated += HandlePowerStatusUpdated;
            }

            UpdateAnimator();
            NotifyTileStateChanged();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            UpdateAnimator();
            NotifyTileStateChanged();
        }

        public override void OnStopServer()
        {
            if (_powerConsumer != null)
            {
                _powerConsumer.OnPowerStatusUpdated -= HandlePowerStatusUpdated;
            }

            base.OnStopServer();
        }

        private void FixedUpdate()
        {
            if (!IsServer)
            {
                return;
            }

            ServerUpdateProximity();
        }

        [Server]
        private void ServerUpdateProximity()
        {
            if (_proximityVolume == null)
            {
                return;
            }

            if (!IsPowered())
            {
                if (_authorizedOccupants.Count > 0)
                {
                    _authorizedOccupants.Clear();
                    ScheduleCloseAfterDelay();
                }

                return;
            }

            if (!SubSystems.TryGet(out EntitySubSystem entitySubSystem))
            {
                return;
            }

            bool wasEmpty = _authorizedOccupants.Count == 0;
            _authorizedOccupants.Clear();

            List<Entity> spawnedPlayers = entitySubSystem.SpawnedPlayers;
            for (int i = 0; i < spawnedPlayers.Count; i++)
            {
                Entity entity = spawnedPlayers[i];
                if (entity == null)
                {
                    continue;
                }

                if ((doorTriggerLayers.value & (1 << entity.gameObject.layer)) == 0)
                {
                    continue;
                }

                Vector3 sample = entity.transform.position + Vector3.up * OccupantSampleHeight;
                if (!IsPointInProximity(sample))
                {
                    continue;
                }

                HumanInventory inventory = entity.GetComponent<HumanInventory>();
                if (inventory == null)
                {
                    continue;
                }

                if (_accessGate != null &&
                    !_accessGate.TryAuthorizeInventory(inventory, out _))
                {
                    continue;
                }

                _authorizedOccupants.Add(inventory);
            }

            bool isEmpty = _authorizedOccupants.Count == 0;
            if (wasEmpty && !isEmpty)
            {
                CancelCloseTimer();
                SetOpen(true);
            }
            else if (!wasEmpty && isEmpty)
            {
                ScheduleCloseAfterDelay();
            }
        }

        private bool IsPointInProximity(Vector3 worldPoint)
        {
            // ClosestPoint returns the point when inside; outside it returns the surface.
            // Padding covers the gap between the closed door mesh and the trigger volume.
            Vector3 closest = _proximityVolume.ClosestPoint(worldPoint);
            return (closest - worldPoint).sqrMagnitude <= ProximityPadding * ProximityPadding;
        }

        private IEnumerator RunCloseEventually(float time)
        {
            yield return new WaitForSeconds(time);
            _closeTimer = null;
            SetOpen(false);
        }

        [Server]
        private void SetOpen(bool open)
        {
            if (open && !IsPowered())
            {
                return;
            }

            _isOpen = open;
            UpdateAnimator();
        }

        private void OnOpenChanged(bool _, bool __, bool asServer)
        {
            UpdateAnimator();
            NotifyTileStateChanged();
        }

        private void UpdateAnimator()
        {
            if (_animator != null)
            {
                _animator.SetBool(OpenId, _isOpen);
            }
        }

        private void NotifyTileStateChanged()
        {
            SubSystems.Get<TileSubSystem>()?.NotifyTileStateChanged(transform.position);
        }

        private void HandlePowerStatusUpdated(object sender, PowerStatus newStatus)
        {
            if (!IsServer || newStatus == PowerStatus.Powered)
            {
                return;
            }

            ScheduleCloseAfterDelay();
        }

        private void ScheduleCloseAfterDelay()
        {
            if (_authorizedOccupants.Count > 0)
            {
                return;
            }

            // Do not restart a running timer. Power-status churn (or repeated exits) used to
            // reset the 2s delay forever so the door never closed.
            if (_closeTimer != null)
            {
                return;
            }

            _closeTimer = StartCoroutine(RunCloseEventually(DoorWaitCloseTime));
        }

        private void CancelCloseTimer()
        {
            if (_closeTimer == null)
            {
                return;
            }

            StopCoroutine(_closeTimer);
            _closeTimer = null;
        }

        private bool IsPowered()
        {
            return PowerGate.IsPowered(_powerConsumer, NullConsumerPolicy.Allow);
        }
    }
}

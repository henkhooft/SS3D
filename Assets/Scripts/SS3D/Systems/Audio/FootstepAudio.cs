using System.Linq;
using FishNet;
using FishNet.Object;
using SS3D.Core;
using SS3D.Data;
using SS3D.Data.Generated;
using SS3D.Logging;
using SS3D.Systems.Entities.Humanoid;
using SS3D.Systems.Inventory.Containers;
using SS3D.Systems.Inventory.Items;
using UnityEngine;

namespace SS3D.Systems.Audio
{
    /// <summary>
    /// Server-side footwear footsteps (audio.md §3). The provided Socks/Shoes/Boots clips are
    /// ~2s walking loops, so this starts a pooled looping source while move input is held and
    /// stops shortly after input ends. Added from <c>OnStartServer</c> — do not edit Human.prefab.
    /// </summary>
    public sealed class FootstepAudio : MonoBehaviour
    {
        private const float MovingHoldSeconds = 0.25f;
        private const float Volume = 0.85f;
        private const float MinRange = 1f;
        private const float MaxRange = 12f;

        private NetworkObject _networkObject;
        private HumanInventory _inventory;
        private Ragdoll _ragdoll;

        private string _playingTrackId = string.Empty;
        private bool _isPlaying;
        private float _lastMovingTime = float.NegativeInfinity;

        private void Awake()
        {
            _networkObject = GetComponent<NetworkObject>();
            _inventory = GetComponent<HumanInventory>();
            _ragdoll = GetComponent<Ragdoll>();
        }

        private void OnDisable()
        {
            if (_isPlaying && InstanceFinder.IsServer)
            {
                StopFootsteps();
            }
        }

        /// <summary>
        /// Called from <c>HumanoidPredictedMovement.Move</c> on ticks with planar input.
        /// </summary>
        public void ServerNotifyMoving(bool isRunning = false)
        {
            if (!InstanceFinder.IsServer)
            {
                return;
            }

            _lastMovingTime = Time.time;
        }

        private void Update()
        {
            if (!InstanceFinder.IsServer || _networkObject == null)
            {
                return;
            }

            bool shouldPlay = Time.time - _lastMovingTime <= MovingHoldSeconds
                && (_ragdoll == null || !_ragdoll.IsKnockedDown);

            if (!shouldPlay)
            {
                if (_isPlaying)
                {
                    StopFootsteps();
                }

                return;
            }

            string trackId = ResolveTrackId();
            if (_isPlaying && trackId == _playingTrackId)
            {
                return;
            }

            if (_isPlaying)
            {
                StopFootsteps();
            }

            StartFootsteps(trackId);
        }

        private string ResolveTrackId()
        {
            Item footwear = TryGetFootwearItem();
            if (footwear == null)
            {
                return FootstepAudioTrackIds.Socks;
            }

            string name = footwear.Name;
            if (name.IndexOf("boot", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return FootstepAudioTrackIds.Boots;
            }

            return FootstepAudioTrackIds.Shoes;
        }

        private Item TryGetFootwearItem()
        {
            if (_inventory == null)
            {
                return null;
            }

            if (TryFirstItem(ContainerType.ShoeLeft, out Item left))
            {
                return left;
            }

            if (TryFirstItem(ContainerType.ShoeRight, out Item right))
            {
                return right;
            }

            return null;
        }

        private bool TryFirstItem(ContainerType type, out Item item)
        {
            item = null;
            if (!_inventory.TryGetTypeContainer(type, 0, out AttachedContainer container) || container == null)
            {
                return false;
            }

            item = container.Items.FirstOrDefault();
            return item != null;
        }

        private void StartFootsteps(string trackId)
        {
            if (!Assets.TryGet(AssetDatabases.Sounds, trackId, out AudioClip _))
            {
                Log.Warning(this,
                    "Footstep clip '{id}' missing from Sounds database — select the mp3s in Unity so they import, then confirm Sounds.asset refs are not missing.",
                    Logs.Generic, trackId);
                _lastMovingTime = float.NegativeInfinity;
                return;
            }

            AudioSubSystem audio = SubSystems.Get<AudioSubSystem>();
            if (audio == null)
            {
                return;
            }

            audio.PlayAudioSource(
                AudioType.Sfx,
                trackId,
                transform.position,
                _networkObject,
                isLooping: true,
                volume: Volume,
                pitch: 1f,
                minRange: MinRange,
                maxRange: MaxRange);

            _playingTrackId = trackId;
            _isPlaying = true;
        }

        private void StopFootsteps()
        {
            SubSystems.Get<AudioSubSystem>()?.StopAudioSource(_networkObject);
            _playingTrackId = string.Empty;
            _isPlaying = false;
        }
    }
}

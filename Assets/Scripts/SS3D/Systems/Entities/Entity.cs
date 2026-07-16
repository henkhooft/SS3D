using System;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using SS3D.Core;
using SS3D.Core.Behaviours;
using SS3D.Systems.Entities.Character;
using SS3D.Systems.Entities.Events;
using SS3D.Systems.Networking;
using UnityEngine;

namespace SS3D.Systems.Entities
{
    /// <summary>
    /// Base class for all things that can be controlled by a player.
    /// </summary>
    [Serializable]
    public class Entity : NetworkActor
    {
        /// <summary>
        /// A reference point to use for this Entities perspective
        /// </summary>
        public GameObject ViewPoint;
        public event Action<Mind> OnMindChanged;

        [SerializeField]
        [SyncVar(OnChange = nameof(SyncMind))]
        private Mind _mind = Mind.Empty;

        [SyncVar]
        private string _characterName = string.Empty;

        [SyncVar]
        private int _hairStyleId;

        [SyncVar]
        private int _beardStyleId;

        [SyncVar]
        private int _skinToneIndex;

        [SyncVar]
        private int _hairColorIndex;

        [SyncVar(OnChange = nameof(SyncHasAppearance))]
        private bool _hasAppearance;

        public Mind Mind
        {
            get => _mind;
            set => _mind = value;
        }

        /// <summary>
        /// Display name from the session character sheet. Falls back to ckey when unset.
        /// </summary>
        public string CharacterName =>
            string.IsNullOrEmpty(_characterName)
                ? (_mind != null && _mind.player != null ? _mind.player.Ckey : string.Empty)
                : _characterName;

        public string Ckey => _mind.player.Ckey;

        private const float ObserverGridCheckIntervalSeconds = 0.25f;

        private Vector2Int? _lastObserverGridCell;
        private float _nextObserverGridCheckTime;

        protected override void OnStart()
        {
            base.OnStart();

            OnSpawn();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (_hasAppearance)
            {
                ApplyAppearanceLocal();
            }
        }

        private void Update()
        {
            if (!IsServer)
                return;

            NetworkConnection owner = Owner;
            if (!owner.IsValid || owner.FirstObject != NetworkObject)
                return;

            float now = Time.unscaledTime;
            if (now < _nextObserverGridCheckTime)
                return;

            _nextObserverGridCheckTime = now + ObserverGridCheckIntervalSeconds;

            Vector2Int gridCell = TileObserverConstants.GetHashGridCell(transform.position);
            if (_lastObserverGridCell == gridCell)
                return;

            _lastObserverGridCell = gridCell;
            ServerManager.Objects.RebuildObservers(owner, timedOnly: false);
        }

        private void OnSpawn()
        {
            OnMindChanged?.Invoke(Mind);
        }

        protected override void OnDestroyed()
        {
            base.OnDestroyed();

            if (IsOwner)
            {
                LocalPlayerObjectChanged localPlayerObjectChanged = new(GameObject, false);
                localPlayerObjectChanged.Invoke(this);
            }
        }

        private void InvokeLocalPlayerObjectChanged()
        {
            if (Mind == null || Mind.player == null) return;

            if (!Mind.player.IsLocalConnection)
            {
                return;
            }

            LocalPlayerObjectChanged localPlayerObjectChanged = new(GameObject, true);
            localPlayerObjectChanged.Invoke(this);
        }

        /// <summary>
        /// Called by FishNet when the value of _mind is synced.
        /// </summary>
        public void SyncMind(Mind oldMind, Mind newMind, bool asServer)
        {
            if (!asServer && IsHost)
            {
                return;
            }

            OnMindChanged?.Invoke(_mind);
            InvokeLocalPlayerObjectChanged();
        }

        public void SyncHasAppearance(bool oldValue, bool newValue, bool asServer)
        {
            if (!asServer && IsHost)
            {
                return;
            }

            if (!newValue)
            {
                return;
            }

            ApplyAppearanceLocal();
        }

        /// <summary>
        /// Updates the mind of this entity.
        /// </summary>
        [Server]
        public void SetMind(Mind mind)
        {
            this._mind = mind;
            if(mind == null) return;
            GiveOwnership(mind.Owner);
        }

        [Server]
        public void SetCharacterName(string characterName)
        {
            _characterName = string.IsNullOrWhiteSpace(characterName)
                ? string.Empty
                : characterName.Trim();
        }

        /// <summary>
        /// Stores sheet fields for network sync and applies appearance on the server.
        /// Clients re-apply when <see cref="_hasAppearance"/> syncs.
        /// </summary>
        [Server]
        public void ApplyCharacterSheet(CharacterSheet sheet, AppearanceCatalog catalog)
        {
            CharacterSheet validated = sheet.Validated(catalog);
            _characterName = validated.Name;
            _hairStyleId = validated.HairStyleId;
            _beardStyleId = validated.BeardStyleId;
            _skinToneIndex = validated.SkinToneIndex;
            _hairColorIndex = validated.HairColorIndex;
            _hasAppearance = true;

            HumanoidAppearanceApplier.Apply(gameObject, validated, catalog);
        }

        private void ApplyAppearanceLocal()
        {
            AppearanceCatalog catalog = null;
            if (SubSystems.TryGet(out CharacterSubSystem characterSystem))
            {
                catalog = characterSystem.Catalog;
            }

            CharacterSheet sheet = new()
            {
                Name = _characterName,
                HairStyleId = _hairStyleId,
                BeardStyleId = _beardStyleId,
                SkinToneIndex = _skinToneIndex,
                HairColorIndex = _hairColorIndex,
            };

            HumanoidAppearanceApplier.Apply(gameObject, sheet, catalog);
        }

		public virtual void Kill()
		{
			throw new NotImplementedException();
		}

        public virtual void DeactivateComponents()
        {
            throw new NotImplementedException();
        }
    }
}

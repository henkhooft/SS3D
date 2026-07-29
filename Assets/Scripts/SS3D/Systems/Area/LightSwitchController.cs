using System.Collections.Generic;
using FishNet.Object;
using SS3D.Core;
using SS3D.Interactions;
using SS3D.Interactions.Interfaces;
using SS3D.Systems.Tile;
using SS3D.Systems.Tile.Connections;
using SS3D.Systems.Electricity;
using UnityEngine;

namespace SS3D.Systems.Area
{
    /// <summary>
    /// Wall light switch that toggles area fixture lighting and dims when unpowered.
    /// </summary>
    [RequireComponent(typeof(BasicPowerConsumer))]
    [RequireComponent(typeof(ElectricDeviceAdjacencyConnector))]
    public sealed class LightSwitchController : InteractionTargetNetworkBehaviour, IToggleable
    {
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int LuminId = Shader.PropertyToID("_Lumin");

        [SerializeField]
        private BasicPowerConsumer _powerConsumer;
        [SerializeField]
        private Renderer[] _emissiveRenderers;

        private readonly List<Renderer> _emissiveRenderersToUpdate = new();
        private MaterialPropertyBlock _emissivePropertyBlock;
        private bool _hasLumin;
        private bool _hasEmission;
        private float _poweredLumin;
        private Color _poweredEmission;
        private float _lastEmissiveLumin = float.NaN;
        private Color _lastEmissiveEmissionColor = Color.clear;
        private AreaId _areaId;
        private bool _hasArea;
        private bool _lightsOn = true;
        private bool _areaEventsSubscribed;
        private bool _powerEventsSubscribed;
        private bool _electricityTickSubscribed;

        public bool GetState() => _lightsOn;

        public override IInteraction[] CreateTargetInteractions(InteractionEvent interactionEvent)
        {
            return new IInteraction[]
            {
                new ToggleInteraction
                {
                    OnName = "Turn lights off",
                    OffName = "Turn lights on",
                    CanInteractCallback = _ => IsPowered(),
                },
            };
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            Initialize();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            Initialize();
        }

        protected override void OnDestroyed()
        {
            UnsubscribeAreaEvents();
            UnsubscribePowerEvents();
            UnsubscribeElectricityTick();
            base.OnDestroyed();
        }

        [Server]
        public void Toggle()
        {
            if (!IsPowered() || !_hasArea || !SubSystems.TryGet(out AreaSubSystem areaSubSystem))
            {
                return;
            }

            if (areaSubSystem.ToggleAreaLightingSwitch(_areaId))
            {
                SyncSwitchState();
            }
        }

        private void Initialize()
        {
            if (_powerConsumer == null)
            {
                TryGetComponent(out _powerConsumer);
            }

            CacheEmissiveMaterials();
            CacheArea();
            TrySubscribeAreaEvents();
            TrySubscribePowerEvents();
            TrySubscribeElectricityTick();
            SyncSwitchState();
            RefreshVisuals();
        }

        private void Update()
        {
            if (!_areaEventsSubscribed)
            {
                TrySubscribeAreaEvents();
            }

            if (!_electricityTickSubscribed)
            {
                TrySubscribeElectricityTick();
            }
        }

        private void CacheEmissiveMaterials()
        {
            _emissiveRenderersToUpdate.Clear();
            _hasLumin = false;
            _hasEmission = false;
            _poweredLumin = 0f;
            _poweredEmission = Color.black;

            Renderer[] renderers = _emissiveRenderers != null && _emissiveRenderers.Length > 0
                ? _emissiveRenderers
                : GetComponentsInChildren<Renderer>(true);

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                bool hasEmissiveProperty = false;

                Material[] sharedMaterials = renderer.sharedMaterials;
                for (int i = 0; i < sharedMaterials.Length; i++)
                {
                    Material sharedMaterial = sharedMaterials[i];
                    if (sharedMaterial == null)
                    {
                        continue;
                    }

                    if (sharedMaterial.HasProperty(LuminId))
                    {
                        hasEmissiveProperty = true;
                        if (!_hasLumin)
                        {
                            _poweredLumin = sharedMaterial.GetFloat(LuminId);
                            _hasLumin = true;
                        }
                    }

                    if (sharedMaterial.HasProperty(EmissionColorId))
                    {
                        hasEmissiveProperty = true;
                        if (!_hasEmission)
                        {
                            _poweredEmission = sharedMaterial.GetColor(EmissionColorId);
                            _hasEmission = true;
                        }
                    }
                }

                if (hasEmissiveProperty)
                {
                    _emissiveRenderersToUpdate.Add(renderer);
                }
            }

            if (_emissiveRenderersToUpdate.Count == 0)
            {
                return;
            }

            // _poweredLumin/_poweredEmission are captured from shared materials above.
        }

        private void CacheArea()
        {
            _hasArea = false;
            PlacedTileObject tileObject = GetComponent<PlacedTileObject>();
            if (tileObject == null || !SubSystems.TryGet(out AreaSubSystem areaSubSystem))
            {
                return;
            }

            if (!areaSubSystem.TryResolveAreaIdForDevice(tileObject, out AreaId areaId))
            {
                return;
            }

            _hasArea = true;
            _areaId = areaId;
        }

        private void SyncSwitchState()
        {
            if (!_hasArea || !SubSystems.TryGet(out AreaSubSystem areaSubSystem))
            {
                return;
            }

            if (areaSubSystem.TryGetAreaLightingSwitchOn(_areaId, out bool on))
            {
                _lightsOn = on;
            }
        }

        private void TrySubscribeAreaEvents()
        {
            if (_areaEventsSubscribed || !SubSystems.TryGet(out AreaSubSystem areaSubSystem))
            {
                return;
            }

            areaSubSystem.OnAreaLightingSwitchChanged += HandleAreaLightingSwitchChanged;
            areaSubSystem.FloorVisualCache.OnDirty += HandleFloorVisualCacheDirty;
            _areaEventsSubscribed = true;
            CacheArea();
            SyncSwitchState();
        }

        private void HandleFloorVisualCacheDirty()
        {
            CacheArea();
            SyncSwitchState();
            RefreshVisuals();
        }

        private void UnsubscribeAreaEvents()
        {
            if (!SubSystems.TryGet(out AreaSubSystem areaSubSystem))
            {
                return;
            }

            if (_areaEventsSubscribed)
            {
                areaSubSystem.OnAreaLightingSwitchChanged -= HandleAreaLightingSwitchChanged;
                areaSubSystem.FloorVisualCache.OnDirty -= HandleFloorVisualCacheDirty;
                _areaEventsSubscribed = false;
            }
        }

        private void TrySubscribePowerEvents()
        {
            if (_powerEventsSubscribed || _powerConsumer == null)
            {
                return;
            }

            _powerConsumer.OnPowerStatusUpdated += HandlePowerStatusUpdated;
            _powerEventsSubscribed = true;
        }

        private void UnsubscribePowerEvents()
        {
            if (!_powerEventsSubscribed || _powerConsumer == null)
            {
                return;
            }

            _powerConsumer.OnPowerStatusUpdated -= HandlePowerStatusUpdated;
            _powerEventsSubscribed = false;
        }

        private void TrySubscribeElectricityTick()
        {
            if (_electricityTickSubscribed || !SubSystems.TryGet(out ElectricitySubSystem electricitySubSystem))
            {
                return;
            }

            electricitySubSystem.OnTick += HandleElectricityTick;
            _electricityTickSubscribed = true;
        }

        private void UnsubscribeElectricityTick()
        {
            if (!_electricityTickSubscribed || !SubSystems.TryGet(out ElectricitySubSystem electricitySubSystem))
            {
                return;
            }

            electricitySubSystem.OnTick -= HandleElectricityTick;
            _electricityTickSubscribed = false;
        }

        private void HandleAreaLightingSwitchChanged(AreaId areaId, bool on)
        {
            if (!_hasArea)
            {
                CacheArea();
            }

            if (_hasArea && _areaId == areaId)
            {
                _lightsOn = on;
                RefreshVisuals();
            }
        }

        private void HandlePowerStatusUpdated(object sender, PowerStatus newStatus)
        {
            RefreshVisuals();
        }

        private void HandleElectricityTick()
        {
            RefreshVisuals();
        }

        private void RefreshVisuals()
        {
            bool emit = IsPowered() && _lightsOn;
            float lumin = emit ? _poweredLumin : 0f;
            Color emission = emit ? _poweredEmission : Color.black;

            if (Mathf.Approximately(_lastEmissiveLumin, lumin)
                && _lastEmissiveEmissionColor == emission)
            {
                return;
            }

            _lastEmissiveLumin = lumin;
            _lastEmissiveEmissionColor = emission;

            foreach (Renderer renderer in _emissiveRenderersToUpdate)
            {
                if (renderer == null)
                {
                    continue;
                }

                _emissivePropertyBlock ??= new MaterialPropertyBlock();
                renderer.GetPropertyBlock(_emissivePropertyBlock);

                if (_hasLumin)
                {
                    _emissivePropertyBlock.SetFloat(LuminId, lumin);
                }

                if (_hasEmission)
                {
                    _emissivePropertyBlock.SetColor(EmissionColorId, emission);
                }

                renderer.SetPropertyBlock(_emissivePropertyBlock);
            }
        }

        private bool IsPowered()
        {
            return PowerGate.IsEffectivelyPowered(_powerConsumer, NullConsumerPolicy.Deny);
        }
    }
}

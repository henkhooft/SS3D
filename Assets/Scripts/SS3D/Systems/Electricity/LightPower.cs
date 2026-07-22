using System.Collections.Generic;
using SS3D.Core;
using SS3D.Systems.Area;
using SS3D.Systems.Tile.Connections;
using UnityEngine;

namespace SS3D.Systems.Electricity
{
    /// <summary>
    /// Toggles a fixture's realtime light and emissive mesh visuals based on power and area lighting state.
    /// </summary>
    public class LightPower : MonoBehaviour
    {
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int LuminId = Shader.PropertyToID("_Lumin");

        [SerializeField]
        private BasicPowerConsumer _consumer;
        [SerializeField]
        private Light _light;
        [SerializeField]
        private Light _fillLight;
        [SerializeField]
        private Renderer[] _emissiveRenderers;
        [SerializeField]
        private bool _respectDevBypass = true;
        [SerializeField]
        private bool _applyDepartmentalLightTint;
        [SerializeField]
        private LightFixtureCapability _fixtureCapability = LightFixtureCapability.NormalOnly;

        public LightFixtureCapability FixtureCapability => _fixtureCapability;
        [SerializeField]
        private float _emergencyIntensityMultiplier = 0.35f;
        [SerializeField]
        [Range(0.05f, 1f)]
        private float _emergencyRangeMultiplier = 0.45f;
        [SerializeField]
        private Color _emergencyTint = new Color(1f, 0.25f, 0.2f);

        private float _poweredIntensity;
        private float _poweredFillIntensity;
        private float _poweredRange;
        private float _poweredFillRange;
        private Color _poweredLightColor = Color.white;
        private float _poweredLumin;
        private Color _poweredEmission;
        private readonly List<Material> _emissiveMaterials = new();
        private AreaId _areaId;
        private bool _hasArea;
        private bool _areaLightingSubscribed;
        private bool _electricityTickSubscribed;

        private void Start()
        {
            if (_consumer != null)
            {
                _consumer.OnPowerStatusUpdated += HandlePowerStatusUpdated;
            }

            if (_light != null)
            {
                _poweredIntensity = _light.intensity;
                _poweredRange = _light.range;
                _poweredLightColor = _light.color;
            }

            if (_fillLight != null)
            {
                _poweredFillIntensity = _fillLight.intensity;
                _poweredFillRange = _fillLight.range;
            }

            CacheEmissiveMaterials();
            CacheAreaId();
            TrySubscribeAreaLighting();
            TrySubscribeElectricityTick();
            RefreshVisuals();
        }

        private void Update()
        {
            // SubSystems / NetworkObjects may spawn after LightPower.Start on pure clients.
            if (!_areaLightingSubscribed)
            {
                TrySubscribeAreaLighting();
            }

            if (!_electricityTickSubscribed)
            {
                TrySubscribeElectricityTick();
            }
        }

        private void OnDestroy()
        {
            if (_consumer != null)
            {
                _consumer.OnPowerStatusUpdated -= HandlePowerStatusUpdated;
            }

            UnsubscribeAreaLighting();
            UnsubscribeElectricityTick();
        }

        private void CacheEmissiveMaterials()
        {
            _emissiveMaterials.Clear();

            if (_emissiveRenderers == null || _emissiveRenderers.Length == 0)
            {
                foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.gameObject.name is "LightBulb" or "LightTube")
                    {
                        _emissiveMaterials.Add(renderer.material);
                    }
                }
            }
            else
            {
                foreach (Renderer renderer in _emissiveRenderers)
                {
                    if (renderer != null)
                    {
                        _emissiveMaterials.Add(renderer.material);
                    }
                }
            }

            if (_emissiveMaterials.Count == 0)
            {
                return;
            }

            Material referenceMaterial = _emissiveMaterials[0];
            if (referenceMaterial.HasProperty(LuminId))
            {
                _poweredLumin = referenceMaterial.GetFloat(LuminId);
            }

            if (referenceMaterial.HasProperty(EmissionColorId))
            {
                _poweredEmission = referenceMaterial.GetColor(EmissionColorId);
            }
        }

        private void CacheAreaId()
        {
            _hasArea = false;
            if (_consumer?.TileObject == null || !SubSystems.TryGet(out AreaSubSystem areaSubSystem))
            {
                return;
            }

            if (!areaSubSystem.TryResolveAreaIdForDevice(_consumer.TileObject, out AreaId areaId))
            {
                return;
            }

            _hasArea = true;
            _areaId = areaId;
        }

        private void TrySubscribeAreaLighting()
        {
            if (_areaLightingSubscribed || !SubSystems.TryGet(out AreaSubSystem areaSubSystem))
            {
                return;
            }

            // Do not wait for IsSetUp — pure clients never flood-fill / set IsSetUp, but still
            // receive floor-cache + lighting snapshots and need these subscriptions.
            areaSubSystem.OnAreaLightingStateChanged += HandleAreaLightingStateChanged;
            areaSubSystem.FloorVisualCache.OnDirty += HandleFloorVisualCacheDirty;
            _areaLightingSubscribed = true;
            CacheAreaId();
        }

        private void HandleFloorVisualCacheDirty()
        {
            CacheAreaId();
            RefreshVisuals();
        }

        private void UnsubscribeAreaLighting()
        {
            if (!SubSystems.TryGet(out AreaSubSystem areaSubSystem))
            {
                return;
            }

            if (_areaLightingSubscribed)
            {
                areaSubSystem.OnAreaLightingStateChanged -= HandleAreaLightingStateChanged;
                areaSubSystem.FloorVisualCache.OnDirty -= HandleFloorVisualCacheDirty;
                _areaLightingSubscribed = false;
            }
        }

        private void HandlePowerStatusUpdated(object sender, PowerStatus newStatus)
        {
            RefreshVisuals();
        }

        private void HandleAreaLightingStateChanged(AreaId areaId, AreaLightingState state)
        {
            // If we haven't resolved an area yet, retry now — the area system just published
            // a state so flood-fill must have run and tiles are assigned.
            if (!_hasArea)
            {
                CacheAreaId();
            }

            if (_hasArea && _areaId == areaId)
            {
                RefreshVisuals();
            }
        }

        private void TrySubscribeElectricityTick()
        {
            if (_electricityTickSubscribed || !SubSystems.TryGet(out ElectricitySubSystem electricitySubSystem))
            {
                return;
            }

            // Subscribe immediately — OnTick is ObserversRpc'd from the server; pure clients
            // never set ElectricitySubSystem.IsSetUp.
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

        private void HandleElectricityTick()
        {
            if (!_hasArea)
            {
                CacheAreaId();
            }

            RefreshVisuals();
        }

        public static void RefreshAllFixtures()
        {
            foreach (LightPower fixture in FindObjectsByType<LightPower>(FindObjectsSortMode.None))
            {
                fixture.RefreshVisuals();
            }
        }

        public void RefreshVisuals()
        {
            if (ShouldBeLit(out bool useEmergencyVisuals))
            {
                if (useEmergencyVisuals)
                {
                    TurnLightOnEmergency();
                }
                else
                {
                    TurnLightOnNormal();
                }
            }
            else
            {
                TurnLightOff();
            }
        }

        private bool ShouldBeLit(out bool useEmergencyVisuals)
        {
            useEmergencyVisuals = false;

            if (_consumer == null)
            {
                return false;
            }

            CacheAreaId();

            if (!IsFixtureLightingChannelOpen())
            {
                return false;
            }

            AreaLightingState areaState = AreaLightingState.Dark;
            bool hasAreaContext = _hasArea;
            if (_hasArea
                && SubSystems.TryGet(out AreaSubSystem areaSubSystem)
                && areaSubSystem.TryGetLightingState(_areaId, out areaState))
            {
                // Use derived area lighting state.
            }
            else if (_hasArea)
            {
                areaState = AreaLightingState.Dark;
            }

            if (hasAreaContext && areaState == AreaLightingState.Dark)
            {
                return false;
            }

            PowerStatus consumerStatus = _consumer.PowerStatus;
            if (_respectDevBypass && LightingDevBypass.IsActive)
            {
                consumerStatus = PowerStatus.Powered;
            }

            return AreaLightFixturePolicy.ShouldEmitLight(
                hasAreaContext,
                areaState,
                _fixtureCapability,
                consumerStatus,
                out useEmergencyVisuals);
        }

        /// <summary>
        /// Lighting fixtures must belong to an area. On host, APC channel flags are checked live.
        /// On pure clients the synced <see cref="AreaLightingState"/> already encodes channel + wall switch.
        /// </summary>
        private bool IsFixtureLightingChannelOpen()
        {
            if (_consumer is not IElectricDevice device)
            {
                return _respectDevBypass && LightingDevBypass.IsActive;
            }

            if (!SubSystems.TryGet(out AreaSubSystem areaSubSystem))
            {
                return _respectDevBypass && LightingDevBypass.IsActive;
            }

            if (areaSubSystem.TryGetEffectiveApcForDevice(device, out IApcChannelSource apc))
            {
                return PowerGate.IsChannelEnabled(_consumer.Channel, apc.Channels);
            }

            // Pure client: membership from floor cache + lighting snapshot (no APC registry).
            if (_hasArea && areaSubSystem.TryGetLightingState(_areaId, out _))
            {
                return true;
            }

            return _respectDevBypass && LightingDevBypass.IsActive;
        }

        private void TurnLightOnNormal()
        {
            Color emission = _poweredEmission;
            if (_applyDepartmentalLightTint
                && _hasArea
                && SubSystems.TryGet(out AreaSubSystem areaSubSystem)
                && areaSubSystem.TryGetDepartmentalLightTint(_areaId, out Color tint))
            {
                emission = MultiplyColor(_poweredEmission, tint);
            }

            ApplyLightState(_light, _poweredIntensity, _poweredRange, _poweredLightColor);
            ApplyLightState(_fillLight, _poweredFillIntensity, _poweredFillRange, _poweredLightColor);

            SetEmissiveState(_poweredLumin, emission);
        }

        private void TurnLightOnEmergency()
        {
            float emergencyIntensity = _poweredIntensity * _emergencyIntensityMultiplier;
            float emergencyRange = _poweredRange * _emergencyRangeMultiplier;
            ApplyLightState(_light, emergencyIntensity, emergencyRange, _emergencyTint);
            ApplyLightState(_fillLight, 0f, _poweredFillRange, _emergencyTint, enabled: false);

            SetEmissiveState(_poweredLumin * _emergencyIntensityMultiplier, MultiplyColor(_poweredEmission, _emergencyTint));
        }

        private void TurnLightOff()
        {
            ApplyLightState(_light, 0f, _poweredRange, _poweredLightColor, enabled: false);
            ApplyLightState(_fillLight, 0f, _poweredFillRange, _poweredLightColor, enabled: false);

            SetEmissiveState(0f, Color.black);
        }

        private static void ApplyLightState(Light light, float intensity, float range, Color color, bool enabled = true)
        {
            if (light == null)
            {
                return;
            }

            light.intensity = intensity;
            light.range = range;
            light.color = color;
            light.enabled = enabled && intensity > 0f;
        }

        private static Color MultiplyColor(Color left, Color right)
        {
            return new Color(left.r * right.r, left.g * right.g, left.b * right.b, left.a * right.a);
        }

        private void SetEmissiveState(float lumin, Color emissionColor)
        {
            foreach (Material material in _emissiveMaterials)
            {
                if (material.HasProperty(LuminId))
                {
                    material.SetFloat(LuminId, lumin);
                }

                if (material.HasProperty(EmissionColorId))
                {
                    material.SetColor(EmissionColorId, emissionColor);
                }
            }
        }
    }
}

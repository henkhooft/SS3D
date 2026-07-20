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
        private Color _emergencyTint = new Color(1f, 0.25f, 0.2f);

        private float _poweredIntensity;
        private float _poweredFillIntensity;
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
                _poweredLightColor = _light.color;
            }

            if (_fillLight != null)
            {
                _poweredFillIntensity = _fillLight.intensity;
            }

            CacheEmissiveMaterials();
            CacheAreaId();
            TrySubscribeAreaLighting();
            TrySubscribeElectricityTick();
            RefreshVisuals();
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

            if (!areaSubSystem.TryGetAreaForDevice(_consumer.TileObject, out AreaRecord record))
            {
                return;
            }

            _hasArea = true;
            _areaId = record.Id;
        }

        private void TrySubscribeAreaLighting()
        {
            if (_areaLightingSubscribed || !SubSystems.TryGet(out AreaSubSystem areaSubSystem))
            {
                return;
            }

            if (areaSubSystem.IsSetUp)
            {
                areaSubSystem.OnAreaLightingStateChanged += HandleAreaLightingStateChanged;
                _areaLightingSubscribed = true;
                return;
            }

            areaSubSystem.OnSystemSetUp += HandleAreaSystemSetup;
        }

        private void HandleAreaSystemSetup()
        {
            if (!SubSystems.TryGet(out AreaSubSystem areaSubSystem))
            {
                return;
            }

            areaSubSystem.OnSystemSetUp -= HandleAreaSystemSetup;
            if (!_areaLightingSubscribed)
            {
                areaSubSystem.OnAreaLightingStateChanged += HandleAreaLightingStateChanged;
                _areaLightingSubscribed = true;
            }

            CacheAreaId();
            RefreshVisuals();
        }

        private void UnsubscribeAreaLighting()
        {
            if (!SubSystems.TryGet(out AreaSubSystem areaSubSystem))
            {
                return;
            }

            areaSubSystem.OnSystemSetUp -= HandleAreaSystemSetup;
            if (_areaLightingSubscribed)
            {
                areaSubSystem.OnAreaLightingStateChanged -= HandleAreaLightingStateChanged;
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

            if (electricitySubSystem.IsSetUp)
            {
                electricitySubSystem.OnTick += HandleElectricityTick;
                _electricityTickSubscribed = true;
                return;
            }

            electricitySubSystem.OnSystemSetUp += HandleElectricitySystemSetup;
        }

        private void HandleElectricitySystemSetup()
        {
            if (!SubSystems.TryGet(out ElectricitySubSystem electricitySubSystem))
            {
                return;
            }

            electricitySubSystem.OnSystemSetUp -= HandleElectricitySystemSetup;
            if (!_electricityTickSubscribed)
            {
                electricitySubSystem.OnTick += HandleElectricityTick;
                _electricityTickSubscribed = true;
            }

            RefreshVisuals();
        }

        private void UnsubscribeElectricityTick()
        {
            if (!SubSystems.TryGet(out ElectricitySubSystem electricitySubSystem))
            {
                return;
            }

            electricitySubSystem.OnSystemSetUp -= HandleElectricitySystemSetup;
            if (_electricityTickSubscribed)
            {
                electricitySubSystem.OnTick -= HandleElectricityTick;
                _electricityTickSubscribed = false;
            }
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
        /// Lighting fixtures must belong to an area APC. PowerGate passthrough (no APC) is treated as off
        /// unless the dev bypass is active for tilemap authoring.
        /// </summary>
        private bool IsFixtureLightingChannelOpen()
        {
            if (!PowerGate.IsChannelOpen(_consumer))
            {
                return false;
            }

            if (_consumer is not IElectricDevice device)
            {
                return _respectDevBypass && LightingDevBypass.IsActive;
            }

            if (!SubSystems.TryGet(out AreaSubSystem areaSubSystem))
            {
                return _respectDevBypass && LightingDevBypass.IsActive;
            }

            if (areaSubSystem.TryGetEffectiveApcForDevice(device, out _))
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
                && _consumer?.TileObject != null
                && areaSubSystem.TryGetAreaForDevice(_consumer.TileObject, out AreaRecord record)
                && record.HasDepartmentalLightTint)
            {
                emission = MultiplyColor(_poweredEmission, record.DepartmentalLightTint);
            }

            ApplyLightState(_light, _poweredIntensity, _poweredLightColor);
            ApplyLightState(_fillLight, _poweredFillIntensity, _poweredLightColor);

            SetEmissiveState(_poweredLumin, emission);
        }

        private void TurnLightOnEmergency()
        {
            float emergencyIntensity = _poweredIntensity * _emergencyIntensityMultiplier;
            float emergencyFillIntensity = _poweredFillIntensity * _emergencyIntensityMultiplier;
            ApplyLightState(_light, emergencyIntensity, _emergencyTint);
            ApplyLightState(_fillLight, emergencyFillIntensity, _emergencyTint);

            SetEmissiveState(_poweredLumin * _emergencyIntensityMultiplier, MultiplyColor(_poweredEmission, _emergencyTint));
        }

        private void TurnLightOff()
        {
            ApplyLightState(_light, 0f, _poweredLightColor, enabled: false);
            ApplyLightState(_fillLight, 0f, _poweredLightColor, enabled: false);

            SetEmissiveState(0f, Color.black);
        }

        private static void ApplyLightState(Light light, float intensity, Color color, bool enabled = true)
        {
            if (light == null)
            {
                return;
            }

            light.intensity = intensity;
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

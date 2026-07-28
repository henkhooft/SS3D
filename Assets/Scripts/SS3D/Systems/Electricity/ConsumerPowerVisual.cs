using System;
using System.Collections.Generic;
using SS3D.Core;
using UnityEngine;

namespace SS3D.Systems.Electricity
{
    /// <summary>
    /// Dims emissive materials and optional panel indicators when a power consumer is inactive.
    /// Driven by <see cref="IPowerConsumer"/> status changes (and a one-shot refresh when electricity
    /// becomes ready) — not per-tick <see cref="ElectricitySubSystem.OnTick"/>, which is too expensive
    /// at SS13 door counts when power is steady.
    /// </summary>
    public class ConsumerPowerVisual : MonoBehaviour
    {
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int LuminId = Shader.PropertyToID("_Lumin");

        [Serializable]
        struct EmissiveSlot
        {
            public Material Material;
            public float PoweredLumin;
            public Color PoweredEmission;
        }

        [Serializable]
        struct PanelIndicatorSlot
        {
            public Renderer Renderer;
            public int MaterialIndex;
            public Color PoweredColor;
            /// <summary>Cached from <see cref="Renderer.materials"/> once — never re-fetch on tick.</summary>
            [NonSerialized]
            public Material Material;
        }

        [SerializeField]
        private MonoBehaviour _consumerBehaviour;
        [SerializeField]
        private Renderer[] _renderers;
        [SerializeField]
        private PanelIndicatorSlot[] _panelIndicators;

        private IPowerConsumer _consumer;
        private readonly List<EmissiveSlot> _emissiveSlots = new();
        private bool _waitingForElectricityReady;
        private bool _hasAppliedPowered;
        private bool _lastShownPowered;

        private void Awake()
        {
            _consumer = ResolveConsumer();
        }

        private void Start()
        {
            if (_consumer == null)
            {
                _consumer = ResolveConsumer();
            }

            if (_consumer is BasicPowerConsumer basicConsumer)
            {
                basicConsumer.OnPowerStatusUpdated += HandlePowerStatusUpdated;
            }
            else if (_consumer is MachinePowerConsumer machineConsumer)
            {
                machineConsumer.OnPowerStatusUpdated += HandlePowerStatusUpdated;
            }

            CacheVisuals();
            TryBindElectricityReady();
            RefreshVisuals();
        }

        private void OnDestroy()
        {
            if (_consumer is BasicPowerConsumer basicConsumer)
            {
                basicConsumer.OnPowerStatusUpdated -= HandlePowerStatusUpdated;
            }
            else if (_consumer is MachinePowerConsumer machineConsumer)
            {
                machineConsumer.OnPowerStatusUpdated -= HandlePowerStatusUpdated;
            }

            UnbindElectricityReady();
        }

        private IPowerConsumer ResolveConsumer()
        {
            if (_consumerBehaviour is IPowerConsumer behaviourConsumer)
            {
                return behaviourConsumer;
            }

            if (TryGetComponent(out BasicPowerConsumer basicConsumer))
            {
                return basicConsumer;
            }

            if (TryGetComponent(out MachinePowerConsumer machineConsumer))
            {
                return machineConsumer;
            }

            return null;
        }

        private void CacheVisuals()
        {
            _emissiveSlots.Clear();

            Renderer[] renderers = _renderers != null && _renderers.Length > 0
                ? _renderers
                : GetComponentsInChildren<Renderer>(true);

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                Material[] materials = renderer.materials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material material = materials[i];
                    if (!material.HasProperty(LuminId) && !material.HasProperty(EmissionColorId))
                    {
                        continue;
                    }

                    float lumin = material.HasProperty(LuminId) ? material.GetFloat(LuminId) : 0f;
                    Color emission = material.HasProperty(EmissionColorId)
                        ? material.GetColor(EmissionColorId)
                        : Color.black;

                    _emissiveSlots.Add(new EmissiveSlot
                    {
                        Material = material,
                        PoweredLumin = lumin,
                        PoweredEmission = emission,
                    });
                }
            }

            if (_panelIndicators == null)
            {
                return;
            }

            for (int i = 0; i < _panelIndicators.Length; i++)
            {
                PanelIndicatorSlot slot = _panelIndicators[i];
                if (slot.Renderer == null || slot.MaterialIndex < 0)
                {
                    continue;
                }

                // .materials allocates a new array every call — cache once for the tick path.
                Material[] materials = slot.Renderer.materials;
                if (slot.MaterialIndex >= materials.Length)
                {
                    continue;
                }

                slot.Material = materials[slot.MaterialIndex];
                slot.PoweredColor = slot.Material.color;
                _panelIndicators[i] = slot;
            }
        }

        private void HandlePowerStatusUpdated(object sender, PowerStatus newStatus)
        {
            RefreshVisuals();
        }

        private void TryBindElectricityReady()
        {
            if (_waitingForElectricityReady || !SubSystems.TryGet(out ElectricitySubSystem electricitySubSystem))
            {
                return;
            }

            if (electricitySubSystem.IsReady)
            {
                return;
            }

            electricitySubSystem.WhenReady += HandleElectricitySystemSetup;
            _waitingForElectricityReady = true;
        }

        private void HandleElectricitySystemSetup()
        {
            if (!SubSystems.TryGet(out ElectricitySubSystem electricitySubSystem))
            {
                return;
            }

            electricitySubSystem.WhenReady -= HandleElectricitySystemSetup;
            _waitingForElectricityReady = false;
            RefreshVisuals();
        }

        private void UnbindElectricityReady()
        {
            if (!_waitingForElectricityReady || !SubSystems.TryGet(out ElectricitySubSystem electricitySubSystem))
            {
                return;
            }

            electricitySubSystem.WhenReady -= HandleElectricitySystemSetup;
            _waitingForElectricityReady = false;
        }

        public void RefreshVisuals()
        {
            bool powered = ShouldShowPowered();
            if (_hasAppliedPowered && _lastShownPowered == powered)
            {
                return;
            }

            _hasAppliedPowered = true;
            _lastShownPowered = powered;

            if (powered)
            {
                SetEmissiveState(true);
                SetPanelIndicators(true);
            }
            else
            {
                SetEmissiveState(false);
                SetPanelIndicators(false);
            }
        }

        private bool ShouldShowPowered()
        {
            // Prefer simulation PowerStatus: area ticks already apply channel gating before
            // assigning Powered/Inactive. Avoids per-tick APC lookups on hundreds of airlocks.
            return PowerGate.IsPowered(_consumer, NullConsumerPolicy.Deny);
        }

        private void SetEmissiveState(bool powered)
        {
            foreach (EmissiveSlot slot in _emissiveSlots)
            {
                if (slot.Material == null)
                {
                    continue;
                }

                if (slot.Material.HasProperty(LuminId))
                {
                    slot.Material.SetFloat(LuminId, powered ? slot.PoweredLumin : 0f);
                }

                if (slot.Material.HasProperty(EmissionColorId))
                {
                    slot.Material.SetColor(EmissionColorId, powered ? slot.PoweredEmission : Color.black);
                }
            }
        }

        private void SetPanelIndicators(bool powered)
        {
            if (_panelIndicators == null)
            {
                return;
            }

            for (int i = 0; i < _panelIndicators.Length; i++)
            {
                PanelIndicatorSlot slot = _panelIndicators[i];
                if (slot.Material == null)
                {
                    continue;
                }

                slot.Material.color = powered ? slot.PoweredColor : Color.black;
            }
        }
    }
}

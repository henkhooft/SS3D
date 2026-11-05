using FishNet.Object.Synchronizing;
using FishNet.Object;
using SS3D.Systems.Atmospherics;
using SS3D.Systems.Audio;
using SS3D.Systems.Tile;
using UnityEngine;

namespace SS3D.Systems.Atmospherics.Pipes
{
    /// <summary>
    /// Pulls filtered gases from the turf cell into the connected pipe network.
    /// </summary>
    public sealed class ScrubberController : AtmosPortControllerBase, IAmbientLoopState
    {
        private static readonly int ScrubActiveId = Animator.StringToHash("scrubActive");

        [SyncVar]
        private int _flowRate = AtmosPortConstants.ScrubberNormalFlowRate;

        // A chamber intake carries air, where a room scrubber must leave air alone.
        [SerializeField]
        private bool _isAirIntake;

        private bool _filterO2;
        private bool _filterN2;
        private bool _filterCo2 = true;
        private bool _filterPlasma;
        private bool _filterToxins;

        public int FlowRate => _flowRate;

        public override void OnStartServer()
        {
            base.OnStartServer();
            ServerApplyNormalSettings();
        }

        /// <summary>
        /// Back to this scrubber's everyday speed and filters.
        /// </summary>
        [Server]
        public void ServerApplyNormalSettings()
        {
            _flowRate = AtmosPortConstants.ScrubberNormalFlowRate;
            ServerSetFilters(o2: _isAirIntake, n2: _isAirIntake, co2: !_isAirIntake, plasma: false, toxins: false);
        }

        /// <summary>
        /// Flat out on every gas, to siphon a room.
        /// </summary>
        [Server]
        public void ServerApplyPanicSettings()
        {
            _flowRate = AtmosPortConstants.ScrubberMaxFlowRate;
            ServerSetFilters(o2: true, n2: true, co2: true, plasma: true, toxins: true);
        }

        public static float GetRatedFlowMolesPerSecond(int flowRate)
        {
            float scale = Mathf.Clamp(flowRate, 1, AtmosPortConstants.ScrubberMaxFlowRate) / (float)AtmosPortConstants.ScrubberMaxFlowRate;
            return AtmosPortConstants.ScrubberRatedFlowMolesPerSecond * scale;
        }

        public void GetFilterStates(
            out bool filterO2,
            out bool filterN2,
            out bool filterCo2,
            out bool filterPlasma,
            out bool filterToxins)
        {
            filterO2 = _filterO2;
            filterN2 = _filterN2;
            filterCo2 = _filterCo2;
            filterPlasma = _filterPlasma;
            filterToxins = _filterToxins;
        }

        [Server]
        public void ServerSetFlowRate(int flowRate)
        {
            _flowRate = Mathf.Clamp(flowRate, 1, AtmosPortConstants.ScrubberMaxFlowRate);
        }

        [Server]
        public void ServerToggleFilter(int filterIndex)
        {
            bool current = GetFilterEnabled(filterIndex);
            ServerSetFilter(filterIndex, !current);
        }

        [Server]
        public void ServerSetFilter(int filterIndex, bool enabled)
        {
            switch (filterIndex)
            {
                case 0:
                    _filterO2 = enabled;
                    break;
                case 1:
                    _filterN2 = enabled;
                    break;
                case 2:
                    _filterCo2 = enabled;
                    break;
                case 3:
                    _filterPlasma = enabled;
                    break;
                case 4:
                    _filterToxins = enabled;
                    break;
            }
        }

        [Server]
        public void ServerSetFilters(bool o2, bool n2, bool co2, bool plasma, bool toxins)
        {
            _filterO2 = o2;
            _filterN2 = n2;
            _filterCo2 = co2;
            _filterPlasma = plasma;
            _filterToxins = toxins;
        }

        public override TileLayer[] PipeLayers => AtmosDevicePipeResolver.ScrubberLayers;

        protected override bool RunPortTick(
            AtmosPipeSimulation pipeSimulation,
            AtmosSimulation turfSimulation,
            float deltaTime)
        {
            if (!TryGetConnectedNetwork(out GasPipeNetworkId networkId)
                || !pipeSimulation.Registry.TryGetNetwork(networkId, out GasPipeNetworkRecord network))
            {
                return false;
            }

            TileCoord turfCell = OriginTile;
            float turfPressure = turfSimulation.GetCellPressure(turfCell);
            float networkPressure = network.GetPressure(AtmosConstants.DefaultGasCount);
            // Scrubbers actively pump gas from turf into the connected network; they are not passive valves.
            // Allow flow at the rated rate, tapering as the connected network pressure approaches/exceeds
            // the turf pressure within the supported differential.
            float maxDiff = Mathf.Max(AtmosPortConstants.PortMaxDifferentialKpa, 1e-3f);
            float differential = turfPressure - networkPressure;
            if (differential <= -maxDiff)
            {
                return false;
            }

            float pressureFactor = Mathf.Clamp01((differential + maxDiff) / maxDiff);
            float budgetMoles = GetRatedFlowMolesPerSecond(_flowRate) * pressureFactor * deltaTime;

            if (budgetMoles <= 0f)
                return false;

            // "Toxins" is currently treated as an alias of plasma in the core gas set.
            bool filterPlasma = _filterPlasma || _filterToxins;
            float selected = SelectedMoles(turfSimulation, turfCell, AtmosConstants.Oxygen, _filterO2)
                + SelectedMoles(turfSimulation, turfCell, AtmosConstants.Nitrogen, _filterN2)
                + SelectedMoles(turfSimulation, turfCell, AtmosConstants.CarbonDioxide, _filterCo2)
                + SelectedMoles(turfSimulation, turfCell, AtmosConstants.Plasma, filterPlasma);
            if (selected <= 0f)
                return false;

            // Each gas moves in proportion to its share of the tile, so an intake carries the mix it
            // sits in and a trace of CO₂ isn't starved by the air around it.
            float fraction = Mathf.Min(budgetMoles / selected, 1f);

            // Unrolled filters — do not use yield/IEnumerable (allocates a state machine per scrubber per tick).
            bool movedAny = false;
            if (_filterO2)
                movedAny |= TryScrubGas(pipeSimulation, turfSimulation, networkId, turfCell, AtmosConstants.Oxygen, fraction);

            if (_filterN2)
                movedAny |= TryScrubGas(pipeSimulation, turfSimulation, networkId, turfCell, AtmosConstants.Nitrogen, fraction);

            if (_filterCo2)
                movedAny |= TryScrubGas(pipeSimulation, turfSimulation, networkId, turfCell, AtmosConstants.CarbonDioxide, fraction);

            if (filterPlasma)
                movedAny |= TryScrubGas(pipeSimulation, turfSimulation, networkId, turfCell, AtmosConstants.Plasma, fraction);

            return movedAny;
        }

        private static float SelectedMoles(AtmosSimulation turfSimulation, TileCoord turfCell, GasId gasId, bool selected) =>
            selected && turfSimulation.TryGetGasMoles(turfCell, gasId, out float moles) ? moles : 0f;

        private static bool TryScrubGas(
            AtmosPipeSimulation pipeSimulation,
            AtmosSimulation turfSimulation,
            GasPipeNetworkId networkId,
            TileCoord turfCell,
            GasId gasId,
            float fraction)
        {
            return turfSimulation.TryGetGasMoles(turfCell, gasId, out float available)
                && available > 0f
                && pipeSimulation.TryTransferMoles(
                    networkId,
                    gasId,
                    available * fraction,
                    turfCell,
                    PipeTransferDirection.ToNetwork,
                    out float moved)
                && moved > 0f;
        }

        protected override void ApplyDeviceSpecificAnimatorState(bool flowing)
        {
            if (_animator == null)
                return;

            _animator.SetBool(ScrubActiveId, flowing);
        }

        private bool GetFilterEnabled(int filterIndex)
        {
            return filterIndex switch
            {
                0 => _filterO2,
                1 => _filterN2,
                2 => _filterCo2,
                3 => _filterPlasma,
                4 => _filterToxins,
                _ => false,
            };
        }

    }
}

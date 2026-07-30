using FishNet.Object;
using FishNet.Object.Synchronizing;
using SS3D.Systems.Atmospherics;
using SS3D.Systems.Tile;
using UnityEngine;

namespace SS3D.Systems.Atmospherics.Pipes
{
    /// <summary>
    /// Pulls filtered gases from the turf cell into the connected pipe network.
    /// </summary>
    public sealed class ScrubberController : AtmosPortControllerBase
    {
        private static readonly int ScrubActiveId = Animator.StringToHash("scrubActive");

        [SyncVar]
        private int _flowRate = 5;

        private bool _filterO2 = true;
        private bool _filterN2 = true;
        private bool _filterCo2 = true;
        private bool _filterPlasma;
        private bool _filterToxins = true;

        public int FlowRate => _flowRate;

        public static float GetRatedFlowMolesPerSecond(int flowRate)
        {
            float scale = Mathf.Clamp(flowRate, 1, 10) / 10f;
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
            _flowRate = Mathf.Clamp(flowRate, 1, 10);
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

            bool movedAny = false;
            float remainingBudget = budgetMoles;

            // Unrolled filters — do not use yield/IEnumerable (allocates a state machine per scrubber per tick).
            if (_filterO2)
            {
                movedAny |= TryScrubGas(pipeSimulation, turfSimulation, networkId, turfCell, AtmosConstants.Oxygen, ref remainingBudget);
            }

            if (_filterN2 && remainingBudget > 0f)
            {
                movedAny |= TryScrubGas(pipeSimulation, turfSimulation, networkId, turfCell, AtmosConstants.Nitrogen, ref remainingBudget);
            }

            if (_filterCo2 && remainingBudget > 0f)
            {
                movedAny |= TryScrubGas(pipeSimulation, turfSimulation, networkId, turfCell, AtmosConstants.CarbonDioxide, ref remainingBudget);
            }

            // "Toxins" is currently treated as an alias of plasma in the core gas set.
            if ((_filterPlasma || _filterToxins) && remainingBudget > 0f)
            {
                movedAny |= TryScrubGas(pipeSimulation, turfSimulation, networkId, turfCell, AtmosConstants.Plasma, ref remainingBudget);
            }

            return movedAny;
        }

        private static bool TryScrubGas(
            AtmosPipeSimulation pipeSimulation,
            AtmosSimulation turfSimulation,
            GasPipeNetworkId networkId,
            TileCoord turfCell,
            GasId gasId,
            ref float remainingBudget)
        {
            if (remainingBudget <= 0f
                || !turfSimulation.TryGetGasMoles(turfCell, gasId, out float available)
                || available <= 0f)
            {
                return false;
            }

            float request = Mathf.Min(remainingBudget, available);
            if (!pipeSimulation.TryTransferMoles(
                    networkId,
                    gasId,
                    request,
                    turfCell,
                    PipeTransferDirection.ToNetwork,
                    out float moved)
                || moved <= 0f)
            {
                return false;
            }

            remainingBudget -= moved;
            return true;
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

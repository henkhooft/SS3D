using SS3D.Core;
using SS3D.Core.Behaviours;
using SS3D.Systems.Tile;
using UnityEngine;

namespace SS3D.Systems.Atmospherics.Pipes
{
    /// <summary>
    /// Holds its turf cell at a fixed mix every tick, so a sealed room becomes a storage chamber that never runs out.
    /// </summary>
    [RequireComponent(typeof(PlacedTileObject))]
    public sealed class AtmosGasSource : NetworkActor, IAtmosPortDevice
    {
        [SerializeField]
        private AtmosSupplyMix _mix;

        [SerializeField]
        private float _pressureKpa = AtmosPipeConstants.ChamberPressureKpa;

        private readonly float[] _heldMoles = new float[AtmosConstants.DefaultGasCount];
        private PlacedTileObject _tileObject;

        public TileCoord OriginTile => new(_tileObject.MapId, _tileObject.WorldOrigin);

        public bool IsEnabled => true;

        public bool TryGetConnectedNetwork(out GasPipeNetworkId networkId)
        {
            networkId = GasPipeNetworkId.None;
            return false;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _tileObject = GetComponent<PlacedTileObject>();
            float moles = AtmosPipeThermo.GetMolesForPressure(_pressureKpa, AtmosConstants.StandardTemperature, AtmosConstants.CellVolume);
            AtmosSupplyMixes.FillMoles(_mix, moles, _heldMoles);
            SubSystems.Get<AtmosSubSystem>()?.RegisterPort(this);
        }

        protected override void OnDestroyed()
        {
            if (SubSystems.TryGet(out AtmosSubSystem atmosSubSystem))
                atmosSubSystem.UnregisterPort(this);

            base.OnDestroyed();
        }

        public void ServerTick(AtmosPipeSimulation pipeSimulation, AtmosSimulation turfSimulation, float deltaTime) =>
            turfSimulation.HoldCell(OriginTile, _heldMoles, AtmosConstants.StandardTemperature);
    }
}

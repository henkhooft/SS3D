using FishNet.Object;
using SS3D.Core;
using SS3D.Systems.Entities.Humanoid;
using SS3D.Systems.Tile;
using UnityEngine;

namespace SS3D.Systems.Atmospherics
{
    public sealed partial class AtmosSubSystem
    {
        private readonly Vector3[] _windPositions = new Vector3[96];
        private readonly Vector3[] _windGradients = new Vector3[96];
        private AtmosWindView _windView;
        private bool _hadWind;

        private void TickWind()
        {
            if (!SubSystems.TryGet(out TileSubSystem tiles) || tiles.QueryService == null) return;
            foreach (Ragdoll body in Ragdoll.WindBodies)
            {
                if (body == null || !body.IsServer) continue;
                TileCoord coord = tiles.QueryService.WorldToTile(body.WindSamplePosition, _simulation.MapId);
                body.ServerApplyWind(AtmosWind.Sample(_simulation, coord), AtmosConstants.TickInterval);
            }

            int count = 0;
            var active = _simulation.ActiveCells;
            for (int i = 0; i < active.Length && count < _windPositions.Length; i++)
            {
                int index = active[i];
                TileChunkRef chunk = _simulation.Chunks[index / AtmosConstants.CellsPerChunk];
                int local = index % AtmosConstants.CellsPerChunk;
                TileCoord coord = new(_simulation.MapId,
                    chunk.ChunkKey.x * AtmosConstants.ChunkSize + local % AtmosConstants.ChunkSize,
                    chunk.ChunkKey.y * AtmosConstants.ChunkSize + local / AtmosConstants.ChunkSize);
                Vector3 gradient = AtmosWind.Sample(_simulation, coord);
                if (gradient.sqrMagnitude < 1f) continue;
                _windPositions[count] = tiles.QueryService.TileToWorld(coord) + Vector3.up * 0.65f;
                _windGradients[count++] = gradient;
            }
            if (count > 0 || _hadWind) RpcWindField(_windPositions, _windGradients, count);
            _hadWind = count > 0;
        }

        [ObserversRpc]
        private void RpcWindField(Vector3[] positions, Vector3[] gradients, int count)
        {
            if (_windView == null) _windView = gameObject.AddComponent<AtmosWindView>();
            _windView.Show(positions, gradients, count);
        }
    }
}

using SS3D.Systems.Atmospherics.ECS;
using SS3D.Systems.Tile;
using UnityEngine;

namespace SS3D.Systems.Atmospherics
{
    /// <summary>Pressure-driven wind through the same open edges used by gas sharing.</summary>
    public static class AtmosWind
    {
        public const float KnockdownPressure = 8f;
        public const float MinPressure = 0.5f;
        public const float MaxAcceleration = 35f;

        public static Vector3 Sample(AtmosSimulation simulation, TileCoord coord)
        {
            if (!simulation.TryGetCellIndex(coord, out int index)) return Vector3.zero;
            AtmosCellMeta cell = simulation.CellMeta[index];
            if (cell.State == AtmosCellState.Blocked || cell.State == AtmosCellState.Vacuum)
                return Vector3.zero;
            float pressure = simulation.GetCellPressure(index);
            Vector3 gradient = Vector3.zero;
            for (int d = 0; d < 4; d++)
            {
                int neighbour = simulation.Neighbours[index].Get(d);
                if (neighbour < 0 || simulation.CellMeta[neighbour].State == AtmosCellState.Blocked) continue;
                float drop = pressure - simulation.GetCellPressure(neighbour);
                if (drop <= MinPressure) continue;
                gradient += Direction(d) * drop;
            }
            return gradient;
        }

        public static Vector3 Direction(int index) => index switch
        {
            0 => Vector3.forward, 1 => Vector3.right,
            2 => Vector3.back, _ => Vector3.left,
        };

        public static Vector3 Acceleration(Vector3 pressureGradient) =>
            Vector3.ClampMagnitude(pressureGradient * 3f, MaxAcceleration);
    }
}

using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace SS3D.Engine.AtmosphericsRework
{
    public struct AtmosObjectInfo
    {
        public AtmosState state;
        public AtmosContainer container;
    }

    public struct AtmosObject
    {
        public AtmosObjectInfo atmosObject;
        public AtmosObjectInfo neighbour1;
        public AtmosObjectInfo neighbour2;
        public AtmosObjectInfo neighbour3;
        public AtmosObjectInfo neighbour4;

        public bool temperatureSetting;
        public bool4 activeDirection;

        public float4 tileFlux;
        public float4 neighbourFlux;
        public float4 difference;

        public bool4 neighbourUpdate;
        public float4 neighbourPressure;

        public void Setup()
        {
            atmosObject.container = new AtmosContainer();
            atmosObject.container.Setup();
        }

        public AtmosObjectInfo GetNeighbour(int index)
        {
            switch (index)
            {
                case 0:
                    return neighbour1;
                case 1:
                    return neighbour2;
                case 2:
                    return neighbour3;
                case 3:
                    return neighbour4;
            }

            return default;
        }

        public void SetNeighbours(AtmosObjectInfo info, int index)
        {
            switch (index)
            {
                case 0:
                    neighbour1 = info;
                    break;
                case 1:
                    neighbour2 = info;
                    break;
                case 2:
                    neighbour3 = info;
                    break;
                case 3:
                    neighbour4 = info;
                    break;
            }
        }
    }

    public static class AtmosCalculator
    {
        // Performance makers
        static ProfilerMarker s_CalculateFluxPerfMarker = new ProfilerMarker("AtmosObject.CalculateFlux");
        static ProfilerMarker s_SimulateFluxPerfMarker = new ProfilerMarker("AtmosObject.SimulateFlux");
        static ProfilerMarker s_SimlateMixingPerfMarker = new ProfilerMarker("AtmosObject.SimulateMixing");


        public static AtmosObject CalculateFlux(AtmosObject atmos)
        {
            s_CalculateFluxPerfMarker.Begin();

            float pressure = atmos.atmosObject.container.GetPressure();
            // tileFlux = 0f;

            for (int i = 0; i < 4; i++)
            {
                if ((!atmos.GetNeighbour(i).Equals(default(AtmosObjectInfo))) && atmos.GetNeighbour(i).state != AtmosState.Blocked)
                {
                    atmos.neighbourPressure[i] = atmos.GetNeighbour(i).container.GetPressure();
                    atmos.neighbourFlux[i] = math.min(atmos.tileFlux[i] * GasConstants.drag + (pressure - atmos.neighbourPressure[i]) * GasConstants.dt, 1000f);
                    atmos.activeDirection[i] = true;


                    if (atmos.neighbourFlux[i] < 0f)
                    {
                        AtmosObjectInfo neighbour = atmos.GetNeighbour(i);
                        neighbour.state = AtmosState.Active;
                        atmos.neighbourFlux[i] = 0f;
                        atmos.SetNeighbours(neighbour, i);
                    }
                }

            }

            if (math.any(atmos.neighbourFlux > GasConstants.fluxEpsilon))
            {
                float scalingFactor = math.min(1f, pressure / math.csum(atmos.neighbourFlux) / GasConstants.dt);

                atmos.neighbourFlux *= scalingFactor;
                atmos.tileFlux = atmos.neighbourFlux;
            }
            else
            {
                atmos.tileFlux = 0f;
                if (!atmos.temperatureSetting)
                    atmos.atmosObject.state = AtmosState.Semiactive;
                else
                    atmos.temperatureSetting = false;
            }

            if (atmos.atmosObject.state == AtmosState.Semiactive || atmos.atmosObject.state == AtmosState.Active)
            {
                atmos = SimulateMixing(atmos);
            }

            s_CalculateFluxPerfMarker.End();

            return atmos;
        }

        private static AtmosObject SimulateMixing(AtmosObject atmos)
        {
            s_SimlateMixingPerfMarker.Begin();

            atmos.difference = 0f;
            bool mixed = false;
            if (math.any(atmos.atmosObject.container.GetCoreGasses() > 0f))
            {
                for (int i = 0; i < 4; i++)
                {
                    if ((!atmos.GetNeighbour(i).Equals(default(AtmosObjectInfo))) && atmos.GetNeighbour(i).state != AtmosState.Blocked)
                    {
                        AtmosObjectInfo neighbour = atmos.GetNeighbour(i);
                        // AtmosContainer neighbourContainer = GetNeighbour(i).container;
                        atmos.difference = (atmos.atmosObject.container.GetCoreGasses() - neighbour.container.GetCoreGasses()) * GasConstants.mixRate;
                        if (math.any(atmos.difference > GasConstants.minMoleTransfer))
                        {
                            // Increase neighbouring tiles moles and decrease ours
                            AtmosContainer neighbourContainer = neighbour.container;
                            neighbourContainer.AddCoreGasses(atmos.difference);
                            neighbour.container = neighbourContainer;
                            atmos.atmosObject.container.RemoveCoreGasses(atmos.difference);
                            mixed = true;


                            // Remain active if there is still a pressure difference
                            if (math.abs(neighbourContainer.GetPressure() - atmos.atmosObject.container.GetPressure()) > GasConstants.minMoleTransfer)
                            {
                                // AtmosObjectInfo neighbour = GetNeighbour(i);
                                neighbour.state = AtmosState.Active;
                                // SetNeighbours(neighbour, i);
                            }
                            else
                            {
                                // AtmosObjectInfo neighbour = GetNeighbour(i);
                                neighbour.state = AtmosState.Semiactive;
                                // SetNeighbours(neighbour, i);
                            }

                            atmos.SetNeighbours(neighbour, i);
                        }
                    }
                }
            }

            if (!mixed && atmos.atmosObject.state == AtmosState.Semiactive)
            {
                atmos.atmosObject.state = AtmosState.Inactive;
            }


            s_SimlateMixingPerfMarker.End();

            return atmos;
        }

        public static AtmosObject SimulateFlux(AtmosObject atmos)
        {
            s_SimulateFluxPerfMarker.Begin();

            if (atmos.atmosObject.state == AtmosState.Active)
            {
                atmos = SimulateFluxActive(atmos);
            }
            else if (atmos.atmosObject.state == AtmosState.Semiactive)
            {
                atmos = SimulateMixing(atmos);
            }

            s_SimulateFluxPerfMarker.End();

            return atmos;
        }

        private static AtmosObject SimulateFluxActive(AtmosObject atmos)
        {
            float pressure = atmos.atmosObject.container.GetPressure();

            // for each neighbour
            if (math.any(atmos.tileFlux > 0f))
            {
                float4 factor = atmos.atmosObject.container.GetCoreGasses() * (atmos.tileFlux / pressure);

                for (int i = 0; i < 4; i++)
                {
                    if ((!atmos.GetNeighbour(i).Equals(default(AtmosObjectInfo))) && atmos.GetNeighbour(i).state != AtmosState.Vacuum)
                    {
                        AtmosObjectInfo neighbour = atmos.GetNeighbour(i);
                        neighbour.container.AddCoreGasses(factor);
                        atmos.SetNeighbours(neighbour, i);
                    }
                    else
                    {
                        atmos.activeDirection[i] = false;
                    }
                    atmos.atmosObject.container.RemoveCoreGasses(factor);
                }
            }

            return atmos;
        }
    }
}
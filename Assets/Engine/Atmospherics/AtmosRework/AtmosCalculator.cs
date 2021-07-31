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
        public int bufferIndex;
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

            for (int i = 0; i < 4; i++)
            {
                AtmosObjectInfo info = new AtmosObjectInfo
                {
                    bufferIndex = -1,
                    container = new AtmosContainer(),
                    state = AtmosState.Blocked
                };

                info.container.Setup();
                SetNeighbour(info, i);
            }
        }

        /// Testing
        public float GetTotalGas()
        {
            float gasAmount = 0f;
            gasAmount += math.csum(atmosObject.container.GetCoreGasses());
            gasAmount += math.csum(neighbour1.container.GetCoreGasses());
            gasAmount += math.csum(neighbour2.container.GetCoreGasses());
            gasAmount += math.csum(neighbour3.container.GetCoreGasses());
            gasAmount += math.csum(neighbour4.container.GetCoreGasses());

            return gasAmount;
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

        public int GetNeighbourIndex(int index)
        {
            switch (index)
            {
                case 0:
                    return neighbour1.bufferIndex;
                case 1:
                    return neighbour2.bufferIndex;
                case 2:
                    return neighbour3.bufferIndex;
                case 3:
                    return neighbour4.bufferIndex;
            }

            return default;
        }

        public void SetNeighbourIndex(int index, int bufferIndex)
        {
            switch (index)
            {
                case 0:
                    neighbour1.bufferIndex = bufferIndex;
                    break;
                case 1:
                    neighbour2.bufferIndex = bufferIndex;
                    break;
                case 2:
                    neighbour3.bufferIndex = bufferIndex;
                    break;
                case 3:
                    neighbour4.bufferIndex = bufferIndex;
                    break;
            }
        }

        public void SetNeighbour(AtmosObjectInfo info, int index)
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

        public override string ToString()
        {
            string text = "State: " + atmosObject.state + ", Pressure: " + atmosObject.container.GetPressure() + 
                "Gasses:" + atmosObject.container.GetCoreGasses() + ", RealPressure: "+ atmosObject.container.GetRealPressure() + "\n";
            text += "Flux:" + tileFlux + "\n";
            for (int i = 0; i < 4; i++)
            {
                AtmosObjectInfo info = GetNeighbour(i);
                text += "neighbour" + i + ": " + "State: " + info.state + ", Pressure: " + info.container.GetPressure() + "\n";
            }

            return text;
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
            float total = atmos.GetTotalGas();

            s_CalculateFluxPerfMarker.Begin();

            float pressure = atmos.atmosObject.container.GetPressure();
            atmos.tileFlux = 0f;

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
                        atmos.SetNeighbour(neighbour, i);
                    }
                }

            }

            if (math.any(atmos.neighbourFlux > GasConstants.fluxEpsilon))
            {
                // float scalingFactor = math.min(1f, pressure / math.csum(atmos.neighbourFlux) / GasConstants.dt);

                // atmos.neighbourFlux *= scalingFactor;
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

            // Sanity check to see if gas is missing
            Debug.Assert(math.abs(total - atmos.GetTotalGas()) < 0.1);


            return atmos;
        }

        private static AtmosObject SimulateMixing(AtmosObject atmos)
        {
            s_SimlateMixingPerfMarker.Begin();

            float total = atmos.GetTotalGas();

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

                        for (int j = 0; j < 4; j++)
                        {
                            if (atmos.difference[j] < 0f) atmos.difference[j] = 0f;
                        }


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

                            atmos.SetNeighbour(neighbour, i);
                        }
                    }
                }
            }

            if (!mixed && atmos.atmosObject.state == AtmosState.Semiactive)
            {
                atmos.atmosObject.state = AtmosState.Inactive;
            }


            s_SimlateMixingPerfMarker.End();

            // Sanity check to see if gas is missing
            float newTotal = atmos.GetTotalGas();
            Debug.Assert(math.abs(total - newTotal) < 0.1);

            return atmos;
        }

        public static AtmosObject SimulateFlux(AtmosObject atmos)
        {
            float total = atmos.GetTotalGas();

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

            Debug.Assert(math.abs(total - atmos.GetTotalGas()) < 0.1);

            return atmos;
        }

        private static AtmosObject SimulateFluxActive(AtmosObject atmos)
        {
            float total = atmos.GetTotalGas();
            float pressure = atmos.atmosObject.container.GetPressure();

            if (pressure <= 0.1f)
                Debug.LogWarning("Pressure is too low to simulate flux");

            // for each neighbour
            if (math.any(atmos.tileFlux > 0f))
            {
                for (int i = 0; i < 4; i++)
                {
                    float4 molesToTransfer = atmos.tileFlux[i] * 1000f * atmos.atmosObject.container.GetVolume() / 
                        (atmos.atmosObject.container.GetTemperature() * GasConstants.gasConstant);

                    // Cannot transfer more gasses then there are, but always transfer a minimum.
                    molesToTransfer = math.min(molesToTransfer, atmos.atmosObject.container.GetCoreGasses());

                    for (int j = 0; j < GasConstants.numOfGases; j++)
                    {
                        if (molesToTransfer[i] > 0f)
                            molesToTransfer[i] = math.max(molesToTransfer[i], GasConstants.minMoleTransfer);
                    }

                    if (atmos.GetNeighbour(i).state != AtmosState.Vacuum)
                    {
                        AtmosObjectInfo neighbour = atmos.GetNeighbour(i);
                        neighbour.container.AddCoreGasses(molesToTransfer);
                        atmos.SetNeighbour(neighbour, i);
                    }
                    else
                    {
                        atmos.activeDirection[i] = false;
                    }

                    atmos.atmosObject.container.RemoveCoreGasses(molesToTransfer);
                }
                /*
                float4 factor = atmos.atmosObject.container.GetCoreGasses() * (atmos.tileFlux / pressure);

                for (int i = 0; i < 4; i++)
                {
                    if (atmos.tileFlux[i] > 0f)
                    {
                        if (atmos.GetNeighbour(i).state != AtmosState.Vacuum)
                        {
                            AtmosObjectInfo neighbour = atmos.GetNeighbour(i);
                            neighbour.container.AddCoreGasses(factor);
                            atmos.SetNeighbour(neighbour, i);
                        }
                        else
                        {
                            atmos.activeDirection[i] = false;
                        }
                        atmos.atmosObject.container.RemoveCoreGasses(factor);
                    }
                }
                */
            }

            // Sanity check to see if gas is missing
            float newTotal = atmos.GetTotalGas();
            Debug.Assert(math.abs(total - atmos.GetTotalGas()) < 0.1);
            if (math.abs(total - newTotal) > 0.1)
                Debug.LogError("Input/Output doesn't match");

            return atmos;
        }
    }
}
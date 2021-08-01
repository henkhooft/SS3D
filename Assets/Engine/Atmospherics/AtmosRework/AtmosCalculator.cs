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

        // public float4 tileFlux;
        // public float4 partialPressureDifference;

        // public bool4 neighbourUpdate;
        // public float4 neighbourPressure;

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
                ", Gasses: " + atmosObject.container.GetCoreGasses() + ", RealPressure: "+ atmosObject.container.GetRealPressure() + "\n";
            text += "Temperature: "+ atmosObject.container.GetTemperature() + " Kelvin" + ", Compressibility factor: " + atmosObject.container.GetCompressionFactor() + "\n";
            for (int i = 0; i < 4; i++)
            {
                AtmosObjectInfo info = GetNeighbour(i);
                text += "neighbour" + i + ": " + "State: " + info.state + ", Pressure: " + info.container.GetPressure() + 
                    ", Temperature: "+ info.container.GetTemperature() + "\n";
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

        /*
        private static AtmosObject CalculateFlux(AtmosObject atmos)
        {
            float total = atmos.GetTotalGas();

            s_CalculateFluxPerfMarker.Begin();

            float pressure = atmos.atmosObject.container.GetPressure();
            atmos.tileFlux = 0f;
            float4 pressureDifference = 0f;

            // Determine pressure difference between us and our neighbours
            for (int i = 0; i < 4; i++)
            {
                if ((!atmos.GetNeighbour(i).Equals(default(AtmosObjectInfo))) && atmos.GetNeighbour(i).state != AtmosState.Blocked)
                {
                    float neighbourPressure = atmos.GetNeighbour(i).container.GetPressure();
                    pressureDifference[i] = (pressure - neighbourPressure);
                    atmos.activeDirection[i] = true;

                    // Just make our neighbour active if we have a negative pressure difference.
                    if (pressureDifference[i] < 0f)
                    {
                        pressureDifference[i] = 0f;

                        AtmosObjectInfo neighbour = atmos.GetNeighbour(i);
                        neighbour.state = AtmosState.Active;
                        atmos.SetNeighbour(neighbour, i);
                    }
                }
            }

            // Determine the amount of moles to be transfered to equalize
            atmos.tileFlux = pressureDifference * 1000f * atmos.atmosObject.container.GetVolume() /
                        (atmos.atmosObject.container.GetTemperature() * GasConstants.gasConstant);

            // We are not transfering all moles at once. So multiply by delta time.
            atmos.tileFlux *= GasConstants.dt;

            // We need to pass the minimum threshold
            if (!math.any(atmos.tileFlux > GasConstants.fluxEpsilon))
            {
                atmos.tileFlux = 0f;
                if (!atmos.temperatureSetting)
                    atmos.atmosObject.state = AtmosState.Semiactive;
                else
                    atmos.temperatureSetting = false;
            }

            s_CalculateFluxPerfMarker.End();

            // Sanity check to see if gas is missing
            Debug.Assert(math.abs(total - atmos.GetTotalGas()) < 0.1);

            return atmos;
        }
        */

        

        public static AtmosObject SimulateFlux(AtmosObject atmos)
        {
            float total = atmos.GetTotalGas();

            s_SimulateFluxPerfMarker.Begin();

            if (atmos.atmosObject.state == AtmosState.Active)
            {
                // atmos = CalculateFlux(atmos);
                atmos = SimulateFluxActive(atmos);
            }

            
            if (atmos.atmosObject.state == AtmosState.Semiactive ||
                atmos.atmosObject.state == AtmosState.Active)
            {
                atmos = SimulateMixing(atmos);
                atmos = SimulateTemperature(atmos);
            }
            


            s_SimulateFluxPerfMarker.End();

            Debug.Assert(math.abs(total - atmos.GetTotalGas()) < 0.1);

            return atmos;
        }

        private static AtmosObject SimulateFluxActive(AtmosObject atmos)
        {
            float total = atmos.GetTotalGas();

            float pressure = atmos.atmosObject.container.GetPressure();
            for (int i = 0; i < 4; i++)
            {
                if (atmos.GetNeighbour(i).state == AtmosState.Blocked)
                    continue;

                float neighbourPressure = atmos.GetNeighbour(i).container.GetPressure();
                if ((pressure - neighbourPressure) > GasConstants.pressureEpsilon)
                {
                    atmos.activeDirection[i] = true;

                    // Use partial pressures to determine how much of each gas to move.
                    float4 partialPressureDifference = atmos.atmosObject.container.GetAllPartialPressures() - atmos.GetNeighbour(i).container.GetAllPartialPressures();

                    // Determine the amount of moles by applying the ideal gas law.
                    float4 molesToTransfer = partialPressureDifference * 1000f * atmos.atmosObject.container.GetVolume() / 
                        (atmos.atmosObject.container.GetTemperature() * GasConstants.gasConstant);

                    // Cannot transfer all moles at once
                    molesToTransfer *= GasConstants.dt;

                    // Cannot transfer more gasses then there are and no one below zero.
                    molesToTransfer = math.clamp(molesToTransfer, 0, atmos.atmosObject.container.GetCoreGasses());


                    /*
                    // Ensure a minimum and no below zero transfers.
                    for (int j = 0; j < GasConstants.numOfGases; j++)
                    {
                        if (molesToTransfer[i] > 0f)
                            molesToTransfer[i] = math.max(molesToTransfer[i], GasConstants.minMoleTransfer);

                        if (molesToTransfer[i] < 0f)
                            molesToTransfer[i] = 0f;
                    }
                    */

                    // We need to pass the minimum threshold
                    if (math.any(molesToTransfer > GasConstants.fluxEpsilon))
                    {
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
                }
                else
                {
                    if (!atmos.temperatureSetting)
                        atmos.atmosObject.state = AtmosState.Semiactive;
                    else
                        atmos.temperatureSetting = false;
                }
            }


            // Sanity check to see if gas is missing
            float newTotal = atmos.GetTotalGas();
            Debug.Assert(math.abs(total - atmos.GetTotalGas()) < 0.1);
            if (math.abs(total - newTotal) > 0.1)
                Debug.LogError("Input/Output doesn't match");

            return atmos;
        }

        private static AtmosObject SimulateMixing(AtmosObject atmos)
        {
            s_SimlateMixingPerfMarker.Begin();

            float total = atmos.GetTotalGas();

            bool mixed = false;
            if (math.any(atmos.atmosObject.container.GetCoreGasses() > 0f))
            {
                for (int i = 0; i < 4; i++)
                {
                    if ((!atmos.GetNeighbour(i).Equals(default(AtmosObjectInfo))) && atmos.GetNeighbour(i).state != AtmosState.Blocked)
                    {
                        AtmosObjectInfo neighbour = atmos.GetNeighbour(i);
                        // float4 molesToTransfer = GasConstants.gasDiffusionRate * ((atmos.atmosObject.container.GetAllPartialPressures() - neighbour.container.GetAllPartialPressures()) *
                        //     1000f * atmos.atmosObject.container.GetVolume() / (atmos.atmosObject.container.GetTemperature()));

                        float4 molesToTransfer = (atmos.atmosObject.container.GetCoreGasses() - atmos.GetNeighbour(i).container.GetCoreGasses())
                            * GasConstants.gasDiffusionRate;



                        if (math.any(molesToTransfer > GasConstants.minMoleTransfer))
                        {
                            for (int j = 0; j < 4; j++)
                            {
                                if (molesToTransfer[j] > 0f)
                                    molesToTransfer[j] = math.max(molesToTransfer[j], GasConstants.minMoleTransfer);
                                else if (molesToTransfer[j] < 0)
                                    molesToTransfer[j] = 0f;
                            }

                            neighbour.container.AddCoreGasses(molesToTransfer);
                            atmos.atmosObject.container.RemoveCoreGasses(molesToTransfer);
                            mixed = true;
                        }

                        
                        // Remain active if there is still a pressure difference
                        if (math.abs(neighbour.container.GetPressure() - atmos.atmosObject.container.GetPressure()) > GasConstants.pressureEpsilon)
                        {
                            neighbour.state = AtmosState.Active;
                        }
                        else
                        {
                            neighbour.state = AtmosState.Semiactive;
                        }

                        atmos.SetNeighbour(neighbour, i);
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

        private static AtmosObject SimulateTemperature(AtmosObject atmos)
        {
            float4 temperatureFlux = 0f;
            for (int i = 0; i < 4; i++)
            {
                if (atmos.activeDirection[i] == true)
                {
                    float difference = (atmos.atmosObject.container.GetTemperature() - atmos.GetNeighbour(i).container.GetTemperature());
                    temperatureFlux[i] = (atmos.atmosObject.container.GetTemperature() - atmos.GetNeighbour(i).container.GetTemperature()) *
                        GasConstants.thermalBase * atmos.atmosObject.container.GetVolume();

                    if (difference > GasConstants.thermalEpsilon)
                    {
                        // Set neighbour
                        AtmosObjectInfo neighbour = atmos.GetNeighbour(i);
                        neighbour.container.SetTemperature(neighbour.container.GetTemperature() + temperatureFlux[i]);
                        atmos.SetNeighbour(neighbour, i);

                        // Set self
                        atmos.atmosObject.container.SetTemperature(atmos.atmosObject.container.GetTemperature() - temperatureFlux[i]);
                        atmos.temperatureSetting = true;
                    }
                }
            }
            
            return atmos;
        }
    }
}
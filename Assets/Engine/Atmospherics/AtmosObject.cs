﻿using System;
using SS3D.Engine.Tiles;
using System.Collections;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace SS3D.Engine.Atmospherics
{
    public class AtmosObject : ScriptableObject
    {
        private AtmosManager manager;
        private AtmosContainer atmosContainer = new AtmosContainer();

        private float[] tileFlux = { 0f, 0f, 0f, 0f };
        private Vector2 velocity = Vector2.zero;
        private AtmosStates state = AtmosStates.Active;
        private TileObject[] tileNeighbours = { null, null, null, null };
        private AtmosObject[] atmosNeighbours = { null, null, null, null };
        private bool tempSetting = false;
        private bool[] activeDirection = {
                false,  // Top AtmosObject active
                false,  // Bottom AtmosObject active
                false,  // Left AtmosObject active
                false   // Right AtmosObject active
            };

        private float[] gasDifference = new float[Gas.numOfGases];
        private float[] neighbourFlux = new float[4];

        // Performance makers
        static ProfilerMarker s_CalculateFluxPerfMarker = new ProfilerMarker("AtmosObject.CalculateFlux");
        static ProfilerMarker s_CalculateFluxOnePerfMarker = new ProfilerMarker("AtmosObject.CalculateFlux.One");
        static ProfilerMarker s_SimulateFluxPerfMarker = new ProfilerMarker("AtmosObject.SimulateFlux");
        static ProfilerMarker s_SimlateMixingPerfMarker = new ProfilerMarker("AtmosObject.SimulateMixing");

        public void setTileNeighbour(TileObject neighbour, int index)
        {
            tileNeighbours[index] = neighbour;
        }

        public void setAtmosNeighbours()
        {
            int i = 0;
            foreach (TileObject tile in tileNeighbours)
            {
                if (tile != null)
                    atmosNeighbours[i] = tile.atmos;
                i++;
            }
        }

        public AtmosStates GetState()
        {
            return state;
        }

        public void SetBlocked(bool blocked)
        {
            if (!blocked)
            {
                state = AtmosStates.Active;
            }
            else
            {
                state = AtmosStates.Blocked;
            }
        }

        public Vector2 GetVelocity()
        {
            return velocity;
        }

        public void RemoveFlux()
        {
            tileFlux = new float[]{ 0f, 0f, 0f, 0f} ;
        }

        public void AddGas(AtmosGasses gas, float amount)
        {
            if (state != AtmosStates.Blocked)
            {
                atmosContainer.AddGas(gas, amount);
                state = AtmosStates.Active;
            }
        }

        public void SetGasses(float[] amounts)
        {
            atmosContainer.SetGasses(amounts);
            state = AtmosStates.Active;
        }

        public void MakeEmpty()
        {
            atmosContainer.MakeEmpty();
        }

        public void MakeAir()
        {
            MakeEmpty();

            atmosContainer.AddGas(AtmosGasses.Oxygen, 20.79f);
            atmosContainer.AddGas(AtmosGasses.Nitrogen, 83.17f);
            atmosContainer.SetTemperature(293f);;
        }

        public void AddHeat(float temp)
        {
            atmosContainer.AddHeat(temp);
            state = AtmosStates.Active;
        }

        public void RemoveHeat(float temp)
        {
            atmosContainer.RemoveHeat(temp);
            state = AtmosStates.Active;
        }

        public float GetTotalMoles()
        {
            return atmosContainer.GetTotalMoles();
        }

        public float GetPressure()
        {
            return atmosContainer.GetPressure();
        }

        public float GetPartialPressure(int index)
        {
            return atmosContainer.GetPartialPressure(index);
        }

        public float GetPartialPressure(AtmosGasses gas)
        {
            return atmosContainer.GetPartialPressure(gas);
        }

        public bool IsBreathable()
        {
            return (GetPartialPressure(AtmosGasses.Oxygen) >= 16f && GetPartialPressure(AtmosGasses.CarbonDioxide) < 8f);
        }

        public AtmosContainer GetAtmosContainer()
        {
            return atmosContainer;
        }

        public void ValidateVacuum()
        {
            // If a tile has no neighbour and is not a wall, consider it vacuum
            bool emptyTileFound = false;
            foreach (TileObject tile in tileNeighbours)
            {
                if (tile == null)
                    emptyTileFound = true;
            }
            if (emptyTileFound && state != AtmosStates.Blocked)
            {
                state = AtmosStates.Vacuum;
                MakeEmpty();
            }
        }

        public bool IsBurnable()
        {
            // TODO determine minimum burn ratio
            return (atmosContainer.GetGas(AtmosGasses.Oxygen) > 1f && atmosContainer.GetGas(AtmosGasses.Plasma) > 1f);
        }

        public bool CheckOverPressure()
        {
            if (state == AtmosStates.Blocked)
            {
                foreach (AtmosObject tile in atmosNeighbours)
                {
                    if (tile?.atmosContainer.GetPressure() > 2000)
                        return true;
                }
            }
            return false;
        }

        public void CalculateFlux()
        {
            s_CalculateFluxPerfMarker.Begin();
            Array.Clear(neighbourFlux, 0, neighbourFlux.Length);
            s_CalculateFluxOnePerfMarker.Begin();
            int i = 0;
            float pressure = GetPressure();
            foreach (AtmosObject tile in atmosNeighbours)
            {
                if (tile != null && tile.state != AtmosStates.Blocked)
                {
                    neighbourFlux[i] = Mathf.Min(tileFlux[i] * Gas.drag + (pressure - tile.GetPressure()) * Gas.dt, 1000f);
                    activeDirection[i] = true;

                    if (neighbourFlux[i] < 0f)
                    {
                        tile.state = AtmosStates.Active;
                        neighbourFlux[i] = 0f;
                    }
                }

                i++;
            }

            s_CalculateFluxOnePerfMarker.End();

            if (neighbourFlux[0] > Gas.fluxEpsilon || neighbourFlux[1] > Gas.fluxEpsilon || neighbourFlux[2] > Gas.fluxEpsilon ||
                neighbourFlux[3] > Gas.fluxEpsilon)
            {
                float scalingFactor = Mathf.Min(1,
                    pressure / (neighbourFlux[0] + neighbourFlux[1] + neighbourFlux[2] + neighbourFlux[3]) / Gas.dt);

                for (int j = 0; j < 4; j++)
                {
                    neighbourFlux[j] *= scalingFactor;
                    tileFlux[j] = neighbourFlux[j];
                }
            }
            else
            {
                for (int j = 0; j < 4; j++)
                {
                    tileFlux[j] = 0;
                }

                if (!tempSetting)
                {
                    state = AtmosStates.Semiactive;
                }
                else
                {
                    tempSetting = false;
                }
            }

            s_CalculateFluxPerfMarker.End();
        }

        public void SimulateFlux()
        {
            s_SimulateFluxPerfMarker.Begin();
            float[] gasses = atmosContainer.GetGasses();

            if (state == AtmosStates.Active)
            {
                float pressure = GetPressure();

                for (int i = 0; i < Gas.numOfGases; i++)
                {
                    if (gasses[i] < 1f)
                        gasses[i] = 0f;

                    if (gasses[i] > 0f)
                    {
                        int k = 0;
                        foreach (AtmosObject tile in atmosNeighbours)
                        {
                            if (tileFlux[k] > 0f)
                            {
                                float factor = gasses[i] * (tileFlux[k] / pressure);
                                if (tile.state != AtmosStates.Vacuum)
                                {
                                    tile.atmosContainer.GetGasses()[i] += factor;
                                    tile.state = AtmosStates.Active;
                                }
                                else
                                {
                                    activeDirection[k] = false;
                                }
                                gasses[i] -= factor;
                            }
                            k++;
                        }
                    }
                }

                float difference;
                int j = 0;
                foreach (AtmosObject tile in atmosNeighbours)
                {
                    if (activeDirection[j] == true)
                    {
                        difference = (atmosContainer.GetTemperature() - tile.atmosContainer.GetTemperature()) * Gas.thermalBase * atmosContainer.Volume; // / (GetSpecificHeat() * 5f);

                        if (difference > Gas.thermalEpsilon)
                        {
                            tile.atmosContainer.SetTemperature(tile.atmosContainer.GetTemperature() + difference);
                            atmosContainer.SetTemperature(atmosContainer.GetTemperature() - difference);
                            tempSetting = true;
                        }
                    }
                    j++;
                }

                float fluxFromLeft = 0;
                if (atmosNeighbours[2] != null)
                    fluxFromLeft = atmosNeighbours[2].tileFlux[3];
                float fluxFromRight = 0;
                if (atmosNeighbours[3] != null)
                    fluxFromLeft = atmosNeighbours[3].tileFlux[2];

                float fluxFromTop = 0;
                if (atmosNeighbours[0] != null)
                    fluxFromTop = atmosNeighbours[0].tileFlux[1];

                float fluxFromBottom = 0;
                if (atmosNeighbours[1] != null)
                    fluxFromBottom = atmosNeighbours[1].tileFlux[0];

                float velHorizontal = tileFlux[3] - fluxFromLeft - tileFlux[2] + fluxFromRight;
                float velVertical = tileFlux[0] - fluxFromTop - tileFlux[1] + fluxFromBottom;

                velocity = new Vector2(velHorizontal, velVertical);
            }
            else if (state == AtmosStates.Semiactive)
            {
                velocity = Vector2.zero;
                SimulateMixing();
            }
            s_SimulateFluxPerfMarker.End();
        }

        public void SimulateMixing()
        {
            float[] gasses = atmosContainer.GetGasses();

            if (AtmosHelper.ArrayZero(gasses, Gas.mixRate))
                return;

            s_SimlateMixingPerfMarker.Begin();
            bool mixed = false;
            Array.Clear(gasDifference, 0, gasDifference.Length);

            foreach (AtmosObject atmosObject in atmosNeighbours)
            {
                if (atmosObject == null)
                    continue;
                if (atmosObject.state != AtmosStates.Blocked)
                {
                    // Get difference in total moles and individual gasses
                    float pressure = GetTotalMoles();
                    float neighbourPressure = atmosObject.GetTotalMoles();
                    ArrayDiff(gasDifference, gasses, atmosObject.atmosContainer.GetGasses(), Gas.mixRate);

                    // If our moles / mixrate > 0.1f
                    if (AtmosHelper.ArrayZero(gasDifference, Gas.mixRate))
                    {
                        // Set neighbour gasses to the normalized 
                        ArraySum(atmosObject.atmosContainer.GetGasses(), gasDifference);
                        AtmosHelper.ArrayNorm(atmosObject.atmosContainer.GetGasses(), neighbourPressure);

                        if (atmosObject.state == AtmosStates.Inactive)
                        {
                            atmosObject.state = AtmosStates.Semiactive;
                        }

                        // Set our own gasses to the normalized
                        ArrayDiff(gasses, gasses, gasDifference, Gas.mixRate);
                        AtmosHelper.ArrayNorm(gasses, pressure);
                        mixed = true;
                    }
                }
            }

            if (!mixed && state == AtmosStates.Semiactive)
            {
                state = AtmosStates.Inactive;
            }
            s_SimlateMixingPerfMarker.End();
        }

        public void ArrayDiff(float[] difference, float[] arr1, float[] arr2, float mixRate)
        {
            for (int i = 0; i < Gas.numOfGases; ++i)
            {
                difference[i] = (arr1[i] - arr2[i]) * mixRate;
            }
        }

        public void ArraySum(float[] arr1, float[] arr2)
        {
            for (int i = 0; i < Gas.numOfGases; ++i)
            {
                arr1[i] = arr1[i] + arr2[i];
            }
        }
    }
}
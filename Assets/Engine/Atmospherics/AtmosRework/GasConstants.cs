using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.Mathematics;

namespace SS3D.Engine.AtmosphericsRework
{
    public enum AtmosState
    {
        Active = 0,     // Tile is active; equalizes pressures, temperatures and mixes gasses
        Semiactive = 1, // No pressure equalization, but mixes gasses
        Inactive = 2,   // Do nothing
        Vacuum = 3,     // Drain other tiles
        Blocked = 4     // Wall, skips calculations
    }

    /// <summary>
    /// Most commonly used gasses. Use a float4 struct for SIMD optimization.
    /// </summary>
    public enum CoreAtmosGasses
    {
        Oxygen = 0,
        Nitrogen = 1,
        CarbonDioxide = 2,
        Plasma = 3
    }

    public static class GasConstants
    {
        // Gas constants
        public const float dt = 0.1f;               // Delta time
        public const float gasConstant = 8.314f;    // Universal gas constant
        public const float drag = 0.95f;            // Fluid drag, slows down flux so that gases don't infinitely slosh
        public const float thermalBase = 0.024f;    // * volume | Rate of temperature equalization
        public const float mixRate = 0.1f;          // Rate of gas mixing
        public const float fluxEpsilon = 0.025f;    // Minimum pressure difference to simulate
        public const float thermalEpsilon = 0.01f;  // Minimum temperature difference to simulate

        public const float windFactor = 0.2f;       // How much force will any wind apply
        public const float minimumWind = 1f;        // Minimum wind required to move items

        public const float maxMoleTransfer = 2f;    // The maximum amount of moles that machines can move per atmos step
        public const float minMoleTransfer = 0.1f;  // The minimum amount of moles that are transfered for every step

        public static float4 coreSpecificHeat = new float4(
            2f,     // Oxygen
            20f,    // Nitrogen
            3f,     // Carbon Dioxide
            10f);   // plasma

        public static float4 coreGasDensity = new float4(
            32f,     // Oxygen
            28f,    // Nitrogen
            44f,     // Carbon Dioxide
            78f);   // plasma

        public static int numOfGases = Enum.GetNames(typeof(AtmosState)).Length;
    }
}
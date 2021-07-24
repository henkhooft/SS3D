using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SS3D.Engine.AtmosphericsRework
{
    public enum AtmosStates
    {
        Active,     // Tile is active; equalizes pressures, temperatures and mixes gasses
        Semiactive, // No pressure equalization, but mixes gasses
        Inactive,   // Do nothing
        Vacuum,     // Drain other tiles
        Blocked     // Wall, skips calculations
    }

    public enum AtmosGasses
    {
        Oxygen,
        Nitrogen,
        CarbonDioxide,
        Plasma
    }

    public class GasConstants
    {
        
    }
}
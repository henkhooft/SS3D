using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SS3D.Systems.Substances
{
    public class Substance : ScriptableObject
    {
        // Generic parameters
        public string nameString;

        // Visual parameters
        public Color color;

        // Chemical parameters
        public float molarMass;
        public float specificHeatCapacity;
        public float density;
        public float maxSoluability;
    }
}
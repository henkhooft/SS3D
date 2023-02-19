using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SS3D.Systems.Substances
{
    public class Substance : ScriptableObject
    {
        // Generic parameters
        public string nameString;
        public SubstanceAttributes attributes;

        // Visual parameters
        public Color color;

        // Chemical parameters
        public float molarMass;
        public float specificHeatCapacity;
        public float density;
        public float maxSoluability;

        public float AntoineConstantA;
        public float AntoineConstantB;
        public float AntoineConstantC;
    }
}
using Unity.Burst;
// using Unity.Mathematics;
using UnityEngine;


namespace SS3D.Systems.Substances
{
    [CreateAssetMenu(fileName = "SubstanceSo", menuName = "Substances/SubstanceSO", order = 0)]
    public class SubstanceSo : ScriptableObject
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

        /*
        [BurstCompile]
        public static float CalculateBoilingPoint(float A, float B, float C, float temperature, float pressure)
        {
            float P = pressure; // atmospheric pressure in Pa
            float logP = math.log10(P); // base 10 logarithm of pressure
            float T = temperature;
            float logPc = A - B / (T + C);
            float logTb = B / (A - logP + logPc) - C;
            float boilingPoint = math.pow(10, logTb);
            return boilingPoint;
        }
        */
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SS3D.Systems.Substances
{
    [CreateAssetMenu(fileName = "MixtureSo", menuName = "Substances/MixtureSO", order = 0)]
    public class MixtureSo : ScriptableObject
    {
        public SubstanceSo[] substances;
        public float temperature;
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SS3D.Systems.Substances
{
    [CreateAssetMenu(fileName = "ReactionSo", menuName = "Substances/ReactionSO", order = 0)]
    public class ReactionSo : ScriptableObject
    {
        [Serializable]
        public struct ReactionDescription
        {
            public SubstanceSo substance;
            public float reactionRatio;
        }

        [Serializable]
        public struct CatalystDescription
        {
            public SubstanceSo substance;
            public float reactionRateRatio;
        }

        public ReactionDescription[] inputs;
        public ReactionDescription[] outputs;

        public ReactionCondition[] conditions;
        public CatalystDescription catalyst;
        public ReactionEffect[] effects;

        public float energyProduced;
        public float reactionRate;
    }
}
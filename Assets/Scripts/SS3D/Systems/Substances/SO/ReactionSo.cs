using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SS3D.Systems.Substances
{
    [Serializable]
    public class ReactionDescription
    {
        public SubstanceSo substance;
        public float reactionRatio;
    }

    [CreateAssetMenu(fileName = "ReactionSo", menuName = "Substances/ReactionSO", order = 0)]
    public class ReactionSo : ScriptableObject
    {
        public ReactionDescription[] inputs;
        public ReactionDescription[] outputs;

        public ReactionCondition[] conditions;
        public ReactionEffect[] effects;

        public float energyProduced;
        public float reactionRate;

    }
}
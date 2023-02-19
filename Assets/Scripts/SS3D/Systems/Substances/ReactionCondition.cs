using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SS3D.Systems.Substances
{
    [Serializable]
    public class ReactionCondition
    {
        public float minimumTemperature;
        public float maximumTemperature;

        public float minimumPH;
        public float maximumPH;
    }
}
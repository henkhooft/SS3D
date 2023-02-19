using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SS3D.Systems.Substances
{
    [Serializable]
    public class ReactionCondition
    {
        public float temperatureMinimum;
        public float temperatureIdeal;
        public float temperatureMaximum;

        public float PhMinimum;
        public float PhIdeal;
        public float PhMaximum;

        public float pressureMinimum;
        public float pressureIdeal;
        public float pressureMaximum;

        public float concentrationMinimum;
        public float concentrationMaximum;


        public float GetEfficiency(float value)
        {
            if (temperatureMinimum > value || temperatureMaximum > value)
                return 0f;
            else if (value < temperatureIdeal)
                return (value - temperatureIdeal) / (temperatureIdeal - temperatureMinimum);
            else if (value <= temperatureIdeal)
                return (temperatureMaximum - value) / (temperatureMaximum - temperatureIdeal);

            return 0f;
        }
    }
}
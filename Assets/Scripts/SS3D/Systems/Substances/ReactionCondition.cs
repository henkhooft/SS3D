using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SS3D.Systems.Substances
{
    [Serializable]
    public class ReactionCondition
    {
        public enum ReactionConditionType
        {
            TemperatureCondition,
            PHCondition,
            PressureCondition,
            ConcentrationCondition
        }

        public ReactionConditionType condition;
        public float minimumValue;
        public float maximumValue;
        public float idealValue;


        public float GetEfficiency(float value)
        {
            if (minimumValue > value || maximumValue > value)
                return 0f;
            else if (value < idealValue)
                return (value - idealValue) / (idealValue - minimumValue);
            else if (value <= idealValue)
                return (maximumValue - value) / (maximumValue - idealValue);

            return 0f;
        }
    }
}
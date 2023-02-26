using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SS3D.Systems.Substances
{
    public struct Substance
    {
        public ushort substanceIndex;
        public float amount;

        public Substance(ushort substanceIndex, float amount)
        {
            this.substanceIndex = substanceIndex;
            this.amount = amount;
        }
    }
}
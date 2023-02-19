using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SS3D.Systems.Substances
{
    [System.Flags]
    public enum SubstanceAttributes
    {
        None,
        Alcoholic,
        Odorless,
        Flamable,
        Poisenous
    }
}
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace SS3D.Systems.Substances
{
    public struct SubstancesMixture
    {
        public NativeArray<Substance> substances;
        public float temperature;

        public SubstancesMixture(int size, Allocator allocator)
        {
            substances = new NativeArray<Substance>(size, allocator);
            temperature = 0;
        }

        public void Dispose()
        {
            substances.Dispose();
        }
    }
}
using SS3D.Systems.Atmospherics.ECS;
using System;
using Unity.Collections;
using UnityEngine;

namespace SS3D.Systems.Atmospherics.Visualization
{
    /// <summary>
    /// Encodes raw sim cell state into the byte/Color32 formats the GPU atlas (and its network
    /// patches) use. Shared by <see cref="AtmosGpuUploader"/> (host atlas) and
    /// <see cref="AtmosChunkPatchBuilder"/> (per-chunk network patches) so both agree on the mask
    /// and composition encoding.
    /// </summary>
    public static class AtmosCellVisualEncoding
    {
        public const int CompositionGasChannels = 4;

        public const byte MaskEmpty = 0;
        public const byte MaskSimulated = 1;
        public const byte MaskVacuum = 2;
        public const byte MaskBlocked = 3;

        public static byte EncodeMask(AtmosCellState state)
        {
            return state switch
            {
                AtmosCellState.Vacuum => MaskVacuum,
                AtmosCellState.Blocked => MaskBlocked,
                AtmosCellState.Active or AtmosCellState.Semiactive or AtmosCellState.Inactive => MaskSimulated,
                _ => MaskEmpty,
            };
        }

        public static Color32 EncodeComposition(NativeArray<float>.ReadOnly molesRead, int cellIndex)
        {
            float totalMoles = 0f;
            Span<float> moles = stackalloc float[CompositionGasChannels];

            for (int gasId = 0; gasId < CompositionGasChannels; gasId++)
            {
                moles[gasId] = molesRead[GasMixture.GetMoleIndex(cellIndex, new GasId((ushort)gasId))];
                totalMoles += moles[gasId];
            }

            if (totalMoles <= 1e-6f)
                return new Color32(0, 0, 0, 0);

            return new Color32(
                ToByte(moles[0] / totalMoles),
                ToByte(moles[1] / totalMoles),
                ToByte(moles[2] / totalMoles),
                ToByte(moles[3] / totalMoles));
        }

        public static byte ToByte(float normalized)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(normalized * 255f), 0, 255);
        }
    }
}

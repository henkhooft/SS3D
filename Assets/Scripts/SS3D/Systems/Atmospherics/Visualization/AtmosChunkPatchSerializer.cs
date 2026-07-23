using FishNet.Serializing;

namespace SS3D.Systems.Atmospherics.Visualization
{
    /// <summary>
    /// Custom FishNet wire format for <see cref="AtmosChunkPatch"/>. Named to match FishNet's
    /// custom-serializer convention (Write/Read + type name as Writer/Reader extensions), same
    /// pattern as the machine interface snapshot serializers.
    /// </summary>
    public static class AtmosChunkPatchSerializer
    {
        public static void WriteAtmosChunkPatch(this Writer writer, AtmosChunkPatch patch)
        {
            writer.WriteInt32(patch.MapId);
            writer.WriteVector2Int(patch.ChunkKey);

            for (int i = 0; i < AtmosChunkPatch.CellCount; i++)
                writer.WriteSingle(patch.Pressure[i]);

            for (int i = 0; i < AtmosChunkPatch.CellCount; i++)
                writer.WriteSingle(patch.Temperature[i]);

            for (int i = 0; i < AtmosChunkPatch.CellCount; i++)
                writer.WriteColor32(patch.Composition[i]);

            for (int i = 0; i < AtmosChunkPatch.CellCount * 2; i++)
                writer.WriteSingle(patch.Flow[i]);

            for (int i = 0; i < AtmosChunkPatch.CellCount; i++)
                writer.WriteSingle(patch.FireIntensity[i]);

            for (int i = 0; i < AtmosChunkPatch.CellCount; i++)
                writer.WriteByte(patch.Mask[i]);
        }

        public static AtmosChunkPatch ReadAtmosChunkPatch(this Reader reader)
        {
            var patch = new AtmosChunkPatch
            {
                MapId = reader.ReadInt32(),
                ChunkKey = reader.ReadVector2Int(),
                Pressure = new float[AtmosChunkPatch.CellCount],
                Temperature = new float[AtmosChunkPatch.CellCount],
                Composition = new UnityEngine.Color32[AtmosChunkPatch.CellCount],
                Flow = new float[AtmosChunkPatch.CellCount * 2],
                FireIntensity = new float[AtmosChunkPatch.CellCount],
                Mask = new byte[AtmosChunkPatch.CellCount],
            };

            for (int i = 0; i < AtmosChunkPatch.CellCount; i++)
                patch.Pressure[i] = reader.ReadSingle();

            for (int i = 0; i < AtmosChunkPatch.CellCount; i++)
                patch.Temperature[i] = reader.ReadSingle();

            for (int i = 0; i < AtmosChunkPatch.CellCount; i++)
                patch.Composition[i] = reader.ReadColor32();

            for (int i = 0; i < AtmosChunkPatch.CellCount * 2; i++)
                patch.Flow[i] = reader.ReadSingle();

            for (int i = 0; i < AtmosChunkPatch.CellCount; i++)
                patch.FireIntensity[i] = reader.ReadSingle();

            for (int i = 0; i < AtmosChunkPatch.CellCount; i++)
                patch.Mask[i] = reader.ReadByte();

            return patch;
        }
    }
}

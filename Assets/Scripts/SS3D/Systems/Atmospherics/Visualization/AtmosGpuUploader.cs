using SS3D.Rendering.URP;
using SS3D.Systems.Atmospherics.ECS;
using SS3D.Systems.Tile;
using System;
using UnityEngine;

namespace SS3D.Systems.Atmospherics.Visualization
{
    /// <summary>
    /// Builds GPU atlas textures from the authoritative turf gas simulation.
    /// </summary>
    public sealed class AtmosGpuUploader : IDisposable
    {
        // Sim burn intensity is cleared every tick; decay the uploaded fire texture slower so
        // flames read longer than a single 0.2s plasma reaction step.
        private const float VisualFireDecayPerTick = AtmosVisualMetrics.VisualFireDecayPerTick;

        private Texture2D _pressure;
        private Texture2D _temperature;
        private Texture2D _composition;
        private Texture2D _flow;
        private Texture2D _fireIntensity;
        private Texture2D _mask;

        private float[] _pressureScratch;
        private float[] _temperatureScratch;
        private Color32[] _compositionScratch;
        private float[] _flowScratch;
        private float[] _fireScratch;
        private float[] _visualFireScratch;
        private byte[] _maskScratch;

        private int _atlasWidth;
        private int _atlasHeight;
        private int _minTileX;
        private int _minTileZ;
        private int _mapId = -1;
        private bool _valid;

        public bool IsValid => _valid;

        public void Refresh(AtmosSimulation simulation)
        {
            _valid = false;
            if (simulation == null || simulation.CellCount == 0)
                return;

            if (!TryComputeBounds(simulation, out int minX, out int minZ, out int maxX, out int maxZ))
                return;

            int dataWidth = maxX - minX + 1;
            int dataHeight = maxZ - minZ + 1;
            int atlasWidth = NextPowerOfTwo(dataWidth);
            int atlasHeight = NextPowerOfTwo(dataHeight);

            EnsureAtlas(atlasWidth, atlasHeight);
            ClearScratch(atlasWidth * atlasHeight);

            _minTileX = minX;
            _minTileZ = minZ;
            _atlasWidth = atlasWidth;
            _atlasHeight = atlasHeight;
            _mapId = simulation.MapId;

            simulation.ForEachCell((coord, cellIndex) =>
            {
                int texel = GetTexelIndex(coord.Grid.x - minX, coord.Grid.y - minZ);
                if (texel < 0)
                    return;

                AtmosCellMeta meta = simulation.CellMeta[cellIndex];
                _pressureScratch[texel] = simulation.GetCellPressure(cellIndex);
                _temperatureScratch[texel] = meta.Temperature;
                float simFire = simulation.BurnIntensity.IsCreated
                    ? simulation.BurnIntensity[cellIndex]
                    : 0f;
                float decayedFire = _visualFireScratch[texel] * VisualFireDecayPerTick;
                float visualFire = Mathf.Max(simFire, decayedFire);
                _visualFireScratch[texel] = visualFire;
                _fireScratch[texel] = visualFire;
                _maskScratch[texel] = AtmosCellVisualEncoding.EncodeMask(meta.State);
                _compositionScratch[texel] = AtmosCellVisualEncoding.EncodeComposition(simulation.MolesRead, cellIndex);
            });

            WriteFlowGradients(simulation, minX, minZ);
            UploadTextures();
            _valid = true;
        }

        public AtmosRenderContext.Snapshot BuildSnapshot(GasVisualProfileBuilder.GpuSet gasProfiles)
        {
            return new AtmosRenderContext.Snapshot
            {
                Pressure = _pressure,
                Temperature = _temperature,
                Composition = _composition,
                Flow = _flow,
                FireIntensity = _fireIntensity,
                Mask = _mask,
                AtlasBounds = new Vector4(_minTileX, _minTileZ, _atlasWidth, _atlasHeight),
                MapId = _mapId,
                Valid = _valid,
                IgnitionTemperature = AtmosFluxConstants.PlasmaIgnitionTemperature,
                GasScatter = gasProfiles.Scatter,
                GasEmission = gasProfiles.Emission,
                GasMisc = gasProfiles.Misc,
            };
        }

        public void Dispose()
        {
            DestroyTexture(ref _pressure);
            DestroyTexture(ref _temperature);
            DestroyTexture(ref _composition);
            DestroyTexture(ref _flow);
            DestroyTexture(ref _fireIntensity);
            DestroyTexture(ref _mask);
            _valid = false;
        }

        private static bool TryComputeBounds(AtmosSimulation simulation, out int minX, out int minZ, out int maxX, out int maxZ)
        {
            int localMinX = int.MaxValue;
            int localMinZ = int.MaxValue;
            int localMaxX = int.MinValue;
            int localMaxZ = int.MinValue;

            simulation.ForEachCoord(coord =>
            {
                localMinX = Mathf.Min(localMinX, coord.Grid.x);
                localMinZ = Mathf.Min(localMinZ, coord.Grid.y);
                localMaxX = Mathf.Max(localMaxX, coord.Grid.x);
                localMaxZ = Mathf.Max(localMaxZ, coord.Grid.y);
            });

            minX = localMinX;
            minZ = localMinZ;
            maxX = localMaxX;
            maxZ = localMaxZ;
            return localMinX != int.MaxValue;
        }

        private void WriteFlowGradients(AtmosSimulation simulation, int minX, int minZ)
        {
            simulation.ForEachCell((coord, cellIndex) =>
            {
                int texel = GetTexelIndex(coord.Grid.x - minX, coord.Grid.y - minZ);
                if (texel < 0 || _maskScratch[texel] == AtmosCellVisualEncoding.MaskEmpty)
                    return;

                float pressure = _pressureScratch[texel];
                float gradientX = SamplePressureOffset(simulation, coord, 1, 0, minX, minZ) - pressure;
                float gradientZ = SamplePressureOffset(simulation, coord, 0, 1, minX, minZ) - pressure;

                Vector2 gradient = new Vector2(gradientX, gradientZ);
                if (gradient.sqrMagnitude > 1e-6f)
                    gradient = gradient.normalized;

                // Pack -1..1 into 0..1 for RG storage; shader unpacks in Phase 2.
                int flowIndex = texel * 2;
                _flowScratch[flowIndex] = gradient.x * 0.5f + 0.5f;
                _flowScratch[flowIndex + 1] = gradient.y * 0.5f + 0.5f;
            });
        }

        private float SamplePressureOffset(
            AtmosSimulation simulation,
            TileCoord coord,
            int offsetX,
            int offsetZ,
            int minX,
            int minZ)
        {
            var neighbourCoord = new TileCoord(coord.MapId, coord.Grid.x + offsetX, coord.Grid.y + offsetZ);
            if (!simulation.TryGetCellIndex(neighbourCoord, out int neighbourIndex))
                return 0f;

            int texel = GetTexelIndex(neighbourCoord.Grid.x - minX, neighbourCoord.Grid.y - minZ);
            if (texel < 0)
                return simulation.GetCellPressure(neighbourIndex);

            return _pressureScratch[texel];
        }

        private int GetTexelIndex(int localX, int localZ)
        {
            if (localX < 0 || localZ < 0 || localX >= _atlasWidth || localZ >= _atlasHeight)
                return -1;

            return localZ * _atlasWidth + localX;
        }

        private void EnsureAtlas(int width, int height)
        {
            int pixelCount = width * height;
            AtmosGpuAtlasTextureUtility.EnsureTexture(ref _pressure, width, height, TextureFormat.RFloat, FilterMode.Bilinear);
            AtmosGpuAtlasTextureUtility.EnsureTexture(ref _temperature, width, height, TextureFormat.RFloat, FilterMode.Bilinear);
            AtmosGpuAtlasTextureUtility.EnsureTexture(ref _composition, width, height, TextureFormat.RGBA32, FilterMode.Bilinear);
            AtmosGpuAtlasTextureUtility.EnsureTexture(ref _flow, width, height, TextureFormat.RGFloat, FilterMode.Bilinear);
            AtmosGpuAtlasTextureUtility.EnsureTexture(ref _fireIntensity, width, height, TextureFormat.RFloat, FilterMode.Bilinear);
            AtmosGpuAtlasTextureUtility.EnsureTexture(ref _mask, width, height, TextureFormat.R8, FilterMode.Point);

            EnsureScratch(ref _pressureScratch, pixelCount);
            EnsureScratch(ref _temperatureScratch, pixelCount);
            EnsureScratch(ref _compositionScratch, pixelCount);
            EnsureScratch(ref _flowScratch, pixelCount * 2);
            EnsureScratch(ref _fireScratch, pixelCount);
            EnsureScratch(ref _visualFireScratch, pixelCount);
            EnsureScratch(ref _maskScratch, pixelCount);
        }

        private static void EnsureScratch<T>(ref T[] scratch, int length)
        {
            if (scratch == null || scratch.Length != length)
                scratch = new T[length];
        }

        private void ClearScratch(int pixelCount)
        {
            Array.Clear(_pressureScratch, 0, pixelCount);
            Array.Clear(_temperatureScratch, 0, pixelCount);
            Array.Clear(_fireScratch, 0, pixelCount);
            Array.Clear(_maskScratch, 0, pixelCount);

            var clearComposition = new Color32(0, 0, 0, 0);
            for (int i = 0; i < pixelCount; i++)
                _compositionScratch[i] = clearComposition;

            for (int i = 0; i < pixelCount * 2; i += 2)
            {
                _flowScratch[i] = 0.5f;
                _flowScratch[i + 1] = 0.5f;
            }
        }

        private void UploadTextures()
        {
            _pressure.SetPixelData(_pressureScratch, 0);
            _pressure.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            _temperature.SetPixelData(_temperatureScratch, 0);
            _temperature.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            _composition.SetPixelData(_compositionScratch, 0);
            _composition.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            _flow.SetPixelData(_flowScratch, 0);
            _flow.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            _fireIntensity.SetPixelData(_fireScratch, 0);
            _fireIntensity.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            _mask.SetPixelData(_maskScratch, 0);
            _mask.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        }

        private static int NextPowerOfTwo(int value) => AtmosGpuAtlasTextureUtility.NextPowerOfTwo(value);

        private static void DestroyTexture(ref Texture2D texture) => AtmosGpuAtlasTextureUtility.DestroyTexture(ref texture);
    }
}

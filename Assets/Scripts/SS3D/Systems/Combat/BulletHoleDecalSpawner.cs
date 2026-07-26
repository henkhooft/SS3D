using Coimbra;
using SS3D.Rendering.URP;
using SS3D.Systems.Health;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SS3D.Systems.Combat
{
    /// <summary>
    /// Spawns capped URP bullet-hole decals on non-living ranged impacts (walls/floors/props).
    /// </summary>
    public static class BulletHoleDecalSpawner
    {
        private const int MaxActiveDecals = 96;
        private const float MinSize = 0.12f;
        private const float MaxSize = 0.22f;
        private const float VolumeDepth = 0.12f;

        private static GameObject _floorDecalPrefab;
        private static Material _decalMaterialTemplate;
        private static readonly List<GameObject> ActiveDecals = new();

        public static bool IsSupported
        {
            get
            {
                EnsureAssetsLoaded();
                return _decalMaterialTemplate != null;
            }
        }

        public static void Spawn(Vector3 point, Vector3 normal)
        {
            if (!IsSupported)
            {
                return;
            }

            if (normal.sqrMagnitude < 0.0001f)
            {
                normal = Vector3.up;
            }
            else
            {
                normal.Normalize();
            }

            GameObject instance = CreateDecalInstance();
            if (instance == null)
            {
                return;
            }

            instance.transform.SetPositionAndRotation(
                point + normal * 0.015f,
                BloodDecalSpawner.RotationOntoSurface(normal, Random.Range(0f, 360f)));

            if (instance.TryGetComponent(out DecalProjector projector))
            {
                projector.renderingLayerMask = DecalRenderingLayers.WorldFloorProjectorMask;
                projector.material = CreateRandomDecalMaterial();
                float size = Random.Range(MinSize, MaxSize);
                projector.size = new Vector3(size, size, VolumeDepth);
                projector.pivot = new Vector3(0f, 0f, VolumeDepth * 0.5f);
                projector.fadeFactor = 1f;
                projector.uvScale = new Vector2(Random.Range(0.95f, 1.05f), Random.Range(0.95f, 1.05f));
                projector.drawDistance = 50f;
            }

            ActiveDecals.Add(instance);
            TrimOldDecals();
        }

        private static GameObject CreateDecalInstance()
        {
            EnsureAssetsLoaded();
            if (_floorDecalPrefab != null)
            {
                return Object.Instantiate(_floorDecalPrefab);
            }

            var go = new GameObject("BulletHoleFloorDecal");
            DecalProjector projector = go.AddComponent<DecalProjector>();
            projector.material = _decalMaterialTemplate;
            projector.scaleMode = DecalScaleMode.ScaleInvariant;
            projector.size = new Vector3(0.18f, 0.18f, VolumeDepth);
            projector.pivot = new Vector3(0f, 0f, VolumeDepth * 0.5f);
            projector.drawDistance = 50f;
            projector.startAngleFade = 180f;
            projector.endAngleFade = 180f;
            projector.renderingLayerMask = DecalRenderingLayers.WorldFloorProjectorMask;
            return go;
        }

        private static Material CreateRandomDecalMaterial()
        {
            BulletHoleVfxCatalog catalog = BulletHoleVfxCatalog.Instance;
            if (catalog != null)
            {
                Material instance = catalog.CreateDecalMaterial();
                if (instance != null)
                {
                    return instance;
                }
            }

            return _decalMaterialTemplate != null ? new Material(_decalMaterialTemplate) : null;
        }

        private static void EnsureAssetsLoaded()
        {
            if (_decalMaterialTemplate != null)
            {
                return;
            }

            BulletHoleVfxCatalog catalog = BulletHoleVfxCatalog.Instance;
            if (catalog != null)
            {
                _decalMaterialTemplate = catalog.DecalMaterial;
                _floorDecalPrefab = catalog.FloorDecalPrefab;
            }
        }

        private static void TrimOldDecals()
        {
            while (ActiveDecals.Count > MaxActiveDecals)
            {
                GameObject oldest = ActiveDecals[0];
                ActiveDecals.RemoveAt(0);
                if (oldest == null)
                {
                    continue;
                }

                if (oldest.TryGetComponent(out DecalProjector projector)
                    && projector.material != null
                    && projector.material != _decalMaterialTemplate)
                {
                    Object.Destroy(projector.material);
                }

                oldest.Dispose(true);
            }
        }
    }
}

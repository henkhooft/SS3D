using Coimbra;
using System.Collections;
using UnityEngine;

namespace SS3D.Systems.Combat
{
    /// <summary>
    /// Procedural muzzle flash — short point light + particle burst at the firearm muzzle socket.
    /// Client-local presentation; spawned via ObserversRpc so all observers see the shot report.
    /// </summary>
    public static class MuzzleFlashVfx
    {
        private const float LifetimeSeconds = 0.08f;
        private const float LightIntensity = 6.5f;
        private const float LightRange = 2.2f;
        private static Material _particleMaterial;

        public static void Play(Vector3 worldPosition, Vector3 worldForward)
        {
            if (worldForward.sqrMagnitude < 0.0001f)
            {
                worldForward = Vector3.forward;
            }

            GameObject root = new("MuzzleFlash");
            root.hideFlags = HideFlags.HideAndDontSave;
            root.transform.SetPositionAndRotation(
                worldPosition,
                Quaternion.LookRotation(worldForward.normalized));

            MuzzleFlashEffect effect = root.AddComponent<MuzzleFlashEffect>();
            effect.Play(LifetimeSeconds, LightIntensity, LightRange);
        }

        private sealed class MuzzleFlashEffect : MonoBehaviour
        {
            private Light _light;
            private ParticleSystem _burst;

            public void Play(float lifetime, float intensity, float range)
            {
                EnsureConfigured();
                if (_light != null)
                {
                    _light.enabled = true;
                    _light.intensity = intensity;
                    _light.range = range;
                }

                if (_burst != null)
                {
                    _burst.Play(true);
                }

                StartCoroutine(Run(lifetime, intensity));
            }

            private IEnumerator Run(float lifetime, float startIntensity)
            {
                float elapsed = 0f;
                while (elapsed < lifetime)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / lifetime);
                    // Hard peak then rapid fall — reads as a gunshot flash, not a lantern.
                    float envelope = t < 0.2f ? 1f : 1f - ((t - 0.2f) / 0.8f);
                    if (_light != null)
                    {
                        _light.intensity = startIntensity * Mathf.Max(0f, envelope);
                    }

                    yield return null;
                }

                gameObject.Dispose(true);
            }

            private void EnsureConfigured()
            {
                _light = gameObject.AddComponent<Light>();
                _light.type = LightType.Point;
                _light.shadows = LightShadows.None;
                _light.color = new Color(1f, 0.82f, 0.45f, 1f);
                _light.enabled = false;

                GameObject burstHost = new("Burst");
                burstHost.transform.SetParent(transform, false);
                burstHost.transform.localPosition = Vector3.zero;
                _burst = burstHost.AddComponent<ParticleSystem>();
                ConfigureBurst(_burst);

                ParticleSystemRenderer renderer = burstHost.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sharedMaterial = GetOrCreateParticleMaterial();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            private static void ConfigureBurst(ParticleSystem ps)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                ParticleSystem.MainModule main = ps.main;
                main.playOnAwake = false;
                main.loop = false;
                main.duration = 0.05f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.04f, 0.07f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
                main.startColor = new Color(1f, 0.92f, 0.65f, 1f);
                main.gravityModifier = 0f;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.maxParticles = 18;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;

                ParticleSystem.EmissionModule emission = ps.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });

                ParticleSystem.ShapeModule shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 18f;
                shape.radius = 0.01f;
                shape.length = 0.05f;

                ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
                colorOverLifetime.enabled = true;
                Gradient gradient = new();
                gradient.SetKeys(
                    new[]
                    {
                        new GradientColorKey(Color.white, 0f),
                        new GradientColorKey(new Color(1f, 0.55f, 0.15f), 0.45f),
                        new GradientColorKey(new Color(0.4f, 0.15f, 0.05f), 1f),
                    },
                    new[]
                    {
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(0.7f, 0.35f),
                        new GradientAlphaKey(0f, 1f),
                    });
                colorOverLifetime.color = gradient;

                ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = ps.sizeOverLifetime;
                sizeOverLifetime.enabled = true;
                sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                    1f,
                    AnimationCurve.EaseInOut(0f, 1f, 1f, 0.15f));
            }

            private static Material GetOrCreateParticleMaterial()
            {
                if (_particleMaterial != null)
                {
                    return _particleMaterial;
                }

                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null)
                {
                    shader = Shader.Find("Particles/Standard Unlit");
                }

                if (shader == null)
                {
                    shader = Shader.Find("Sprites/Default");
                }

                _particleMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    color = Color.white,
                };
                if (_particleMaterial.HasProperty("_Surface"))
                {
                    _particleMaterial.SetFloat("_Surface", 1f); // Transparent
                }

                if (_particleMaterial.HasProperty("_Blend"))
                {
                    _particleMaterial.SetFloat("_Blend", 1f); // Additive-ish
                }

                return _particleMaterial;
            }
        }
    }
}

using SS3D.Systems.Tile;
using UnityEngine;
using UnityEngine.Rendering;

namespace SS3D.Systems.StructuralDamage
{
    internal static class SteelWallDamageVfx
    {
        private static readonly int CrackedId = Shader.PropertyToID("_Cracked");
        private static readonly int ParticleId = Shader.PropertyToID("_Particle");
        private static Material _dent;
        private static Material _crack;
        private static Material _particle;

        public static bool IsSteelWall(PlacedTileObject wall)
        {
            return wall.tileObjectSO != null && wall.GenericType == TileObjectGenericType.Wall;
        }

        public static MeshRenderer CreateOverlay(MeshRenderer surface)
        {
            if (!surface.TryGetComponent(out MeshFilter source) || source.sharedMesh == null)
                return null;

            GameObject overlay = new("Steel wall damage overlay");
            overlay.transform.SetParent(surface.transform, false);
            overlay.layer = surface.gameObject.layer;
            overlay.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
            MeshRenderer renderer = overlay.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        public static void SetStage(MeshRenderer overlay, Mesh mesh, bool cracked)
        {
            if (!EnsureMaterials())
                return;

            overlay.GetComponent<MeshFilter>().sharedMesh = mesh;
            int slots = mesh.subMeshCount;
            Material[] materials = new Material[slots];
            for (int i = 0; i < slots; i++)
                materials[i] = cracked ? _crack : _dent;

            overlay.sharedMaterials = materials;
        }

        public static void PlayImpact(Vector3 point, Vector3 normal)
        {
            if (!EnsureMaterials())
                return;

            Vector3 origin = point + normal * 0.025f;
            Quaternion rotation = Quaternion.LookRotation(normal);
            Emit(origin, rotation, "Metal flecks", 10, 2.6f, 0.17f, 0.042f, 0.65f,
                new Color(0.95f, 0.79f, 0.52f, 1f), ParticleSystemRenderMode.Stretch);
            Emit(origin, rotation, "Dust", 9, 0.63f, 0.25f, 0.13f, 0f,
                new Color(0.54f, 0.49f, 0.43f, 0.5f), ParticleSystemRenderMode.Billboard);
        }

        private static bool EnsureMaterials()
        {
            if (_dent != null)
                return true;

            Shader shader = Resources.Load<Shader>("StructuralDamage/SteelWallDamage");
            if (shader == null || !shader.isSupported)
                return false;

            _dent = new Material(shader) { name = "Steel dent", hideFlags = HideFlags.HideAndDontSave };
            _crack = new Material(shader) { name = "Steel tear", hideFlags = HideFlags.HideAndDontSave };
            _crack.SetFloat(CrackedId, 1f);
            _particle = new Material(shader) { name = "Steel impact", hideFlags = HideFlags.HideAndDontSave };
            _particle.SetFloat(ParticleId, 1f);
            return true;
        }

        private static void Emit(Vector3 origin, Quaternion rotation, string name, int count, float speed, float lifetime,
            float size, float gravity, Color color, ParticleSystemRenderMode renderMode)
        {
            GameObject emitter = new(name);
            emitter.SetActive(false);
            emitter.transform.SetPositionAndRotation(origin, rotation);
            ParticleSystem particles = emitter.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = false;
            main.duration = lifetime;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.65f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.45f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.45f, size);
            main.startColor = color;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 52f;
            shape.radius = 0.028f;

            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var fade = particles.colorOverLifetime;
            fade.enabled = true;
            Gradient gradient = new();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            var renderer = emitter.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _particle;
            renderer.renderMode = renderMode;
            renderer.lengthScale = 1.7f;
            renderer.velocityScale = 0.025f;
            emitter.SetActive(true);
            particles.Play();
        }
    }
}

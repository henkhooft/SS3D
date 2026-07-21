using SS3D.Core;
using SS3D.Core.Behaviours;
using SS3D.Systems.Tile;
using UnityEngine;

namespace SS3D.Systems.StructuralDamage
{
    /// <summary>
    /// Entry point for structural integrity damage (walls, doors, windows).
    /// Self-bootstraps — do not add to Boot.unity.
    /// </summary>
    public sealed class StructuralDamageSubSystem : SubSystem
    {
        private StructuralDamageService _service;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (SubSystems.TryGet(out StructuralDamageSubSystem _))
                return;

            GameObject host = new(nameof(StructuralDamageSubSystem));
            DontDestroyOnLoad(host);
            host.AddComponent<StructuralDamageSubSystem>();
        }

        public IStructuralDamageService Service => _service ??= CreateService();

        protected override void OnAwake()
        {
            base.OnAwake();
            _service = CreateService();
        }

        public bool TryApplyStructuralDamage(TileCoord coord, float force, StructuralDamageSource source)
        {
            return Service.TryApplyStructuralDamage(coord, force, source);
        }

        public bool TryGetIntegrity(TileCoord coord, out StructuralIntegrityStage stage, out float remaining, out float max)
        {
            return Service.TryGetIntegrity(coord, out stage, out remaining, out max);
        }

        private static StructuralDamageService CreateService()
        {
            TileSubSystem tiles = SubSystems.Get<TileSubSystem>();
            if (tiles == null)
                return new StructuralDamageService(null, null);

            return new StructuralDamageService(tiles.QueryService, tiles.Construction);
        }
    }
}

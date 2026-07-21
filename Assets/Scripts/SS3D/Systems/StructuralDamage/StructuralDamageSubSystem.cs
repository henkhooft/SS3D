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
        private ITileQueryService _boundQuery;
        private IConstructionService _boundConstruction;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (SubSystems.TryGet(out StructuralDamageSubSystem _))
                return;

            GameObject host = new(nameof(StructuralDamageSubSystem));
            DontDestroyOnLoad(host);
            host.AddComponent<StructuralDamageSubSystem>();
        }

        /// <summary>
        /// Resolves against the current <see cref="TileSubSystem"/> map services.
        /// Do not cache at Awake — tile map is created later in TileSubSystem.OnStart.
        /// </summary>
        public IStructuralDamageService Service
        {
            get
            {
                EnsureServiceBound();
                return _service;
            }
        }

        public bool TryApplyStructuralDamage(TileCoord coord, float force, StructuralDamageSource source)
        {
            IStructuralDamageService service = Service;
            return service != null && service.TryApplyStructuralDamage(coord, force, source);
        }

        public bool TryGetIntegrity(TileCoord coord, out StructuralIntegrityStage stage, out float remaining, out float max)
        {
            stage = StructuralIntegrityStage.Intact;
            remaining = 0f;
            max = 0f;

            IStructuralDamageService service = Service;
            return service != null && service.TryGetIntegrity(coord, out stage, out remaining, out max);
        }

        private void EnsureServiceBound()
        {
            if (!SubSystems.TryGet(out TileSubSystem tiles)
                || tiles.QueryService == null
                || tiles.Construction == null)
            {
                _service = null;
                _boundQuery = null;
                _boundConstruction = null;
                return;
            }

            if (_service != null
                && ReferenceEquals(_boundQuery, tiles.QueryService)
                && ReferenceEquals(_boundConstruction, tiles.Construction))
            {
                return;
            }

            _boundQuery = tiles.QueryService;
            _boundConstruction = tiles.Construction;
            _service = new StructuralDamageService(_boundQuery, _boundConstruction);
        }
    }
}

using Coimbra;
using System.Collections.Generic;
using UnityEngine;

namespace SS3D.Systems.Tile.FloorVisuals
{
    /// <summary>
    /// Loads <see cref="FloorDecalDefinition"/> assets from Resources/FloorVisuals/Decals.
    /// </summary>
    [ProjectSettings("SS3D/Assets", "Floor Decal Catalog")]
    public sealed class FloorDecalCatalog : ScriptableSettings
    {
        [SerializeField]
        private List<FloorDecalDefinition> _definitions = new();

        private Dictionary<ushort, FloorDecalDefinition> _byId;
        private static FloorDecalCatalog _runtimeFallback;

        public IReadOnlyList<FloorDecalDefinition> Definitions
        {
            get
            {
                EnsureIndex();
                return _definitions;
            }
        }

        public static FloorDecalCatalog Get()
        {
            FloorDecalCatalog catalog = ScriptableSettings.GetOrFind<FloorDecalCatalog>();
            if (catalog != null)
                return catalog;

            if (_runtimeFallback == null)
            {
                _runtimeFallback = CreateInstance<FloorDecalCatalog>();
                _runtimeFallback.hideFlags = HideFlags.HideAndDontSave;
            }

            return _runtimeFallback;
        }

        public bool TryGet(ushort id, out FloorDecalDefinition definition)
        {
            EnsureIndex();
            return _byId.TryGetValue(id, out definition);
        }

        public void RebuildIndex()
        {
            _byId = null;
            EnsureIndex();
        }

        private void EnsureIndex()
        {
            if (_byId != null)
                return;

            _byId = new Dictionary<ushort, FloorDecalDefinition>();
            if (_definitions == null)
                return;

            foreach (FloorDecalDefinition definition in _definitions)
            {
                if (definition == null || definition.Id == 0)
                    continue;

                _byId[definition.Id] = definition;
            }

            FloorDecalDefinition[] resources = Resources.LoadAll<FloorDecalDefinition>("FloorVisuals/Decals");
            foreach (FloorDecalDefinition definition in resources)
            {
                if (definition == null || definition.Id == 0)
                    continue;

                _byId[definition.Id] = definition;
                if (!_definitions.Contains(definition))
                    _definitions.Add(definition);
            }

            if (_byId.Count == 0)
            {
                FloorDecalDefinition placeholder = ScriptableObject.CreateInstance<FloorDecalDefinition>();
                placeholder.Id = 1;
                placeholder.DisplayName = "Department Corner";
                placeholder.Texture = FloorVisualMesh.GetStripeCornerTexture();
                placeholder.Tint = new Color(1f, 0.85f, 0.2f, 1f);
                _byId[placeholder.Id] = placeholder;
                _definitions.Add(placeholder);
            }
        }
    }
}

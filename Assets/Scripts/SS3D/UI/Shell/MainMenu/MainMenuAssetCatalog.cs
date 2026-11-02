using SS3D.UI.Shell.Catalog;
using UnityEngine;
using UnityEngine.UIElements;

namespace SS3D.UI.Shell.MainMenu
{
    public sealed class MainMenuAssetCatalog : UiAssetCatalogBase
    {
        [SerializeField] private VisualTreeAsset _template;
        [SerializeField] private StyleSheet _style;
        [SerializeField] private Texture2D _logo;
        [SerializeField] private Texture2D[] _wallpapers;
        public VisualTreeAsset Template => _template;
        public StyleSheet Style => _style;
        public Texture2D Logo => _logo;
        public Texture2D Wallpaper(int index) => _wallpapers[index % _wallpapers.Length];
        protected override bool HasSurfaceAssets(out string missingField)
        {
            missingField = null;
            if (_template == null) missingField = nameof(_template);
            else if (_style == null) missingField = nameof(_style);
            else if (_logo == null) missingField = nameof(_logo);
            else if (_wallpapers == null || _wallpapers.Length == 0) missingField = nameof(_wallpapers);
            if (missingField == null)
                foreach (Texture2D texture in _wallpapers)
                    if (texture == null) missingField = nameof(_wallpapers);
            return missingField == null;
        }
#if UNITY_EDITOR
        public void EditorAssign(PanelSettings panel, StyleSheet tokens, StyleSheet typography,
            VisualTreeAsset template, StyleSheet style, Texture2D logo, Texture2D[] wallpapers)
        {
            EditorAssignShared(panel, tokens, typography);
            _template = template;
            _style = style;
            _logo = logo;
            _wallpapers = wallpapers;
        }
#endif
    }
}

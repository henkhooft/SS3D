using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SS3D.UI.Lobby
{
    /// <summary>
    /// Committed resolved refs for the pre-round lobby UITK shell. Rebuild via
    /// <c>SS3D → Data → Rebuild All UI Catalogs</c>.
    /// </summary>
    [CreateAssetMenu(
        fileName = LobbyAssetPaths.ResourcesCatalogName,
        menuName = "SS3D/UI/Lobby Asset Catalog")]
    public sealed class LobbyAssetCatalog : ScriptableObject
    {
        [SerializeField] private StyleSheet _lobbyStyle;
        [SerializeField] private Sprite _previewPlaceholder;
        [SerializeField] private List<LobbyNamedSprite> _jobIcons = new();

        public StyleSheet LobbyStyle => _lobbyStyle;

        public Sprite PreviewPlaceholder => _previewPlaceholder;

        public bool TryGetJobIcon(string id, out Sprite sprite)
        {
            sprite = null;
            if (string.IsNullOrEmpty(id) || _jobIcons == null)
            {
                return false;
            }

            for (int i = 0; i < _jobIcons.Count; i++)
            {
                LobbyNamedSprite entry = _jobIcons[i];
                if (entry != null && entry.Id == id && entry.Sprite != null)
                {
                    sprite = entry.Sprite;
                    return true;
                }
            }

            return false;
        }

        public bool HasRequiredAssets(out string missingField)
        {
            if (_lobbyStyle == null)
            {
                missingField = "lobbyStyle";
                return false;
            }

            if (_previewPlaceholder == null)
            {
                missingField = "previewPlaceholder";
                return false;
            }

            missingField = null;
            return true;
        }

#if UNITY_EDITOR
        public void EditorAssign(StyleSheet lobbyStyle, Sprite previewPlaceholder, List<LobbyNamedSprite> jobIcons)
        {
            _lobbyStyle = lobbyStyle;
            _previewPlaceholder = previewPlaceholder;
            _jobIcons = jobIcons ?? new List<LobbyNamedSprite>();
        }
#endif
    }

    [Serializable]
    public sealed class LobbyNamedSprite
    {
        public string Id;
        public Sprite Sprite;

        public LobbyNamedSprite()
        {
        }

        public LobbyNamedSprite(string id, Sprite sprite)
        {
            Id = id;
            Sprite = sprite;
        }
    }
}

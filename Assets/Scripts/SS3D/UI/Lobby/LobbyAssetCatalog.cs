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
        [SerializeField] private Sprite _serverInfoBanner;
        [SerializeField] private Texture2D _chevronDown;
        [SerializeField] private GameObject _previewHumanPrefab;
        [SerializeField] private List<LobbyNamedSprite> _jobIcons = new();
        [SerializeField] private List<LobbyNamedTexture> _departmentIcons = new();
        [SerializeField] private List<LobbyNamedSprite> _loadoutThumbs = new();

        public StyleSheet LobbyStyle => _lobbyStyle;

        public Sprite PreviewPlaceholder => _previewPlaceholder;

        public Sprite ServerInfoBanner => _serverInfoBanner;

        public Texture2D ChevronDown => _chevronDown;

        public GameObject PreviewHumanPrefab => _previewHumanPrefab;

        public bool TryGetJobIcon(string id, out Sprite sprite) => TryGetNamedSprite(_jobIcons, id, out sprite);

        public bool TryGetDepartmentIcon(string id, out Texture2D texture) =>
            TryGetNamedTexture(_departmentIcons, id, out texture);

        public bool TryGetLoadoutThumb(string id, out Sprite sprite) =>
            TryGetNamedSprite(_loadoutThumbs, id, out sprite);

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

            if (_serverInfoBanner == null)
            {
                missingField = "serverInfoBanner";
                return false;
            }

            if (_chevronDown == null)
            {
                missingField = "chevronDown";
                return false;
            }

            if (_previewHumanPrefab == null)
            {
                missingField = "previewHumanPrefab";
                return false;
            }

            missingField = null;
            return true;
        }

#if UNITY_EDITOR
        public void EditorAssign(
            StyleSheet lobbyStyle,
            Sprite previewPlaceholder,
            Sprite serverInfoBanner,
            Texture2D chevronDown,
            GameObject previewHumanPrefab,
            List<LobbyNamedSprite> jobIcons,
            List<LobbyNamedTexture> departmentIcons,
            List<LobbyNamedSprite> loadoutThumbs)
        {
            _lobbyStyle = lobbyStyle;
            _previewPlaceholder = previewPlaceholder;
            _serverInfoBanner = serverInfoBanner;
            _chevronDown = chevronDown;
            _previewHumanPrefab = previewHumanPrefab;
            _jobIcons = jobIcons ?? new List<LobbyNamedSprite>();
            _departmentIcons = departmentIcons ?? new List<LobbyNamedTexture>();
            _loadoutThumbs = loadoutThumbs ?? new List<LobbyNamedSprite>();
        }
#endif

        private static bool TryGetNamedSprite(List<LobbyNamedSprite> list, string id, out Sprite sprite)
        {
            sprite = null;
            if (string.IsNullOrEmpty(id) || list == null)
            {
                return false;
            }

            for (int i = 0; i < list.Count; i++)
            {
                LobbyNamedSprite entry = list[i];
                if (entry != null && entry.Id == id && entry.Sprite != null)
                {
                    sprite = entry.Sprite;
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetNamedTexture(List<LobbyNamedTexture> list, string id, out Texture2D texture)
        {
            texture = null;
            if (string.IsNullOrEmpty(id) || list == null)
            {
                return false;
            }

            for (int i = 0; i < list.Count; i++)
            {
                LobbyNamedTexture entry = list[i];
                if (entry != null && entry.Id == id && entry.Texture != null)
                {
                    texture = entry.Texture;
                    return true;
                }
            }

            return false;
        }
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

    [Serializable]
    public sealed class LobbyNamedTexture
    {
        public string Id;
        public Texture2D Texture;

        public LobbyNamedTexture()
        {
        }

        public LobbyNamedTexture(string id, Texture2D texture)
        {
            Id = id;
            Texture = texture;
        }
    }
}

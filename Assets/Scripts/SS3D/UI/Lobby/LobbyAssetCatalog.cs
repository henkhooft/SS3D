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

        /// <summary>Ordered head-hair prefabs. Index 0 = None (no prefab).</summary>
        [SerializeField] private List<LobbyNamedPrefab> _hairStyles = new();

        /// <summary>Ordered facial-hair prefabs. Index 0 = None.</summary>
        [SerializeField] private List<LobbyNamedPrefab> _beardStyles = new();

        /// <summary>Hair / skin tint colours shown in the Body colour pickers.</summary>
        [SerializeField] private List<Color> _hairColors = new();

        public StyleSheet LobbyStyle => _lobbyStyle;

        public Sprite PreviewPlaceholder => _previewPlaceholder;

        public Sprite ServerInfoBanner => _serverInfoBanner;

        public Texture2D ChevronDown => _chevronDown;

        public GameObject PreviewHumanPrefab => _previewHumanPrefab;

        public IReadOnlyList<LobbyNamedPrefab> HairStyles => _hairStyles;

        public IReadOnlyList<LobbyNamedPrefab> BeardStyles => _beardStyles;

        public IReadOnlyList<Color> HairColors => _hairColors;

        public bool TryGetJobIcon(string id, out Sprite sprite) => TryGetNamedSprite(_jobIcons, id, out sprite);

        public bool TryGetDepartmentIcon(string id, out Texture2D texture) =>
            TryGetNamedTexture(_departmentIcons, id, out texture);

        public bool TryGetLoadoutThumb(string id, out Sprite sprite) =>
            TryGetNamedSprite(_loadoutThumbs, id, out sprite);

        public GameObject GetHairStyle(int index)
        {
            if (_hairStyles == null || index <= 0 || index >= _hairStyles.Count)
            {
                return null;
            }

            return _hairStyles[index].Prefab;
        }

        public GameObject GetBeardStyle(int index)
        {
            if (_beardStyles == null || index <= 0 || index >= _beardStyles.Count)
            {
                return null;
            }

            return _beardStyles[index].Prefab;
        }

        public Color GetHairColor(int index)
        {
            if (_hairColors == null || _hairColors.Count == 0)
            {
                return new Color(0.5f, 0.28f, 0.19f);
            }

            return _hairColors[Mathf.Clamp(index, 0, _hairColors.Count - 1)];
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
            List<LobbyNamedSprite> loadoutThumbs,
            List<LobbyNamedPrefab> hairStyles,
            List<LobbyNamedPrefab> beardStyles,
            List<Color> hairColors)
        {
            _lobbyStyle = lobbyStyle;
            _previewPlaceholder = previewPlaceholder;
            _serverInfoBanner = serverInfoBanner;
            _chevronDown = chevronDown;
            _previewHumanPrefab = previewHumanPrefab;
            _jobIcons = jobIcons ?? new List<LobbyNamedSprite>();
            _departmentIcons = departmentIcons ?? new List<LobbyNamedTexture>();
            _loadoutThumbs = loadoutThumbs ?? new List<LobbyNamedSprite>();
            _hairStyles = hairStyles ?? new List<LobbyNamedPrefab>();
            _beardStyles = beardStyles ?? new List<LobbyNamedPrefab>();
            _hairColors = hairColors ?? new List<Color>();
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
    public sealed class LobbyNamedPrefab
    {
        public string Id;
        public GameObject Prefab;

        public LobbyNamedPrefab()
        {
        }

        public LobbyNamedPrefab(string id, GameObject prefab)
        {
            Id = id;
            Prefab = prefab;
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

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using SS3D.UI.Lobby;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SS3D.Editor
{
    /// <summary>
    /// Rebuilds the committed <see cref="LobbyAssetCatalog"/> from <see cref="LobbyAssetPaths"/>.
    /// Menu: use <see cref="UiCatalogRebuildAll"/>.
    /// </summary>
    public static class LobbyAssetCatalogBuilder
    {
        private static readonly (string Id, string FileName)[] DepartmentIconFiles =
        {
            ("command", "OfficeBuilding.png"),
            ("security", "ShieldCheck.png"),
            ("engineering", "Cog.png"),
            ("medical", "Plus.png"),
            ("science", "Beaker.png"),
            ("cargo", "Truck.png"),
            ("service", "Cake.png"),
        };

        /// <summary>BatchMode: <c>-executeMethod SS3D.Editor.LobbyAssetCatalogBuilder.RebuildCatalogBatch</c></summary>
        public static void RebuildCatalogBatch()
        {
            if (!TryRebuildCatalog(out string error))
            {
                Debug.LogError(error);
                if (UnityEngine.Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }

                return;
            }

            Debug.Log($"[LobbyAssetCatalogBuilder] Rebuilt catalog at {LobbyAssetPaths.CatalogAssetPath}");
            if (UnityEngine.Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }

        public static bool TryRebuildCatalog(out string error)
        {
            error = null;
            List<string> missing = new();

            StyleSheet lobbyStyle = LoadRequired<StyleSheet>(LobbyAssetPaths.StyleSheet, missing);
            Sprite preview = LoadRequiredSprite(LobbyAssetPaths.PreviewPlaceholder, missing);
            Sprite banner = LoadRequiredSprite(LobbyAssetPaths.ServerInfoBanner, missing);
            // Heroicons are Default Texture2D (not Sprite) — load as textures for UITK backgrounds.
            Texture2D chevron = LoadRequiredTexture(LobbyAssetPaths.ChevronDownIcon, missing);

            List<LobbyNamedSprite> jobIcons = new();
            if (Directory.Exists(LobbyAssetPaths.JobIconRoot))
            {
                string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { LobbyAssetPaths.JobIconRoot.TrimEnd('/') });
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprite == null)
                    {
                        missing.Add($"Sprite: {path}");
                        continue;
                    }

                    string id = Path.GetFileNameWithoutExtension(path);
                    jobIcons.Add(new LobbyNamedSprite(id, sprite));
                }
            }
            else
            {
                missing.Add(LobbyAssetPaths.JobIconRoot);
            }

            List<LobbyNamedTexture> departmentIcons = new();
            foreach ((string id, string fileName) in DepartmentIconFiles)
            {
                Texture2D texture = LoadRequiredTexture(LobbyAssetPaths.HeroiconsOutlineRoot + fileName, missing);
                if (texture != null)
                {
                    departmentIcons.Add(new LobbyNamedTexture(id, texture));
                }
            }

            if (missing.Count > 0)
            {
                error = "Lobby asset catalog rebuild failed. Missing assets:\n- "
                    + string.Join("\n- ", missing);
                return false;
            }

            string directory = Path.GetDirectoryName(LobbyAssetPaths.CatalogAssetPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            LobbyAssetCatalog catalog =
                AssetDatabase.LoadAssetAtPath<LobbyAssetCatalog>(LobbyAssetPaths.CatalogAssetPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<LobbyAssetCatalog>();
                AssetDatabase.CreateAsset(catalog, LobbyAssetPaths.CatalogAssetPath);
            }

            catalog.EditorAssign(lobbyStyle, preview, banner, chevron, jobIcons, departmentIcons);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return true;
        }

        private static T LoadRequired<T>(string path, List<string> missing) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                missing.Add($"{typeof(T).Name}: {path}");
            }

            return asset;
        }

        private static Sprite LoadRequiredSprite(string path, List<string> missing)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                missing.Add($"Sprite: {path}");
            }

            return sprite;
        }

        private static Texture2D LoadRequiredTexture(string path, List<string> missing)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                missing.Add($"Texture2D: {path}");
            }

            return texture;
        }
    }
}
#endif

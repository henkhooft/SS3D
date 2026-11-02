#if UNITY_EDITOR
using System.Collections.Generic;
using SS3D.UI.Shell.Catalog;
using SS3D.UI.Shell.MainMenu;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace SS3D.Editor
{
    public static class MainMenuAssetCatalogBuilder
    {
        private static void ConfigureWallpaper(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;
            if (importer.npotScale == TextureImporterNPOTScale.None && !importer.mipmapEnabled
                && importer.textureCompression == TextureImporterCompression.Uncompressed) return;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        public static bool TryRebuildCatalog(out string error)
        {
            ConfigureWallpaper(MainMenuAssetPaths.WallpaperFolder + "FloatingSpace2.png");
            ConfigureWallpaper(MainMenuAssetPaths.WallpaperFolder + "FloatingSpace3.png");
            List<string> missing = new();
            PanelSettings settings = UiCatalogBuilderKit.LoadRequired<PanelSettings>(UiShellAssetPaths.PanelSettings, missing);
            StyleSheet tokens = UiCatalogBuilderKit.LoadRequired<StyleSheet>(UiShellAssetPaths.Ss3dTokens, missing);
            StyleSheet typography = UiCatalogBuilderKit.LoadRequired<StyleSheet>(UiShellAssetPaths.Ss3dTypography, missing);
            VisualTreeAsset template = UiCatalogBuilderKit.LoadRequired<VisualTreeAsset>(MainMenuAssetPaths.Template, missing);
            StyleSheet style = UiCatalogBuilderKit.LoadRequired<StyleSheet>(MainMenuAssetPaths.Style, missing);
            Texture2D logo = UiCatalogBuilderKit.LoadRequired<Texture2D>(MainMenuAssetPaths.Logo, missing);
            Texture2D[] wallpapers = {
                UiCatalogBuilderKit.LoadRequired<Texture2D>(MainMenuAssetPaths.WallpaperFolder + "FloatingSpace2.png", missing),
                UiCatalogBuilderKit.LoadRequired<Texture2D>(MainMenuAssetPaths.WallpaperFolder + "FloatingSpace3.png", missing),
            };
            MainMenuAssetCatalog catalog = UiCatalogBuilderKit.LoadOrCreateCatalogAsset<MainMenuAssetCatalog>(MainMenuAssetPaths.Catalog);
            catalog.EditorAssign(settings, tokens, typography, template, style, logo, wallpapers);
            return UiCatalogBuilderKit.FinalizeSave(missing, "Main menu visuals", catalog, out error);
        }
    }
}
#endif

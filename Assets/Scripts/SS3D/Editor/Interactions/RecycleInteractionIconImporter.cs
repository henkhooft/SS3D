#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SS3D.Editor.Interactions
{
    /// <summary>
    /// Rebuilds <c>Recycle.asset</c> as a full-rect sprite from <c>Recycle.png</c>, preserving the
    /// asset GUID so <see cref="SS3D.Data.Generated.InteractionIcons.Recycle"/> keeps working.
    /// Runs once per machine via EditorPrefs.
    /// </summary>
    [InitializeOnLoad]
    internal static class RecycleInteractionIconImporter
    {
        private const string TexturePath = "Assets/Art/Graphics/UI/Interactions/InteractionIcons/Recycle.png";
        private const string SpriteAssetPath = "Assets/Content/Systems/UI/Systems/Interactions/InteractionIcons/Recycle.asset";
        private const string PrefKey = "SS3D.RecycleInteractionIcon.Rebuilt.v1";

        static RecycleInteractionIconImporter()
        {
            if (EditorPrefs.GetBool(PrefKey, false))
                return;

            EditorApplication.delayCall += TryRebuild;
        }

        private static void TryRebuild()
        {
            if (EditorPrefs.GetBool(PrefKey, false))
                return;

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (texture == null)
                return;

            TextureImporter importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (importer == null)
                return;

            bool wasReadable = importer.isReadable;
            if (!wasReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            }

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            sprite.name = "Recycle";

            string metaPath = SpriteAssetPath + ".meta";
            string preservedMeta = File.Exists(metaPath) ? File.ReadAllText(metaPath) : null;

            if (AssetDatabase.LoadMainAssetAtPath(SpriteAssetPath) != null)
                AssetDatabase.DeleteAsset(SpriteAssetPath);

            AssetDatabase.CreateAsset(sprite, SpriteAssetPath);

            if (!string.IsNullOrEmpty(preservedMeta))
            {
                File.WriteAllText(metaPath, preservedMeta);
                AssetDatabase.Refresh();
            }

            AssetDatabase.SaveAssets();

            if (!wasReadable)
            {
                importer.isReadable = false;
                importer.SaveAndReimport();
            }

            EditorPrefs.SetBool(PrefKey, true);
            Debug.Log("[SS3D] Rebuilt Recycle interaction icon sprite from Recycle.png.");
        }
    }
}
#endif

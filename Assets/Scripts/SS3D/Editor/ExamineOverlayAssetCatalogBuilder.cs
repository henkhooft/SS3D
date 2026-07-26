#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using SS3D.UI.Examine;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SS3D.Editor
{
    /// <summary>
    /// Rebuilds the committed <see cref="ExamineOverlayAssetCatalog"/> from <see cref="ExamineOverlayAssetPaths"/>.
    /// </summary>
    public static class ExamineOverlayAssetCatalogBuilder
    {
        [MenuItem("SS3D/Examine/Rebuild Examine Asset Catalog")]
        public static void RebuildCatalogMenu()
        {
            if (!TryRebuildCatalog(out string error))
            {
                Debug.LogError(error);
                EditorUtility.DisplayDialog("Examine Asset Catalog", error, "OK");
                return;
            }

            Debug.Log($"Rebuilt examine overlay asset catalog at {ExamineOverlayAssetPaths.CatalogAssetPath}");
        }

        public static bool TryRebuildCatalog(out string error)
        {
            error = null;
            List<string> missing = new();

            StyleSheet examineStyle = LoadRequired<StyleSheet>(ExamineOverlayAssetPaths.StyleSheet, missing);
            StyleSheet inventorySlotStyle = LoadRequired<StyleSheet>(ExamineOverlayAssetPaths.InventorySlotStyle, missing);
            StyleSheet diegeticTokensStyle = LoadRequired<StyleSheet>(ExamineOverlayAssetPaths.DiegeticTokensStyle, missing);
            StyleSheet machineWindowStyle = LoadRequired<StyleSheet>(ExamineOverlayAssetPaths.MachineWindowStyle, missing);

            CharacterExamineIconSet icons = new()
            {
                Head = LoadRequiredSprite("BeepHead", missing),
                Eyes = LoadRequiredSprite("Eyes", missing),
                Face = LoadRequiredSprite("Face", missing),
                Ears = LoadRequiredSprite("Ears", missing),
                Suit = LoadRequiredSprite("Suit", missing),
                Shirt = LoadRequiredSprite("Shirt", missing),
                // No dedicated glove silhouette ships today — Main HUD's equipment doll reuses the
                // hand icon for the same reason (EquipmentGrid.CreateSlot(Slot.GloveLeft, ..., icons.HandLeft)).
                Gloves = LoadRequiredSprite("HandLeft", missing),
                Back = LoadRequiredSprite("BeepBack", missing),
                Feet = LoadRequiredSprite("Feet", missing),
                Belt = LoadRequiredSprite("Waist", missing),
                IdCard = LoadRequiredSprite("Neck", missing),
                Pocket = LoadRequiredSprite("Pocket", missing),
                HandLeft = LoadRequiredSprite("HandLeft", missing),
                HandRight = LoadRequiredSprite("HandRight", missing),
            };

            if (missing.Count > 0)
            {
                error = "Examine overlay asset catalog rebuild failed. Missing assets:\n- "
                    + string.Join("\n- ", missing);
                return false;
            }

            string directory = Path.GetDirectoryName(ExamineOverlayAssetPaths.CatalogAssetPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            ExamineOverlayAssetCatalog catalog =
                AssetDatabase.LoadAssetAtPath<ExamineOverlayAssetCatalog>(ExamineOverlayAssetPaths.CatalogAssetPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ExamineOverlayAssetCatalog>();
                AssetDatabase.CreateAsset(catalog, ExamineOverlayAssetPaths.CatalogAssetPath);
            }

            catalog.EditorAssign(examineStyle, inventorySlotStyle, diegeticTokensStyle, machineWindowStyle, icons);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return true;
        }

        private static T LoadRequired<T>(string path, List<string> missing) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                missing.Add(path);
            }

            return asset;
        }

        private static Sprite LoadRequiredSprite(string fileName, List<string> missing)
        {
            string path = $"{ExamineOverlayAssetPaths.IconRoot}{fileName}.png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                missing.Add(path);
            }

            return sprite;
        }
    }
}
#endif

#if UNITY_EDITOR
using SS3D.Systems.Examine;
using SS3D.Systems.StructuralDamage;
using SS3D.Systems.Tile;
using UnityEditor;
using UnityEngine;

namespace SS3D.Systems.StructuralDamage.Editor
{
    /// <summary>
    /// Adds <see cref="StructuralIntegrityPresenter"/> and swaps SimpleExaminable →
    /// StructuralIntegrityExaminable on wall/window prefabs that bake PlacedTileObject.
    /// </summary>
    public static class StructuralIntegrityPrefabSetup
    {
        private const string WindLightClip = "Assets/Art/Sound/World/Ambience/Air/WindLight.wav";

        private static readonly string[] PrefabPaths =
        {
            "Assets/Content/WorldObjects/Structures/Walls/SteelWall.prefab",
            "Assets/Content/WorldObjects/Structures/Walls/SteelWallReinforced.prefab",
            "Assets/Content/WorldObjects/Structures/Walls/SteelWindow.prefab",
            "Assets/Content/WorldObjects/Structures/Walls/SteelWindowReinforced.prefab",
            "Assets/Content/WorldObjects/Structures/Walls/SteelGirder.prefab",
        };

        [MenuItem("SS3D/Structural Damage/Setup Wall Integrity Presentation")]
        public static void SetupMenu()
        {
            int updated = SetupAll();
            EditorUtility.DisplayDialog(
                "Structural Integrity Presentation",
                $"Updated {updated} prefabs.",
                "OK");
        }

        /// <summary>BatchMode: <c>-executeMethod SS3D.Systems.StructuralDamage.Editor.StructuralIntegrityPrefabSetup.SetupBatch</c></summary>
        public static void SetupBatch()
        {
            int updated = SetupAll();
            Debug.Log($"[StructuralIntegrityPrefabSetup] Updated {updated} prefabs.");
            if (Application.isBatchMode)
                EditorApplication.Exit(updated > 0 ? 0 : 1);
        }

        public static int SetupAll()
        {
            AudioClip hiss = AssetDatabase.LoadAssetAtPath<AudioClip>(WindLightClip);
            int updated = 0;
            for (int i = 0; i < PrefabPaths.Length; i++)
            {
                if (SetupPrefab(PrefabPaths[i], hiss))
                    updated++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return updated;
        }

        private static bool SetupPrefab(string path, AudioClip hiss)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                Debug.LogError($"[StructuralIntegrityPrefabSetup] Missing {path}");
                return false;
            }

            try
            {
                if (!root.TryGetComponent(out PlacedTileObject _))
                {
                    Debug.LogWarning($"[StructuralIntegrityPrefabSetup] Skip (no PlacedTileObject): {path}");
                    return false;
                }

                bool changed = false;

                if (!root.TryGetComponent(out StructuralIntegrityPresenter presenter))
                {
                    presenter = root.AddComponent<StructuralIntegrityPresenter>();
                    changed = true;
                }

                SerializedObject presenterSo = new SerializedObject(presenter);
                SerializedProperty clipProp = presenterSo.FindProperty("_crackedHiss");
                if (clipProp != null && clipProp.objectReferenceValue == null && hiss != null)
                {
                    clipProp.objectReferenceValue = hiss;
                    presenterSo.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }

                if (EnsureStructuralExaminable(root))
                    changed = true;

                if (changed)
                    PrefabUtility.SaveAsPrefabAsset(root, path);

                return changed;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool EnsureStructuralExaminable(GameObject root)
        {
            if (root.TryGetComponent(out StructuralIntegrityExaminable _))
                return false;

            ExamineData data = null;
            if (root.TryGetComponent(out SimpleExaminable simple))
            {
                SerializedObject simpleSo = new SerializedObject(simple);
                SerializedProperty keyProp = simpleSo.FindProperty("key");
                if (keyProp != null)
                    data = keyProp.objectReferenceValue as ExamineData;

                Object.DestroyImmediate(simple, true);
            }

            StructuralIntegrityExaminable examinable = root.AddComponent<StructuralIntegrityExaminable>();
            if (data != null)
            {
                SerializedObject examinableSo = new SerializedObject(examinable);
                SerializedProperty keyProp = examinableSo.FindProperty("key");
                if (keyProp != null)
                {
                    keyProp.objectReferenceValue = data;
                    examinableSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            return true;
        }
    }
}
#endif

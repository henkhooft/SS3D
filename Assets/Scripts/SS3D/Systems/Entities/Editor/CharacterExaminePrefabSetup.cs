#if UNITY_EDITOR
using System.IO;
using SS3D.Systems.Examine;
using SS3D.Systems.Selection;
using UnityEditor;
using UnityEngine;

namespace SS3D.Systems.Entities.Editor
{
    /// <summary>
    /// Wires character examine onto <c>Human.prefab</c>'s root (<see cref="Selectable"/> + a
    /// range-check collider + <see cref="CharacterExaminable"/>) so <see cref="SS3D.Systems.Selection.SelectionSubSystem"/>
    /// can resolve "hovering a character" at all — none of this exists on the prefab today
    /// (Documents/architecture/systems/examine.md § character examine: greenfield, no character
    /// <c>IExaminable</c> anywhere under Systems/Entities/ or on Human*.prefab).
    /// <see cref="PrefabUtility"/>-based, not a hand-edited prefab — same convention as
    /// <c>MeleePrefabSetup</c>.
    /// </summary>
    public static class CharacterExaminePrefabSetup
    {
        private const string HumanPrefabPath = "Assets/Content/WorldObjects/Entities/Humanoids/Human/Human.prefab";
        private const string ExamineDataPath = "Assets/Content/Data/Examine/CharacterExamineData.asset";

        // Matches Human.prefab's root CharacterController (m_Height 1.27, m_Radius 0.25,
        // m_Center {0, 0.63, 0}) so the new range-check collider approximates the same bounds.
        private const float ColliderHeight = 1.27f;
        private const float ColliderRadius = 0.25f;
        private static readonly Vector3 ColliderCenter = new(0f, 0.63f, 0f);

        [MenuItem("SS3D/Entities/Setup Character Examine (Selectable + Collider)")]
        public static void SetupMenu()
        {
            bool changed = Setup();
            EditorUtility.DisplayDialog(
                "Character Examine Setup",
                changed ? "Human.prefab updated." : "Human.prefab already set up.",
                "OK");
        }

        /// <summary>BatchMode: <c>-executeMethod SS3D.Systems.Entities.Editor.CharacterExaminePrefabSetup.SetupBatch</c></summary>
        public static void SetupBatch()
        {
            bool changed = Setup();
            Debug.Log($"[CharacterExaminePrefabSetup] Human.prefab {(changed ? "updated" : "already set up")}.");
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }

        public static bool Setup()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(HumanPrefabPath);
            if (root == null)
            {
                Debug.LogError($"[CharacterExaminePrefabSetup] Missing {HumanPrefabPath}");
                return false;
            }

            bool changed = false;

            try
            {
                if (!root.TryGetComponent(out Selectable _))
                {
                    root.AddComponent<Selectable>();
                    changed = true;
                }

                if (!root.TryGetComponent(out CapsuleCollider _))
                {
                    CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
                    collider.height = ColliderHeight;
                    collider.radius = ColliderRadius;
                    collider.center = ColliderCenter;
                    changed = true;
                }

                if (!root.TryGetComponent(out CharacterExaminable examinable))
                {
                    examinable = root.AddComponent<CharacterExaminable>();
                    changed = true;
                }

                ExamineData data = EnsureExamineDataAsset();
                SerializedObject serializedExaminable = new(examinable);
                SerializedProperty dataProperty = serializedExaminable.FindProperty("_data");
                if (dataProperty.objectReferenceValue != data)
                {
                    dataProperty.objectReferenceValue = data;
                    serializedExaminable.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, HumanPrefabPath);
                    Debug.Log($"[CharacterExaminePrefabSetup] Character examine wired on {HumanPrefabPath}");
                }

                return changed;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static ExamineData EnsureExamineDataAsset()
        {
            ExamineData data = AssetDatabase.LoadAssetAtPath<ExamineData>(ExamineDataPath);
            if (data != null)
            {
                return data;
            }

            data = ScriptableObject.CreateInstance<ExamineData>();
            data.Type = ExamineType.CHARACTER;

            string directory = Path.GetDirectoryName(ExamineDataPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            AssetDatabase.CreateAsset(data, ExamineDataPath);
            AssetDatabase.SaveAssets();
            return data;
        }
    }
}
#endif

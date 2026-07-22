#if UNITY_EDITOR
using SS3D.Systems.Combat;
using SS3D.Systems.Combat.Interactions;
using UnityEditor;
using UnityEngine;

namespace SS3D.Systems.Combat.Editor
{
    /// <summary>
    /// Wires Phase 1 melee extensions onto hand and tool prefabs (PrefabUtility — not Human.prefab).
    /// </summary>
    public static class MeleePrefabSetup
    {
        private const string HandLeft = "Assets/Content/WorldObjects/Entities/Humanoids/Human/HumanBodyParts/HumanHandLeft.prefab";
        private const string HandRight = "Assets/Content/WorldObjects/Entities/Humanoids/Human/HumanBodyParts/HumanHandRight.prefab";
        private const string Crowbar = "Assets/Content/WorldObjects/Items/Functional/Tools/Engineering/Crowbar.prefab";
        private const string Hatchet = "Assets/Content/WorldObjects/Items/Functional/Tools/Botany/Hatchet.prefab";
        private const string KitchenKnife = "Assets/Content/WorldObjects/Items/Functional/Tools/Kitchen/KitchenKnife.prefab";

        [MenuItem("SS3D/Combat/Setup Melee Prefabs (Hands + Tools)")]
        public static void SetupMenu()
        {
            int updated = SetupAll();
            EditorUtility.DisplayDialog("Melee Prefabs", $"Updated {updated} prefabs.", "OK");
        }

        /// <summary>BatchMode: <c>-executeMethod SS3D.Systems.Combat.Editor.MeleePrefabSetup.SetupBatch</c></summary>
        public static void SetupBatch()
        {
            int updated = SetupAll();
            Debug.Log($"[MeleePrefabSetup] Updated {updated} prefabs.");
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(updated > 0 ? 0 : 1);
            }
        }

        public static int SetupAll()
        {
            int updated = 0;
            if (EnsureHandMelee(HandLeft))
            {
                updated++;
            }

            if (EnsureHandMelee(HandRight))
            {
                updated++;
            }

            if (EnsureWeapon(Crowbar, MeleeWeaponProfile.Crowbar))
            {
                updated++;
            }

            if (EnsureWeapon(Hatchet, MeleeWeaponProfile.Hatchet))
            {
                updated++;
            }

            if (EnsureWeapon(KitchenKnife, MeleeWeaponProfile.KitchenKnife))
            {
                updated++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return updated;
        }

        private static bool EnsureHandMelee(string path)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                Debug.LogError($"[MeleePrefabSetup] Missing {path}");
                return false;
            }

            try
            {
                if (!root.TryGetComponent(out HandMeleeExtension _))
                {
                    root.AddComponent<HandMeleeExtension>();
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[MeleePrefabSetup] Hand melee on {path}");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool EnsureWeapon(string path, MeleeWeaponProfile profile)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                Debug.LogError($"[MeleePrefabSetup] Missing {path}");
                return false;
            }

            try
            {
                if (!root.TryGetComponent(out MeleeWeaponItemExtension extension))
                {
                    extension = root.AddComponent<MeleeWeaponItemExtension>();
                }

                SerializedObject so = new(extension);
                so.FindProperty("_profile.BruteDamage").floatValue = profile.BruteDamage;
                so.FindProperty("_profile.BurnDamage").floatValue = profile.BurnDamage;
                so.FindProperty("_profile.WindupSeconds").floatValue = profile.WindupSeconds;
                so.FindProperty("_profile.RecoverySeconds").floatValue = profile.RecoverySeconds;
                so.FindProperty("_profile.StaminaCost").floatValue = profile.StaminaCost;
                so.FindProperty("_profile.CanSever").boolValue = profile.CanSever;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[MeleePrefabSetup] Weapon melee on {path}");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
#endif

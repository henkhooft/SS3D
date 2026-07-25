#if UNITY_EDITOR
using SS3D.Systems.Combat;
using SS3D.Systems.Combat.Interactions;
using UnityEditor;
using UnityEngine;

namespace SS3D.Systems.Combat.Editor
{
    /// <summary>
    /// Wires Phase 3 ranged extensions onto gun prefabs (PrefabUtility — not Human.prefab).
    /// </summary>
    public static class RangedPrefabSetup
    {
        private const string M4 = "Assets/Content/WorldObjects/Items/Weapons/M4.prefab";

        /// <summary>BatchMode: <c>-executeMethod SS3D.Systems.Combat.Editor.RangedPrefabSetup.SetupBatch</c>
        /// Prefer <see cref="CombatContentPrefabRecipes.RunAllBatch"/> for domain re-runs.</summary>
        public static void SetupBatch()
        {
            int updated = SetupAll();
            Debug.Log($"[RangedPrefabSetup] Updated {updated} prefabs.");
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(updated > 0 ? 0 : 1);
            }
        }

        public static int SetupAll()
        {
            int updated = 0;
            if (EnsureWeapon(M4, RangedWeaponProfile.M4))
            {
                updated++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return updated;
        }

        private static bool EnsureWeapon(string path, RangedWeaponProfile profile)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                Debug.LogError($"[RangedPrefabSetup] Missing {path}");
                return false;
            }

            try
            {
                if (!root.TryGetComponent(out RangedWeaponItemExtension extension))
                {
                    extension = root.AddComponent<RangedWeaponItemExtension>();
                }

                SerializedObject so = new(extension);
                so.FindProperty("_profile.BruteDamage").floatValue = profile.BruteDamage;
                so.FindProperty("_profile.BurnDamage").floatValue = profile.BurnDamage;
                so.FindProperty("_profile.CanSever").boolValue = profile.CanSever;
                so.FindProperty("_profile.BaseSpreadDegrees").floatValue = profile.BaseSpreadDegrees;
                so.FindProperty("_profile.RecoilClimbDegrees").floatValue = profile.RecoilClimbDegrees;
                so.FindProperty("_profile.RecoilPerShot").floatValue = profile.RecoilPerShot;
                so.FindProperty("_profile.RecoilDecayPerSecond").floatValue = profile.RecoilDecayPerSecond;
                so.FindProperty("_profile.MovementBloomPerSpeed").floatValue = profile.MovementBloomPerSpeed;
                so.FindProperty("_profile.FalloffStartMeters").floatValue = profile.FalloffStartMeters;
                so.FindProperty("_profile.FalloffEndMeters").floatValue = profile.FalloffEndMeters;
                so.FindProperty("_profile.FalloffExtraSpreadDegrees").floatValue = profile.FalloffExtraSpreadDegrees;
                so.FindProperty("_profile.MaxRangeMeters").floatValue = profile.MaxRangeMeters;
                so.FindProperty("_profile.FireCooldownSeconds").floatValue = profile.FireCooldownSeconds;
                so.FindProperty("_profile.MagazineSize").intValue = profile.MagazineSize;
                so.FindProperty("_profile.ReloadSeconds").floatValue = profile.ReloadSeconds;
                so.FindProperty("_profile.StructuralForce").floatValue = profile.StructuralForce;
                so.FindProperty("_profile.StaminaCost").floatValue = profile.StaminaCost;
                so.FindProperty("_profile.ExhaustionSpreadDegrees").floatValue = profile.ExhaustionSpreadDegrees;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[RangedPrefabSetup] Ranged weapon on {path}");
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

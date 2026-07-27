#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SS3D.Systems.Electricity.Editor
{
    /// <summary>
    /// Domain recipe aggregator for electricity content prefabs (tier B — see
    /// 2026-07_editor-tooling-tiers.md).
    /// </summary>
    public static class ElectricityContentPrefabRecipes
    {
        [MenuItem("SS3D/Electricity/Run Content Prefab Recipes")]
        public static void RunAllMenu()
        {
            int solar = SolarPrefabSetup.SetupAll();

            EditorUtility.DisplayDialog(
                "Electricity Content Prefab Recipes",
                $"Solar: updated {solar} prefab(s).",
                "OK");
        }

        /// <summary>BatchMode: <c>-executeMethod SS3D.Systems.Electricity.Editor.ElectricityContentPrefabRecipes.RunAllBatch</c></summary>
        public static void RunAllBatch()
        {
            int solar = SolarPrefabSetup.SetupAll();
            Debug.Log($"[ElectricityContentPrefabRecipes] Solar {solar}.");

            if (UnityEngine.Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }
    }
}
#endif

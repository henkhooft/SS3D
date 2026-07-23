using FishNet.Object;
using SS3D.Networking;
using SS3D.Systems.Comms;
using UnityEditor;
using UnityEngine;

namespace SS3D.Editor.Bootstrap
{
    /// <summary>
    /// Editor menus for session/world lifecycle Phase 3: hub prefab creation and Human speech wiring.
    /// </summary>
    public static class SessionWorldLifecycleEditorMenus
    {
        private const string HubResourcesPath = "Assets/Resources/NetworkSystemsHub.prefab";
        private const string HumanPrefabPath =
            "Assets/Content/WorldObjects/Entities/Humanoids/Human/Human.prefab";

        [MenuItem("SS3D/Bootstrap/Create NetworkSystemsHub Prefab")]
        public static void CreateNetworkSystemsHubPrefab()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            GameObject root = new("NetworkSystemsHub");
            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkSystemsHub>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, HubResourcesPath);
            UnityEngine.Object.DestroyImmediate(root);

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"Created {HubResourcesPath}. FishNet DefaultPrefabObjects will pick it up on next refresh.");
        }

        [MenuItem("SS3D/Bootstrap/Ensure LocalSpeechEmitter on Human Prefab")]
        public static void EnsureLocalSpeechEmitterOnHuman()
        {
            GameObject human = AssetDatabase.LoadAssetAtPath<GameObject>(HumanPrefabPath);
            if (human == null)
            {
                Debug.LogError($"Human prefab not found at {HumanPrefabPath}");
                return;
            }

            string path = AssetDatabase.GetAssetPath(human);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponent<LocalSpeechEmitter>() == null)
                {
                    root.AddComponent<LocalSpeechEmitter>();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Debug.Log($"Added LocalSpeechEmitter to {HumanPrefabPath}");
                }
                else
                {
                    Debug.Log("LocalSpeechEmitter already present on Human prefab.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("SS3D/Bootstrap/Remove Scene VisionSystem (use SystemsBootstrap DDOL)")]
        public static void RemoveSceneVisionSystem()
        {
            string gameScenePath = "Assets/Content/Scenes/Game.unity";
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                gameScenePath,
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            int removed = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                removed += RemoveVisionRecursive(root);
            }

            if (removed > 0)
            {
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
                Debug.Log($"Removed {removed} VisionSystem GameObject(s) from Game.unity");
            }
            else
            {
                Debug.Log("No VisionSystem GameObject found in Game.unity");
            }
        }

        private static int RemoveVisionRecursive(GameObject go)
        {
            int removed = 0;
            for (int i = go.transform.childCount - 1; i >= 0; i--)
            {
                removed += RemoveVisionRecursive(go.transform.GetChild(i).gameObject);
            }

            if (go.name == "VisionSystem")
            {
                UnityEngine.Object.DestroyImmediate(go);
                return removed + 1;
            }

            return removed;
        }
    }
}

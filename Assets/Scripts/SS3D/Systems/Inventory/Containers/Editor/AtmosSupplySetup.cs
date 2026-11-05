#if UNITY_EDITOR
using FishNet.Object;
using FishNet.Observing;
using SS3D.Data.AssetDatabases;
using SS3D.Systems.Atmospherics.Pipes;
using SS3D.Systems.Tile;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using DB = UnityEditor.AssetDatabase;

namespace SS3D.Systems.Atmospherics.Editor
{
    /// <summary>
    /// Builds the hidden gas source and line fill tile objects: a flat coloured marker that only the
    /// map editor draws, on a tile object with no collider.
    /// Tier B recipe — no MenuItem.
    /// </summary>
    public static class AtmosSupplySetup
    {
        private const string PrefabFolder = "Assets/Content/WorldObjects/Structures/Pipes/Atmospherics/Supply/";
        private const string TileFolder = "Assets/Content/Data/TileMap/Resources/Structures/Pipes/Atmospherics/";
        private const string ReferenceFolder = "Assets/Content/Data/ObjectAssetReferences/";
        private const string ItemsDatabase = "Assets/Content/Data/Databases/Items.asset";
        private const string GridConditionPath = "Assets/FishNet/Runtime/Observing/Conditions/GridCondition/GridCondition.asset";
        private const string ItemsDatabaseGuid = "c2644d7b34e956743a0bc485e7ca5e37";

        public static void Run()
        {
            if (!DB.IsValidFolder(PrefabFolder.TrimEnd('/')))
                DB.CreateFolder("Assets/Content/WorldObjects/Structures/Pipes/Atmospherics", "Supply");

            Material air = EnsureMaterial("AtmosSupplyAir", new Color(0.85f, 0.9f, 0.95f));
            Material oxygen = EnsureMaterial("AtmosSupplyOxygen", new Color(0.2f, 0.45f, 0.95f));
            Material nitrogen = EnsureMaterial("AtmosSupplyNitrogen", new Color(0.9f, 0.25f, 0.2f));

            BuildSource("AtmosSourceAir", AtmosSupplyMix.Air, air);
            BuildSource("AtmosSourceOxygen", AtmosSupplyMix.Oxygen, oxygen);
            BuildSource("AtmosSourceNitrogen", AtmosSupplyMix.Nitrogen, nitrogen);
            BuildFill("AtmosFillSupply", TileLayer.PipeLeft, air);
            BuildFill("AtmosFillMixed", TileLayer.PipeMiddle, air);

            DB.SaveAssets();
            Debug.Log("[AtmosSupplySetup] Built gas sources and line fills.");
        }

        private static void BuildSource(string name, AtmosSupplyMix mix, Material material)
        {
            Build(name, material, root =>
            {
                var source = new SerializedObject(root.AddComponent<AtmosGasSource>());
                source.FindProperty("_mix").enumValueIndex = (int)mix;
                source.ApplyModifiedPropertiesWithoutUndo();
            });
        }

        private static void BuildFill(string name, TileLayer line, Material material)
        {
            Build(name, material, root =>
            {
                var fill = new SerializedObject(root.AddComponent<AtmosLineFill>());
                fill.FindProperty("_line").intValue = (int)line;
                fill.ApplyModifiedPropertiesWithoutUndo();
            });
        }

        private static void Build(string name, Material material, System.Action<GameObject> addBehaviour)
        {
            string path = PrefabFolder + name + ".prefab";
            var root = new GameObject(name);
            try
            {
                root.AddComponent<NetworkObject>();
                root.AddComponent<PlacedTileObject>();
                addBehaviour(root);
                AddGridObserver(root);

                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                marker.name = "EditorMarker";
                Object.DestroyImmediate(marker.GetComponent<Collider>());
                marker.transform.SetParent(root.transform, false);
                marker.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                marker.transform.localScale = new Vector3(0.6f, 0.1f, 0.6f);
                marker.GetComponent<MeshRenderer>().sharedMaterial = material;

                FishNet.Managing.Object.ManagedObjects.InitializePrefab(root.GetComponent<NetworkObject>(), -1);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            TileObjectSo tile = RegisterTileObject(name, path, TileLayer.FurnitureBase);
            tile.mapEditorOnly = true;
            EditorUtility.SetDirty(tile);
        }

        /// <summary>
        /// Gives a prefab its tile asset and a place in the Items database and addressables.
        /// </summary>
        public static TileObjectSo RegisterTileObject(string name, string path, TileLayer layer)
        {
            var reference = EnsureAsset<ObjectAssetReference>(ReferenceFolder + name + ".asset");
            reference.Id = DB.AssetPathToGUID(path);
            reference.Database = ItemsDatabaseGuid;
            EditorUtility.SetDirty(reference);

            var tile = EnsureAsset<TileObjectSo>(TileFolder + name + ".asset");
            tile.PrefabAsset = reference;
            tile.layer = layer;
            EditorUtility.SetDirty(tile);

            var database = DB.LoadAssetAtPath<SS3D.Data.AssetDatabases.AssetDatabase>(ItemsDatabase);
            AddressableAssetSettingsDefaultObject.Settings.CreateOrMoveEntry(reference.Id, database.AssetGroup).address = path;
            database.Assets[reference.Id] = DB.LoadAssetAtPath<GameObject>(path);
            EditorUtility.SetDirty(database);
            return tile;
        }

        // Same observer TileGridObserverConfigurator gives every tile prefab.
        public static void AddGridObserver(GameObject root)
        {
            var observer = new SerializedObject(root.AddComponent<NetworkObserver>());
            observer.FindProperty("_overrideType").intValue = (int)NetworkObserver.ConditionOverrideType.IgnoreManager;
            observer.FindProperty("_updateHostVisibility").boolValue = true;
            SerializedProperty conditions = observer.FindProperty("_observerConditions");
            conditions.arraySize = 1;
            conditions.GetArrayElementAtIndex(0).objectReferenceValue = DB.LoadAssetAtPath<ObserverCondition>(GridConditionPath);
            observer.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material EnsureMaterial(string name, Color color)
        {
            string path = PrefabFolder + name + ".mat";
            var material = DB.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", color);
            DB.CreateAsset(material, path);
            return material;
        }

        private static T EnsureAsset<T>(string path) where T : ScriptableObject
        {
            var asset = DB.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;

            asset = ScriptableObject.CreateInstance<T>();
            DB.CreateAsset(asset, path);
            return asset;
        }
    }
}
#endif

using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SS3D.Engine.Tiles
{

    public class TileAssetLoader
    {
        private static TileAssetLoader instance;
        public static TileAssetLoader GetInstance()
        {
            if (instance == null)
                instance = new TileAssetLoader();

            return instance;
        }

        private List<TileBase> assetList = new List<TileBase>();
        private List<GUIContent> assetIcons = new List<GUIContent>();
        private string searchString = "";

        private TileAssetLoader() { }


        public void LoadAssetLayer<T>(string assetName = "") where T : TileBase
        {
            assetList.Clear();
            assetIcons.Clear();

            string[] guids = AssetDatabase.FindAssets(string.Format("t:{0}", typeof(T)));

            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);

                // Case insensitive search for name
                if (assetName != "" && !asset.name.ToUpper().Contains(assetName.ToUpper()))
                {
                    continue;
                }
                Texture2D texture = AssetPreview.GetAssetPreview(asset.prefab);
                assetIcons.Add(new GUIContent(asset.name, texture));
                assetList.Add(asset);
            }
        }


        public void LoadTileLayer(TileVisibilityLayers tileLayers, string assetName = "")
        {
            switch (tileLayers)
            {
                case TileVisibilityLayers.Plenum:
                    LoadAssetLayer<Plenum>(assetName);
                    break;
                case TileVisibilityLayers.Turf:
                    LoadAssetLayer<Turf>(assetName);
                    break;
                case TileVisibilityLayers.Wire:
                    LoadAssetLayer<WireFixture>(assetName);
                    break;
                case TileVisibilityLayers.Disposal:
                    LoadAssetLayer<DisposalFixture>(assetName);
                    break;
                case TileVisibilityLayers.Pipe:
                    LoadAssetLayer<PipeFixture>(assetName);
                    break;
                case TileVisibilityLayers.HighWall:
                    LoadAssetLayer<HighWallFixture>(assetName);
                    break;
                case TileVisibilityLayers.LowWall:
                    LoadAssetLayer<LowWallFixture>(assetName);
                    break;
                case TileVisibilityLayers.AtmosMachinery:
                    LoadAssetLayer<AtmosMachineryFixture>(assetName);
                    break;
                case TileVisibilityLayers.Furniture:
                    LoadAssetLayer<FurnitureFloorFixture>(assetName);
                    break;
                case TileVisibilityLayers.Overlay:
                    LoadAssetLayer<OverlayFloorFixture>(assetName);
                    break;
            }
        }

        public List<TileBase> GetLoadedAssets()
        {
            return assetList;
        }

        public List<GUIContent> GetAssetIcons()
        {
            return assetIcons;
        }
    }
}
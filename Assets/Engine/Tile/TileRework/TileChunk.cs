using SS3D.Engine.AtmosphericsRework;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SS3D.Engine.Tiles
{
    /// <summary>
    /// Chunk class used for grouping together TileObjects.
    /// 
    /// One dimensional arrays are used for 2 dimensional grids that can be addressed via [y * width + x]
    /// 
    /// </summary>
    public class TileChunk : AbstractChunk
    {
        /// <summary>
        /// Grid for grouping TileObjects per layer. Can be used for walking through objects on the same layer fast.
        /// </summary>
        public struct TileGrid
        {
            public TileLayer layer;
            public TileObject[] tileObjectsGrid;
        }

        /*
        /// <summary>
        /// SaveObject used by chunks.
        /// </summary>
        [Serializable]
        public class ChunkSaveObject
        {
            public Vector2Int chunkKey;
            public int width;
            public int height;
            public float tileSize;
            public Vector3 originPosition;
            public TileObject.TileSaveObject[] tileObjectSaveArray;
        }
        */

        private List<TileGrid> tileGridList;

        public TileChunk(TileMap map, Vector2Int chunkKey, int width, int height, 
            float tileSize, Vector3 originPosition) : 
            base (map, chunkKey, width, height, tileSize, originPosition)
        {
            CreateAllGrids();
        }

        /// <summary>
        /// Create a new empty grid for a given layer.
        /// </summary>
        /// <param name="layer"></param>
        /// <returns></returns>
        private TileGrid CreateGrid(TileLayer layer)
        {
            TileGrid grid = new TileGrid { layer = layer };

            int gridSize = GetWidth() * GetHeight();
            grid.tileObjectsGrid = new TileObject[gridSize];

            int subLayerSize = TileHelper.GetSubLayerSize(layer);

            for (int x = 0; x < GetWidth(); x++)
            {
                for (int y = 0; y < GetHeight(); y++)
                {
                    grid.tileObjectsGrid[y * GetWidth() + x] = new TileObject(this, layer, x, y, subLayerSize);
                }
            }

            return grid;
        }

        /// <summary>
        /// Create empty grids for all layers and atmos.
        /// </summary>
        protected override void CreateAllGrids()
        {
            tileGridList = new List<TileGrid>();

            foreach (TileLayer layer in TileHelper.GetTileLayers())
            {
                tileGridList.Add(CreateGrid(layer));
            }
        }

        /// <summary>
        /// Determines if all layers in the chunk are completely empty.
        /// </summary>
        /// <returns></returns>
        public override bool IsEmpty()
        {
            bool empty = true;

            foreach (TileLayer layer in TileHelper.GetTileLayers())
            {
                for (int x = 0; x < GetWidth(); x++)
                {
                    for (int y = 0; y < GetHeight(); y++)
                    {
                        TileObject tileObject = GetTileObject(layer, x, y);
                        if (!tileObject.IsCompletelyEmpty())
                            empty = false;
                    }
                }
            }

            return empty;
        }

        /// <summary>
        /// Sets all gameobjects for a given layer to either enabled or disabled.
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="enabled"></param>
        public void SetEnabled(TileLayer layer, bool enabled)
        {
            for (int x = 0; x < GetWidth(); x++)
            {
                for (int y = 0; y < GetHeight(); y++)
                {
                    for (int i = 0; i < TileHelper.GetSubLayerSize(layer); i++)
                    {
                        GetTileObject(layer, x, y).GetPlacedObject(i)?.gameObject.SetActive(enabled);
                    }
                }
            }
        }

        /// <summary>
        /// Sets a TileObject value for a given x and y.
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="value"></param>
        public void SetTileObject(TileLayer layer, int x, int y, TileObject value)
        {
            if (x >= 0 && y >= 0 && x < GetWidth() && y < GetHeight())
            {
                tileGridList[(int)layer].tileObjectsGrid[y * GetWidth() + x] = value;
                TriggerGridObjectChanged(x, y);
            }
        }

        /// <summary>
        /// Sets a TileObject value for a given worldposition.
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="worldPosition"></param>
        /// <param name="value"></param>
        public void SetTileObject(TileLayer layer, Vector3 worldPosition, TileObject value)
        {
            Vector2Int vector = GetXY(worldPosition);
            SetTileObject(layer, vector.x, vector.y, value);
        }

        /// <summary>
        /// Gets a TileObject value for a given x and y.
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public TileObject GetTileObject(TileLayer layer, int x, int y)
        {
            if (x >= 0 && y >= 0 && x < GetWidth() && y < GetHeight())
            {
                return tileGridList[(int)layer].tileObjectsGrid[y * GetWidth() + x];
            }
            else
            {
                return default;
            }
        }

        /// <summary>
        /// Gets a TileObject value for a given worldposition.
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="worldPosition"></param>
        /// <returns></returns>
        public TileObject GetTileObject(TileLayer layer, Vector3 worldPosition)
        {
            Vector2Int vector = new Vector2Int();
            vector = GetXY(worldPosition);
            return GetTileObject(layer, vector.x, vector.y);
        }

        /// <summary>
        /// Clears the entire chunk of any PlacedTileObject.
        /// </summary>
        public void Clear()
        {
            foreach (TileLayer layer in TileHelper.GetTileLayers())
            {
                for (int x = 0; x < GetWidth(); x++)
                {
                    for (int y = 0; y < GetHeight(); y++)
                    {
                        TileObject tileObject = GetTileObject(layer, x, y);
                        for (int i = 0; i < TileHelper.GetSubLayerSize(layer); i++)
                        {
                            if (!tileObject.IsEmpty(i))
                            {
                                tileObject.ClearPlacedObject(i);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Saves all the TileObjects in the chunk.
        /// </summary>
        /// <returns></returns>
        public ChunkSaveObject Save()
        {
            // Let's save the tile objects first
            List<TileObject.TileSaveObject> tileObjectSaveList = new List<TileObject.TileSaveObject>();
            foreach (TileLayer layer in TileHelper.GetTileLayers())
            {
                for (int x = 0; x < GetWidth(); x++)
                {
                    for (int y = 0; y < GetHeight(); y++)
                    {
                        TileObject tileObject = GetTileObject(layer, x, y);
                        if (!tileObject.IsCompletelyEmpty())
                        {
                            tileObjectSaveList.Add(tileObject.Save());
                        }
                    }
                }
            }

            ChunkSaveObject saveObject = new ChunkSaveObject {
                height = GetHeight(),
                originPosition = GetOrigin(),
                tileSize = GetTileSize(),
                width = GetWidth(),
                chunkKey = GetKey(),
                saveArray = tileObjectSaveList.ToArray()
            };

            return saveObject;
        }
    }
}
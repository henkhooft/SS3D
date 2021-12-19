using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SS3D.Engine.Tiles
{
    public abstract class AbstractChunk
    {
        /// <summary>
        /// Generic SaveObject used by chunks.
        /// </summary>
        [Serializable]
        public class ChunkSaveObject
        {
            public Vector2Int chunkKey;
            public int width;
            public int height;
            public float tileSize;
            public Vector3 originPosition;
            public TileObject.TileSaveObject[] saveArray;
            // public TileObject.TileSaveObject[] tileObjectSaveArray;
        }

        /// <summary>
        /// Event that is triggered when a TileObject changes.
        /// </summary>
        public event EventHandler<OnGridObjectChangedEventArgs> OnGridObjectChanged;
        public class OnGridObjectChangedEventArgs : EventArgs
        {
            public int x;
            public int y;
        }

        /// <summary>
        /// Unique key for each chunk
        /// </summary>
        private Vector2Int chunkKey;

        private int width;
        private int height;
        private float tileSize = 1f;
        private Vector3 originPosition;
        protected TileMap map;

        public AbstractChunk(TileMap map, Vector2Int chunkKey, int width, int height, float tileSize, Vector3 originPosition)
        {
            this.map = map;
            this.chunkKey = chunkKey;
            this.width = width;
            this.height = height;
            this.tileSize = tileSize;
            this.originPosition = originPosition;

            CreateAllGrids();
        }
        
        /// <summary>
        /// Initializes all grids that are part of the chunk.
        /// </summary>
        protected abstract void CreateAllGrids();

        /// <summary>
        /// Determines if all layers in the chunk are completely empty.
        /// </summary>
        /// <returns></returns>
        public abstract bool IsEmpty();

        public int GetWidth()
        {
            return width;
        }

        public int GetHeight()
        {
            return height;
        }

        public float GetTileSize()
        {
            return tileSize;
        }

        public Vector3 GetOrigin()
        {
            return originPosition;
        }

        public Vector2Int GetKey()
        {
            return chunkKey;
        }

        /// <summary>
        /// Returns the worldposition for a given x and y offset.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public Vector3 GetWorldPosition(int x, int y)
        {
            return new Vector3(x, 0, y) * tileSize + originPosition;
        }

        /// <summary>
        /// Returns the x and y offset for a given chunk position.
        /// </summary>
        /// <param name="worldPosition"></param>
        /// <returns></returns>
        public Vector2Int GetXY(Vector3 worldPosition)
        {
            return new Vector2Int((int)Math.Round(worldPosition.x - originPosition.x), (int)Math.Round(worldPosition.z - originPosition.z));
        }

        public void TriggerGridObjectChanged(int x, int y)
        {
            OnGridObjectChanged?.Invoke(this, new OnGridObjectChangedEventArgs { x = x, y = y });
        }

        
    }
}
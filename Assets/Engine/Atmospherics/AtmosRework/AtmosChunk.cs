using SS3D.Engine.Tiles;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SS3D.Engine.AtmosphericsRework
{
    public class AtmosChunk : AbstractChunk
    {
        private TileAtmosObject[] atmosGridList;

        public AtmosChunk(AtmosMap map, Vector2Int chunkKey, int width, 
            int height, float tileSize, Vector3 originPosition) : 
            base(map, chunkKey, width, height, tileSize, originPosition)
        {
            CreateAllGrids();
        }

        protected override void CreateAllGrids()
        {
            atmosGridList = new TileAtmosObject[GetWidth() * GetHeight()];

            for (int x = 0; x < GetWidth(); x++)
            {
                for (int y = 0; y < GetHeight(); y++)
                {
                    atmosGridList[y * GetWidth() + x] = new TileAtmosObject(map, this, x, y);
                }
            }
        }

        public override bool IsEmpty()
        {
            throw new System.NotImplementedException();
        }

        public TileAtmosObject GetTileAtmosObject(int x, int y)
        {
            if (x >= 0 && y >= 0 && x < GetWidth() && y < GetHeight())
            {
                return atmosGridList[y * GetWidth() + x];
            }
            else
            {
                return default;
            }
        }

        public TileAtmosObject GetTileAtmosObject(Vector3 worldPosition)
        {
            Vector2Int vector = new Vector2Int();
            vector = GetXY(worldPosition);
            return GetTileAtmosObject(vector.x, vector.y);
        }

        public List<TileAtmosObject> GetAllTileAtmosObjects()
        {
            return new List<TileAtmosObject>(atmosGridList);
        }

        /// <summary>
        /// Saves all the TileAtmosObjects in the chunk.
        /// </summary>
        /// <returns></returns>
        public ChunkSaveObject Save()
        {
            /*
            // Let's save the tile objects first
            List<TileAtmosObject.AtmosSaveObject> atmosObjectSaveList = new List<TileAtmosObject.AtmosSaveObject>();
            foreach (TileLayer layer in TileHelper.GetTileLayers())
            {
                for (int x = 0; x < GetWidth(); x++)
                {
                    for (int y = 0; y < GetHeight(); y++)
                    {
                        TileAtmosObject atmosObject = GetTileAtmosObject(x, y);
                        
                        atmosObjectSaveList.Add(atmosObject.Save());
                    }
                }
            }

            ChunkSaveObject<TileAtmosObject.AtmosSaveObject> saveObject = new ChunkSaveObject<TileAtmosObject.AtmosSaveObject>
            {
                height = GetHeight(),
                originPosition = GetOrigin(),
                tileSize = GetTileSize(),
                width = GetWidth(),
                chunkKey = GetKey(),
                saveArray = atmosObjectSaveList.ToArray()
            };

            return saveObject;
            */

            return null;
        }
    }
}
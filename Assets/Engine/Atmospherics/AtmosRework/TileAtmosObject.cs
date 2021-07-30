using SS3D.Engine.Tiles;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SS3D.Engine.AtmosphericsRework
{
    public class TileAtmosObject
    {
        private TileAtmosObject[] neighbours;
        private AtmosObject atmosObject;
        private TileMap map;
        private TileChunk chunk;
        private int x;
        private int y;

        public TileAtmosObject(TileMap map, TileChunk chunk, int x, int y)
        {
            this.map = map;
            this.chunk = chunk;
            this.x = x;
            this.y = y;

            atmosObject = new AtmosObject();
            atmosObject.Setup();
        }

        public AtmosObject GetAtmosObject()
        {
            return atmosObject;
        }

        public void SetAtmosObject(AtmosObject atmosObject)
        {
            this.atmosObject = atmosObject;
        }

        public void LoadNeighbours()
        {
            // Get neighbours
            TileAtmosObject[] neighbours = new TileAtmosObject[4];
            for (Direction direction = Direction.North; direction <= Direction.NorthWest; direction += 2)
            {
                var vector = TileHelper.ToCardinalVector(direction);

                TileAtmosObject tileAtmosObject = map.GetTileAtmosObject(chunk.GetWorldPosition(x + vector.Item1, y + vector.Item2));
                if (tileAtmosObject != null)
                    neighbours[TileHelper.GetDirectionIndex(direction)] = tileAtmosObject;
            }

            this.neighbours = neighbours;
        }

        public TileAtmosObject[] GetNeighbours()
        {
            return neighbours;
        }

        /*
        public void SetNeighbours()
        {
            TileAtmosObject[] neighbours = new TileAtmosObject[4];
            for (Direction direction = Direction.North; direction <= Direction.NorthWest; direction += 2)
            {
                var vector = TileHelper.ToCardinalVector(direction);

                TileAtmosObject tileAtmosObject = chunk.GetTileAtmosObject(x + vector.Item1, y + vector.Item2);
                if (tileAtmosObject != null)
                    neighbours[TileHelper.GetDirectionIndex(direction)] = tileAtmosObject;
            }

            for (int i = 0; i < neighbours.Length; i++)
            {
                if (neighbours[i] == null)
                {
                    atmosObject.SetNeighbours(new AtmosObjectInfo(), i);
                    continue;
                }

                AtmosObjectInfo info = atmosObject.GetNeighbour(i);
                AtmosObject neighbourObject = neighbours[i].GetAtmosObject();
                neighbourObject.atmosObject.state = info.state;
                neighbourObject.atmosObject.container = info.container;

                neighbours[i].SetAtmosObject(neighbourObject);
            }
        }
        */

        public void Initialize()
        {
            LoadNeighbours();

            // Set to default air mixture
            // atmosObject.MakeAir();
            

            // Set blocked or vacuum if there is a wall or there is no plenum
            if (chunk.GetTileObject(TileLayer.Plenum, x, y).IsEmpty(0))
            {
                atmosObject.atmosObject.container.MakeEmpty();
                atmosObject.atmosObject.state = AtmosState.Blocked;

                // atmosObject.state = AtmosState.Inactive;
            }
            else
            {
                atmosObject.atmosObject.container.MakeRandom();
            }

            if (!chunk.GetTileObject(TileLayer.Turf, x, y).IsEmpty(0) &&
                chunk.GetTileObject(TileLayer.Turf, x, y).GetPlacedObject(0).GetGenericType().Contains("wall"))
            {
                atmosObject.atmosObject.container.MakeEmpty();
                atmosObject.atmosObject.state = AtmosState.Blocked;
            }

            if (atmosObject.atmosObject.container.GetTemperature() <= 0.1f)
            {
                Debug.Log("Warning, temperature initialized at zero");
            }


            Debug.Assert(atmosObject.atmosObject.container.GetTemperature() >= 0.1f);
        }

        public Vector3 GetWorldPosition()
        {
            return chunk.GetWorldPosition(x, y);
        }
    }
}
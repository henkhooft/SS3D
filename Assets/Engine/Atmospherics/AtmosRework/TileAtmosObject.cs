using SS3D.Engine.Tiles;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SS3D.Engine.AtmosphericsRework
{
    public class TileAtmosObject
    {
        Unity.Mathematics.Random random;
        private AtmosObject atmosObject;
        private TileChunk chunk;
        private int x;
        private int y;

        public TileAtmosObject(TileChunk chunk, int x, int y)
        {
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

        public void LoadNeighbours()
        {
            // Set neighbours
            AtmosObject[] neighbours = new AtmosObject[4];
            for (Direction direction = Direction.North; direction <= Direction.NorthWest; direction += 2)
            {
                var vector = TileHelper.ToCardinalVector(direction);

                TileAtmosObject tileAtmosObject = chunk.GetTileAtmosObject(x + vector.Item1, y + vector.Item2);
                if (tileAtmosObject != null)
                    neighbours[TileHelper.GetDirectionIndex(direction)] = tileAtmosObject.GetAtmosObject();
            }

            for (int i = 0; i < neighbours.Length; i++)
            {
                AtmosObjectInfo info = new AtmosObjectInfo()
                {
                    state = neighbours[i].GetState(),
                    container = neighbours[i].GetContainer(),
                };
                
                if (!info.container.IsEmpty())
                    atmosObject.SetNeighbours(info, i);
            }
        }

        public void SetNeighbours()
        {
            // Set neighbours
            AtmosObject[] neighbours = new AtmosObject[4];
            for (Direction direction = Direction.North; direction <= Direction.NorthWest; direction += 2)
            {
                var vector = TileHelper.ToCardinalVector(direction);

                TileAtmosObject tileAtmosObject = chunk.GetTileAtmosObject(x + vector.Item1, y + vector.Item2);
                if (tileAtmosObject != null)
                    neighbours[TileHelper.GetDirectionIndex(direction)] = tileAtmosObject.GetAtmosObject();
            }

            for (int i = 0; i < neighbours.Length; i++)
            {
                AtmosObjectInfo info = atmosObject.GetNeighbour(i);
                neighbours[i].SetState(info.state);
                neighbours[i].SetContainer(info.container);
            }
        }

        public void Initialize()
        {
            LoadNeighbours();

            // Set to default air mixture
            // atmosObject.MakeAir();

            atmosObject.MakeRandom();

            // Set blocked or vacuum if there is a wall or there is no plenum
            if (chunk.GetTileObject(TileLayer.Plenum, x, y).IsEmpty(0))
            {
                atmosObject.MakeEmpty();
                atmosObject.SetState(AtmosState.Blocked);

                // atmosObject.state = AtmosState.Inactive;
            }

            if (!chunk.GetTileObject(TileLayer.Turf, x, y).IsEmpty(0) &&
                chunk.GetTileObject(TileLayer.Turf, x, y).GetPlacedObject(0).GetGenericType().Contains("wall"))
            {
                atmosObject.SetState(AtmosState.Blocked);
            }
        }

        public Vector3 GetWorldPosition()
        {
            return chunk.GetWorldPosition(x, y);
        }
    }
}
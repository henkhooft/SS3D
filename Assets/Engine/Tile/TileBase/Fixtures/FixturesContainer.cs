using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


namespace SS3D.Engine.Tiles
{

    // Struct that holds all fixtures that can be placed on a tile
    [Serializable]
    public struct FixturesContainer
    {
        public WireFixture wire;
        public DisposalFixture disposal;
        public PipeFixture[] pipes;
        public HighWallFixture[] highWalls;
        public LowWallFixture[] lowWalls;
        public AtmosMachineryFixture atmosMachinary;
        public OverlayFloorFixture[] overlays;
        public FurnitureFloorFixture[] furniture;

        public void Init()
        {
            pipes = new PipeFixture[3];
            highWalls = new HighWallFixture[4];
            lowWalls = new LowWallFixture[4];
            overlays = new OverlayFloorFixture[3];
            furniture = new FurnitureFloorFixture[5];
        }

        public Fixture GetFixtureAtLayer(TileLayers layer)
        {
            switch (layer)
            {
                case TileLayers.Disposal:
                    return disposal;
                case TileLayers.Pipe1:
                    return pipes[0];
                case TileLayers.Pipe2:
                    return pipes[1];
                case TileLayers.Pipe3:
                    return pipes[2];
                case TileLayers.Wire:
                    return wire;

                case TileLayers.HighWallNorth:
                    return highWalls[0];
                case TileLayers.HighWallEast:
                    return highWalls[1];
                case TileLayers.HighWallSouth:
                    return highWalls[2];
                case TileLayers.HighWallWest:
                    return highWalls[3];

                case TileLayers.LowWallNorth:
                    return lowWalls[0];
                case TileLayers.LowWallEast:
                    return lowWalls[1];
                case TileLayers.LowWallSouth:
                    return lowWalls[2];
                case TileLayers.LowWallWest:
                    return lowWalls[3];

                case TileLayers.FurnitureMain:
                    return furniture[0];
                case TileLayers.Furniture2:
                    return furniture[1];
                case TileLayers.Furniture3:
                    return furniture[2];
                case TileLayers.Furniture4:
                    return furniture[3];
                case TileLayers.Furniture5:
                    return furniture[4];

                case TileLayers.Overlay1:
                    return overlays[0];
                case TileLayers.Overlay2:
                    return overlays[1];
                case TileLayers.Overlay3:
                    return overlays[2];

                case TileLayers.AtmosMachinery:
                    return atmosMachinary;
            }
            return null;
        }

        public Fixture GetFixtureAtLayer(int layerIndex)
        {
            TileLayers[] layers = (TileLayers[])Enum.GetValues(typeof(TileLayers));
            return GetFixtureAtLayer(layers[layerIndex + Fixture.LayerOffset]);
        }
       

        public List<Fixture> GetAllFixtures()
        {
            List<Fixture> fixtures = new List<Fixture>();

            for (int i = 0; i < TileDefinition.GetFixtureLayerSize(); i++)
            {
                if (GetFixtureAtLayer(i)) fixtures.Add(GetFixtureAtLayer(i));
            }

            return fixtures;
        }
    }
}
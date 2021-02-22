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
            TileLayers[] layers = TileDefinition.GetTileLayers();
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

        public void SetFixture(Fixture fixture, TileLayers layer)
        {
            switch (layer)
            {
                case TileLayers.Disposal:
                    disposal = (DisposalFixture)fixture;
                    break;
                case TileLayers.Pipe1:
                    pipes[0] = (PipeFixture)fixture;
                    break;
                case TileLayers.Pipe2:
                    pipes[1] = (PipeFixture)fixture;
                    break;
                case TileLayers.Pipe3:
                    pipes[2] = (PipeFixture)fixture;
                    break;
                case TileLayers.Wire:
                    wire = (WireFixture)fixture;
                    break;

                case TileLayers.HighWallNorth:
                    highWalls[0] = (HighWallFixture)fixture;
                    break;
                case TileLayers.HighWallEast:
                    highWalls[1] = (HighWallFixture)fixture;
                    break;
                case TileLayers.HighWallSouth:
                    highWalls[2] = (HighWallFixture)fixture;
                    break;
                case TileLayers.HighWallWest:
                    highWalls[3] = (HighWallFixture)fixture;
                    break;

                case TileLayers.LowWallNorth:
                    lowWalls[0] = (LowWallFixture)fixture;
                    break;
                case TileLayers.LowWallEast:
                    lowWalls[1] = (LowWallFixture)fixture;
                    break;
                case TileLayers.LowWallSouth:
                    lowWalls[2] = (LowWallFixture)fixture;
                    break;
                case TileLayers.LowWallWest:
                    lowWalls[3] = (LowWallFixture)fixture;
                    break;

                case TileLayers.FurnitureMain:
                    furniture[0] = (FurnitureFloorFixture)fixture;
                    break;
                case TileLayers.Furniture2:
                    furniture[1] = (FurnitureFloorFixture)fixture;
                    break;
                case TileLayers.Furniture3:
                    furniture[2] = (FurnitureFloorFixture)fixture;
                    break;
                case TileLayers.Furniture4:
                    furniture[3] = (FurnitureFloorFixture)fixture;
                    break;
                case TileLayers.Furniture5:
                    furniture[4] = (FurnitureFloorFixture)fixture;
                    break;

                case TileLayers.Overlay1:
                    overlays[0] = (OverlayFloorFixture)fixture;
                    break;
                case TileLayers.Overlay2:
                    overlays[1] = (OverlayFloorFixture)fixture;
                    break;
                case TileLayers.Overlay3:
                    overlays[2] = (OverlayFloorFixture)fixture;
                    break;

                case TileLayers.AtmosMachinery:
                    atmosMachinary = (AtmosMachineryFixture)fixture;
                    break;
            }
        }

        public bool IsEmpty()
        {
            return GetAllFixtures().Count == 0;
        }

        public static bool operator ==(FixturesContainer a, FixturesContainer b)
        {
            return a.GetAllFixtures().Equals(b.GetAllFixtures());
        }
        public static bool operator !=(FixturesContainer a, FixturesContainer b)
        {
            return !(a == b);
        }
    }
}
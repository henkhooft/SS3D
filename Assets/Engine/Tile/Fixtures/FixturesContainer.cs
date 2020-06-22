﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace SS3D.Engine.Tiles
{
    // Class that holds all fixtures that can be placed on a tile
    [Serializable]
    public class FixturesContainer : ICloneable
    {
        public TileFixtureDefinition tileFixtureDefinition;
        public WallFixtureDefinition wallFixtureDefinition;
        public FloorFixtureDefinition floorFixtureDefinition;

        public TileFixture GetTileFixtureAtLayer(TileFixtureLayers layer)
        {
            switch (layer)
            {
                case TileFixtureLayers.Disposal:
                    return tileFixtureDefinition.disposal;
                case TileFixtureLayers.Pipe1:
                    return tileFixtureDefinition.pipe1;
                case TileFixtureLayers.Pipe2:
                    return tileFixtureDefinition.pipe2;
                case TileFixtureLayers.Pipe3:
                    return tileFixtureDefinition.pipe3;
                case TileFixtureLayers.Plenum:
                    return tileFixtureDefinition.plenumCap;
                case TileFixtureLayers.Wire:
                    return tileFixtureDefinition.wire;
            }
            return null;
        }

        public WallFixture GetWallFixtureAtLayer(WallFixtureLayers layer)
        {
            switch (layer)
            {
                case WallFixtureLayers.HighWallNorth:
                    return wallFixtureDefinition.highWallNorth;
                case WallFixtureLayers.HighWallEast:
                    return wallFixtureDefinition.highWallEast;
                case WallFixtureLayers.HighWallSouth:
                    return wallFixtureDefinition.highWallSouth;
                case WallFixtureLayers.HighWallWest:
                    return wallFixtureDefinition.highWallWest;

                case WallFixtureLayers.LowWallNorth:
                    return wallFixtureDefinition.lowWallNorth;
                case WallFixtureLayers.LowWallEast:
                    return wallFixtureDefinition.lowWallEast;
                case WallFixtureLayers.LowWallSouth:
                    return wallFixtureDefinition.lowWallSouth;
                case WallFixtureLayers.LowWallWest:
                    return wallFixtureDefinition.lowWallWest;

            }
            return null;
        }

        public FloorFixture GetFloorFixtureAtLayer(FloorFixtureLayers layer)
        {
            switch (layer)
            {
                case FloorFixtureLayers.TableMachineFixture1:
                    return floorFixtureDefinition.tableMachine1;
                case FloorFixtureLayers.TableMachineFixture2:
                    return floorFixtureDefinition.tableMachine2;
                case FloorFixtureLayers.TableMachineFixture3:
                    return floorFixtureDefinition.tableMachine3;
                case FloorFixtureLayers.TableMachineFixture4:
                    return floorFixtureDefinition.tableMachine4;
                case FloorFixtureLayers.TableMachineFixture5:
                    return floorFixtureDefinition.tableMachine5;

                case FloorFixtureLayers.OverlayFixture1:
                    return floorFixtureDefinition.overlay1;
                case FloorFixtureLayers.OverlayFixture2:
                    return floorFixtureDefinition.overlay2;
                case FloorFixtureLayers.OverlayFixture3:
                    return floorFixtureDefinition.overlay3;

                case FloorFixtureLayers.FurnitureFixture:
                    return floorFixtureDefinition.furniture;
            }
            return null;
        }

        public void SetTileFixtureAtLayer(TileFixture fixture, TileFixtureLayers layer)
        {
            switch (layer)
            {
                case TileFixtureLayers.Disposal:
                    tileFixtureDefinition.disposal = (DisposalFixture)fixture;
                    break;
                case TileFixtureLayers.Pipe1:
                    tileFixtureDefinition.pipe1 = (PipeFixture)fixture;
                    break;
                case TileFixtureLayers.Pipe2:
                    tileFixtureDefinition.pipe2 = (PipeFixture)fixture;
                    break;
                case TileFixtureLayers.Pipe3:
                    tileFixtureDefinition.pipe3 = (PipeFixture)fixture;
                    break;
                case TileFixtureLayers.Plenum:
                    tileFixtureDefinition.plenumCap = (PlenumFixture)fixture;
                    break;
                case TileFixtureLayers.Wire:
                    tileFixtureDefinition.wire = (WireFixture)fixture;
                    break;
            }
        }

        public void SetWallFixtureAtLayer(WallFixture fixture, WallFixtureLayers layer)
        {
            switch (layer)
            {
                case WallFixtureLayers.HighWallNorth:
                    wallFixtureDefinition.highWallNorth = (HighWallFixture)fixture;
                    wallFixtureDefinition.highWallNorth?.SetOrientation(WallFixture.Orientation.North);
                    break;
                case WallFixtureLayers.HighWallEast:
                    wallFixtureDefinition.highWallEast = (HighWallFixture)fixture;
                    wallFixtureDefinition.highWallEast?.SetOrientation(WallFixture.Orientation.East);
                    break;
                case WallFixtureLayers.HighWallSouth:
                    wallFixtureDefinition.highWallSouth = (HighWallFixture)fixture;
                    wallFixtureDefinition.highWallSouth?.SetOrientation(WallFixture.Orientation.South);
                    break;
                case WallFixtureLayers.HighWallWest:
                    wallFixtureDefinition.highWallWest = (HighWallFixture)fixture;
                    wallFixtureDefinition.highWallWest?.SetOrientation(WallFixture.Orientation.West);
                    break;

                case WallFixtureLayers.LowWallNorth:
                    wallFixtureDefinition.lowWallNorth = (LowWallFixture)fixture;
                    wallFixtureDefinition.lowWallNorth?.SetOrientation(WallFixture.Orientation.North);
                    break;
                case WallFixtureLayers.LowWallEast:
                    wallFixtureDefinition.lowWallEast = (LowWallFixture)fixture;
                    wallFixtureDefinition.lowWallEast?.SetOrientation(WallFixture.Orientation.East);
                    break;
                case WallFixtureLayers.LowWallSouth:
                    wallFixtureDefinition.lowWallSouth = (LowWallFixture)fixture;
                    wallFixtureDefinition.lowWallSouth?.SetOrientation(WallFixture.Orientation.South);
                    break;
                case WallFixtureLayers.LowWallWest:
                    wallFixtureDefinition.lowWallWest = (LowWallFixture)fixture;
                    wallFixtureDefinition.lowWallWest?.SetOrientation(WallFixture.Orientation.West);
                    break;

            }
        }

        public void SetFloorFixtureAtLayer(FloorFixture fixture, FloorFixtureLayers layer)
        {
            switch (layer)
            {
                case FloorFixtureLayers.TableMachineFixture1:
                    floorFixtureDefinition.tableMachine1 = (TableMachineFixture)fixture;
                    break;
                case FloorFixtureLayers.TableMachineFixture2:
                    floorFixtureDefinition.tableMachine2 = (TableMachineFixture)fixture;
                    break;
                case FloorFixtureLayers.TableMachineFixture3:
                    floorFixtureDefinition.tableMachine3 = (TableMachineFixture)fixture;
                    break;
                case FloorFixtureLayers.TableMachineFixture4:
                    floorFixtureDefinition.tableMachine4 = (TableMachineFixture)fixture;
                    break;
                case FloorFixtureLayers.TableMachineFixture5:
                    floorFixtureDefinition.tableMachine5 = (TableMachineFixture)fixture;
                    break;

                case FloorFixtureLayers.OverlayFixture1:
                    floorFixtureDefinition.overlay1 = (OverlayFloorFixture)fixture;
                    break;
                case FloorFixtureLayers.OverlayFixture2:
                    floorFixtureDefinition.overlay2 = (OverlayFloorFixture)fixture;
                    break;
                case FloorFixtureLayers.OverlayFixture3:
                    floorFixtureDefinition.overlay3 = (OverlayFloorFixture)fixture;
                    break;

                case FloorFixtureLayers.FurnitureFixture:
                    floorFixtureDefinition.furniture = (FurnitureFloorFixture)fixture;
                    break;
            }
        }

        public Fixture[] GetAllFixtures()
        {
            List<Fixture> fixtures = new List<Fixture>();

            foreach (TileFixtureLayers layer in TileDefinition.GetTileFixtureLayerNames())
            {
                fixtures.Add(GetTileFixtureAtLayer(layer));
            }

            foreach (WallFixtureLayers layer in TileDefinition.GetWallFixtureLayerNames())
            {
                fixtures.Add(GetWallFixtureAtLayer(layer));
            }

            foreach (FloorFixtureLayers layer in TileDefinition.GetFloorFixtureLayerNames())
            {
                fixtures.Add(GetFloorFixtureAtLayer(layer));
            }

            return fixtures.ToArray();
        }

        public Fixture GetFixtureAtLayerIndex(int index)
        {
            int offsetFloor = TileDefinition.GetTileFixtureLayerSize();
            int offsetWall = TileDefinition.GetWallFixtureLayerSize();
            int offsetTotal = offsetFloor + offsetWall + TileDefinition.GetFloorFixtureLayerSize();

            if (index < offsetFloor)
            {
                // We are a Tile fixture
                return GetTileFixtureAtLayer((TileFixtureLayers)index);
            }

            else if (index >= offsetFloor && index < offsetWall)
            {
                // We are a Wall fixture
                return GetWallFixtureAtLayer((WallFixtureLayers)(index - offsetFloor));
            }

            else if (index >= offsetWall && index < offsetTotal)
            {
                // We are a Floor fixture
                return GetFloorFixtureAtLayer((FloorFixtureLayers)(index - offsetFloor - offsetWall));
            }

            else
            {
                Debug.LogError("Requesting out of index Fixture");
            }

            return null;
        }

        public object Clone()
        {
            return this.MemberwiseClone();
        }

        // Remove the selection if the option is impossible
        // For example: tables cannot be build in walls, or wall fixtures cannot be build on floors
        public static TileDefinition ValidateFixtures(TileDefinition tileDefinition)
        {
            if ((tileDefinition.turf != null && tileDefinition.turf.isWall) || tileDefinition.turf == null)
            {
                // Remove floor fixtures
                foreach (FloorFixtureLayers layer in TileDefinition.GetFloorFixtureLayerNames())
                {
                    if (tileDefinition.fixtures.GetFloorFixtureAtLayer(layer) != null)
                        Debug.Log("Cannot set a floor fixture when there is no floor");

                    tileDefinition.fixtures.SetFloorFixtureAtLayer(null, layer);
                }
            }

            if ((tileDefinition.turf != null && !tileDefinition.turf.isWall) || tileDefinition.turf == null)
            {
                // Remove wall fixtures
                foreach (WallFixtureLayers layer in TileDefinition.GetWallFixtureLayerNames())
                {
                    if (tileDefinition.fixtures.GetWallFixtureAtLayer(layer) != null)
                        Debug.Log("Cannot set a wall fixture when there is no wall");

                    tileDefinition.fixtures.SetWallFixtureAtLayer(null, layer);
                }
            }
            return tileDefinition;
        }
    }
}
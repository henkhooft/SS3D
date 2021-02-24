using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

        public PipeFixture pipe1;
        public PipeFixture pipe2;
        public PipeFixture pipe3;

        public HighWallFixture highWallNorth;
        public HighWallFixture highWallEast;
        public HighWallFixture highWallSouth;
        public HighWallFixture highWallWest;

        public LowWallFixture lowWallNorth;
        public LowWallFixture lowWallEast;
        public LowWallFixture lowWallSouth;
        public LowWallFixture lowWallWest;

        public AtmosMachineryFixture atmosMachinary;

        public OverlayFloorFixture overlay1;
        public OverlayFloorFixture overlay2;
        public OverlayFloorFixture overlay3;

        public FurnitureFloorFixture furnitureMain;
        public FurnitureFloorFixture furniture2;
        public FurnitureFloorFixture furniture3;
        public FurnitureFloorFixture furniture4;
        public FurnitureFloorFixture furniture5;


        public Fixture GetFixture(TileLayers layer)
        {
            switch (layer)
            {
                case TileLayers.Disposal:
                    return disposal;
                case TileLayers.Pipe1:
                    return pipe1;
                case TileLayers.Pipe2:
                    return pipe2;
                case TileLayers.Pipe3:
                    return pipe3;
                case TileLayers.Wire:
                    return wire;

                case TileLayers.HighWallNorth:
                    return highWallNorth;
                case TileLayers.HighWallEast:
                    return highWallEast;
                case TileLayers.HighWallSouth:
                    return highWallSouth;
                case TileLayers.HighWallWest:
                    return highWallWest;

                case TileLayers.LowWallNorth:
                    return lowWallNorth;
                case TileLayers.LowWallEast:
                    return lowWallEast;
                case TileLayers.LowWallSouth:
                    return lowWallSouth;
                case TileLayers.LowWallWest:
                    return lowWallWest;

                case TileLayers.FurnitureMain:
                    return furnitureMain;
                case TileLayers.Furniture2:
                    return furniture2;
                case TileLayers.Furniture3:
                    return furniture3;
                case TileLayers.Furniture4:
                    return furniture4;
                case TileLayers.Furniture5:
                    return furniture5;

                case TileLayers.Overlay1:
                    return overlay1;
                case TileLayers.Overlay2:
                    return overlay2;
                case TileLayers.Overlay3:
                    return overlay3;

                case TileLayers.AtmosMachinery:
                    return atmosMachinary;
            }
            return null;
        }

        public Fixture GetFixture(int layerIndex)
        {
            TileLayers[] layers = TileDefinition.GetTileLayers();
            return GetFixture(layers[layerIndex + Fixture.LayerOffset]);
        }
       
        public List<Fixture> GetAllFixtures()
        {
            List<Fixture> fixtures = new List<Fixture>();

            for (int i = 0; i < TileDefinition.GetFixtureLayerSize(); i++)
            {
                fixtures.Add(GetFixture(i));
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
                    pipe1 = (PipeFixture)fixture;
                    break;
                case TileLayers.Pipe2:
                    pipe2 = (PipeFixture)fixture;
                    break;
                case TileLayers.Pipe3:
                    pipe3 = (PipeFixture)fixture;
                    break;
                case TileLayers.Wire:
                    wire = (WireFixture)fixture;
                    break;

                case TileLayers.HighWallNorth:
                    highWallNorth = (HighWallFixture)fixture;
                    break;
                case TileLayers.HighWallEast:
                    highWallEast = (HighWallFixture)fixture;
                    break;
                case TileLayers.HighWallSouth:
                    highWallSouth = (HighWallFixture)fixture;
                    break;
                case TileLayers.HighWallWest:
                    highWallWest = (HighWallFixture)fixture;
                    break;

                case TileLayers.LowWallNorth:
                    lowWallNorth = (LowWallFixture)fixture;
                    break;
                case TileLayers.LowWallEast:
                    lowWallEast = (LowWallFixture)fixture;
                    break;
                case TileLayers.LowWallSouth:
                    lowWallSouth = (LowWallFixture)fixture;
                    break;
                case TileLayers.LowWallWest:
                    lowWallWest = (LowWallFixture)fixture;
                    break;

                case TileLayers.FurnitureMain:
                    furnitureMain = (FurnitureFloorFixture)fixture;
                    break;
                case TileLayers.Furniture2:
                    furniture2 = (FurnitureFloorFixture)fixture;
                    break;
                case TileLayers.Furniture3:
                    furniture3 = (FurnitureFloorFixture)fixture;
                    break;
                case TileLayers.Furniture4:
                    furniture4 = (FurnitureFloorFixture)fixture;
                    break;
                case TileLayers.Furniture5:
                    furniture5 = (FurnitureFloorFixture)fixture;
                    break;

                case TileLayers.Overlay1:
                    overlay1 = (OverlayFloorFixture)fixture;
                    break;
                case TileLayers.Overlay2:
                    overlay2 = (OverlayFloorFixture)fixture;
                    break;
                case TileLayers.Overlay3:
                    overlay3 = (OverlayFloorFixture)fixture;
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

        public override bool Equals(object obj)
        {
            if (!(obj is FixturesContainer))
            {
                return false;
            }

            var container = (FixturesContainer)obj;
            return EqualityComparer<WireFixture>.Default.Equals(wire, container.wire) &&
                   EqualityComparer<DisposalFixture>.Default.Equals(disposal, container.disposal) &&
                   EqualityComparer<PipeFixture>.Default.Equals(pipe1, container.pipe1) &&
                   EqualityComparer<PipeFixture>.Default.Equals(pipe2, container.pipe2) &&
                   EqualityComparer<PipeFixture>.Default.Equals(pipe3, container.pipe3) &&
                   EqualityComparer<HighWallFixture>.Default.Equals(highWallNorth, container.highWallNorth) &&
                   EqualityComparer<HighWallFixture>.Default.Equals(highWallEast, container.highWallEast) &&
                   EqualityComparer<HighWallFixture>.Default.Equals(highWallSouth, container.highWallSouth) &&
                   EqualityComparer<HighWallFixture>.Default.Equals(highWallWest, container.highWallWest) &&
                   EqualityComparer<LowWallFixture>.Default.Equals(lowWallNorth, container.lowWallNorth) &&
                   EqualityComparer<LowWallFixture>.Default.Equals(lowWallEast, container.lowWallEast) &&
                   EqualityComparer<LowWallFixture>.Default.Equals(lowWallSouth, container.lowWallSouth) &&
                   EqualityComparer<LowWallFixture>.Default.Equals(lowWallWest, container.lowWallWest) &&
                   EqualityComparer<AtmosMachineryFixture>.Default.Equals(atmosMachinary, container.atmosMachinary) &&
                   EqualityComparer<OverlayFloorFixture>.Default.Equals(overlay1, container.overlay1) &&
                   EqualityComparer<OverlayFloorFixture>.Default.Equals(overlay2, container.overlay2) &&
                   EqualityComparer<OverlayFloorFixture>.Default.Equals(overlay3, container.overlay3) &&
                   EqualityComparer<FurnitureFloorFixture>.Default.Equals(furnitureMain, container.furnitureMain) &&
                   EqualityComparer<FurnitureFloorFixture>.Default.Equals(furniture2, container.furniture2) &&
                   EqualityComparer<FurnitureFloorFixture>.Default.Equals(furniture3, container.furniture3) &&
                   EqualityComparer<FurnitureFloorFixture>.Default.Equals(furniture4, container.furniture4) &&
                   EqualityComparer<FurnitureFloorFixture>.Default.Equals(furniture5, container.furniture5);
        }

        public override int GetHashCode()
        {
            var hashCode = -1261705589;
            hashCode = hashCode * -1521134295 + EqualityComparer<WireFixture>.Default.GetHashCode(wire);
            hashCode = hashCode * -1521134295 + EqualityComparer<DisposalFixture>.Default.GetHashCode(disposal);
            hashCode = hashCode * -1521134295 + EqualityComparer<PipeFixture>.Default.GetHashCode(pipe1);
            hashCode = hashCode * -1521134295 + EqualityComparer<PipeFixture>.Default.GetHashCode(pipe2);
            hashCode = hashCode * -1521134295 + EqualityComparer<PipeFixture>.Default.GetHashCode(pipe3);
            hashCode = hashCode * -1521134295 + EqualityComparer<HighWallFixture>.Default.GetHashCode(highWallNorth);
            hashCode = hashCode * -1521134295 + EqualityComparer<HighWallFixture>.Default.GetHashCode(highWallEast);
            hashCode = hashCode * -1521134295 + EqualityComparer<HighWallFixture>.Default.GetHashCode(highWallSouth);
            hashCode = hashCode * -1521134295 + EqualityComparer<HighWallFixture>.Default.GetHashCode(highWallWest);
            hashCode = hashCode * -1521134295 + EqualityComparer<LowWallFixture>.Default.GetHashCode(lowWallNorth);
            hashCode = hashCode * -1521134295 + EqualityComparer<LowWallFixture>.Default.GetHashCode(lowWallEast);
            hashCode = hashCode * -1521134295 + EqualityComparer<LowWallFixture>.Default.GetHashCode(lowWallSouth);
            hashCode = hashCode * -1521134295 + EqualityComparer<LowWallFixture>.Default.GetHashCode(lowWallWest);
            hashCode = hashCode * -1521134295 + EqualityComparer<AtmosMachineryFixture>.Default.GetHashCode(atmosMachinary);
            hashCode = hashCode * -1521134295 + EqualityComparer<OverlayFloorFixture>.Default.GetHashCode(overlay1);
            hashCode = hashCode * -1521134295 + EqualityComparer<OverlayFloorFixture>.Default.GetHashCode(overlay2);
            hashCode = hashCode * -1521134295 + EqualityComparer<OverlayFloorFixture>.Default.GetHashCode(overlay3);
            hashCode = hashCode * -1521134295 + EqualityComparer<FurnitureFloorFixture>.Default.GetHashCode(furnitureMain);
            hashCode = hashCode * -1521134295 + EqualityComparer<FurnitureFloorFixture>.Default.GetHashCode(furniture2);
            hashCode = hashCode * -1521134295 + EqualityComparer<FurnitureFloorFixture>.Default.GetHashCode(furniture3);
            hashCode = hashCode * -1521134295 + EqualityComparer<FurnitureFloorFixture>.Default.GetHashCode(furniture4);
            hashCode = hashCode * -1521134295 + EqualityComparer<FurnitureFloorFixture>.Default.GetHashCode(furniture5);
            return hashCode;
        }

        public static bool operator ==(FixturesContainer a, FixturesContainer b)
        {
            return a.wire == b.wire && a.disposal == b.disposal && 
                a.pipe1 == b.pipe1 && a.pipe2 == b.pipe2 && a.pipe3 == b.pipe3 &&
                a.highWallNorth == b.highWallNorth && a.highWallEast == b.highWallEast &&
                a.highWallSouth == b.highWallSouth && a.highWallWest == b.highWallWest &&
                a.lowWallNorth == b.lowWallNorth && a.lowWallEast == b.lowWallEast &&
                a.lowWallSouth == b.lowWallSouth && a.lowWallWest == b.lowWallWest &&
                a.atmosMachinary == b.atmosMachinary && 
                a.overlay1 == b.overlay1 && a.overlay2 == b.overlay2 && a.overlay3 == b.overlay3 &&
                a.furnitureMain == b.furnitureMain && a.furniture2 == b.furniture2 && a.furniture3 == b.furniture3 &&
                a.furniture4 == b.furniture4 && a.furniture5 == b.furniture5;
        }

        public static bool operator !=(FixturesContainer a, FixturesContainer b)
        {
            return !(a == b);
        }

    }
}
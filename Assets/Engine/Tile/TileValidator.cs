using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SS3D.Engine.Tiles
{
    public class TileValidator
    {
        /// <summary>
        /// Validates a given tiledefinition and removes invalid fixtures
        /// For example: A table cannot be build in walls, or wall fixtures canno be build on floors
        /// </summary>
        /// <param name="tileDefinition"></param>
        /// <returns></returns>
        public static TileDefinition ValidateFixtures(TileDefinition tileDefinition)
        {
            string reason = "";

            FixturesContainer fixtures = tileDefinition.fixtures;

            // Restrictions on lattices
            if (tileDefinition.plenum.name.Contains("Lattice"))
            {
                // Remove tile fixtures
                if (fixtures.GetFixtureAtLayer(TileLayers.Disposal) ||
                    fixtures.GetFixtureAtLayer(TileLayers.Wire) ||
                    fixtures.GetFixtureAtLayer(TileLayers.Pipe1) ||
                    fixtures.GetFixtureAtLayer(TileLayers.Pipe2) ||
                    fixtures.GetFixtureAtLayer(TileLayers.Pipe3))
                {
                    fixtures.disposal = null;
                    fixtures.wire = null;
                    fixtures.pipes = new PipeFixture[3];

                    reason += "Lattices do not support any wall/floor fixture.\n";
                }

                // Remove floor fixtures
                if (tileDefinition.turf == null || tileDefinition.turf.isWall)
                {
                    if (fixtures.GetFixtureAtLayer(TileLayers.FurnitureMain) ||
                        fixtures.GetFixtureAtLayer(TileLayers.Furniture2) ||
                        fixtures.GetFixtureAtLayer(TileLayers.Furniture3) ||
                        fixtures.GetFixtureAtLayer(TileLayers.Furniture4) ||
                        fixtures.GetFixtureAtLayer(TileLayers.Furniture5) ||
                        fixtures.GetFixtureAtLayer(TileLayers.Overlay1) ||
                        fixtures.GetFixtureAtLayer(TileLayers.Overlay2) ||
                        fixtures.GetFixtureAtLayer(TileLayers.Overlay3) ||
                        fixtures.GetFixtureAtLayer(TileLayers.AtmosMachinery))
                    {
                        fixtures.furniture = new FurnitureFloorFixture[5];
                        fixtures.overlays = new OverlayFloorFixture[3];
                        fixtures.atmosMachinary = null;

                        reason += "Cannot set a floor fixture on lattice.\n";
                    }
                }

                // Remove wall fixtures
                if (tileDefinition.turf == null || !tileDefinition.turf.isWall)
                {
                    if (fixtures.GetFixtureAtLayer(TileLayers.HighWallNorth) ||
                        fixtures.GetFixtureAtLayer(TileLayers.HighWallEast) ||
                        fixtures.GetFixtureAtLayer(TileLayers.HighWallSouth) ||
                        fixtures.GetFixtureAtLayer(TileLayers.HighWallWest) ||
                        fixtures.GetFixtureAtLayer(TileLayers.LowWallNorth) ||
                        fixtures.GetFixtureAtLayer(TileLayers.LowWallEast) ||
                        fixtures.GetFixtureAtLayer(TileLayers.LowWallSouth) ||
                        fixtures.GetFixtureAtLayer(TileLayers.LowWallWest))
                    {
                        fixtures.highWalls = new HighWallFixture[4];
                        fixtures.lowWalls = new LowWallFixture[4];

                        reason += "Cannot set a wall fixture when there is no wall.\n";
                    }

                }
            }
           
            // Prevent low wall mounts on glass walls and reinforced glass walls
            if (tileDefinition.turf != null && tileDefinition.turf.isWall && tileDefinition.turf.name.Contains("GlassWall"))
            {
                if (fixtures.GetFixtureAtLayer(TileLayers.LowWallNorth) ||
                        fixtures.GetFixtureAtLayer(TileLayers.LowWallEast) ||
                        fixtures.GetFixtureAtLayer(TileLayers.LowWallSouth) ||
                        fixtures.GetFixtureAtLayer(TileLayers.LowWallWest))
                {
                    fixtures.lowWalls = new LowWallFixture[4];

                    reason += "Glass walls do not allow low wall fixtures.\n";
                }
            }

            // Restrict pipes to their own layer
            TileLayers[] pipeLayers = { TileLayers.Pipe1, TileLayers.Pipe2, TileLayers.Pipe3 };
            Fixture pipe;
            for (int i = 0; i < pipeLayers.Length; i++)
            {
                pipe = tileDefinition.fixtures.GetFixtureAtLayer(pipeLayers[i]);
                if (pipe != null && !pipe.name.Contains((i+1).ToString()))
                {
                    fixtures.pipes[i] = null;
                }
            }
            

#if UNITY_EDITOR
            if (reason != "")
            {
                EditorUtility.DisplayDialog("Fixture combination", "Invalid because of the following: \n\n" +
                    reason +
                    "\n" +
                    "Definition has been reset.", "ok");
            }
#endif

            return tileDefinition;
        }
    }
}
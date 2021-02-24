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
                if (fixtures.GetFixture(TileLayers.Disposal) ||
                    fixtures.GetFixture(TileLayers.Wire) ||
                    fixtures.GetFixture(TileLayers.Pipe1) ||
                    fixtures.GetFixture(TileLayers.Pipe2) ||
                    fixtures.GetFixture(TileLayers.Pipe3))
                {
                    fixtures.disposal = null;
                    fixtures.wire = null;
                    fixtures.pipes = new PipeFixture[3];

                    reason += "Lattices do not support any wall/floor fixture.\n";
                }

                // Remove floor fixtures
                if (tileDefinition.turf == null || tileDefinition.turf.isWall)
                {
                    if (fixtures.GetFixture(TileLayers.FurnitureMain) ||
                        fixtures.GetFixture(TileLayers.Furniture2) ||
                        fixtures.GetFixture(TileLayers.Furniture3) ||
                        fixtures.GetFixture(TileLayers.Furniture4) ||
                        fixtures.GetFixture(TileLayers.Furniture5) ||
                        fixtures.GetFixture(TileLayers.Overlay1) ||
                        fixtures.GetFixture(TileLayers.Overlay2) ||
                        fixtures.GetFixture(TileLayers.Overlay3) ||
                        fixtures.GetFixture(TileLayers.AtmosMachinery))
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
                    if (fixtures.GetFixture(TileLayers.HighWallNorth) ||
                        fixtures.GetFixture(TileLayers.HighWallEast) ||
                        fixtures.GetFixture(TileLayers.HighWallSouth) ||
                        fixtures.GetFixture(TileLayers.HighWallWest) ||
                        fixtures.GetFixture(TileLayers.LowWallNorth) ||
                        fixtures.GetFixture(TileLayers.LowWallEast) ||
                        fixtures.GetFixture(TileLayers.LowWallSouth) ||
                        fixtures.GetFixture(TileLayers.LowWallWest))
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
                if (fixtures.GetFixture(TileLayers.LowWallNorth) ||
                        fixtures.GetFixture(TileLayers.LowWallEast) ||
                        fixtures.GetFixture(TileLayers.LowWallSouth) ||
                        fixtures.GetFixture(TileLayers.LowWallWest))
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
                pipe = tileDefinition.fixtures.GetFixture(pipeLayers[i]);
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
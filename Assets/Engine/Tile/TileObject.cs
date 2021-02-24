using Tile;
using UnityEngine;
using UnityEditor;

using SS3D.Engine.Tiles.Connections;
using SS3D.Engine.Tiles.State;
using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using SS3D.Engine.Atmospherics;

namespace SS3D.Engine.Tiles
{
    /**
     * The tile object takes information about a tile and transforms it into the world gameobject.
     */
    [ExecuteAlways]
    [SelectionBase]
    public class TileObject : MonoBehaviour
    {
        public TileDefinition Tile
        {
            get => tile;
            set => SetContents(value);
        }

        /**
         * Passes through an adjacency update to all children in the fixture layer
         */
        public void UpdateFixtureAdjacency(Direction direction, TileDefinition tile, TileLayers layer)
        {
            int index = (int)layer - Fixture.LayerOffset;
            fixtureConnectors[index]?.UpdateSingle(direction, tile);
        }

        /**
         * Passes through an adjacency update to all children and all layers.
         */
        public void UpdateSingleAdjacency(Direction direction, TileDefinition tile)
        {
            // Handle plenum and turf
            plenumConnector?.UpdateSingle(direction, tile);
            turfConnector?.UpdateSingle(direction, tile);

            foreach (TileLayers layer in TileDefinition.GetFixtureLayers())
            {
                UpdateFixtureAdjacency(direction, tile, layer);
            }
        }

        /**
         * Passes through an adjacency update to all children
         */
        public void UpdateAllFixtureAdjacencies(TileDefinition[] tiles, TileLayers layer)
        {
            int index = (int)layer - Fixture.LayerOffset;
            AdjacencyConnector ac = fixtureConnectors[index];
            if (ac != null)
            {
                ac.LayerIndex = index;
                ac?.UpdateAll(tiles);
            }
        }

        public void UpdateAllAdjacencies(TileDefinition[] tiles)
        {
            // Update plenum first
            plenumConnector?.UpdateAll(tiles);
            turfConnector?.UpdateAll(tiles);

            // Update every tile layer
            foreach (TileLayers layer in TileDefinition.GetFixtureLayers())
            {
                UpdateAllFixtureAdjacencies(tiles, layer);
            }
        }

#if UNITY_EDITOR
        /**
         * Allows the editor to refresh the tile.subStates when it knows it has
         * modified the child of a tile.
         */
        public void RefreshSubData()
        {
            UpdateSubDataFromChildren();
        }

        public void RefreshAdjacencies()
        {
            var tileManager = transform.root.GetComponent<TileManager>();

            if (tileManager != null && tileManager.Count > 0 && !TileMapEditorHelpers.IsGhostTile(this))
            {
                var pos = tileManager.GetIndexAt(transform.position);
                tileManager.EditorUpdateTile(pos.x, pos.y, tile);
            }
        }

#endif

        /**
         * Fill in our non-serialized variables
         */
        private void OnEnable()
        {
            UpdateContents(true);
        }

#if UNITY_EDITOR
        /**
         * When the value is changed, refresh
         */
        private void OnValidate()
        {
            // If we haven't started yet, don't try to validate.
            if (!this || tile.IsEmpty())
                return;

            var tileManager = transform.root.GetComponent<TileManager>();

            // Can't do most things in OnValidate, so wait a sec.
            EditorApplication.delayCall += () => {
                if (!this)
                    return;

                // Update contents
                UpdateContents(false);
                // Inform the tilemanager that the tile has updated, so it can update surroundings
                if (tileManager != null && tileManager.Count > 0 && !TileMapEditorHelpers.IsGhostTile(this))
                {
                    var pos = tileManager.GetIndexAt(transform.position);
                    tileManager.EditorUpdateTile(pos.x, pos.y, tile);
                }
            };
        }

        /**
         * If the user deletes a tile in the scene while in edit mode, notify the tile manager
         */
        private void OnDestroy()
        {
            // If we are playing a game, a tile removal will be initiated by the tilemanager. 
            if (EditorApplication.isPlaying)
                return;

            var tileManager = transform.root.GetComponent<TileManager>();
            if (tileManager != null && !TileMapEditorHelpers.IsGhostTile(this))
                tileManager.RemoveTile(this);
        }
#endif


        /**
         * Modify the tile based on the given information
         */
        private void SetContents(TileDefinition newTile)
        {
            // We are not a ghost tile
            if (!gameObject.name.Contains("Ghost"))
                newTile = ValidateTurf(newTile);

            if (newTile.plenum != tile.plenum)
                CreatePlenum(newTile.plenum);
            if (newTile.turf != tile.turf)
            {
                CreateTurf(newTile.turf);
            }
            if (newTile.fixtures != tile.fixtures)
            {
                if (!gameObject.name.Contains("Ghost"))
                    newTile = TileValidator.ValidateFixtures(newTile);
                CreateFixtures(newTile.fixtures);
            }

            UpdateChildrenFromSubData(newTile);

            tile = newTile;

#if UNITY_EDITOR
            // If we're in the editor we'll try to correct any errors with tilestate.
            UpdateSubDataFromChildren();
#endif
        }

        private TileDefinition ValidateTurf(TileDefinition tileDefinition)
        {
            bool altered = false;
            string reason = "";

            if (tileDefinition.plenum == null)
                Debug.LogError("No plenum found in new tile definition");

            // Turfs cannot be build on lattices
            if (tileDefinition.plenum.name.Contains("Lattice") && tileDefinition.turf != null)
            {
                altered = true;
                tileDefinition.turf = null;
                reason += "No wall or floor can be build on lattices.\n";
            }

#if UNITY_EDITOR
            if (altered)
            {
                EditorUtility.DisplayDialog("Plenum combination", "Invalid because of the following: \n\n" +
                    reason +
                    "\n" +
                    "Definition has been reset.", "ok");
            }
#endif

            return tileDefinition;
        }

        /**
         * Run a more comprehensive complete update of tile, used
         * when you don't know what the previous tile contents was.
         */
        private void UpdateContents(bool shouldWarn)
        {
            // Fill in our references to objects using the saved information from our tile variable.
            // Effectively, this code expects the tile's children to match up to the turf and fixtures.
            // If it finds any inconsistencies, it rectifies them.
			GameObject alternateNameObject;
            if (tile.plenum)
            {
				// Check both the instance name and the prefab name. Save whichever we have as our plenum.
                plenum = transform.Find("plenum_" + tile.plenum.id)?.gameObject;
				alternateNameObject = transform.Find(tile.plenum.name)?.gameObject;
				plenum = plenum ?? alternateNameObject;

                if (plenum == null)
                {
                    if (shouldWarn)
                        Debug.LogWarning("Tile's plenum was not created? Creating now.");

                    // Create the object
                    CreatePlenum(tile.plenum);
                }
                else
                {
                    plenumConnector = plenum.GetComponent<AdjacencyConnector>();
                }
            }
            else
            {
                plenum = null;
            }


            if (tile.turf)
            {
				// Check both the instance name and the prefab name. Save whichever we have as our turf.
                turf = transform.Find("turf_" + tile.turf.id)?.gameObject;
				alternateNameObject = transform.Find(tile.turf.name)?.gameObject;
				turf = turf ?? alternateNameObject;
				

                if (turf != null)
                {
                    turfConnector = turf.GetComponent<AdjacencyConnector>();
                }
                else
                {
                    // Update our tile object to make up for the fact that the object doesn't exist in the world.
                    // A user would have to fuck around in the editor to get to this point.
                    if (shouldWarn)
                        Debug.LogWarning("Tile's turf was not created? Creating now. ");

                    // Create the object
                    CreateTurf(tile.turf);
                }
            }
            else
            {
                turf = null;
                turfConnector = null;
            }

            ValidateFixtures(shouldWarn);

            UpdateChildrenFromSubData(tile);
            UpdateSubDataFromChildren();

            // As extra fuckery ensure no NEW objects have been added either
            for (int j = transform.childCount - 1; j >= 0; j--)
            {
                var child = transform.GetChild(j).gameObject;

                if (child != plenum && child != turf && !fixtures.Contains(child))
                {
                    if (shouldWarn)
                    {
#if UNITY_EDITOR
                        if (MigrateTileDefinition(tile, child))
                        {
                            Debug.Log("Succesfully migrated " + child.name);
                        }
                        else
                        {
                            Debug.LogWarning("Unknown object found in tile " + name + ": " + child.name + ", deleting.");
                        }
#endif
                    }
                    EditorAndRuntime.Destroy(child);
                }
            }
        }

        private void ValidateFixtures(bool shouldWarn)
        {
            // FixturesContainer must exist
            if (tile.fixtures != null)
            {

                int i = 0;
                GameObject alternateNameObject;

                // Loop through every tile layer
                foreach (TileLayers layer in TileDefinition.GetFixtureLayers())
                {
                    var tileFixture = tile.fixtures.GetFixture(layer);
                    if (tileFixture != null)
                    {
                        string layerName = layer.ToString();
						
						// Check both the instance name and the prefab name. Save whichever we have as our fixture.
                        fixtures[i] = transform.Find("fixture_" + "tile_" + layerName.ToLower() + "_" + tileFixture.id)?.gameObject;
						alternateNameObject = transform.Find(tileFixture.name)?.gameObject;
						fixtures[i] = fixtures[i] ?? alternateNameObject;

                        if (fixtures[i] != null)
                        {
                            fixtureConnectors[i] = fixtures[i].GetComponent<AdjacencyConnector>();
                        }
                        else
                        {
                            // Update our tile object to make up for the fact that the object doesn't exist in the world.
                            // A user would have to fuck around in the editor to get to this point.
                            if (shouldWarn)
                                Debug.LogWarning("Fixture in Tile but not in TileObject. Creating: " + tileFixture.name);
                            CreateFixture(tileFixture, layer);
                        }
                    }
                    else
                    {
                        fixtures[i] = null;
                        fixtureConnectors[i] = null;
                    }
                    i++;
                } 
            }
        }


        private void CreatePlenum(Plenum plenumDefinition)
        {
            if (plenum != null)
                EditorAndRuntime.Destroy(plenum);
            
            if (plenumDefinition != null)
            {
                plenum = EditorAndRuntime.InstantiatePrefab(plenumDefinition.prefab, transform);
                plenum.name = "plenum_" + plenumDefinition.id;
                plenumConnector = plenum.GetComponent<AdjacencyConnector>();
            }
            else
            {
                plenum = null;
                plenumConnector = null;
            }
        }

        private void CreateTurf(Turf turfDefinition)
        {
            if (turf != null)
                EditorAndRuntime.Destroy(turf);
            if(turfDefinition != null)
            {
                turf = EditorAndRuntime.InstantiatePrefab(turfDefinition.prefab, transform);

                turf.name = "turf_" + turfDefinition.id;
                turfConnector = turf.GetComponent<AdjacencyConnector>();
            }
            else
            {
                turf = null;
                turfConnector = null;
            }
        }
        private void CreateFixture(Fixture fixtureDefinition, TileLayers layer)
        {
            int index = (int)layer - Fixture.LayerOffset;
            if (fixtures[index] != null)
                EditorAndRuntime.Destroy(fixtures[index]);

            if (fixtureDefinition != null)
            {
                if (fixtures[index] != null)
                {
                    Debug.LogWarning("Trying to overwrite fixture");
                }
                fixtures[index] = EditorAndRuntime.InstantiatePrefab(fixtureDefinition.prefab, transform);
                fixtureConnectors[index] = fixtures[index].GetComponent<AdjacencyConnector>();
            }
            else
            {
                fixtures[index] = null;
                fixtureConnectors[index] = null;
                return;
            }

            string layerName = Enum.GetName(typeof(TileLayers), layer).ToLower();
            fixtures[index].name = layerName.ToLower() + "_" + fixtureDefinition.id;
        }


        private void CreateFixtures(FixturesContainer fixturesDefinition)
        {
            foreach (TileLayers layer in TileDefinition.GetFixtureLayers())
            {
                CreateFixture(fixturesDefinition.GetFixture(layer), layer);
            }
        }

        private void UpdateChildrenFromSubData(TileDefinition newTile)
        {
            if (newTile.subStates != null && newTile.subStates.Length >= 1 && newTile.subStates[0] != null)
                plenum?.GetComponent<TileStateCommunicator>()?.SetTileState(newTile.subStates[0]);

            if (newTile.subStates != null && newTile.subStates.Length >= 2 && newTile.subStates[1] != null)
                turf?.GetComponent<TileStateCommunicator>()?.SetTileState(newTile.subStates[1]);

            for (int i = 0; i < fixtures.Length; i++)
            {
                if (newTile.subStates != null && newTile.subStates.Length >= i + 3 && newTile.subStates[i + 2] != null)
                {
                    fixtures[i]?.GetComponent<TileStateCommunicator>()?.SetTileState(newTile.subStates[i + 2]);
                }
            }
        }

        private void UpdateSubDataFromChildren()
        {
            // Plenum + Turf + all fixtures layers
            tile.subStates = new object[TileDefinition.GetTileLayerSize()];

            tile.subStates[0] = plenum != null ? plenum?.GetComponent<TileStateCommunicator>()?.GetTileState() : null;
            tile.subStates[1] = turf != null ? turf?.GetComponent<TileStateCommunicator>()?.GetTileState() : null;

            for (int i = 0; i < fixtures.Length; i++)
            {
                if (fixtures[i] != null)
                {
                    tile.subStates[i + Fixture.LayerOffset] = fixtures[i].GetComponent<TileStateCommunicator>()?.GetTileState();
                }
            }
        }

        public GameObject GetLayer(TileLayers layer)
        {
            switch (layer)
            {
                case TileLayers.Plenum:
                    return plenum;
                case TileLayers.Turf:
                    return turf;
                default:
                    if (((int)layer - Fixture.LayerOffset) >= fixtures.Length)
                        return null;
                    if (fixtures[(int)layer - Fixture.LayerOffset] != null)
                        return fixtures[(int)layer - Fixture.LayerOffset];
                    else return null;
            }
        }

#if UNITY_EDITOR
        /**
         * Migrates existing fixtures that do not have a fixturelayer set.
         */
        private bool MigrateTileDefinition(TileDefinition tile, GameObject child)
        {
           // CreateDefinitionFromChild(child)


            return false;
        }

        private TileDefinition CreateDefinitionFromChild(GameObject child)
        {
            // Load assets

            // Match the placed GameObject or prefab

            // Use the old name to guess the correct layer


            return new TileDefinition();
        }

#endif
        [SerializeField]
        private TileDefinition tile = new TileDefinition();

        private GameObject plenum = null;
        private AdjacencyConnector plenumConnector = null;

        private GameObject turf = null;
        private AdjacencyConnector turfConnector = null; // may be null
        public AtmosObject atmos;

        // Total fixtures = tile + wall + floor
        private GameObject[] fixtures = new GameObject[TileDefinition.GetFixtureLayerSize()];
        private AdjacencyConnector[] fixtureConnectors = new AdjacencyConnector[TileDefinition.GetFixtureLayerSize()];
    }
}

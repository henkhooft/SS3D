using SS3D.Engine.Tiles;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

namespace SS3D.Engine.AtmosphericsRework
{
    public class AtmosManager : MonoBehaviour
    {
        public bool showUpdate = true;
        public bool showDebug = false;

        public float UpdateRate = 0.1f;
        private float lastStep;

        private TileManager tileManager;
        private List<TileMap> mapList;
        private NativeArray<AtmosObject> atmosObjects;
        private List<TileAtmosObject> tileAtmosObjects;

        // Performance markers
        static ProfilerMarker s_PreparePerfMarker = new ProfilerMarker("Atmospherics.Initialize");
        static ProfilerMarker s_StepPerfMarker = new ProfilerMarker("Atmospherics.Step");


        // [BurstCompile(CompileSynchronously = true)]
        private struct SimulateFluxJob : IJob
        {
            public NativeArray<AtmosObject> buffer;

            /// <summary>
            /// Set the internal neighbour state based on the neighbour
            /// </summary>
            /// <param name="index"></param>
            private void LoadNeighbour(int ownIndex, int neighbourIndex, int neighbourOffset)
            {
                AtmosObjectInfo info = new AtmosObjectInfo()
                {
                    state = buffer[neighbourIndex].atmosObject.state,
                    container = buffer[neighbourIndex].atmosObject.container,
                    bufferIndex = neighbourIndex,
                };

                AtmosObject writeObject = buffer[ownIndex];
                writeObject.SetNeighbour(info, neighbourOffset);
                buffer[ownIndex] = writeObject;
            }

            /// <summary>
            /// Modify the neighbour based on the internal update
            /// </summary>
            /// <param name="index"></param>
            private void SetNeighbour(int ownIndex, int neighbourIndex, int neighbourOffset)
            {
                AtmosObject writeObject = buffer[neighbourIndex];
                writeObject.atmosObject = buffer[ownIndex].GetNeighbour(neighbourOffset);
                buffer[neighbourIndex] = writeObject;
            }

            public void Execute()
            {
                for (int index = 0; index < buffer.Length; index++)
                {
                    if (buffer[index].atmosObject.state == AtmosState.Active || buffer[index].atmosObject.state == AtmosState.Semiactive)
                    {
                        // Load neighbour
                        for (int i = 0; i < 4; i++)
                        {
                            int neighbourIndex = buffer[index].GetNeighbourIndex(i);

                            if (neighbourIndex != -1)
                            {
                                LoadNeighbour(index, neighbourIndex, i);
                            }
                        }

                        // Do actual work
                        buffer[index] = AtmosCalculator.SimulateFlux(buffer[index]);

                        // Set neighbour
                        for (int i = 0; i < 4; i++)
                        {
                            int neighbourIndex = buffer[index].GetNeighbourIndex(i);
                            if (neighbourIndex != -1)
                            {
                                SetNeighbour(index, neighbourIndex, i);
                            }
                        }
                    }
                }
            }
        }

        private void PrintAtmosList()
        {
            for (int i = 0; i < atmosObjects.Length; i++)
            {
                Debug.Log(atmosObjects[i].ToString());
            }
        }


        private void Start()
        {
            tileManager = TileManager.Instance;
            mapList = new List<TileMap>();
            tileAtmosObjects = new List<TileAtmosObject>();

            Initialize();
        }

        private void OnDestroy()
        {
            atmosObjects.Dispose();
        }

        private void Update()
        {
            if (Time.fixedTime >= lastStep)
            {
                // PrintAtmosInformation();
                // PrintAtmosList();

                int counter = RunAtmosJob();

                if (showUpdate)
                    Debug.Log("Atmos loop took: " + (Time.fixedTime - lastStep) + " seconds, simulating " + counter + " active atmos objects. Fixed update rate: " + UpdateRate);
                lastStep = Time.fixedTime + UpdateRate;

                // PrintAtmosInformation();

                bool debugEachTile = false;
                if (debugEachTile)
                {
                    for (int i = 0; i < atmosObjects.Length; i++)
                    {
                        if (atmosObjects[i].atmosObject.container.GetPressure() > 0f)
                            Debug.Log($"State: {atmosObjects[i].atmosObject.state} Pressure for tile " + i + " : " + atmosObjects[i].atmosObject.container.GetPressure());
                    }
                }
            }
        }

        private void PrintAtmosInformation()
        {
            float total = 0f;
            int nonEmptyTiles = 0;
            for (int i = 0; i < atmosObjects.Length; i++)
            {
                if (atmosObjects[i].GetTotalGas() > 0f)
                    nonEmptyTiles++;

                total += atmosObjects[i].GetTotalGas();
            }

            Debug.Log("Total amount of gas is: " + total);
            Debug.Log("Amount of tiles with gas is: " + nonEmptyTiles);
        }

        private void Initialize()
        {
            s_PreparePerfMarker.Begin();

            Debug.Log("AtmosManager: Initializing tiles");

            mapList.Clear();
            mapList.AddRange(tileManager.GetTileMaps());

            // Get all atmos tiles and devices
            tileAtmosObjects.Clear();
            List<IAtmosLoop> atmosDevices = new List<IAtmosLoop>();
            foreach (TileMap map in mapList)
            {
                foreach (TileChunk chunk in map.GetChunks())
                {
                    tileAtmosObjects.AddRange(chunk.GetAllTileAtmosObjects());
                }

                atmosDevices.AddRange(map.GetComponentsInChildren<IAtmosLoop>());
            }

            List<AtmosObject> atmosObjectList = new List<AtmosObject>();
            foreach (TileAtmosObject tileAtmosObject in tileAtmosObjects)
            {
                tileAtmosObject.Initialize();
                atmosObjectList.Add(tileAtmosObject.GetAtmosObject());
            }

            // Construct native array for use in jobs
            atmosObjects = new NativeArray<AtmosObject>(atmosObjectList.ToArray(), Allocator.Persistent);
            LoadNeighboursToArray();

            Debug.Log($"AtmosManager: Finished initializing {atmosObjects.Length} tiles");
        }

        private void LoadNeighboursToArray()
        {
            // For each Tile atmos object
            for (int tileIndex = 0; tileIndex < tileAtmosObjects.Count; tileIndex++)
            {
                // Retrieve the neighbours that were set before
                TileAtmosObject[] neighbours = tileAtmosObjects[tileIndex].GetNeighbours();

                // For each neighbour
                for (int neighbourIndex = 0; neighbourIndex < neighbours.Length; neighbourIndex++)
                {
                    // Find index
                    int foundIndex = tileAtmosObjects.FindIndex(tileObject => tileObject == neighbours[neighbourIndex]);

                    // Get corresponding atmos object
                    AtmosObject atmosObject = atmosObjects[tileIndex];

                    // Set index for object
                    atmosObject.SetNeighbourIndex(neighbourIndex, foundIndex);

                    // Write back info into native array
                    atmosObjects[tileIndex] = atmosObject;

                }
            }
        }

        private int RunAtmosJob()
        {
            s_StepPerfMarker.Begin();
            int counter = 0;

            // Step 1: Simulate
            SimulateFluxJob simulateJob = new SimulateFluxJob()
            {
                buffer = atmosObjects,
            };

            JobHandle simulateHandle = simulateJob.Schedule();
            simulateHandle.Complete();

            // Write results back
            for (int i = 0; i < atmosObjects.Length; i++)
            {
                if (showDebug)
                {
                    if (atmosObjects[i].atmosObject.state == AtmosState.Active || atmosObjects[i].atmosObject.state == AtmosState.Semiactive)
                        Debug.Log(atmosObjects[i].ToString());
                }

                tileAtmosObjects[i].SetAtmosObject(atmosObjects[i]);
                if (atmosObjects[i].atmosObject.state == AtmosState.Active)
                    counter++;
            }

            s_StepPerfMarker.End();

            return counter;
        }

        private void OnDrawGizmos()
        {
#if UNITY_EDITOR
            if (!EditorApplication.isPlaying)
                return;
#endif

            for (int i = 0; i < tileAtmosObjects.Count; i++)
            {
                Color state;
                switch (atmosObjects[i].atmosObject.state)
                {
                    case AtmosState.Active: state = new Color(0, 0, 0, 0); break;
                    case AtmosState.Semiactive: state = new Color(0, 0, 0, 0.4f); break;
                    case AtmosState.Inactive: state = new Color(0, 0, 0, 0.8f); break;
                    default: state = new Color(0, 0, 0, 1); break;
                }

                Vector3 position = tileAtmosObjects[i].GetWorldPosition();
                float pressure = atmosObjects[i].atmosObject.container.GetPressure() / 160f;

                if (pressure > 0f)
                {
                    Gizmos.color = Color.white - state;
                    Gizmos.DrawCube(position, new Vector3(0.8f, pressure, 0.8f));
                }
            }
        }
    }
}
using SS3D.Engine.Tiles;
using System.Collections.Generic;
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

        public float UpdateRate = 0.1f;
        private float lastStep;

        private TileManager tileManager;
        private List<TileMap> mapList;
        private NativeArray<AtmosObject> atmosObjects;
        private List<TileAtmosObject> tileAtmosObjects;

        // Performance markers
        static ProfilerMarker s_PreparePerfMarker = new ProfilerMarker("Atmospherics.Initialize");
        static ProfilerMarker s_StepPerfMarker = new ProfilerMarker("Atmospherics.Step");

        private struct CalculateFluxJob : IJob
        {
            public NativeArray<AtmosObject> buffer;
            // public NativeArray<AtmosObject> output;

            public void Execute()
            {
                for (int index = 0; index < buffer.Length; index++)
                {
                    if (buffer[index].atmosObject.state == AtmosState.Active)
                    {
                        // Load neighbour
                        for (int i = 0; i < 4; i++)
                        {
                            int neighbourIndex = buffer[index].GetNeighbourIndex(i);

                            if (neighbourIndex != -1)
                            {
                                if (buffer[index].GetNeighbour(i).container.GetTemperature() <= 0.1)
                                {
                                    Debug.LogError("Neighbour temperature 0");
                                }

                                AtmosObjectInfo info = new AtmosObjectInfo()
                                {
                                    state = buffer[neighbourIndex].atmosObject.state,
                                    container = buffer[neighbourIndex].atmosObject.container,
                                };

                                buffer[index].SetNeighbours(info, i);
                            }
                        }

                        // Do actual work
                        buffer[index] = AtmosCalculator.CalculateFlux(buffer[index]);

                        bool noneFound = true;
                        // Set neighbour
                        for (int i = 0; i < 4; i++)
                        {
                            
                            AtmosObjectInfo info = buffer[index].GetNeighbour(i);
                            int neighbourIndex = buffer[index].GetNeighbourIndex(i);
                            if (neighbourIndex != -1)
                            {
                                noneFound = false;
                                AtmosObject neighbourObject = buffer[neighbourIndex];

                                neighbourObject.atmosObject.state = info.state;
                                neighbourObject.atmosObject.container = info.container;

                                buffer[neighbourIndex] = neighbourObject;
                            }
                        }

                        if (noneFound)
                            Debug.LogError("No neighbours found for a tile");
                    }
                }
            }
        }

        private struct SimulateFluxJob : IJob
        {
            public NativeArray<AtmosObject> buffer;
            // public NativeArray<AtmosObject> output;

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
                                AtmosObjectInfo info = new AtmosObjectInfo()
                                {
                                    state = buffer[neighbourIndex].atmosObject.state,
                                    container = buffer[neighbourIndex].atmosObject.container,
                                };

                                buffer[index].SetNeighbours(info, i);
                            }
                        }


                        // Do actual work
                        buffer[index] = AtmosCalculator.SimulateFlux(buffer[index]);

                        // Set neighbour
                        for (int i = 0; i < 4; i++)
                        {
                            AtmosObjectInfo info = buffer[index].GetNeighbour(i);
                            int neighbourIndex = buffer[index].GetNeighbourIndex(i);
                            if (neighbourIndex != -1)
                            {
                                AtmosObject neighbourObject = buffer[neighbourIndex];

                                neighbourObject.atmosObject.state = info.state;
                                neighbourObject.atmosObject.container = info.container;

                                buffer[neighbourIndex] = neighbourObject;
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
                PrintAtmosList();

                int counter = RunAtmosJob();
                // int counter = RunAtmosLoop();

                Debug.Log("Atmos loop took: " + (Time.fixedTime - lastStep) + " seconds, simulating " + counter + " active atmos objects. Fixed update rate: " + UpdateRate);
                lastStep = Time.fixedTime + UpdateRate;

                PrintAtmosInformation();

                bool debugEachTile = true;
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

                    /*
                    var tileAtmosObjects = chunk.GetAllTileAtmosObjects();
                    tileAtmosObjects.ForEach(atmosTileObject =>
                    {
                        atmosTileObject.Initialize();
                        atmosTileObjects.Add(atmosTileObject.GetAtmosObject());
                    });
                    */
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

            PrintAtmosList();
        }

        private void LoadNeighboursToArray()
        {
            /*
            // Retrieve neighbours
            TileAtmosObject[] neighbours = new TileAtmosObject[4];
            for (Direction direction = Direction.North; direction <= Direction.NorthWest; direction += 2)
            {
                var vector = TileHelper.ToCardinalVector(direction);

                TileAtmosObject tileAtmosObject = map.GetTileAtmosObject(new Vector3(worldPosition.x + vector.Item1, 0, worldPosition.z + vector.Item2));
                if (tileAtmosObject != null)
                    neighbours[TileHelper.GetDirectionIndex(direction)] = tileAtmosObject;
            }
            */

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

                    if(atmosObject.atmosObject.container.GetVolume() <= 0.1)
                    {
                        Debug.LogError("Zero volume tile found");
                    }

                    if (atmosObject.atmosObject.container.GetTemperature() <= 0.1)
                    {
                        Debug.LogError("Neighbour temperature 0");
                    }

                    // Set index for object
                    atmosObject.SetNeighbourIndex(neighbourIndex, foundIndex);

                    // Write back info into native array
                    atmosObjects[tileIndex] = atmosObject;

                }
            }

                /*
            // Find index for each neighbour
            for (int i = 0; i < neighbours.Length; i++)
            {
                AtmosObject findNeighbour = neighbours[i].GetAtmosObject();
                for (int j = 0; j < atmosObjects.Length; j++)
                {
                    if (atmosObjects[j].Equals(findNeighbour))
                    {
                        AtmosObject currentAtmosObject = currentTileAtmosObject.GetAtmosObject();
                        currentAtmosObject.SetNeighbourIndex(i, j);
                        currentTileAtmosObject.SetAtmosObject(currentAtmosObject);
                    }
                }
            }
            */
        }

        private int RunAtmosJob()
        {
            s_StepPerfMarker.Begin();

            // NativeArray<AtmosObject> input = new NativeArray<AtmosObject>(atmosTileObjects.Count, Allocator.TempJob);
            // NativeArray<AtmosObject> output = new NativeArray<AtmosObject>(atmosTileObjects.Count, Allocator.TempJob);

            int counter = 0;

            /*
            // Step 0: Fill neighbour structs
            for (int i = 0; i < atmosObjects.Length; i++)
            {
                if (atmosObjects[i].atmosObject.state == AtmosState.Active)
                {
                    atmosTileObjects[i].LoadNeighbours();
                    atmosObjects[i] = atmosTileObjects[i].GetAtmosObject();
                    counter++;
                }
            }
            */

            // Step 1: Calculate flux
            CalculateFluxJob calculateJob = new CalculateFluxJob()
            {
                buffer = atmosObjects,
            };

            // Schedule flux calculation job with one item per processing batch
            JobHandle calculateHandle = calculateJob.Schedule();

            //calculateJob.Run();

            /*
            for (int i = 0; i < atmosObjects.Length; i++)
            {
                if (atmosObjects[i].atmosObject.state == AtmosState.Active)
                {
                    atmosTileObjects[i].SetAtmosObject(atmosObjects[i]);
                    atmosTileObjects[i].SetNeighbours();
                }
            }
            */

            // Step 2: Simulate
            SimulateFluxJob simulateJob = new SimulateFluxJob()
            {
                buffer = atmosObjects,
            };

            // Schedule simulation job and pass the handle of the first job as a dependency
            JobHandle simulateHandle = simulateJob.Schedule(calculateHandle);

            /*
            for (int i = 0; i < atmosObjects.Length; i++)
            {
                if (atmosObjects[i].atmosObject.state == AtmosState.Active ||
                    atmosObjects[i].atmosObject.state == AtmosState.Semiactive)
                {
                    atmosTileObjects[i].LoadNeighbours();
                    atmosObjects[i] = atmosTileObjects[i].GetAtmosObject();
                }
            }
            */

            // simulateJob.Run();

            // Because we passed the first job as a dependency, only wait for the completion of the second job
            simulateHandle.Complete();

            /*
            // Step 3: Write back results
            for (int i = 0; i < atmosObjects.Length; i++)
            {
                if (atmosObjects[i].atmosObject.state == AtmosState.Active ||
                    atmosObjects[i].atmosObject.state == AtmosState.Semiactive)
                {
                    atmosTileObjects[i].SetAtmosObject(atmosObjects[i]);
                    atmosTileObjects[i].SetNeighbours();
                }
            }
            */

            // Write results back
            for (int i = 0; i < atmosObjects.Length; i++)
            {
                if (atmosObjects[i].GetTotalGas() > 0)
                    Debug.Log("Has container or neighbour with non-null amounts of gas");

                if (math.any(atmosObjects[i].atmosObject.container.GetCoreGasses() > 0))
                    Debug.Log("Has container or neighbour with non-null amounts of gas");


                if (atmosObjects[i].atmosObject.container.GetPressure() > 0)
                    Debug.Log("Has container with non-null pressure");
                tileAtmosObjects[i].SetAtmosObject(atmosObjects[i]);
            }

            s_StepPerfMarker.End();

            return counter;
        }

        public int RunAtmosLoop()
        {
            /*
            int counter = 0;

            //float total = 0f;
            //for (int i = 0; i < atmosObjects.Length; i++)
            //{
            //    total += atmosObjects[i].GetTotalGas();
            //}

            //Debug.Log("Step 1: gas is: " + total);

            // Step 0: Fill neighbour structs
            for (int i = 0; i < atmosObjects.Length; i++)
            {
                if (atmosObjects[i].atmosObject.state == AtmosState.Active || atmosObjects[i].atmosObject.state == AtmosState.Semiactive)
                {
                    // atmosTileObjects[i].LoadNeighbours();
                    // atmosObjects[i] = atmosTileObjects[i].GetAtmosObject();
                    counter++;
                }
            }

            //total = 0f;
            //for (int i = 0; i < atmosObjects.Length; i++)
            //{
            //    total += atmosObjects[i].GetTotalGas();
            //}

            //Debug.Log("Step 2: gas is: " + total);

            for (int i = 0; i < atmosObjects.Length; i++)
            {
                if (atmosObjects[i].atmosObject.state == AtmosState.Active)
                {
                    tileAtmosObjects[i].LoadNeighbours();
                    atmosObjects[i] = tileAtmosObjects[i].GetAtmosObject();
                    atmosObjects[i] = AtmosCalculator.CalculateFlux(atmosObjects[i]);
                    tileAtmosObjects[i].SetAtmosObject(atmosObjects[i]);
                    tileAtmosObjects[i].SetNeighbours();
                }
            }

            //total = 0f;
            //for (int i = 0; i < atmosobjects.length; i++)
            //{
            //    total += atmosobjects[i].gettotalgas();
            //}

            //debug.log("step 3: gas is: " + total);

            for (int i = 0; i < atmosObjects.Length; i++)
            {
                if (atmosObjects[i].atmosObject.state == AtmosState.Active ||
                    atmosObjects[i].atmosObject.state == AtmosState.Semiactive)
                {
                    tileAtmosObjects[i].LoadNeighbours();
                    atmosObjects[i] = tileAtmosObjects[i].GetAtmosObject();
                    atmosObjects[i] = AtmosCalculator.SimulateFlux(atmosObjects[i]);
                    tileAtmosObjects[i].SetAtmosObject(atmosObjects[i]);
                    tileAtmosObjects[i].SetNeighbours();
                }
            }

            //total = 0f;
            //for (int i = 0; i < atmosObjects.Length; i++)
            //{
            //    total += atmosObjects[i].GetTotalGas();
            //}

            //Debug.Log("Step 4: gas is: " + total);

            // Step 3: Write back results
            for (int i = 0; i < atmosObjects.Length; i++)
            {
                atmosTileObjects[i].SetAtmosObject(atmosObjects[i]);
                atmosTileObjects[i].SetNeighbours();
            }

            //total = 0f;
            //for (int i = 0; i < atmosObjects.Length; i++)
            //{
            //    total += atmosObjects[i].GetTotalGas();
            //}

            //Debug.Log("Step 5: gas is: " + total);

            */
            int counter = -1;
            return counter;
        }

        public void AddGas(CoreAtmosGasses gas, Vector3 worldPosition, float amount)
        {
            // TileAtmosObject atmosTileObject = mapList[0].GetAtmos
        }

        private void OnDrawGizmos()
        {
#if UNITY_EDITOR
            if (!EditorApplication.isPlaying)
                return;
#endif

            Gizmos.color = Color.white;
            for (int i = 0; i < tileAtmosObjects.Count; i++)
            {
                Vector3 position = tileAtmosObjects[i].GetWorldPosition();
                float pressure = tileAtmosObjects[i].GetAtmosObject().atmosObject.container.GetPressure() / 160f;

                if (pressure > 0f)
                {
                    Gizmos.DrawWireCube(position, new Vector3(0.8f, pressure, 0.8f));
                }
                else
                    Gizmos.DrawWireCube(position, new Vector3(0.8f, 0.1f, 0.8f));
            }
        }
    }
}
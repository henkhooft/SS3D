using SS3D.Engine.Tiles;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
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
        private List<TileAtmosObject> atmosTileObjects;

        // Performance markers
        static ProfilerMarker s_PreparePerfMarker = new ProfilerMarker("Atmospherics.Initialize");
        static ProfilerMarker s_StepPerfMarker = new ProfilerMarker("Atmospherics.Step");

        private struct CalculateFluxJob : IJobParallelFor
        {
            public NativeArray<AtmosObject> buffer;
            // public NativeArray<AtmosObject> output;

            public void Execute(int index)
            {

                if (buffer[index].atmosObject.state == AtmosState.Active)
                {
                    // Load neighbour
                    for (int i = 0; i < 4; i++)
                    {
                        int neighbourIndex = buffer[index].GetNeighbourIndex(i);

                        AtmosObjectInfo info = new AtmosObjectInfo()
                        {
                            state = buffer[neighbourIndex].atmosObject.state,
                            container = buffer[neighbourIndex].atmosObject.container,
                        };

                        if (!info.container.IsEmpty())
                            buffer[index].SetNeighbours(info, i);
                    }


                    // Do actual work
                    buffer[index] = AtmosCalculator.CalculateFlux(buffer[index]);

                    // Set neighbour
                    for (int i = 0; i < 4; i++)
                    {
                        AtmosObjectInfo info = buffer[index].GetNeighbour(i);
                        int neighbourIndex = buffer[index].GetNeighbourIndex(i);
                        AtmosObject neighbourObject = buffer[neighbourIndex];

                        neighbourObject.atmosObject.state = info.state;
                        neighbourObject.atmosObject.container = info.container;

                        buffer[neighbourIndex] = neighbourObject;

                        // neighbours[i].SetAtmosObject(neighbourObject);

                        /*
                        AtmosObjectInfo info = atmosObject.GetNeighbour(i);
                        AtmosObject neighbourObject = neighbours[i].GetAtmosObject();
                        neighbourObject.atmosObject.state = info.state;
                        neighbourObject.atmosObject.container = info.container;

                        neighbours[i].SetAtmosObject(neighbourObject);
                        */

                    }
                }

            }
        }

        private struct SimulateFluxJob : IJobParallelFor
        {
            public NativeArray<AtmosObject> buffer;
            // public NativeArray<AtmosObject> output;

            public void Execute()
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    if (buffer[i].atmosObject.state == AtmosState.Active ||
                    buffer[i].atmosObject.state == AtmosState.Semiactive)
                    {
                        buffer[i] = AtmosCalculator.SimulateFlux(buffer[i]);
                    }
                }
            }
        }


        private void Start()
        {
            tileManager = TileManager.Instance;
            mapList = new List<TileMap>();
            atmosTileObjects = new List<TileAtmosObject>();

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
                int counter = RunAtmosJob();
                // int counter = RunAtmosLoop();

                Debug.Log("Atmos loop took: " + (Time.fixedTime - lastStep) + " seconds, simulating " + counter + " active atmos objects. Fixed update rate: " + UpdateRate);
                lastStep = Time.fixedTime + UpdateRate;

                
                float total = 0f;
                for (int i = 0; i < atmosObjects.Length; i++)
                {
                    total += atmosObjects[i].GetTotalGas();
                }

                Debug.Log("Total amount of gas is: " + total);
                

                /*
                for (int i = 0; i < atmosObjects.Length; i++)
                {
                    if (atmosObjects[i].atmosObject.container.GetPressure() > 0f)
                        Debug.Log($"State: {atmosObjects[i].atmosObject.state} Pressure for tile " + i + " : " + atmosObjects[i].atmosObject.container.GetPressure());
                }
                */
            }
        }

        private void Initialize()
        {
            s_PreparePerfMarker.Begin();

            Debug.Log("AtmosManager: Initializing tiles");

            mapList.Clear();
            mapList.AddRange(tileManager.GetTileMaps());

            // Get all atmos tiles and devices
            atmosTileObjects.Clear();
            List<IAtmosLoop> atmosDevices = new List<IAtmosLoop>();
            foreach (TileMap map in mapList)
            {
                foreach (TileChunk chunk in map.GetChunks())
                {
                    atmosTileObjects.AddRange(chunk.GetAllTileAtmosObjects());

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
            foreach (TileAtmosObject tileAtmosObject in atmosTileObjects)
            {
                tileAtmosObject.Initialize();
                atmosObjectList.Add(tileAtmosObject.GetAtmosObject());
            }

            // Construct native array for use in jobs
            atmosObjects = new NativeArray<AtmosObject>(atmosObjectList.ToArray(), Allocator.Persistent);

            Debug.Log($"AtmosManager: Finished initializing {atmosObjects.Length} tiles");
        }

        private void InitializeNeighbours()
        {
            for (int i = 0; i < atmosObjects.Length; i++)
            {
                
            }
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
            // JobHandle calculateHandle = calculateJob.Schedule(atmosObjects.Length, 1);
            calculateJob.Run(atmosObjects.Length);

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
            // JobHandle simulateHandle = simulateJob.Schedule(atmosObjects.Length, 1, calculateHandle);

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

            simulateJob.Run(atmosObjects.Length);

            // Because we passed the first job as a dependency, only wait for the completion of the second job
            // simulateHandle.Complete();

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

            s_StepPerfMarker.End();

            return counter;
        }

        public int RunAtmosLoop()
        {
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
                    atmosTileObjects[i].LoadNeighbours();
                    atmosObjects[i] = atmosTileObjects[i].GetAtmosObject();
                    atmosObjects[i] = AtmosCalculator.CalculateFlux(atmosObjects[i]);
                    atmosTileObjects[i].SetAtmosObject(atmosObjects[i]);
                    atmosTileObjects[i].SetNeighbours();
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
                    atmosTileObjects[i].LoadNeighbours();
                    atmosObjects[i] = atmosTileObjects[i].GetAtmosObject();
                    atmosObjects[i] = AtmosCalculator.SimulateFlux(atmosObjects[i]);
                    atmosTileObjects[i].SetAtmosObject(atmosObjects[i]);
                    atmosTileObjects[i].SetNeighbours();
                }
            }

            //total = 0f;
            //for (int i = 0; i < atmosObjects.Length; i++)
            //{
            //    total += atmosObjects[i].GetTotalGas();
            //}

            //Debug.Log("Step 4: gas is: " + total);

            /*
            // Step 3: Write back results
            for (int i = 0; i < atmosObjects.Length; i++)
            {
                atmosTileObjects[i].SetAtmosObject(atmosObjects[i]);
                atmosTileObjects[i].SetNeighbours();
            }
            */

            //total = 0f;
            //for (int i = 0; i < atmosObjects.Length; i++)
            //{
            //    total += atmosObjects[i].GetTotalGas();
            //}

            //Debug.Log("Step 5: gas is: " + total);

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
            for (int i = 0; i < atmosTileObjects.Count; i++)
            {
                Vector3 position = atmosTileObjects[i].GetWorldPosition();
                float pressure = atmosTileObjects[i].GetAtmosObject().atmosObject.container.GetPressure() / 160f;

                if (pressure > 0f)
                {
                    Gizmos.DrawWireCube(position, new Vector3(0.8f, pressure, 0.8f));
                    
                }
            }
        }
    }
}
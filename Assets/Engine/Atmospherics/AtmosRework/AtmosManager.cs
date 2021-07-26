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
            public NativeArray<AtmosObject> jobAtmosObjects;

            public void Execute(int index)
            {
                if (jobAtmosObjects[index].GetState() == AtmosState.Active)
                {
                    jobAtmosObjects[index].CalculateFlux();
                }
            }
        }

        private struct SimulateFluxJob : IJobParallelFor
        {
            public NativeArray<AtmosObject> jobAtmosObjects;

            public void Execute(int index)
            {
                if (jobAtmosObjects[index].GetState() == AtmosState.Active ||
                    jobAtmosObjects[index].GetState() == AtmosState.Semiactive)
                {
                    jobAtmosObjects[index].SimulateFlux();
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
                Debug.Log("Atmos loop took: " + (Time.fixedTime - lastStep) + " seconds, simulating " + counter + " active atmos objects. Fixed update rate: " + UpdateRate);
                lastStep = Time.fixedTime + UpdateRate;
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

        private int RunAtmosJob()
        {
            s_StepPerfMarker.Begin();

            int counter = 0;

            // Step 0: Fill neighbour structs
            for (int i = 0; i < atmosTileObjects.Count; i++)
            {
                if (atmosTileObjects[i].GetAtmosObject().GetState() == AtmosState.Active)
                {
                    atmosTileObjects[i].LoadNeighbours();
                    counter++;
                }
            }

            // Step 1: Calculate flux
            CalculateFluxJob calculateJob = new CalculateFluxJob()
            {
                jobAtmosObjects = atmosObjects
            };

            // Schedule flux calculation job with one item per processing batch
            // JobHandle calculateHandle = calculateJob.Schedule(atmosObjects.Length, 1);
            calculateJob.Run(atmosObjects.Length);

            // Step 2: Simulate
            SimulateFluxJob simulateJob = new SimulateFluxJob()
            {
                jobAtmosObjects = atmosObjects
            };

            // Schedule simulation job and pass the handle of the first job as a dependency
            // JobHandle simulateHandle = simulateJob.Schedule(atmosObjects.Length, 1, calculateHandle);

            simulateJob.Run(atmosObjects.Length);

            // Because we passed the first job as a dependency, only wait for the completion of the second job
            // simulateHandle.Complete();

            for (int i = 0; i < atmosTileObjects.Count; i++)
            {
                if (atmosTileObjects[i].GetAtmosObject().GetState() == AtmosState.Active)
                    atmosTileObjects[i].SetNeighbours();
            }

            s_StepPerfMarker.End();

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
                float pressure = atmosTileObjects[i].GetAtmosObject().GetContainer().GetPressure() / 160f;

                if (pressure > 0f)
                {
                    Gizmos.DrawWireCube(position, new Vector3(0.8f, pressure, 0.8f));
                    
                }
            }
        }
    }
}
using SS3D.Engine.Tiles;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Profiling;
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
                RunAtmosJob();
                Debug.Log("Atmos loop took: " + (Time.fixedTime - lastStep) + " seconds, simulating " + atmosObjects.Length + " atmos objects. Fixed update rate: " + UpdateRate);
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
            List<AtmosObject> atmosTiles = new List<AtmosObject>();
            List<IAtmosLoop> atmosDevices = new List<IAtmosLoop>();
            foreach (TileMap map in mapList)
            {
                foreach (TileChunk chunk in map.GetChunks())
                {
                    var tileAtmosObjects = chunk.GetAllTileAtmosObjects();
                    tileAtmosObjects.ForEach(atmosTileObject
                        => 
                    atmosTiles.Add(atmosTileObject.GetAtmosObject()));
                }

                atmosDevices.AddRange(map.GetComponentsInChildren<IAtmosLoop>());
            }

            // Construct native array for use in jobs
            atmosObjects = new NativeArray<AtmosObject>(atmosTiles.ToArray(), Allocator.Persistent);

            // Make a default air mixture
            for (int i = 0; i < atmosObjects.Length; i++)
            {
                atmosObjects[i].MakeAir();
            }

            Debug.Log($"AtmosManager: Finished initializing {atmosObjects.Length} tiles");
        }

        private void InitializeAtmosTile(AtmosObject atmosObject)
        {
            /*
            // Set neighbours
            AtmosObject[] neighbours = new AtmosObject[4];
            for (int i = 0; i < neighbours.Length; i++)
            {
                tileManager.Get
            }
            */

            atmosObject.MakeAir();

            // Set to default air mixture

            // Set blocked or vacuum if there is a wall or there is no plenum
        }

        private void RunAtmosJob()
        {
            s_StepPerfMarker.Begin();

            // Step 1: Calculate flux
            CalculateFluxJob calculateJob = new CalculateFluxJob()
            {
                jobAtmosObjects = atmosObjects
            };

            // Schedule flux calculation job with one item per processing batch
            JobHandle calculateHandle = calculateJob.Schedule(atmosObjects.Length, 1);

            // Step 2: Simulate
            SimulateFluxJob simulateJob = new SimulateFluxJob()
            {
                jobAtmosObjects = atmosObjects
            };

            // Schedule simulation job and pass the handle of the first job as a dependency
            JobHandle simulateHandle = simulateJob.Schedule(atmosObjects.Length, 1, calculateHandle);

            // Because we passed the first job as a dependency, only wait for the completion of the second job
            simulateHandle.Complete();

            s_StepPerfMarker.End();
        }
    }
}
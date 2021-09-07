using SS3D.Engine.Tiles;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

namespace SS3D.Engine.AtmosphericsRework
{
    public class AtmosManager : MonoBehaviour
    {
        public bool showUpdate = true;
        public float UpdateRate = 0.1f;
       
        private TileManager tileManager;
        private List<AtmosMap> atmosMaps;
        private List<JobHandle> jobHandles;
        private float lastStep;

        // Performance markers
        static ProfilerMarker s_PreparePerfMarker = new ProfilerMarker("Atmospherics.Initialize");
        static ProfilerMarker s_StepPerfMarker = new ProfilerMarker("Atmospherics.Step");

        private void Start()
        {
            tileManager = TileManager.Instance;
            atmosMaps = new List<AtmosMap>();
            jobHandles = new List<JobHandle>();

            Initialize();
        }

        private void OnDestroy()
        {
            foreach (AtmosMap atmosMap in atmosMaps)
            {
                atmosMap.Destroy();
            }
        }

        private void Update()
        {
            if (Time.fixedTime >= lastStep)
            {
                int tileCounter = StepAtmos();

                if (showUpdate)
                    Debug.Log("Atmos loop took: " + (Time.fixedTime - lastStep) + " seconds, simulating " + tileCounter + " active atmos objects. Fixed update rate: " + UpdateRate);
                lastStep = Time.fixedTime + UpdateRate;
            }
        }

        private void Initialize()
        {
            if (!tileManager || tileManager.GetTileMaps().Count == 0)
                Debug.LogError("AtmosManager couldn't find the tilemanager or map.");

            s_PreparePerfMarker.Begin();
            atmosMaps.Clear();

            if (showUpdate)
                Debug.Log("AtmosManager: Initializing tiles");

            int initCounter = 0;
            foreach (TileMap map in tileManager.GetTileMaps())
            {
                List<TileAtmosObject> tiles = new List<TileAtmosObject>();
                List<IAtmosLoop> devices = new List<IAtmosLoop>();

                foreach (TileChunk chunk in map.GetChunks())
                {
                    var tileAtmosObjects = chunk.GetAllTileAtmosObjects();

                    // Initialize the atmos tiles. Cannot be done in the tilemap as it may still be creating tiles.
                    tileAtmosObjects.ForEach(tile => tile.Initialize());
                    tiles.AddRange(tileAtmosObjects);
                }

                devices.AddRange(map.GetComponentsInChildren<IAtmosLoop>());

                AtmosMap atmosMap = new AtmosMap(map, tiles, devices);
                atmosMaps.Add(atmosMap);

                initCounter += tiles.Count; 
            }

            if (showUpdate)
                Debug.Log($"AtmosManager: Finished initializing {initCounter} tiles");

        }

        

        private int StepAtmos()
        {
            s_StepPerfMarker.Begin();
            int counter = 0;

            // Step 0: Loop through every map
            jobHandles.Clear();
            foreach (AtmosMap atmosMap in atmosMaps)
            {

                // Step 1: Simulate tiles
                SimulateFluxJob simulateTilesJob = new SimulateFluxJob()
                {
                    buffer = atmosMap.nativeAtmosTiles,
                };

                // Step 2: Simulate atmos devices and pipes
                SimulateFluxJob simulateDevicesJob = new SimulateFluxJob()
                {
                    buffer = atmosMap.nativeAtmosDevices,
                };

                JobHandle simulateTilesHandle = simulateTilesJob.Schedule();
                JobHandle simulateDevicesHandle = simulateDevicesJob.Schedule();

                jobHandles.Add(simulateTilesHandle);
                jobHandles.Add(simulateDevicesHandle);
            }

            // Step 3: Complete the work
            foreach (JobHandle handle in jobHandles)
            {
                handle.Complete();
            }

            // Step 4: Write back the results
            foreach (AtmosMap atmosMap in atmosMaps)
            {
                atmosMap.WriteResultsToList();
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

            foreach (AtmosMap atmosMap in atmosMaps)
            {

                for (int i = 0; i < atmosMap.atmosTiles.Count; i++)
                {
                    Color state;
                    AtmosState tileState = atmosMap.atmosTiles[i].GetAtmosObject().atmosObject.state;
                    switch (tileState)
                    {
                        case AtmosState.Active: state = new Color(0, 0, 0, 0); break;
                        case AtmosState.Semiactive: state = new Color(0, 0, 0, 0.4f); break;
                        case AtmosState.Inactive: state = new Color(0, 0, 0, 0.8f); break;
                        default: state = new Color(0, 0, 0, 1); break;
                    }

                    Vector3 position = atmosMap.atmosTiles[i].GetWorldPosition();
                    float pressure = atmosMap.nativeAtmosTiles[i].atmosObject.container.GetPressure() / 160f;

                    if (tileState == AtmosState.Active || tileState == AtmosState.Semiactive || tileState == AtmosState.Inactive)
                    {
                        Gizmos.color = Color.white - state;
                        Gizmos.DrawCube(position, new Vector3(0.8f, pressure, 0.8f));
                    }
                    else if (tileState == AtmosState.Blocked)
                    {
                        Gizmos.color = Color.black;
                        Gizmos.DrawCube(position, new Vector3(0.8f, 2.5f, 0.8f));
                    }
                    else if (tileState == AtmosState.Vacuum)
                    {
                        Gizmos.color = Color.blue;
                        Gizmos.DrawCube(position, new Vector3(0.8f, pressure, 0.8f));
                    }
                }
            }
        }
    }
}
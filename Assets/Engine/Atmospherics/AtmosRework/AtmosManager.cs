using SS3D.Engine.Tiles;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace SS3D.Engine.AtmosphericsRework
{
    public class AtmosManager : MonoBehaviour
    {
        public float UpdateRate = 0.1f;

        private TileManager tileManager;
        private List<TileMap> mapList;
        private NativeArray<AbstractAtmosObject> atmosObjects;

        // Performance markers
        static ProfilerMarker s_PreparePerfMarker = new ProfilerMarker("Atmospherics.Initialize");
        static ProfilerMarker s_StepPerfMarker = new ProfilerMarker("Atmospherics.Step");

        private void Start()
        {
            tileManager = TileManager.Instance;
            mapList = new List<TileMap>();
        }

        private void Initialize()
        {
            s_PreparePerfMarker.Begin();

            Debug.Log("AtmosManager: Initializing tiles");

            mapList.Clear();
            mapList.AddRange(tileManager.GetTileMaps());

            foreach (TileMap map in mapList)
            {
                foreach (TileChunk chunk in map.GetChunks())
                {
                    
                }
            }
        }
    }
}
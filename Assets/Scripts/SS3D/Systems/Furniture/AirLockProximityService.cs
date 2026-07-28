using System.Collections.Generic;
using FishNet;
using SS3D.Core;
using SS3D.Systems.Entities;
using SS3D.Systems.Networking;
using SS3D.Systems.Tile.MapEditor;
using UnityEngine;

namespace SS3D.Systems.Furniture
{
    /// <summary>
    /// Server-only index of airlocks by HashGrid cell. Ticks proximity from players outward
    /// so cost scales with nearby doors, not station door count.
    /// </summary>
    public sealed class AirLockProximityService : MonoBehaviour
    {
        private static AirLockProximityService _instance;

        private readonly Dictionary<Vector2Int, List<AirLockOpener>> _byCell = new();
        private readonly Dictionary<AirLockOpener, Vector2Int> _openerCells = new();
        private readonly HashSet<AirLockOpener> _visitedScratch = new();
        private readonly List<AirLockOpener> _nearbyScratch = new();

        public static AirLockProximityService Instance
        {
            get
            {
                if (_instance == null)
                    EnsureExists();

                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (_instance != null)
                return;

            var go = new GameObject(nameof(AirLockProximityService));
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<AirLockProximityService>();
        }

        public void Register(AirLockOpener opener)
        {
            if (opener == null || _openerCells.ContainsKey(opener))
                return;

            Vector2Int cell = TileObserverConstants.GetHashGridCell(opener.transform.position);
            _openerCells[opener] = cell;
            if (!_byCell.TryGetValue(cell, out List<AirLockOpener> list))
            {
                list = new List<AirLockOpener>(4);
                _byCell[cell] = list;
            }

            list.Add(opener);
        }

        public void Unregister(AirLockOpener opener)
        {
            if (opener == null || !_openerCells.TryGetValue(opener, out Vector2Int cell))
                return;

            _openerCells.Remove(opener);
            if (_byCell.TryGetValue(cell, out List<AirLockOpener> list))
            {
                list.Remove(opener);
                if (list.Count == 0)
                    _byCell.Remove(cell);
            }
        }

        /// <summary>EditMode / tests: doors currently indexed.</summary>
        public int RegisteredCount => _openerCells.Count;

        /// <summary>EditMode / tests: collect openers in the 3×3 HashGrid neighborhood of <paramref name="worldPosition"/>.</summary>
        public void CollectNearby(Vector3 worldPosition, List<AirLockOpener> results)
        {
            results.Clear();
            Vector2Int center = TileObserverConstants.GetHashGridCell(worldPosition);
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    Vector2Int cell = new Vector2Int(center.x + dx, center.y + dz);
                    if (!_byCell.TryGetValue(cell, out List<AirLockOpener> list))
                        continue;

                    for (int i = 0; i < list.Count; i++)
                    {
                        AirLockOpener opener = list[i];
                        if (opener != null)
                            results.Add(opener);
                    }
                }
            }
        }

        private void FixedUpdate()
        {
            if (!InstanceFinder.IsServerStarted || _openerCells.Count == 0)
                return;

            if (SubSystems.TryGet(out MapEditorSubSystem editor) && editor.IsActive)
                return;

            if (!SubSystems.TryGet(out EntitySubSystem entities))
                return;

            IReadOnlyList<Entity> players = entities.SpawnedPlayers;
            if (players.Count == 0)
            {
                // Still need powered/empty close logic for doors that were occupied — tick all
                // only when no players is rare; skip (doors stay as last state until a player exists).
                return;
            }

            _visitedScratch.Clear();

            for (int p = 0; p < players.Count; p++)
            {
                Entity entity = players[p];
                if (entity == null)
                    continue;

                CollectNearby(entity.transform.position, _nearbyScratch);
                for (int i = 0; i < _nearbyScratch.Count; i++)
                {
                    AirLockOpener opener = _nearbyScratch[i];
                    if (opener == null || !_visitedScratch.Add(opener))
                        continue;

                    opener.ServerUpdateProximityFromService();
                }
            }

            // Doors that had occupants but no longer have nearby players still need an empty tick
            // so close timers schedule. Walk openers that were not visited this frame only if they
            // report needing an empty pass.
            foreach (KeyValuePair<AirLockOpener, Vector2Int> pair in _openerCells)
            {
                AirLockOpener opener = pair.Key;
                if (opener == null || _visitedScratch.Contains(opener))
                    continue;

                if (opener.NeedsEmptyProximityPass)
                    opener.ServerUpdateProximityFromService();
            }
        }
    }
}

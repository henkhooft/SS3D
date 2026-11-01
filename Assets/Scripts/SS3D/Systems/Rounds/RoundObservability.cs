using System;
using System.Globalization;
using FishNet.Connection;
using FishNet.Object;
using SS3D.Core;
using SS3D.Data.Management;
using SS3D.Data.Persistence;
using SS3D.Logging;
using SS3D.Systems.Comms;
using SS3D.Systems.Entities;
using SS3D.Systems.Entities.Humanoid;
using SS3D.Systems.Inventory.Items;
using SS3D.Systems.Tile;
using UnityEngine;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;

namespace SS3D.Systems.Rounds
{
    /// <summary>One line of <c>reset-health.jsonl</c>: what was left after a round's station was unloaded.</summary>
    [Serializable]
    public sealed class ResetHealthEntry
    {
        public string timestamp;
        public int roundId;
        public int players;
        public int networkObjects;
        public int gameObjects;
        public long managedHeapBytes;
        public long totalAllocatedBytes;
        /// <summary>Placed tile objects still in the scene after unload; anything above 0 is a leak.</summary>
        public int tileObjects;
        /// <summary>Items still in the scene after unload; anything above 0 is a leak.</summary>
        public int items;
    }

    /// <summary>One line of <c>bugs.jsonl</c>, written by the <c>/bug</c> chat command.</summary>
    [Serializable]
    public sealed class BugReportEntry
    {
        public string timestamp;
        public int roundId;
        public string ckey;
        /// <summary>alive, ghost or lobby.</summary>
        public string state;
        public Vector3 position;
        public string note;
    }

    /// <summary>Parses the <c>/bug &lt;note&gt;</c> chat command.</summary>
    public static class BugCommand
    {
        public const string Usage = "Usage: /bug <what happened>";
        public const int MaxNoteLength = 500;

        /// <summary>True for any <c>/bug</c> message; <paramref name="note"/> is empty when none was given.</summary>
        public static bool TryParse(string text, out string note)
        {
            note = null;
            if (!CommsComposePrefix.TrySplit(text, out string token, out string body) || token != "bug")
                return false;

            note = body.Trim();
            if (note.Length > MaxNoteLength)
                note = note.Substring(0, MaxNoteLength);
            return true;
        }
    }

    /// <summary>
    /// Sandbox observability (<c>2026-11_sandbox-server.md</c> P4): a per-process round ID on the round log
    /// lines, a <c>reset-health.jsonl</c> line after every station unload, and <c>/bug</c> reports. Both
    /// files sit in <c>ServerMeta</c> beside the round history.
    /// </summary>
    public sealed partial class RoundSubSystem
    {
        private int _roundId;

        /// <summary>Counts round starts in this server process; 0 before the first. Server only.</summary>
        public int RoundId => _roundId;

        [Server]
        private void BeginRoundId()
        {
            _roundId++;
            Log.Information(this, "Round {roundId} preparing on {map}", Logs.ServerOnly, _roundId, _serverSettings.Map);
        }

        /// <summary>Writes the reset-health line once the station is unloaded and the crew despawned.</summary>
        [Server]
        private void AppendResetHealth()
        {
            ResetHealthEntry entry = new()
            {
                timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                roundId = _roundId,
                players = ServerManager != null ? ServerManager.Clients.Count : 0,
                networkObjects = ServerManager != null ? ServerManager.Objects.Spawned.Count : 0,
                gameObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
                managedHeapBytes = GC.GetTotalMemory(false),
                totalAllocatedBytes = Profiler.GetTotalAllocatedMemoryLong(),
                tileObjects = Object.FindObjectsByType<PlacedTileObject>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
                items = Object.FindObjectsByType<Item>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
            };
            if (!LocalStorage.AppendJsonlLine(PersistencePaths.ResetHealth, JsonUtility.ToJson(entry)))
                Log.Warning(this, "Could not write reset-health", Logs.ServerOnly);
            Log.Information(this,
                "Round {roundId} reset health: {networkObjects} network objects, {gameObjects} game objects, {tileObjects} tile objects, {items} items, {heap} B heap",
                Logs.ServerOnly, entry.roundId, entry.networkObjects, entry.gameObjects, entry.tileObjects, entry.items, entry.managedHeapBytes);
        }

        /// <summary>Server: records a <c>/bug</c> report and returns the confirmation for the player.</summary>
        [Server]
        public string RecordBugReport(NetworkConnection conn, string ckey, string note)
        {
            if (string.IsNullOrWhiteSpace(note))
                return BugCommand.Usage;

            string state = "lobby";
            Vector3 position = Vector3.zero;
            if (CurrentRoundState == RoundState.Ongoing && SubSystems.TryGet(out EntitySubSystem entities)
                && entities.TryGetSpawnedEntity(conn, out Entity entity))
            {
                state = entity.GetComponent<HumanoidGhostController>() != null ? "ghost" : "alive";
                position = entity.transform.position;
            }

            BugReportEntry entry = new()
            {
                timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                roundId = _roundId,
                ckey = ckey,
                state = state,
                position = position,
                note = note,
            };
            bool saved = LocalStorage.AppendJsonlLine(PersistencePaths.BugReports, JsonUtility.ToJson(entry));
            Log.Information(this, "Bug report from {ckey} in round {roundId}: {note}", Logs.ServerOnly, ckey, _roundId, note);
            return saved
                ? "Bug report saved. Thanks!"
                : "Your bug report reached the server log but could not be saved to file.";
        }
    }
}

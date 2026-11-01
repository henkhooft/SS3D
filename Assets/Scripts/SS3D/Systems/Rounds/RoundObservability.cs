using System;
using System.Globalization;
using System.IO;
using FishNet;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using FishNet.Object;
using SS3D.Core;
using SS3D.Data;
using SS3D.Data.Management;
using SS3D.Data.Persistence;
using SS3D.Logging;
using SS3D.Systems.BugReports;
using SS3D.Systems.PlayerControl;
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

    /// <summary>
    /// One line of <c>bugs.jsonl</c>, written by <c>/bug &lt;note&gt;</c> (<see cref="source"/> "chat") or the bug
    /// report dialog ("dialog"). The ckey, round, state and position are the server's view; <see cref="client"/>
    /// is what the player's game saw when the dialog opened.
    /// </summary>
    [Serializable]
    public sealed class BugReportEntry
    {
        public string timestamp;
        public int roundId;
        public string ckey;
        /// <summary>alive, ghost or lobby.</summary>
        public string state;
        public Vector3 position;
        /// <summary>The chat note, or the dialog's summary.</summary>
        public string note;
        public string source;
        public string details;
        /// <summary>"", minor, major or blocker.</summary>
        public string severity;
        public bool crashed;
        /// <summary>File name under <c>ServerMeta/bug-screenshots/</c>, or empty.</summary>
        public string screenshot;
        public BugReportClientInfo client;
        public string[] logTail;
    }

    /// <summary>What happened to a report; <see cref="Message"/> is shown to the player.</summary>
    public readonly struct BugReportOutcome
    {
        public readonly bool Saved;
        public readonly float RetryAfterSeconds;
        public readonly string Message;

        public BugReportOutcome(bool saved, float retryAfterSeconds, string message)
        {
            Saved = saved;
            RetryAfterSeconds = retryAfterSeconds;
            Message = message;
        }
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
        /// <summary>Synced so a client's bug report names the round its player saw.</summary>
        [SyncVar] private int _roundId;

        private readonly BugReportRateLimiter _bugRateLimiter = new();

        /// <summary>Counts round starts in this server process; 0 before the first. Replicated to clients.</summary>
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

        /// <summary>Server: records a <c>/bug &lt;note&gt;</c> report and returns the confirmation for the player.</summary>
        [Server]
        public string RecordBugReport(NetworkConnection conn, string ckey, string note)
        {
            if (string.IsNullOrWhiteSpace(note))
                return BugCommand.Usage;

            BugReportEntry entry = new() { note = note, source = "chat" };
            return RecordBugReport(conn, ckey, entry, null).Message;
        }

        /// <summary>
        /// Server: stamps the server's view (time, round, ckey, state, position) on <paramref name="entry"/>,
        /// enforces one report per player per minute, saves the screenshot and appends the line.
        /// </summary>
        [Server]
        public BugReportOutcome RecordBugReport(NetworkConnection conn, string ckey, BugReportEntry entry, byte[] screenshotJpg)
        {
            if (!_bugRateLimiter.TryConsume(ckey, Time.unscaledTime, out float retryAfter))
            {
                return new BugReportOutcome(false, retryAfter,
                    $"You can send another report in {BugReportText.FormatCountdown(retryAfter)}.");
            }

            string state = "lobby";
            Vector3 position = Vector3.zero;
            if (CurrentRoundState == RoundState.Ongoing && SubSystems.TryGet(out EntitySubSystem entities)
                && entities.TryGetSpawnedEntity(conn, out Entity entity))
            {
                state = entity.GetComponent<HumanoidGhostController>() != null ? "ghost" : "alive";
                position = entity.transform.position;
            }

            DateTime now = DateTime.UtcNow;
            entry.timestamp = now.ToString("o", CultureInfo.InvariantCulture);
            entry.roundId = _roundId;
            entry.ckey = ckey;
            entry.state = state;
            entry.position = position;
            entry.screenshot = SaveBugScreenshot(screenshotJpg, ckey, now);

            bool saved = LocalStorage.AppendJsonlLine(PersistencePaths.BugReports, JsonUtility.ToJson(entry));
            Log.Information(this, "Bug report ({source}, {severity}) from {ckey} in round {roundId}: {note}", Logs.ServerOnly,
                entry.source, string.IsNullOrEmpty(entry.severity) ? "untriaged" : entry.severity, ckey, _roundId, entry.note);
            return saved
                ? new BugReportOutcome(true, BugReportRateLimiter.IntervalSeconds, $"Bug report saved (round {_roundId}). Thanks!")
                : new BugReportOutcome(false, 0f, "Your bug report reached the server log but could not be saved to file.");
        }

        /// <summary>Writes the dialog's screenshot beside <c>bugs.jsonl</c>; returns its file name, or empty.</summary>
        private string SaveBugScreenshot(byte[] jpg, string ckey, DateTime utc)
        {
            if (jpg == null || jpg.Length == 0 || jpg.Length > BugReportText.MaxScreenshotBytes)
                return "";

            string name = $"{_roundId}-{utc:yyyyMMdd-HHmmss}-{SafeName(ckey)}.jpg";
            try
            {
                string folder = Path.GetFullPath(Paths.GetPath(GamePaths.Data, true) + PersistencePaths.BugScreenshots);
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, name), jpg);
                return name;
            }
            catch (Exception exception)
            {
                Log.Warning(this, "Could not save bug screenshot: {error}", Logs.ServerOnly, exception.Message);
                return "";
            }
        }

        private static string SafeName(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "unknown";
            foreach (char c in Path.GetInvalidFileNameChars())
                text = text.Replace(c, '_');
            return text;
        }

        /// <summary>Called from <c>OnStartServer</c> (ServerInfo.cs).</summary>
        [Server]
        private void RegisterBugReportBroadcasts()
        {
            ServerManager.RegisterBroadcast<BugReportSubmitMessage>(OnServerReceiveBugReport);
        }

        /// <summary>Called from <c>OnStopServer</c> (MapEditorRoundStop.cs).</summary>
        private void UnregisterBugReportBroadcasts()
        {
            if (ServerManager != null)
                ServerManager.UnregisterBroadcast<BugReportSubmitMessage>(OnServerReceiveBugReport);
        }

        /// <summary>Server: a report from the dialog. The client's text is clamped again; its context is kept as sent.</summary>
        private void OnServerReceiveBugReport(NetworkConnection conn, BugReportSubmitMessage message)
        {
            string ckey = SubSystems.TryGet(out PlayerSubSystem players) ? players.GetCkey(conn) : null;
            if (string.IsNullOrEmpty(ckey))
                return;

            string summary = BugReportText.Clamp(message.Summary, BugReportText.MaxSummaryLength);
            BugReportOutcome outcome;
            if (summary.Length == 0)
            {
                outcome = new BugReportOutcome(false, 0f, "A bug report needs a summary.");
            }
            else
            {
                BugReportEntry entry = new()
                {
                    note = summary,
                    source = "dialog",
                    details = BugReportText.Clamp(message.Details, BugReportText.MaxDetailsLength),
                    severity = BugReportText.SeverityKey(BugReportText.ParseSeverity(message.Severity)),
                    crashed = message.Crashed,
                    client = message.Client,
                    logTail = BugReportText.ClampLog(message.LogTail),
                };
                outcome = RecordBugReport(conn, ckey, entry, message.ScreenshotJpg);
            }

            InstanceFinder.ServerManager.Broadcast(conn, new BugReportResultMessage
            {
                Saved = outcome.Saved,
                RoundId = _roundId,
                RetryAfterSeconds = outcome.RetryAfterSeconds,
                Message = outcome.Message,
            });
        }
    }
}

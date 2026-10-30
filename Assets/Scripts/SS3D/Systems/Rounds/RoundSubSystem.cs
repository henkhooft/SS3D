using System.Threading;
using Cysharp.Threading.Tasks;
using FishNet.Object;
using SS3D.Core;
using SS3D.Core.WorldReadiness;
using SS3D.Logging;
using SS3D.Systems.Comms;
using SS3D.Systems.Gamemodes;
using SS3D.Systems.Persistence;
using SS3D.Systems.Rounds.Messages;
using SS3D.Systems.WorldReadiness;
using System;
using System.Linq;

namespace SS3D.Systems.Rounds
{
    /// <summary>
    /// Round system base implementation for basic round functionality
    /// </summary>
    public sealed partial class RoundSubSystem : RoundSubSystemBase
    {
        /// <summary>
        /// Round loop runner
        /// </summary>
        [Server]
        protected override async UniTask ProcessChangeRoundState(ChangeRoundStateMessage m, CancellationToken cancellationToken)
        {
            if (!IsServer)
            {
                return;
            }

            if (m.State)
            {
                await StartRoundSequence(cancellationToken);
            }
            else
            {
                await StopRoundSequence(cancellationToken);
            }
        }

        [Server]
        private async UniTask StartRoundSequence(CancellationToken cancellationToken)
        {
            _serverSettingsError = "";
            PersistenceSubSystem persistence = SubSystems.Get<PersistenceSubSystem>();
            if (string.IsNullOrEmpty(_serverSettings.Map))
                _serverSettings.Map = DefaultStation(persistence.ListStationTemplates().ToArray());
            if (string.IsNullOrEmpty(_serverSettings.Map))
            {
                _serverSettingsError = "No saved maps available. Import or save a station before starting a round.";
                _stationLoadState = StationLoadState.Failed;
                return;
            }

            RoundState = RoundState.Preparing;
            RoundSeconds = 0;
            _stationName = _serverSettings.Map;
            _stationLoadState = StationLoadState.Loading;
            try
            {
                // Let the loading state reach the UI before disk parsing / object restoration.
                await UniTask.NextFrame(cancellationToken: cancellationToken);
                Tile.TileSubSystem tiles = null;
                await UniTask.WaitUntil(() => SubSystems.TryGet(out tiles) && tiles.CurrentMap != null
                    && tiles.Loader != null && tiles.Loader.IsInitialized, cancellationToken: cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                // Every start restores the selected template, including after a cancelled countdown.
                // A previous round's in-memory station is never an implicit selection.
                bool loaded = await persistence.LoadStationTemplateAsync(_serverSettings.Map);
                cancellationToken.ThrowIfCancellationRequested();
                if (!loaded)
                {
                    FailStationLoad("Station could not be loaded. Select another saved map.");
                    return;
                }
                tiles.SyncFloorDecalsToClients();
                _stationLoadState = StationLoadState.Initializing;
                await PrepareRound(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                _stationLoadState = StationLoadState.Ready;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                FailStationLoad("Station load failed. Select a map and try again.");
                Log.Error(this, "Station restore failed: {error}", Logs.ServerOnly, exception.Message);
                return;
            }

            await ProcessRoundTick(cancellationToken);
        }

        [Server]
        private void FailStationLoad(string message)
        {
            _serverSettingsError = message;
            _stationLoadState = StationLoadState.Failed;
            RoundState = RoundState.Stopped;
            RoundSeconds = 0;
        }

        [Server]
        private async UniTask StopRoundSequence(CancellationToken cancellationToken)
        {
            if (RoundState == RoundState.Stopped)
            {
                return;
            }

            if (RoundState != RoundState.Ending)
            {
                await ProcessEndRound(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }

            await StopRound(cancellationToken);
        }

        /// <summary>
        /// Prepares the round before starting — waits for world readiness, not a fixed delay.
        /// </summary>
        [Server]
        protected override async UniTask PrepareRound(CancellationToken cancellationToken)
        {
            Log.Information(this, "Preparing round", Logs.ServerOnly);

            RoundState = RoundState.Preparing;

            WorldReadinessSubSystem readiness = null;
            await UniTask.WaitUntil(
                () => SubSystems.TryGet(out readiness),
                cancellationToken: cancellationToken);

            await readiness.WaitUntilAsync(WorldReadyPhase.WorldReady, cancellationToken);
        }

        /// <summary>
        /// Process the round tick until the round ends
        /// </summary>
        [Server]
        protected override async UniTask ProcessRoundTick(CancellationToken cancellationToken)
        {
            Log.Information(this, "Starting {seconds} seconds warmup tick", Logs.ServerOnly, _warmupSeconds);

            RoundSeconds = _warmupSeconds;
            _forceStart = false;
            CancelTick();
            TickCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            System.TimeSpan second = System.TimeSpan.FromSeconds(1);

            RoundState = RoundState.WarmingUp;

            while (IsWarmingUp && RoundSeconds > 0 && !_forceStart)
            {
                await UniTask.Delay(second, cancellationToken: TickCancellationToken.Token);
                RoundSeconds--;
            }

            cancellationToken.ThrowIfCancellationRequested();

            RoundSeconds = 0;
            RoundState = RoundState.Ongoing;
            Log.Information(this, "Starting round tick", Logs.ServerOnly);
            SubSystems.Get<CommsSubSystem>()?.SendAnnouncement(
                "Welcome to the station crew, enjoy your stay.",
                CommsAudioTrackIds.StationWelcome);
            if (SubSystems.TryGet(out CommsSubSystem comms)) comms.PostOocSystem(OocChannel.Ooc, "The round has started.");

            while (IsOngoing)
            {
                await UniTask.Delay(second, cancellationToken: TickCancellationToken.Token);
                RoundSeconds++;
                if (RoundSeconds >= _serverSettings.RoundLengthMinutes * 60)
                {
                    RequestRoundEnd();
                    return;
                }
            }
        }

        [Server]
        protected override async UniTask ProcessEndRound(CancellationToken cancellationToken)
        {
            RoundState = RoundState.Ending;
            CancelTick();
            if (SubSystems.TryGet(out CommsSubSystem comms)) comms.PostOocSystem(OocChannel.Ooc, "The round has ended.");

            AppendRoundHistory();

            await UniTask.Delay(System.TimeSpan.FromSeconds(3), cancellationToken: cancellationToken);
        }

        [Server]
        private void AppendRoundHistory()
        {
            if (!SubSystems.TryGet(out PersistenceSubSystem persistenceSubSystem))
            {
                return;
            }

            string mapId = SubSystems.TryGet(out Tile.TileSubSystem tileSubSystem) && tileSubSystem.CurrentMap != null
                ? tileSubSystem.CurrentMap.gameObject.name
                : string.Empty;

            string gamemode = SubSystems.TryGet(out GamemodeSubSystem gamemodeSubSystem)
                ? gamemodeSubSystem.CurrentGamemodeName
                : "unknown";

            persistenceSubSystem.AppendRoundHistory(new RoundHistoryEntry
            {
                timestamp = DateTime.UtcNow.ToString("o"),
                gamemode = gamemode,
                mapId = mapId,
                playerCount = ServerManager?.Clients.Count ?? 0,
                durationSeconds = RoundSeconds,
                fallbackMap = true,
            });
        }

        [Server]
        protected override async UniTask StopRound(CancellationToken cancellationToken)
        {
            if (RoundState == RoundState.Stopped)
            {
                return;
            }

            RoundState = RoundState.Stopped;
            RoundSeconds = 0;

            await UniTask.Delay(System.TimeSpan.FromMilliseconds(500), cancellationToken: cancellationToken);
        }

#if UNITY_EDITOR
        /// <summary>
        /// This method facilitates automated testing, and is not to be used in production.
        /// It simulates a ChangeRoundStateMessage broadcast received from a client, and
        /// is handled normally by the server. Method required because the server cannot
        /// broadcast to itself. Note that authentication in RoundSystemBase has been
        /// bypassed by this method.
        /// </summary>
        /// <param name="m">The ChangeRoundStateMessage apparently broadcast</param>
        [Server]
        public void ChangeRoundStateMessageStubBroadcast(ChangeRoundStateMessage m)
        {
            RequestRoundStateChange(m);
        }
#endif
    }
}

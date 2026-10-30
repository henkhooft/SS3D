using System;
using System.Linq;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using SS3D.Core;
using SS3D.Permissions;
using SS3D.Systems.Persistence;
using SS3D.Systems.PlayerControl;
using SS3D.Systems.Rounds.Messages;

namespace SS3D.Systems.Rounds
{
    [Serializable]
    public struct ServerSettings
    {
        // Empty means no preference yet; the server selects an available saved template.
        public string Map;
        public string Mode;
        public int PlayerCap;
        public int RoundLengthMinutes;
        public bool AllowLateJoin;

        public static ServerSettings Default => new()
        {
            Map = "", Mode = "Secret", PlayerCap = 32, RoundLengthMinutes = 90, AllowLateJoin = true,
        };

        public bool IsValid => PlayerCap is >= 1 and <= 256
            && RoundLengthMinutes is >= 1 and <= 1440;
    }

    public enum StationLoadState { NotLoaded, Loading, Initializing, Ready, Failed }

    public enum ServerSetting { Map, Mode, PlayerCap, RoundLengthMinutes, AllowLateJoin }

    public enum RoundAdminAction { Start, ForceStart, ExtendTimer, CancelCountdown, Stop }

    public sealed partial class RoundSubSystem
    {
        private ServerSettings _serverSettings = ServerSettings.Default;
        private bool _forceStart;
        private string _serverSettingsError = "";
        // Only the public join policy is replicated; admin configuration stays behind a TargetRpc.
        [SyncVar] private bool _allowLateJoin = true;
        [SyncVar] private StationLoadState _stationLoadState;
        [SyncVar] private string _stationName = "";
        public StationLoadState CurrentStationLoadState => _stationLoadState;
        public string StationName => _stationName;
        public bool AllowLateJoin => _allowLateJoin;
        public int PlayerCap => _serverSettings.PlayerCap;
        public ServerSettings SettingsForServer => _serverSettings;
        public ServerSettings AdminSettings { get; private set; } = ServerSettings.Default;
        public string[] AdminMaps { get; private set; } = Array.Empty<string>();
        public string AdminSettingsStatus { get; private set; } = "Loading server settings…";
        public bool HasAdminSettings { get; private set; }

        private bool IsAdministrator(NetworkConnection sender)
        {
            return sender != null && SubSystems.TryGet(out PlayerSubSystem players)
                && SubSystems.TryGet(out PermissionSubSystem permissions)
                && permissions.IsAtLeast(players.GetCkey(sender), ServerRoleTypes.Administrator);
        }

        [ServerRpc(RequireOwnership = false)]
        public void CmdReadServerSettings(NetworkConnection sender = null)
        {
            if (IsAdministrator(sender)) SendServerSettings(sender, "");
        }

        [ServerRpc(RequireOwnership = false)]
        public void CmdSaveServerSettings(ServerSettings settings, NetworkConnection sender = null)
        {
            SaveServerSettings(settings, sender);
        }

        [ServerRpc(RequireOwnership = false)]
        public void CmdUpdateServerSetting(ServerSetting field, ServerSettings value, NetworkConnection sender = null)
        {
            if (!IsAdministrator(sender)) return;
            // Merge only the edited field with current authoritative data, never an old UI snapshot.
            ServerSettings updated = _serverSettings;
            switch (field)
            {
                case ServerSetting.Map: updated.Map = value.Map; break;
                case ServerSetting.Mode: updated.Mode = value.Mode; break;
                case ServerSetting.PlayerCap: updated.PlayerCap = value.PlayerCap; break;
                case ServerSetting.RoundLengthMinutes: updated.RoundLengthMinutes = value.RoundLengthMinutes; break;
                case ServerSetting.AllowLateJoin: updated.AllowLateJoin = value.AllowLateJoin; break;
                default: SendServerSettings(sender, "Unknown setting rejected."); return;
            }
            SaveServerSettings(updated, sender);
        }

        [Server]
        private void SaveServerSettings(ServerSettings settings, NetworkConnection sender)
        {
            if (!IsAdministrator(sender)) return;
            bool idle = CurrentRoundState is RoundState.Stopped or RoundState.Ended;
            string[] availableMaps = SubSystems.Get<PersistenceSubSystem>().ListStationTemplates().ToArray();
            if (string.IsNullOrEmpty(settings.Map)) settings.Map = DefaultStation(availableMaps);
            bool mapValid = availableMaps.Contains(settings.Map);
            if (!idle || !settings.IsValid || !mapValid
                || (settings.Mode != "Secret" && settings.Mode != "Extended" && settings.Mode != "Nuclear Emergency"))
            {
                SendServerSettings(sender, "Settings rejected. Edit valid values while the round is stopped.");
                return;
            }
            _serverSettingsError = "";
            _serverSettings = settings;
            _allowLateJoin = settings.AllowLateJoin;
            SendServerSettings(sender, "Settings saved for this server session.");
        }

        [ServerRpc(RequireOwnership = false)]
        public void CmdRoundAdminAction(RoundAdminAction action, NetworkConnection sender = null)
        {
            if (!IsAdministrator(sender)) return;
            switch (action)
            {
                case RoundAdminAction.Start when CurrentRoundState is RoundState.Stopped or RoundState.Ended:
                    RequestRoundStateChange(new ChangeRoundStateMessage(true));
                    break;
                case RoundAdminAction.ForceStart when IsWarmingUp:
                    _forceStart = true;
                    break;
                case RoundAdminAction.ExtendTimer when IsWarmingUp:
                    RoundSeconds = Math.Min(RoundSeconds + 60, 3600);
                    break;
                case RoundAdminAction.CancelCountdown when IsWarmingUp:
                    CancelCountdown();
                    break;
                case RoundAdminAction.Stop when IsOngoing:
                    RequestRoundEnd();
                    break;
            }
        }

        [Server]
        private void SendServerSettings(NetworkConnection sender, string status)
        {
            string[] maps = SubSystems.Get<PersistenceSubSystem>().ListStationTemplates().ToArray();
            if (string.IsNullOrEmpty(_serverSettings.Map)) _serverSettings.Map = DefaultStation(maps);
            TargetServerSettings(sender, _serverSettings, maps, string.IsNullOrEmpty(status) ? _serverSettingsError : status);
        }

        private static string DefaultStation(string[] maps) => maps.Contains("ministation")
            ? "ministation" : maps.FirstOrDefault() ?? "";

        [TargetRpc]
        private void TargetServerSettings(NetworkConnection target, ServerSettings settings, string[] maps, string status)
        {
            AdminSettings = settings;
            AdminMaps = maps;
            if (!string.IsNullOrEmpty(status) || !HasAdminSettings) AdminSettingsStatus = status;
            HasAdminSettings = true;
        }
    }
}

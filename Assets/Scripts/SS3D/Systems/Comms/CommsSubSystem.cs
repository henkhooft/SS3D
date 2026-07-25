using Coimbra;
using FishNet;
using FishNet.Connection;
using JetBrains.Annotations;
using SS3D.Core;
using SS3D.Core.Behaviours;
using SS3D.Logging;
using SS3D.Permissions;
using SS3D.Systems.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SS3D.Systems.Comms
{
    /// <summary>
    /// Hub for local speech (proximity ObserversRpc) and non-positional radio/announcements
    /// (global FishNet broadcast). See Documents/design/comms.md §3–§8.
    /// </summary>
    public class CommsSubSystem : NetworkSubSystem
    {
        private const int MaxMessageLength = 256;
        private const string CommsLogFolderName = "Comms";

        /// <summary>
        /// Raised on every client that receives a local speech line.
        /// </summary>
        public Action<Entity, SpeechEvent> OnLocalSpeechReceived;

        /// <summary>
        /// Raised on every client that receives a non-positional comms message (radio / announce).
        /// </summary>
        public Action<CommsMessage> OnCommsMessageReceived;

        private readonly Dictionary<string, CommsChannel> _channelsById = new();
        private string _commsLogPath;
        private CommsChannels _channelSettings;

        public CommsChannels ChannelSettings => _channelSettings;

        protected override void OnAwake()
        {
            base.OnAwake();
            EnsureChannelRegistry();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            _commsLogPath = $"{UnityEngine.Application.dataPath}/../Logs/{CommsLogFolderName}.txt";
            EnsureChannelRegistry();

            InstanceFinder.ClientManager.RegisterBroadcast<CommsMessage>(OnClientReceiveCommsMessage);
            InstanceFinder.ServerManager.RegisterBroadcast<CommsMessage>(OnServerReceiveCommsMessage);
        }

        private void EnsureChannelRegistry()
        {
            if (_channelSettings == null)
            {
                _channelSettings = ScriptableSettings.GetOrFind<CommsChannels>();
            }

            _channelsById.Clear();

            if (_channelSettings != null && _channelSettings.AllChannels != null)
            {
                foreach (CommsChannel channel in _channelSettings.AllChannels)
                {
                    if (channel == null)
                    {
                        continue;
                    }

                    _channelsById[channel.name] = channel;
                }
            }

            if (_channelsById.Count == 0)
            {
                RecoverChannelsFromLoadedAssets();
            }

            // Still empty (common right after Chat→Comms script swap in Editor) — load from disk.
#if UNITY_EDITOR
            if (_channelsById.Count == 0)
            {
                RecoverChannelsFromAssetDatabase();
            }
#endif

            if (_channelSettings != null && _channelSettings.AllChannels != null
                && _channelSettings.AllChannels.Count == 0 && _channelsById.Count > 0)
            {
                _channelSettings.AllChannels.AddRange(_channelsById.Values);
                if (_channelSettings.AnnouncementChannel == null
                    && _channelsById.TryGetValue("StationAlerts", out CommsChannel alerts))
                {
                    _channelSettings.AnnouncementChannel = alerts;
                }
            }
        }

        private void RecoverChannelsFromLoadedAssets()
        {
            foreach (CommsChannel channel in Resources.FindObjectsOfTypeAll<CommsChannel>())
            {
                if (channel == null || string.IsNullOrEmpty(channel.name))
                {
                    continue;
                }

                _channelsById[channel.name] = channel;
            }
        }

#if UNITY_EDITOR
        private void RecoverChannelsFromAssetDatabase()
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets(
                "t:CommsChannel", new[] { "Assets/Content/Data/Comms/Channels" });
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                CommsChannel channel = UnityEditor.AssetDatabase.LoadAssetAtPath<CommsChannel>(path);
                if (channel == null || string.IsNullOrEmpty(channel.name))
                {
                    continue;
                }

                _channelsById[channel.name] = channel;
            }
        }
#endif

        /// <summary>Writable radio channels for Tab compose (Local is not included).</summary>
        public List<CommsChannel> GetWritableRadioChannels()
        {
            EnsureChannelRegistry();
            List<CommsChannel> result = new();
            foreach (CommsChannel channel in _channelsById.Values)
            {
                if (channel.Kind == CommsChannelKind.Radio && !channel.CodeOnlyChannel)
                {
                    result.Add(channel);
                }
            }

            // If Kind/CodeOnly filters wiped everything (bad salvage data), still offer non-meta.
            if (result.Count == 0)
            {
                foreach (CommsChannel channel in _channelsById.Values)
                {
                    if (channel.Kind != CommsChannelKind.Announcement && !channel.CodeOnlyChannel)
                    {
                        result.Add(channel);
                    }
                }
            }

            result.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return result;
        }

        public override void OnStopNetwork()
        {
            if (InstanceFinder.ClientManager != null)
            {
                InstanceFinder.ClientManager.UnregisterBroadcast<CommsMessage>(OnClientReceiveCommsMessage);
            }

            if (InstanceFinder.ServerManager != null)
            {
                InstanceFinder.ServerManager.UnregisterBroadcast<CommsMessage>(OnServerReceiveCommsMessage);
            }

            base.OnStopNetwork();
        }

        public bool TryGetChannel(string id, out CommsChannel channel) =>
            _channelsById.TryGetValue(id, out channel);

        /// <summary>
        /// Server-only: validates and dispatches a local speech line from the given emitter.
        /// </summary>
        public void HandleSpeakRequest(LocalSpeechEmitter emitter, string text, SpeechMode mode)
        {
            if (!IsServer)
            {
                return;
            }

            if (!TryNormalizeText(text, out text))
            {
                return;
            }

            if (!Enum.IsDefined(typeof(SpeechMode), mode)
                || mode is SpeechMode.Radio or SpeechMode.Announcement)
            {
                mode = SpeechMode.Speak;
            }

            SpeechEvent speechEvent = new()
            {
                Text = text,
                Mode = mode,
                ServerTimestamp = Time.time,
            };

            emitter.ServerBroadcastSpeech(speechEvent);
        }

        /// <summary>
        /// Server-only: player radio send from an owned LocalSpeechEmitter.
        /// </summary>
        public void HandleRadioRequest(LocalSpeechEmitter emitter, string channelId, string text)
        {
            if (!IsServer || emitter == null)
            {
                return;
            }

            if (!TryNormalizeText(text, out text))
            {
                return;
            }

            if (!TryGetChannel(channelId, out CommsChannel channel)
                || channel.Kind != CommsChannelKind.Radio
                || channel.CodeOnlyChannel)
            {
                return;
            }

            Entity entity = emitter.GetComponent<Entity>();
            if (entity == null || entity.Mind == null || entity.Mind == Mind.Empty || entity.Mind.player == null)
            {
                return;
            }

            Player player = entity.Mind.player;
            if (channel.RoleRequiredToUse != ServerRoleTypes.None)
            {
                PermissionSubSystem permissionSystem = SubSystems.Get<PermissionSubSystem>();
                if (permissionSystem == null || !permissionSystem.IsAtLeast(player.Ckey, channel.RoleRequiredToUse))
                {
                    return;
                }
            }

            BroadcastCommsMessage(new CommsMessage
            {
                ChannelId = channel.name,
                Sender = player.Ckey,
                Text = text,
                Kind = CommsChannelKind.Radio,
            });
        }

        /// <summary>
        /// Server-only: station / code announcement (top banner).
        /// </summary>
        public void SendAnnouncement([NotNull] string text)
        {
            if (!IsServer)
            {
                return;
            }

            if (!TryNormalizeText(text, out text))
            {
                return;
            }

            CommsChannel channel = _channelSettings != null ? _channelSettings.AnnouncementChannel : null;
            string channelId = channel != null ? channel.name : "StationAlerts";

            BroadcastCommsMessage(new CommsMessage
            {
                ChannelId = channelId,
                Sender = "Server",
                Text = text,
                Kind = CommsChannelKind.Announcement,
            });
        }

        public void NotifyLocalSpeechReceived(Entity speaker, SpeechEvent speechEvent)
        {
            OnLocalSpeechReceived?.Invoke(speaker, speechEvent);
        }

        private void BroadcastCommsMessage(CommsMessage message)
        {
            if (!InstanceFinder.IsServer)
            {
                return;
            }

            AppendServerLog(message);
            InstanceFinder.ServerManager.Broadcast(message);
        }

        private void OnServerReceiveCommsMessage(NetworkConnection conn, CommsMessage msg)
        {
            // Clients must not forge global broadcasts — radio goes through LocalSpeechEmitter ServerRpc.
        }

        private void OnClientReceiveCommsMessage(CommsMessage message)
        {
            OnCommsMessageReceived?.Invoke(message);
        }

        private void AppendServerLog(CommsMessage msg)
        {
            try
            {
                using StreamWriter writer = new StreamWriter(_commsLogPath, true);
                writer.WriteLine($"[{msg.Kind}] [{msg.ChannelId}] [{msg.Sender}] {msg.Text}");
            }
            catch (Exception e)
            {
                Log.Information(typeof(CommsSubSystem), "Error writing comms log: {error}", Logs.ServerOnly, e.Message);
            }
        }

        private static bool TryNormalizeText(string text, out string normalized)
        {
            normalized = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            normalized = text.Replace("\r", string.Empty).Replace("\n", " ").Trim();
            if (normalized.Length == 0)
            {
                return false;
            }

            if (normalized.Length > MaxMessageLength)
            {
                normalized = normalized.Substring(0, MaxMessageLength);
            }

            return true;
        }
    }
}

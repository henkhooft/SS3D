using System.Collections.Generic;
using Coimbra.Services.Events;
using FishNet.Connection;
using FishNet.Object;
using SS3D.Core;
using SS3D.Core.Behaviours;
using SS3D.Core.Settings;
using SS3D.Logging;
using SS3D.Systems.Entities.Character.Messages;
using SS3D.Systems.PlayerControl;
using SS3D.Systems.PlayerControl.Events;
using UnityEngine;

namespace SS3D.Systems.Entities.Character
{
    /// <summary>
    /// Server-authoritative session store for character sheets. Clients submit via broadcast.
    /// </summary>
    public class CharacterSubSystem : NetworkSubSystem
    {
        [SerializeField] private AppearanceCatalog _appearanceCatalog;

        private readonly Dictionary<Player, CharacterSheet> _sheets = new();
        private CharacterSheet? _localDraft;
        private bool _localSheetSubmitted;

        public AppearanceCatalog Catalog => _appearanceCatalog;

        public CharacterSheet LocalDraft
        {
            get
            {
                if (_localDraft.HasValue)
                {
                    return _localDraft.Value;
                }

                return CharacterSheet.CreateDefault(LocalPlayer.Ckey);
            }
            set => _localDraft = value;
        }

        public bool HasLocalSheetSubmitted => _localSheetSubmitted;

        public override void OnStartServer()
        {
            base.OnStartServer();

            ServerManager.RegisterBroadcast<SubmitCharacterSheetMessage>(HandleSubmitCharacterSheet);
            AddHandle(OnlinePlayersChanged.AddListener(HandleOnlinePlayersChanged));
        }

        public bool TryGetSheet(Player player, out CharacterSheet sheet)
        {
            return _sheets.TryGetValue(player, out sheet);
        }

        [Server]
        public CharacterSheet GetOrDefault(Player player)
        {
            if (player != null && _sheets.TryGetValue(player, out CharacterSheet sheet))
            {
                return sheet.Validated(_appearanceCatalog);
            }

            string ckey = player != null ? player.Ckey : "Unnamed";
            return CharacterSheet.CreateDefault(ckey).Validated(_appearanceCatalog);
        }

        /// <summary>
        /// Client: ensure a sheet exists on the server before Ready. Uses local draft or defaults.
        /// </summary>
        [Client]
        public void EnsureLocalSheetSubmitted()
        {
            if (_localSheetSubmitted)
            {
                return;
            }

            SubmitLocalSheet(LocalDraft);
        }

        [Client]
        public void SubmitLocalSheet(CharacterSheet sheet)
        {
            string ckey = LocalPlayer.Ckey;
            if (string.IsNullOrEmpty(ckey))
            {
                Log.Error(this, "Cannot submit character sheet without a local ckey", Logs.ClientOnly);
                return;
            }

            CharacterSheet validated = sheet.Validated(_appearanceCatalog);
            _localDraft = validated;
            _localSheetSubmitted = true;

            SubmitCharacterSheetMessage message = new(ckey, validated);
            ClientManager.Broadcast(message);
        }

        [Server]
        private void HandleSubmitCharacterSheet(NetworkConnection sender, SubmitCharacterSheetMessage message)
        {
            PlayerSubSystem playerSystem = SubSystems.Get<PlayerSubSystem>();
            Player player = playerSystem.GetPlayer(message.Ckey);

            if (player == null)
            {
                Log.Warning(this, "Rejecting character sheet for unknown ckey {ckey}", Logs.ServerOnly, message.Ckey);
                return;
            }

            if (player.Owner != sender)
            {
                Log.Warning(this, "Rejecting character sheet: connection does not own ckey {ckey}", Logs.ServerOnly, message.Ckey);
                return;
            }

            CharacterSheet sheet = message.ToSheet().Validated(_appearanceCatalog);
            _sheets[player] = sheet;

            Log.Information(this, "Stored character sheet for {ckey}: {name}", Logs.ServerOnly, player.Ckey, sheet.Name);
        }

        [Server]
        private void HandleOnlinePlayersChanged(ref EventContext context, in OnlinePlayersChanged e)
        {
            if (!e.AsServer)
            {
                return;
            }

            if (e.ChangeType == ChangeType.Removal && e.ChangedPlayer != null)
            {
                _sheets.Remove(e.ChangedPlayer);
            }
        }

#if UNITY_EDITOR
        [Server]
        public void SubmitCharacterSheetMessageStubBroadcast(NetworkConnection sender, SubmitCharacterSheetMessage message)
        {
            HandleSubmitCharacterSheet(sender, message);
        }
#endif
    }
}

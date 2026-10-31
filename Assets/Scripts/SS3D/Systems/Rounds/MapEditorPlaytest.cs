using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using SS3D.Core;
using SS3D.Logging;
using SS3D.Systems.Persistence;
using SS3D.Systems.Rounds.Messages;
using SS3D.Systems.Tile;
using SS3D.Systems.Tile.MapEditor;

namespace SS3D.Systems.Rounds
{
    public enum MapStartChoice { Cancel, SaveAndStart, DiscardAndStart }

    public sealed partial class RoundSubSystem
    {
        private sealed class PendingMapStart
        {
            public int Token;
            public long Revision;
            public string SelectedMap;
            public string EditingMap;
        }

        private readonly Dictionary<NetworkConnection, PendingMapStart> _pendingMapStarts = new();
        private int _nextMapStartToken;
        private bool _approvedDiscard;
        private string _editorReturnTemplate;
        [SyncVar] private bool _restoringEditorMap;
        [SyncVar] private bool _editorRestoreFailed;

        public bool MapEditingAllowed => CurrentRoundState is RoundState.Stopped or RoundState.Ended
            && !_restoringEditorMap && !_editorRestoreFailed;
        public int MapStartToken { get; private set; }
        public string MapStartSelectedMap { get; private set; }
        public string MapStartEditingMap { get; private set; }
        public string MapStartError { get; private set; }

        protected override bool CanStartRound => !_restoringEditorMap
            && (!SubSystems.TryGet(out PersistenceSubSystem persistence) || !persistence.IsRestoringStationTemplate)
            && (_approvedDiscard || !SubSystems.TryGet(out MapEditorSubSystem editor) || !editor.HasUnsavedChanges);

        protected override void RequestPlayerRoundStart(NetworkConnection sender)
        {
            if (CurrentRoundState is not (RoundState.Stopped or RoundState.Ended) || _restoringEditorMap) return;
            if (SubSystems.TryGet(out MapEditorSubSystem editor) && editor.HasUnsavedChanges)
            {
                PendingMapStart pending = new()
                {
                    Token = ++_nextMapStartToken,
                    Revision = editor.EditRevision,
                    SelectedMap = _serverSettings.Map,
                    EditingMap = editor.EditingTemplateName,
                };
                _pendingMapStarts[sender] = pending;
                TargetMapStartPrompt(sender, pending.Token, pending.SelectedMap, pending.EditingMap, "");
                return;
            }
            RequestRoundStateChange(new ChangeRoundStateMessage(true));
        }

        [ServerRpc(RequireOwnership = false)]
        public void CmdResolveMapStart(int token, MapStartChoice choice, string saveName, NetworkConnection sender = null)
        {
            if (!IsAdministrator(sender) || !_pendingMapStarts.TryGetValue(sender, out PendingMapStart pending)
                || pending.Token != token) return;
            _pendingMapStarts.Remove(sender);
            TargetMapStartPrompt(sender, 0, "", "", "");
            if (choice == MapStartChoice.Cancel || _restoringEditorMap
                || CurrentRoundState is not (RoundState.Stopped or RoundState.Ended)) return;
            if (SubSystems.Get<PersistenceSubSystem>().IsRestoringStationTemplate)
            {
                ReshowMapStart(sender, pending, "Wait for the map to finish loading, then try again.");
                return;
            }
            if (!SubSystems.TryGet(out MapEditorSubSystem editor)) return;
            if (pending.Revision != editor.EditRevision || pending.SelectedMap != _serverSettings.Map
                || pending.EditingMap != editor.EditingTemplateName)
            {
                // Another admin edited / loaded / saved the map or changed the selection. Reconfirm current data.
                RequestPlayerRoundStart(sender);
                return;
            }
            if (choice == MapStartChoice.SaveAndStart)
            {
                // A known template is an explicit overwrite; a new map requires a fresh name.
                string name = string.IsNullOrEmpty(pending.EditingMap) ? saveName?.Trim() : pending.EditingMap;
                PersistenceSubSystem persistence = SubSystems.Get<PersistenceSubSystem>();
                if (string.IsNullOrEmpty(pending.EditingMap)
                    && (!ValidNewTemplateName(name) || persistence.ListStationTemplates().Contains(name)))
                {
                    ReshowMapStart(sender, pending, "Choose a new map name (letters, numbers, spaces, - or _). Existing maps cannot be overwritten here.");
                    return;
                }
                try
                {
                    if (!persistence.SaveStationTemplate(name, overwrite: !string.IsNullOrEmpty(pending.EditingMap)))
                    {
                        ReshowMapStart(sender, pending, "Save failed. Your edits are still here; retry or cancel.");
                        return;
                    }
                }
                catch (Exception exception)
                {
                    Log.Error(this, "Save before playtest failed: {error}", Logs.ServerOnly, exception.Message);
                    ReshowMapStart(sender, pending, "Save failed. Your edits are still here; retry or cancel.");
                    return;
                }
                // Starting a newly authored map should play that map, rather than the old default.
                if (string.IsNullOrEmpty(pending.EditingMap)) _serverSettings.Map = name;
            }
            else if (choice != MapStartChoice.DiscardAndStart) return;

            _approvedDiscard = true;
            try { RequestRoundStateChange(new ChangeRoundStateMessage(true)); }
            finally { _approvedDiscard = false; }
        }

        private static bool ValidNewTemplateName(string name) => !string.IsNullOrWhiteSpace(name)
            && name.Length <= 80 && name.All(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_');

        private void ReshowMapStart(NetworkConnection sender, PendingMapStart pending, string error)
        {
            _pendingMapStarts[sender] = pending;
            TargetMapStartPrompt(sender, pending.Token, pending.SelectedMap, pending.EditingMap, error);
        }

        [TargetRpc]
        private void TargetMapStartPrompt(NetworkConnection sender, int token, string selectedMap, string editingMap, string error)
        {
            MapStartToken = token;
            MapStartSelectedMap = selectedMap;
            MapStartEditingMap = editingMap;
            MapStartError = error;
        }

        private void RememberEditorReturnTemplate(string selectedMap)
        {
            foreach (NetworkConnection connection in _pendingMapStarts.Keys)
                if (connection.IsActive) TargetMapStartPrompt(connection, 0, "", "", "");
            _pendingMapStarts.Clear();
            _editorRestoreFailed = false;
            _editorReturnTemplate = SubSystems.TryGet(out MapEditorSubSystem editor)
                && !string.IsNullOrEmpty(editor.EditingTemplateName)
                && SubSystems.Get<PersistenceSubSystem>().ListStationTemplates().Contains(editor.EditingTemplateName)
                ? editor.EditingTemplateName : selectedMap;
        }

        public override void OnStopServer()
        {
            _pendingMapStarts.Clear();
            _editorReturnTemplate = null;
            _restoringEditorMap = false;
            _editorRestoreFailed = false;
            base.OnStopServer();
        }

        private void BeginEditorRestore()
        {
            _restoringEditorMap = true;
            _stationLoadState = StationLoadState.Loading;
        }

        private void RestoreEditorAfterCancelledStart()
        {
            BeginEditorRestore();
            RestoreEditorTemplate(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTask RestoreEditorTemplate(CancellationToken cancellationToken)
        {
            PersistenceSubSystem persistence = SubSystems.Get<PersistenceSubSystem>();
            try
            {
                // Cancelling a start doesn't interrupt the contributor restore halfway through.
                await UniTask.WaitUntil(() => !persistence.IsRestoringStationTemplate, cancellationToken: cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                bool loaded = !string.IsNullOrEmpty(_editorReturnTemplate)
                    && await persistence.LoadStationTemplateAsync(_editorReturnTemplate);
                cancellationToken.ThrowIfCancellationRequested();
                if (!loaded) throw new InvalidOperationException("Saved authoring template could not be restored.");
                if (SubSystems.TryGet(out TileSubSystem tiles)) tiles.SyncFloorDecalsToClients();
                _stationName = _editorReturnTemplate;
                _stationLoadState = StationLoadState.Ready;
                _editorRestoreFailed = false;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception)
            {
                _editorRestoreFailed = true;
                _stationLoadState = StationLoadState.Failed;
                _serverSettingsError = "Could not restore the saved authoring map. Editing and saving are locked. Select a saved map and start/stop to retry.";
                Log.Error(this, "Authoring template restore failed: {error}", Logs.ServerOnly, exception.Message);
            }
            finally { _restoringEditorMap = false; }
        }
    }
}

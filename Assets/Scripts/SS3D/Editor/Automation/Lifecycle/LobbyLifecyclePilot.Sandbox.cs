using System.Linq;
using SS3D.Core;
using SS3D.Systems.Rounds;
using SS3D.Systems.Tile;
using SS3D.Systems.Tile.MapEditor;
using SS3D.Systems.Tile.MapEditor.Commands;
using UnityEditor;
using UnityEngine.UIElements;

namespace SS3D.Editor.Automation
{
    /// <summary>
    /// Sandbox server pilot (<c>2026-11_sandbox-server.md</c>): the host applies the sandbox profile in
    /// Play Mode, as <c>-sandbox</c> does at boot, then lets the server run two rounds on its own with
    /// nobody pressing Ready or Start.
    /// </summary>
    public static partial class LobbyLifecyclePilot
    {
        private const string SandboxMode = "sandbox";

        private const string SandboxScope = "Single-editor Host session with the sandbox profile applied in Play Mode "
            + "(no -sandbox flag in the editor). Two automatic round cycles; nobody presses Ready or Start. "
            + "Does not cover a remote client, non-admin sandbox players or a dedicated server build.";

        [MenuItem("SS3D/Testing/Run Sandbox Pilot")]
        public static void RunSandbox() => Start(SandboxMode);

        private static bool AutosaveExists() =>
            SubSystems.Get<SS3D.Systems.Persistence.PersistenceSubSystem>().StationTemplateExists(_validationStation + RoundSubSystem.AutosaveSuffix);

        private static bool StationEmpty() => SubSystems.Get<TileSubSystem>().CurrentMap.ChunkCount == 0;

        /// <summary>Places an Assistant spawn on a plenum tile the template left empty, so its presence proves the edit.</summary>
        private static void EditEmptySpawnTile()
        {
            TileSubSystem tiles = SubSystems.Get<TileSubSystem>();
            _mapEditSpawnPosition = tiles.CurrentMap.GetAllChunks()
                .SelectMany(chunk => chunk.GetAllTilePlacedObjects())
                .Where(tile => tile.Layer == TileLayer.Plenum)
                .Select(tile => tile.transform.position)
                .First(position => !tiles.SpawnPoints.HasAt(position));
            SubSystems.Get<MapEditorSubSystem>().SubmitCommands(new MapEditorCommandDto[] { new MapEditorCommandDto
            {
                Kind = MapEditorCommandKind.PlaceSpawnPoint, AssetName = "spawn:job:Assistant",
                Position = _mapEditSpawnPosition, Direction = Direction.North,
            } });
        }

        private static string SidebarSub() => Shell()?.Q<Label>(className: "lobby-shell__countdown-sub")?.text;

        private static Step[] BuildSandboxSteps() => new Step[]
        {
            new Step
            {
                Name = "Lobby ready on host boot",
                Timeout = 180,
                Until = () => Online() && Local() != null && Rounds() != null && Visible(Shell())
                    && Rounds().CurrentRoundState == RoundState.Stopped,
                Verify = () =>
                {
                    Require(!Rounds().AutoStartEnabled && Rounds().AutoStartSecondsLeft == 0, "A normal server does not start rounds on its own");
                    CreateValidationStation(valid: true);
                },
            },
            new Step
            {
                Name = "Apply the sandbox profile with a two-minute first round",
                Enter = () =>
                {
                    ServerSettings settings = SandboxProfile.Apply(Rounds().SettingsForServer);
                    settings.Map = _validationStation;
                    settings.RoundLengthMinutes = 2;
                    Rounds().ApplyServerSettingsForTesting(settings);
                },
                Until = () => Rounds().AutoStartSecondsLeft > 0 && SidebarSub() == "Starting In",
                Verify = () =>
                {
                    Require(!IsReady(), "Countdown started without anyone pressing Ready");
                    Require(!MapEditorPermissions.PersistenceAllowed, "Sandbox profile turns map persistence off");
                    Require(Rounds().Motd == SandboxProfile.Motd, "MOTD carries the sandbox rules");
                    Capture("sandbox-autostart-countdown");
                },
            },
            new Step
            {
                Name = "First round starts and spawns the unready host",
                Timeout = 180,
                Until = () => Rounds().CurrentRoundState == RoundState.Ongoing && Spawned(),
                Verify = () =>
                {
                    Require(SubSystems.Get<SS3D.Systems.Gamemodes.GamemodeSubSystem>().CurrentGamemodeName == "Extended", "Sandbox rounds run Extended");
                    Require(Rounds().StationName == _validationStation, "Auto-start restored the selected station");
                },
            },
            new Step
            {
                Name = "Map edit during the sandbox round",
                Enter = EditEmptySpawnTile,
                Until = () => UnsavedMapEdits() && MapEditSpawnPresent(),
            },
            new Step
            {
                Name = "One-minute warning",
                Timeout = 90,
                Until = () => HostChatContains("The round ends in 1 minute."),
                Verify = () => Require(Rounds().CurrentRoundState == RoundState.Ongoing, "Warning arrives while the round runs"),
            },
            new Step
            {
                Name = "First round ends on its own and the station is wiped",
                Timeout = 90,
                Until = () => Rounds().CurrentRoundState == RoundState.Stopped && !Spawned() && StationEmpty(),
                Verify = () =>
                {
                    Require(!AutosaveExists(), "No round-end autosave with persistence off");
                    ServerSettings settings = Rounds().SettingsForServer;
                    settings.RoundLengthMinutes = 1;
                    Rounds().ApplyServerSettingsForTesting(settings);
                },
            },
            new Step
            {
                Name = "Second countdown starts without Ready",
                Timeout = 30,
                Until = () => Rounds().AutoStartSecondsLeft > 0,
                Verify = () => Require(!IsReady(), "Ready flags are not remembered across rounds"),
            },
            new Step
            {
                Name = "Second round starts on a fresh station",
                Timeout = 180,
                Until = () => Rounds().CurrentRoundState == RoundState.Ongoing && Spawned(),
                Verify = () =>
                {
                    Require(!MapEditSpawnPresent(), "Last round's edit is gone from the fresh station");
                    Require(!UnsavedMapEdits(), "Editor history starts clean");
                },
            },
            new Step
            {
                Name = "Second round ends on its own",
                Timeout = 120,
                Until = () => Rounds().CurrentRoundState == RoundState.Stopped && !Spawned() && StationEmpty(),
                Verify = () => Capture("sandbox-second-reset"),
            },
        };
    }
}

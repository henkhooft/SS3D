> Code paths: Assets/Scripts/SS3D/Systems/Rounds/, Assets/Scripts/SS3D/Systems/Lobby/, Assets/Scripts/SS3D/UI/Lobby/
> Entry points: RoundSubSystem, ReadyPlayersSubSystem, RoundSubSystemBase, LobbyUiSubSystem
> Status: partial
> Verified: Phase F legacy purge — 2026-10-30

# Rounds / lobby

## Overview

Round lifecycle state machine with single-flight `CancellationTokenSource` (prevents double start/stop and embark-during-ending races). States: `Stopped → Preparing → WarmingUp → Ongoing → Ending → Ended`. `PrepareRound` awaits `WorldReadinessSubSystem` `WorldReady` (not a fixed 500 ms delay).

**UITK lobby (in progress):** Phase A shell + Phase B Character Creator visual + Phase E1 live booth
preview from first lobby attachment on `UiLayer.Modal` — [2026-08_lobby-uitk-redesign](../2026-08_lobby-uitk-redesign.md).
Ready/late-join, replicated player readiness/counts, round timer/state, admin start/stop, spawn-based visibility and job/antagonist preferences (Phase D: live slots/demand, round-start resolution, latejoin) are wired. The saved character (name, body morphs, hair/beard + colors) is sent to the server and applied to the spawned body (E2 slice); species/skin/eyes/eyebrows and persistence remain pending. Host-mode lifecycle (ready/unready → start → spawn → stop → return, late join, local client disconnect/reconnect) is verified by the lifecycle pilot; a built non-admin client process joining the editor Host (permission gating, ready via UI, admin start/stop, late join) is verified by the remote client pilot, whose Ready click and chat typing are real OS input (xdotool). Other pilot steps remain synthetic. Player shell Settings now matches the reference controls with session-local values; runtime settings/input application remains unwired.

Rendered verification: [Unity MCP lobby pilot](../2026-10_unity-mcp-lobby-pilot.md) — player-tab/creator screenshots and UI assertions via MCP; expanded pilot passes 102 checks with no captured errors (player tabs, local Settings, default clothing, creator palettes/footer and character-slot restoration).

**Legacy uGUI purged (Phase F):** `Systems/Lobby/UI`, `LobbyCanvas`, the old Character Customizer canvas and the `GameScreens` system are deleted; the UITK shell is the only lobby UI. Round state machine was never condemned ([agent-first composition](../2026-07_agent-first-composition.md)).

## Start here

- `Assets/Scripts/SS3D/UI/Lobby/LobbyUiSubSystem.cs` — UITK shell + Character Creator host; attaches into Modal
- `Assets/Scripts/SS3D/UI/Lobby/LobbyShellView.cs` — Player View tabs/sidebar + local Display/Sound/Keyboard controls
- `Assets/Scripts/SS3D/UI/Lobby/CharacterCreatorView.cs` — guided-steps Character Creator, local character slots + pinned Save footer
- `Assets/Scripts/SS3D/UI/Lobby/CharacterCreatorDraft.cs` — local name/species/body/style/preferences + separate hair/beard/brow colors + stable outfit index
- `Assets/Scripts/SS3D/Systems/Entities/Character/CharacterAppearance.cs` — replicated character + server sanitization; `Human.cs` applies it, `Mind.DisplayName` is the in-world name
- `Assets/Scripts/SS3D/Systems/Entities/Character/CharacterPreviewBooth.cs` — off-map RT booth (layer 23, own lights), worn jumpsuit/shoe mesh application
- `Assets/Scripts/SS3D/UI/Lobby/LobbyAssetCatalog.cs` / `Assets/Scripts/SS3D/Editor/LobbyAssetCatalogBuilder.cs` — curated preview outfit references; umbrella catalog rebuild
- `Assets/Scripts/SS3D/Systems/Roles/RoleSubSystem.cs` / `JobResolver.cs` — replicated job slots, `JobPreferencesMessage` handling, round-start + latejoin assignment ([roles map](gamemodes-roles-traits.md))
- `Assets/Scripts/SS3D/Systems/Rounds/RoundSubSystem.cs` — concrete round subsystem; `PrepareRound` →
  `WaitUntilAsync(WorldReady)`; on Ongoing fires welcome via `CommsSubSystem.SendAnnouncement`
  (`StationWelcome` follow-up clip)
- `Assets/Scripts/SS3D/Systems/Rounds/RoundSubSystemBase.cs` — state machine base with generation-tracked CTS
- `Assets/Scripts/SS3D/Systems/Rounds/ReadyPlayersSubSystem.cs` — player ready tracking; spawns on `Ongoing`
- `Assets/Scripts/SS3D/Systems/WorldReadiness/WorldReadinessSubSystem.cs` — readiness gate for prepare

- `Assets/Scripts/SS3D/Editor/Automation/LobbyUiPilot.cs` — **SS3D → Testing → Run Lobby UI Pilot**, screenshot/check report under `artifacts/ui/`
- `Assets/Scripts/SS3D/Editor/Automation/Lifecycle/LobbyLifecyclePilot.cs` — **SS3D → Testing → Run Lobby Lifecycle Pilot**, host session lifecycle report under `artifacts/ui/lifecycle-*`; **Run Lobby Remote Client Pilot** launches `Builds/Game` as non-admin `remoteprobe` (report + client screenshots under `artifacts/ui/remote-*`); performs the client's `os_*` requests as real X11 input (xdotool)
- `Testing/multiplayer/scenarios/lobby-remote-client.txt` — the remote client's half (UI and `os_*` opcodes in `AutomationSubSystem`)
- `Testing/ui/validate.sh` — preflight → compile → EditMode → both pilots → stale-build rebuild → remote; `Testing/ui/README.md` — runbook

## Extension points

- Round transitions: extend `RoundSubSystemBase` state handlers and messages in `Messages/`.
- Spawn flow: `SpawnReadyPlayersEvent` after Ongoing (world must already be ready from PrepareRound).
- Lobby UITK: Session snapshots/actions live in `LobbyUiSubSystem` (including job preference sends); `LobbyShellView` renders `RoleSubSystem.Jobs`; Character Creator is a separate Modal screen (Phase B/E).

## Pitfalls

- **Legacy lobby purged:** the uGUI lobby is deleted (Phase F), not suppressed. Before that, the suppression keyed on a name and silently matched nothing because `Game.unity` renamed the `LobbyCanvas` instance to `Lobby`, so the old lobby stayed live under the UITK shell. The pilots now assert there is no root `Lobby`/`Character Customizer` canvas. `GamemodeObjectivesView` still lives on the `Networked View` prefab; don't delete that prefab as lobby debris.
- **Ready window:** the server accepts ready toggles in every state except Ongoing/Ending and spawns whoever is ready on Ongoing, so Ready stays enabled through Preparing/WarmingUp (countdown). Ongoing switches it to Join Round (late spawn RPC).
- **Pilot clicks:** a synthetic `ClickEvent` does not fire `Button.clicked` (its `Clickable` listens to pointer/submit). Drive buttons with `NavigationSubmitEvent`; keep `ClickEvent` for `RegisterCallback<ClickEvent>` surfaces such as the preview. UI snapshots lag subsystem state by the 200ms refresh — wait on the UI, not only the backend.
- **Playtest map:** editor boot loads `ApplicationSettings.EditorStationTemplate` (`ministation`) instead of the most recently saved template (Metastation, ~70s to WorldReady). Round start is then ~5s.
- **Session lifetime:** the lobby host survives network teardown. Read replicated subsystem snapshots with `TryGet`, hide both surfaces/booth offline or when the local player is spawned, and restore the saved draft on return.
- **Jobs prefs:** shell uses H/M/L/N chips (attached design), not drag-ranked lists (`lobby.md` §3) — recorded on the architecture effort. Jobs come only from `StartingRoles.asset`; a job needs a `RoleData` there to appear. Job resolution must run before `SpawnPlayer` (loadout reads the assignment); anything spawning players outside `SpawnReadyPlayers` falls through to latejoin assignment.
- **Preview booth:** live preview uses `CharacterPreviewBooth` on layer **23** with its own camera/lights/RT at (-250, -250, 250). Never share layer 22 or (-250, -250, -250) with `RuntimePreviewGenerator`: sharing put the dummy in every item icon — (the old `LobbyCameraRenderTexture`/`StaticWorldObjects` customizer was deleted in Phase F). Dispose the booth on host destroy; deactivate (don’t destroy) on Return to avoid respawn hitch.
- **E1 vs E2:** Save stores session-local character slots and sends the saved slot as `CharacterAppearanceMessage` (also once per connection); `EntitySubSystem` sanitizes/stores it per ckey and stamps `Mind.CharacterName` + `Human.SetAppearance` at spawn. Save never changes a living body. Species/skin/eye/brow rendering and disk persistence remain deferred.
- **Spawned height:** scale the armature (`HumanoidRigReferences.ArmatureRoot`), never the networked root (capsule, NetworkTransform). The server applies morphs too so bone hit colliders match. Clothing mesh swaps reset shape keys — `ClothesDisplayer` calls `Human.ReapplyBodyMorphs`.
- **Style indices:** hair/beard replicate as `LobbyAssetCatalog` indices resolved via `HumanoidStyleSource.Current` (registered by the lobby UI); a headless server spawns no style meshes, and mismatched client/server catalogs would show the wrong style. Body sliders map to Human.fbx blend shapes via `HumanoidMorphApplier` (Height is root scale — there is no Height shape key).

- **Preview stripping order:** remove combat/speech/movement dependents and the non-networked `HumanoidBodyStateBridge` before the generic network pass; prefab component order is not dependency order and Unity refuses required-component removal.
- **Preview orientation:** Human faces +Z at yaw 0 and the booth camera sits on +Z; a 180° base yaw silently swaps Front/Back. Keep base yaw at 0; verify the face in screenshots, not just the RenderTexture binding.

- **Creator colors:** lobby and creator are sibling Modal surfaces; variables declared only on `.lobby-shell` do not reach the creator. Scope shared lobby variables to both roots; reference text colors are overridden locally on `.char-creator`.
- **Character drafts:** live preview callbacks must not mutate the saved draft; commit on Save only, or switching/New Character silently overwrites the saved body/style. The loadout popup is the window's last absolute child so the step form cannot paint over it.

- **Slider fill:** Unity 6's Slider has no `unity-base-slider__fill` element; styling that selector silently leaves an empty track. Add a non-picking fill under the tracker and update its percentage width with the value.

- **Linux rounded inputs/images:** rounded borders on the settings dropdown and sprite preview rendered solid white in the tested editor. Keep those surfaces square; screenshot review is necessary even when all UI assertions pass.
- **Settings toggles:** use `Toggle.text` for check-before-label layout; forcing its input to `flex-grow: 0` collapses the text. Keep the input growing with an automatic basis.
- **Settings tests:** a `ScrollView` contains internal `Slider`s for its scrollers. Query `lobby-shell__settings-slider` when counting or checking enabled volume controls.

- **Startup preview goes black:** `AreaSubSystem.CompleteSetup` runs the orphan-light guard after an early booth is created. The guard must exempt `CharacterPreviewBooth`-owned lights; delaying the booth until creator open only hides the ownership bug.

- **Preview clothes:** use `Cloth.GetClothMesh` on catalog prefab assets and activate the dummy's existing Jumpsuit/ShoeLeft/ShoeRight renderers. Do not instantiate folded Item prefabs or route offline preview through server-only `ClothesDisplayer`. Pick an outfit once per draft, not on each appearance callback.
- **Remote client booth threw in `HumanoidController.Setup`:** instantiating the active Human prefab ran every gameplay `Awake` before stripping; on a remote client the booth is built before `CameraSubSystem` exists. The dummy is now instantiated under an inactive staging parent and stripped with `DestroyImmediate` before activation. Host-only pilots never saw this — run the remote pilot after booth changes.
- **Disabled lobby buttons looked clickable:** the fill-colour button rules had no `:disabled` state, so a non-admin's Admin (and unwired Observe) rendered like live buttons; `ui_wait … disabled` still passed. `.lobby-shell__btn:disabled` dims them. UI renders in linear colour space, so opacity 0.35 reads closer to 60% — judge the screenshot, not the number.
- **Chat Return ignored in real play:** a real Return keydown targets the `TextField`'s inner text element, which consumes it before it bubbles; the composer's bubble-phase `KeyDownEvent` handler never ran, while synthetic events sent to the field itself passed. Register Return/submit handlers on text fields with `TrickleDown.TrickleDown`; found by the remote pilot's real keyboard input.
- **Catalog rebuild over MCP:** SS3D → Data → Rebuild All UI Catalogs ends in a modal "UI Catalogs" OK dialog that blocks MCP and ignores xdotool under XWayland; prefer `LobbyAssetCatalogBuilder.RebuildCatalogBatch` via `execute_code`.
- **Players persist offline:** `PlayerSubSystem.GetPlayer(ckey)` still returns a disconnected player. Use the online count or the connection, not a null record, to detect a remote leaving.

## Depends on / Used by

- **Depends on:** [entities](entities.md), [player-control](player-control.md),
  [gamemodes-roles-traits](gamemodes-roles-traits.md), [persistence](persistence.md),
  [chat-audio-screens](chat-audio-screens.md) (round-start announce), [ui-shell](ui-shell.md),
  world readiness (tile/area/electricity/atmos/disposal)
- **Used by:** All in-round gameplay

## Related docs

- Design (read-only): [Documents/design/lobby.md](../../design/lobby.md), [round-config.md](../../design/round-config.md)
- Effort: [2026-08_lobby-uitk-redesign](../2026-08_lobby-uitk-redesign.md)
- [2026-07_session-world-lifecycle](../2026-07_session-world-lifecycle.md)
- [2026-07_comms-non-diegetic-feed](../2026-07_comms-non-diegetic-feed.md)
- [INDEX.md](../INDEX.md)

- **Preview framing:** authored skinned-renderer bounds include animation padding; empty equipment slots can report large bounds despite having no mesh. Fit the shared lobby/creator camera to baked visible geometry after evaluating the initial pose, skipping inactive renderers and null meshes. Keep framing fixed during morph edits so Height remains visible; padding leaves room for shoes and walking.

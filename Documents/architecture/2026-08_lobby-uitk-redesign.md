> Implements: Documents/design/lobby.md
> Touches systems: rounds-lobby, ui-shell, electricity (preview light ownership), inventory (read-only worn-mesh sources)
> Status: in-progress (Phase A/B visual + Phase D jobs + Phase E1 live preview/draft; C verified; E2 networked name/body/hair shipped, species/skin/persistence pending; F pending)

# Lobby UITK redesign

Replace the condemned uGUI lobby with a UiShell Modal UITK Lobby Shell matching the attached
pre-round lobby design. Character Creator is a **separate** modal screen opened from the lobby
character preview.

## Phases

| Phase | Scope | Status |
|---|---|---|
| A | Lobby Shell layout + USS + mock data; catalog; preview placeholder click stub | **shipped** |
| B | Character Creator separate screen (guided-steps visual) | **shipped** (visual) |
| C | Wire Ready / round state / admin start-stop / player list / show-hide | implemented; host lifecycle + remote non-admin client verified (Ready click and chat typing as real OS input; other steps synthetic) |
| D | Jobs H/M/L/N + antag prefs sync + resolution | **shipped** (round-start pass + latejoin; antag opt-ins stored, no gamemode consumer yet) |
| E1 | Live booth preview + local Save/Return draft (name) | **shipped** |
| E2 | Appearance apply/sync (hair/skin/catalog, network, spawn) | **partial** — name, body morphs, hair/beard + colors sync to the spawned body; species/skin/eyes/eyebrows/persistence pending |
| F | Purge condemned LobbyCanvas / uGUI views; docs finalize | pending |

## Design divergences (do not edit design docs)

- Jobs UI uses **H/M/L/N priority chips** from the attached design, not `lobby.md` §3 drag-ranked list.
- Character Creator follows design option **1A Guided Steps** (Identity → Body → Style → Review).
- Character Creator is in scope for this effort (separate screen); `lobby.md` §6/§11 name-only identity is deferred only until Phase E2 lands.

## Entry points

- `Assets/Scripts/SS3D/UI/Lobby/LobbyUiSubSystem.cs` — DDOL bootstrap; shell + creator + booth lifecycle
- `Assets/Scripts/SS3D/UI/Lobby/LobbyShellView.cs` — full shell chrome
- `Assets/Scripts/SS3D/UI/Lobby/CharacterCreatorView.cs` — guided-steps Character Creator
- `Assets/Scripts/SS3D/Systems/Entities/Character/CharacterPreviewBooth.cs` — off-map RT booth (layer 22)
- `Assets/Scripts/SS3D/Systems/Entities/Character/HumanoidMorphApplier.cs` — Human.fbx body blend shapes (Female/Fat/Muscle/…)
- `Assets/Content/Systems/UI/Lobby/LobbyShell.uss` (+ `@import CharacterCreator.uss`) + `Resources/LobbyAssetCatalog.asset`
- Rebuild: **SS3D → Data → Rebuild All UI Catalogs**

## 2026-10-30 Character Creator refresh

Source: `SS3D Pre-round Lobby UI(1).zip`, `Character Creator - Options.dc.html`,
active option **1A Guided Steps**. The archive's older, removed lobby Character tab
is not the target. Implemented Species dropdown, removal of Uniform Color,
1360×820 maximum window with a 400px preview dock, independent step scrolling,
and one pinned footer action (Next → Save Character → Saved). Creator-local colors
and typography follow the supplied reference; shared lobby variables apply to both
sibling surfaces. Hair/beard labels strip internal prefab prefixes.

The owner explicitly chose to keep the six model-supported body controls
(Height/Female/Fat/Muscle/Chest/Jaw) rather than approximate the reference's
Belly/Upper Body/Lower Body/Waist/Skin Tone controls. Body numeric values use 0–1.
Species, eyes and eyebrow selections are local draft preferences; only the Human
model, body morphs, and catalog hair/beard/color are applied in the live booth.
The follow-up `SS3D Pre-round Lobby UI(2).zip` moves Hair Color off Body and places
a context-labelled palette below the Style tiles: Hair Color, Facial Hair Color,
and Eyebrow Color. Each color is retained independently in the session-local draft;
hair and beard use independent live tints. Eyebrow color is stored pending its mesh.

Species models, skin/eye tint, eyebrow meshes, disk persistence and networked
appearance remain deferred E2 work.

Local character slots now retain saved name/body/style/preferences, switching
restores the selected slot, New Character resets the form, and Return restores the
last explicitly saved draft. These slots live only for the current UI session.

## 2026-10-30 Player View shell refresh

Source: `SS3D Pre-round Lobby UI(2).zip`, `Lobby Shell - Player View.dc.html` plus
its bundled Button/ToggleButton and color/type tokens. Player tabs remain Server
Info, Jobs, Chat, Players and Settings; the removed Character tab stays removed.

- Shell uses 70% width clamped to 900–1180px, a 280px sidebar, 24px content padding,
  reference text/button colors (height subsequently increased by owner request below). Ready/Observe/Admin
  use bold titling rather than arcade glyphs. Sidebar starts in Waiting For Players,
  with the ready ratio beneath; its preview remains clickable to open the creator.
- Server Info uses a contained banner in a 21:9 well, an inset information card,
  220px Change Log column (outer scrolling subsequently removed by owner request below). Jobs uses compact names, a separate
  slot count, contiguous 20px priority segments and visible disabled locked-job controls.
- Settings has underlined Display/Sound/Keyboard subtabs and 420px forms. Display:
  resolution dropdown, Fullscreen checkbox, UI Scale and Brightness blue sliders with
  0–1 readouts. Sound: Mute All Audio checkbox and three percentage volume sliders;
  muting dims/disables the sliders. Keyboard: eight action rows, binding buttons,
  local next-key capture and Escape cancellation. Values survive subtab changes.

**Scope:** these are session-local UI interactions, matching the supplied prototype.
Resolution/fullscreen, UI scale/brightness, audio mixer levels and input-action
rebinding are not applied to the game or persisted. The reference keyboard labels
(including Tab inventory / T chat) are prototype data, not the fork's actual default
scheme; consult [inputs](systems/inputs.md) before runtime wiring. Ready/jobs/chat,
player actions, observer and admin round APIs retain their existing mock/stub status
(Phases C/D). Admin Server Settings is a later fidelity/wiring pass.

**Rendering constraint:** the tested Linux Unity path rendered the dropdown and
sprite preview white with rounded input/preview borders. Those two surfaces use
square corners to preserve their actual content; solid-color action buttons keep
3px radii. No design spec or prefab asset was changed.

## Lobby layout/startup follow-up — 2026-10-30

Owner requested a scroll-free Server Info pane, a live character from the first
lobby frame, and a gap above Ready. Shell height is now 840px (still capped at 92%
of the viewport). Server Info uses a plain card instead of a ScrollView; banner
height follows its actual sprite aspect and can shrink before the information
section. All four Change Log entries and Map/Mode remain visible at the tested size.
Ready keeps bottom alignment and 16px top padding.

`LobbyUiSubSystem` creates the booth during shell attachment, applies the current
draft and binds the RT immediately. `LobbyShellView` no longer substitutes a static
preview image. Early creation exposed `GameplayLightGuard` disabling booth lights
as orphans during `AreaSubSystem.CompleteSetup`; the guard now recognizes lights
owned by `CharacterPreviewBooth` independently of station `LightPower` fixtures.

## Default preview outfits — 2026-10-30

The booth now reads a curated list of existing clothing assets from
`LobbyAssetCatalog.PreviewOutfits`: civilian (grey + high-tops), engineering
(engineer + jackboots), botany (botany + high-tops), and security (security +
jackboots). The shared UI catalog builder supplies these references and checks
for a jumpsuit mesh and both shoe meshes. No clothing Item is instantiated or
network-spawned: `CharacterPreviewBooth.ApplyOutfit` applies the existing `Cloth`
mesh maps to the dummy's Jumpsuit/ShoeLeft/ShoeRight rig renderers, matching worn
presentation. Existing palette materials and animation bones are retained.

An outfit is chosen once per new session-local draft with an independent RNG and
stored as `OutfitIndex`. Body/style edits do not reroll it; Save, Return and character
switching restore that draft's selection. This is preview clothing, not authoritative
spawn equipment or job loadouts. No Human/clothing prefab assets or art imports
are changed.

Verified by run `20261030-144343-787`: 100 checks, zero captured errors, 14 PNGs.
Startup, creator and side/body-edit captures were inspected. The run checked active
jumpsuit/both-shoe renderers, unchanged outfit meshes during body edits, and restoration
of the same meshes after Save → other character → New Character → saved character.

## Visual QA

The [MCP lobby pilot](2026-10_unity-mcp-lobby-pilot.md) automates Identity/Body/Style/
Review, name/body edits, preview rotation, Save/Return, character switching and
New Character reset, plus all player tabs and Settings interactions. See [Testing/ui/README.md](../../Testing/ui/README.md)
for commands and evidence. It does not establish physical-input or multiplayer correctness.

Play Mode: click character preview → Character Creator with live humanoid RT; Body sliders drive
Human.fbx shape keys (Female/Fat/Muscle/Chest/Jaw) + height scale; Front/Side/Back rotates the
dummy; **Save Character** updates lobby name; **Return to Lobby** restores the shell with the saved
RT appearance. The runtime host suppresses the old `LobbyScreen` (the renamed `LobbyCanvas` instance); the smoke test checks that suppression.

## Related

- Design: [lobby.md](../design/lobby.md)
- Maps: [rounds-lobby](systems/rounds-lobby.md), [ui-shell](systems/ui-shell.md)

## Preview framing — 2026-10-30

The shared booth camera now fits baked visible geometry after evaluating the initial pose, ignoring inactive renderers and empty equipment slots. This removes inflated animation bounds from the fit and enlarges both lobby and creator previews. Framing remains fixed during morph edits, with padding for movement, shoes and height changes.

Run `20261030-150133-789` passed 102 checks with zero captured errors. Rendered character occupancy measured 84% of preview height, with head and feet inside the frame. Lobby, Identity and Body side-view screenshots were visually reviewed.

## Session wiring — 2026-10-30

`LobbyUiSubSystem` reads replicated player, ready, entity and round snapshots every
200ms. Ready broadcasts to the existing server API; Ongoing switches the action to
Join Round via the existing late-spawn RPC. The server resolves ready requests from
the sending connection instead of trusting a supplied ckey. The shell and creator
hide on spawn/disconnect, returning with the saved draft when back in the lobby.
The preview booth is deactivated while hidden. Headless/batch sessions skip UI bootstrap.

Players and ready counts are live, with unavailable ping/account fields shown as
unknown. Administrator permission gates the Admin button and start/stop requests;
the server retains its permission check. Mock force-start/extend-timer controls are
removed. Observe is disabled until an observer backend exists. Legacy LobbyCanvas
roots are disabled at runtime, including after scene reload; asset purge remains Phase F.

Lobby chat, settings application, server metadata/configuration
and networked/persisted character appearance remain subsequent implementation slices.
The existing visual pilot now checks production legacy suppression instead of doing
the suppression itself. Runtime/multiplayer verification is required before Phase C
can be called shipped.

Validation: Unity 6000.3.16f1 `-batchmode -nographics -quit` completed successfully
with the runtime/editor assemblies and enforced analyzers. `git diff --check` passed.
The graphical editor exited during display initialization, so the rendered pilot
and host/client ready → round → spawn → return flow were not rerun in this pass. See the lifecycle verification below.

## Lifecycle verification — 2026-10-30

New **SS3D → Testing → Run Lobby Lifecycle Pilot** (`Assets/Scripts/SS3D/Editor/Automation/Lifecycle/`, moved from `Assets/Editor/Automation/`,
own asmdef because it references SS3D runtime assemblies and therefore the enforced
analyzers). Run `lifecycle-20261030-153833-081` **passed**: 28 checks, zero captured errors,
72s on a single editor Host. Covered: legacy suppression, ready/unready counts and labels,
round start while the creator is open (both surfaces and the booth hide on spawn), stop →
shell (not creator) with booth/RT and saved name restored, Admin → Start Round with an
unready player (stays in lobby, Join Round enabled), late join spawn/hide, second stop/return,
local client disconnect (hidden, booth off) and reconnect 70s after server start (shell, booth,
player list, Ready restored). The existing visual pilot also passed (run `20261030-154104-676`).

Fixes found by the runs:

- Legacy suppression never matched: `Game.unity` names the `LobbyCanvas` instance `Lobby`.
  Suppression and the visual pilot now key on the `LobbyScreen` component.
- Ready was disabled during Preparing/WarmingUp although the backend accepts it then; it now
  stays enabled until Ongoing (Join Round) and is disabled only while Ending.
- FishNet (vendored) seeded a new connection's `PacketTick` with a rejected update, leaving
  `LocalTick` 0: any connection opened 60s+ after server start could be timed out before its
  first packet (host reconnect was kicked). One-line `SS3D patch` in `NetworkConnection.Initialize`.
- Editor playtests now boot `ApplicationSettings.EditorStationTemplate` (`ministation`), not the
  most recent Metastation template: first round start dropped from ~72s to ~5s.

Not covered: a separate client process (no current client build), non-admin permission
gating, OS input. The pilot reconnects the Host's local client, retrying like the recovery
overlay because a start issued while Tugboat is still stopping is dropped.

## Remote client verification — 2026-10-30

**SS3D → Testing → Run Lobby Remote Client Pilot** boots the editor Host, launches a fresh Linux
client build (`Builds/Game`, ckey `remoteprobe`, 1600×900 windowed) running
`Testing/multiplayer/scenarios/lobby-remote-client.txt`, and drives the admin side. The client
asserts its own UITK lobby through new `AutomationSubSystem` opcodes (`ui_wait`, `ui_text`,
`ui_submit`, `screenshot`, `wait_timeout`). Run `remote-20261030-162400-944` **passed**: 15 host
checks plus the client script, zero errors on either side, 37s. The lifecycle pilot
(`lifecycle-20261030-162625-193`, 28 checks) and visual pilot (`20261030-162749-980`, 122
checks) were rerun green afterwards.

Covered from the remote side: shell visible/creator hidden on join, Admin disabled for a
non-admin, `0/2` → `1/2 ready`, a non-admin start request ignored by the server, Ready via the
button, spawn on admin Start Round with the unready host left in the lobby, return on Stop Round,
Join Round late spawn in a second round, return and disconnect. Client screenshots were reviewed.

Fixes found by the runs:

- The booth's Human dummy ran gameplay `Awake`s before stripping; on the client
  `HumanoidController.Setup` threw because `CameraSubSystem` did not exist yet. It is now
  instantiated inactive and stripped immediately.
- Disabled lobby buttons had no disabled style; a non-admin's Admin looked clickable.
- `AttachedContainer` threw on the server for every despawned body (`SyncList.Clear` passes
  default items).
- Unrelated but found while building: an async station load outliving Play Mode wrote
  `Chunk [x,z]` objects into the open `Game.unity` (saved once during this session, then
  reverted). `TileMap.CreateChunk` now cancels when its map is destroyed.

Not covered: OS mouse/keyboard input, a second machine, and the client after `ScriptComplete`
(a windowed player may stall in `Environment.Exit`; the pilot kills it). The remote client's first
post-spawn capture showed the camera over open space; not investigated. Header map/mode is
still the mock "Cerestation".

## Jobs and antagonist preferences (Phase D) — 2026-10-30

The Jobs tab now shows `RoleSubSystem`'s replicated `JobSlotInfo` list instead of mock departments:
role name, department (`IdAccessSubSystem.DepartmentForRole`), slots (0 = unlimited, shown as OPEN),
assigned and **interested** (online players ranking the job above Never, shown per row as live
demand, `lobby.md` §3). Today that is the two authored roles in `StartingRoles.asset`: Security (1)
and Assistant (unlimited). Departments without roles do not appear; Civilian uses Heroicons `UserGroup`.

- **Sync:** every chip/opt-in change sends a `JobPreferencesMessage` broadcast with the full list;
  `LobbyUiSubSystem` also resends once per connection after the local player record replicates. The
  server resolves the player from the sending connection, ignores unknown jobs/categories, and keeps
  preferences per ckey, so they survive reconnects within a server session (not persisted to disk).
- **Defaults:** every job Never and no antagonist opt-ins — an empty list is the explicit
  "no preference" and resolves to the fallback.
- **Resolution (`JobResolver`, pure + EditMode-tested):** `EntitySubSystem.SpawnReadyPlayers` calls
  `RoleSubSystem.ResolveRoundStartRoles` before spawning. All players' High jobs are tried before any
  Medium, then Low, in a fresh random order per tier with a random pick among a player's tied open
  jobs. Leftovers get `_fallbackRole` (Assistant), which ignores its cap. Latejoin
  (`GiveRoleLoadoutToPlayer` with no assignment) takes the highest ranked open job, else the fallback.
  Assignments clear on `Stopped`. The old assign-on-connect (first joiner → Security) is removed, as is
  the client-callable `ServerRpc` on `GiveRoleLoadoutToPlayer` (now `[Server]`).
- **Antagonists:** `AntagonistCategories` (Nuclear Operative, Traitor) replaces the mock list.
  `RoleSubSystem.IsAntagonistOptedIn(ckey, category)` is the server API; no gamemode selects from it yet
  (MVP1 M0 assignment).

Divergences from `lobby.md`: H/M/L/N tiers instead of a drag-ranked list (already recorded); latejoin
uses the same preference chips filtered by open slots rather than a direct pick list; no playtime
locks exist, so full jobs (not locked ones) render dimmed.

Verified by `Testing/ui/validate.sh --full` (`validate-20261030-182754`): 2101 EditMode tests incl.
8 `JobResolverTests`, visual 122 checks, lifecycle 38 checks (Security H chip → server interest 1 and
row label → round start assigns Security and slot 1/1 taken → stop clears → late join takes Security),
client rebuild and remote pilot 17 checks.

## Networked character (Phase E2 slice) — 2026-10-30

The saved lobby character now reaches the spawned body. `lobby.md` §6 requires only a name;
appearance goes beyond that as the effort's recorded Character Creator scope.

- **Sync:** `LobbyUiSubSystem` sends a `CharacterAppearanceMessage` broadcast on Save and once
  per connection (same pattern as job preferences). It carries the last *saved* draft (never
  unsaved creator edits), with name, the six body values, hair/beard indices and resolved hair/beard
  colors. `EntitySubSystem` resolves the player from the sending connection, runs
  `CharacterAppearance.Sanitized()` (name: no control chars or `<`/`>`, max 32; values clamped;
  colors opaque) and keeps it per ckey for the server session (not persisted).
- **Spawn:** `SpawnPlayer` sets `Mind.CharacterName` and `Human.SetAppearance` (one SyncVar).
  A Save affects the next spawn only, never a living body.
- **Apply:** `Human` applies on the server and on every client (`OnStartClient` + SyncVar change).
  Body morphs use `HumanoidMorphApplier`, but height scales the **armature** (`HumanoidRigReferences.ArmatureRoot`), not the
  networked root, so the movement capsule and root stay unit scale. The server applies it too so its
  bone hit colliders match clients. `ClothesDisplayer` re-applies morphs after a worn-mesh swap.
  Hair/beard prefabs resolve through `IHumanoidStyleSource`, which `LobbyAssetCatalog` implements and
  registers at UI bootstrap. Headless servers have none and spawn no style meshes. Like the booth,
  applying a style hides the baked Hair/Facial Hair/Eyebrows meshes.
- **Name:** `Mind.DisplayName` (character name, else ckey) replaces the ckey in speech bubbles,
  radio/announcement senders, crew records/ID cards and the late-join announcement. A ghost keeps
  its mind and therefore its name. The comms log now records the character name, not the ckey.

Still deferred in E2: species models, skin/eye tint, eyebrow meshes/color, disk persistence.
Index-based styles assume server and clients run the same build/catalog.

Verified by `Testing/ui/validate.sh --full` (`validate-20261030-184621`): 2110 EditMode tests incl. 9
`CharacterAppearanceTests`, visual 122 checks, lifecycle 58 checks (creator name/height/fat/first hair →
Save → round-start spawn and late-join spawn each carry the mind name, armature height with unit root
scale, the Fat shape key and one hair mesh; `spawned-appearance.png` reviewed), client rebuild, remote
pilot 17 checks. The remote client was not asserted on another client's appearance.

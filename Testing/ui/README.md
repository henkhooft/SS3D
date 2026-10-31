# Unity MCP and lobby pilot

Pinned integration: CoplayDev Unity MCP 10.3.0, commit
`aa5fc638d623f56178d50329d9ad8c541a57fe66`, on Unity `6000.3.16f1`.
The Unity package is pinned in `Packages/manifest.json`; the Python environment is
local/ignored under `artifacts/unity-mcp/venv`. `requirements-mcp.txt` records the
tested Python 3.14 environment. No server or tooling is added to release builds by
the SS3D editor assembly.

## Run

From the repository root, start these long-lived processes separately:

```bash
Testing/ui/start-mcp.sh
Testing/ui/open-unity.sh
```

The first script installs the server if missing, then serves MCP on loopback
`http://127.0.0.1:8080/mcp` (override with `SS3D_MCP_URL`, e.g. `http://127.0.0.1:8095`, in the environment of all three: start-mcp.sh, open-unity.sh and the drivers). The second uses the exact project editor version and
connects the bridge. Set `SS3D_UNITY_EDITOR` if Unity is installed elsewhere. A
valid Unity license and graphical desktop are required. Do not launch a second
editor against an already-open checkout; use **SS3D → Testing → Connect Unity MCP**
in the existing editor instead. Script recompilation reconnects automatically
when launched with `open-unity.sh`.

Codex registration (already performed on Rutger's machine):

```bash
codex mcp add ss3d-unity --url http://127.0.0.1:8080/mcp
```

A newly registered server may need a new Codex session to appear as native tools.
The checked-in driver uses standard MCP immediately, including in an older chat:

```bash
artifacts/unity-mcp/venv/bin/python Testing/ui/lobby-pilot.py
```

It checks the selected editor's project path, invokes **SS3D → Testing → Run Lobby
UI Pilot**, waits for a completed report, and returns nonzero on failure/timeout.
Native MCP clients can call `execute_menu_item` with that same menu path. Inspect
`mcpforunity://editor/state` after `refresh_unity` before starting a run.

## Validation loop (agents: start here)

One command validates UI/session work in the open editor, unattended:

```bash
Testing/ui/validate.sh            # --quick (default): preflight, compile, EditMode, visual, lifecycle
Testing/ui/validate.sh --full     # + PlayMode, client+server build if stale, remote pilot, multiplayer smoke
Testing/ui/validate.sh --steps compile,visual   # any subset, in order
Testing/ui/compile.sh             # = --steps compile
```

Run it outside the agent's Bash sandbox (the dialog watchdog needs X11). It stops at the first
failure, prints a summary table with report paths and the screenshots to review, and writes
`artifacts/ui/validate-<UTC>/summary.md`. Exit status is nonzero on any failure. A full run takes
~6 min warm (EditMode ~40 s, visual ~70 s, lifecycle ~85 s, build ~30 s, remote ~50 s).

| Step | What it checks |
|------|----------------|
| preflight | `AutomationPreflight.Check()`: no dirty open scene, no leaked runtime root objects (`Chunk [`, `CharacterPreviewDummy`, `CharacterPreviewStaging`). Never saves or discards. |
| compile | Refreshes, requests compilation, waits for `artifacts/ui/compile-result.json` (written by `AutomationCompileReport` from `CompilationPipeline`) newer than the request. Lists every compiler and analyzer error (`CS*`, `COIMBRA*`, `RCS*`) with file:line, plus `scriptCompilationFailed`. |
| editmode | All EditMode tests via MCP `run_tests`; `editmode.json` in the validate folder. Then waits for the Test Runner to restore the scene setup and fails if tests left a dirty/polluted scene. |
| playmode | `--full` only. All PlayMode tests via MCP `run_tests`; failures are checked against `playmode-known-issues.txt` (see [PlayMode step](#playmode-step)). Same scene-restore check as editmode. |
| visual / lifecycle | The two pilots below. Each refuses on a failed preflight (a failed `status.json` at once) and, after Play exits and 3 s of settling, re-runs the check as a postcondition: a leaked `Chunk [` fails the report (`postconditions` field). Reports pass through `verifying` before `passed`/`failed`. |
| build | Client and dedicated server, each skipped when its `Builds/{Game,GameServer}/ss3d-build-hash.txt` matches `python3 Testing/ui/source_hash.py`. Otherwise `AutomationBuild.BuildClient()` / `BuildServer()`: preflight, then the build on the next editor tick; the driver waits on `artifacts/ui/{client,server}-build-result.json` (`status`, `seconds`, `error`, `sourceHash`), since a build blocks the main thread and MCP. The server build restores the editor's standalone subtarget afterwards, so the editor never stays on UNITY_SERVER. Cold server build ~6.5 min on tomnas. |
| remote | The remote client pilot. Run directly with a stale build, it refuses with both hashes. |
| smoke | `--full` only. `Testing/multiplayer/run_smoketest.sh` for basic-round, late-join (2 clients), reconnect and atmos-client-sync, the same set as `multiplayer-smoke-test.yml`. It runs a real headless dedicated server and clients from the current builds (refuses if either is stale). One log per scenario, `smoke-<scenario>.log` in the validate folder, which names the preserved `Testing/multiplayer/.runs/` directory. Runs every scenario, then fails if any failed. ~9 min on tomnas. |

Each step has an expected duration and a timeout. Past the expected duration,
`Testing/ui/watch-dialogs.sh` runs every 30 s; a modal dialog fails the step at once. On
timeout the watchdog output and the last 40 `editor.log` lines are printed.

`watch-dialogs.sh` lists the editor's X11 windows (`wmctrl -l -p`), ignores the main window and
progress windows (`Compiling…`, `Building…`, `Hold on`, and the bare title `Unity`), and saves
any other window to `artifacts/ui/dialogs/<UTC>-<title>.png` (exit 1). Use it whenever the editor
sits at 0% CPU with no log progress.

The build hash covers `Assets/`, `Packages/` and `ProjectSettings/` by git blob id (working
tree included, staging/committing irrelevant), excluding `Editor/` folders and the Addressables
output a build writes. Reverting an edit makes an old build current again. A build rewrites
`UniversalRenderPipelineGlobalSettings.asset` (`m_RuntimeSettings`); the stamp is taken after
the build so this does not leave it stale.

Two editor behaviours the tooling works around:

- The MCP `editor/state` resource reports `ready_for_tools: false` with `stale_status` while an
  idle editor is fine. The driver asks the editor directly (`isCompiling`, `isUpdating`,
  Play Mode) through `execute_code`.
- An idle, unfocused editor can leave `EditorApplication.delayCall` unprocessed indefinitely while
  `EditorApplication.update` keeps ticking. Automation schedules one-shot `update` callbacks.

### PlayMode step

`playmode` (in `--full`, not `--quick`) runs every PlayMode test through MCP `run_tests` (~6.5 min on
tomnas) and writes `validate-*/playmode.json`. Failures are checked against
[`playmode-known-issues.txt`](playmode-known-issues.txt), which lists expected failures:
- a listed test that fails is reported and does not fail the step;
- a listed test that passes is flagged, so its entry can be removed;
- any unlisted failure fails the step.

Known-issue fixtures (`Assets/Scripts/Tests/KnownIssueReproduction/`) are reported separately from
regular tests, in `playmode-known-issues.md` and in the summary. Keep the list's comments current: each
entry should say why it fails.

## Evidence and scope

Each run writes `artifacts/ui/<UTC timestamp>/status.json` and fourteen PNGs: eight player-view captures (Server Info, Jobs, Chat, Players,
Display, Sound, muted Sound, Keyboard), plus lobby, identity, body/side, style,
review and saved lobby. Captures use a fixed 1920×1080 Game
View preset. The selected Game View size and Play Mode start scene are restored
after the run; Boot/Game assets are not edited. The pilot asserts the legacy uGUI lobby canvas (purged in Phase F) is absent. Other startup/game UI can still appear.

The pilot opens the real lobby and creator, changes name and body sliders, rotates
the preview, navigates steps, saves, and returns. It checks visibility, control
presence, preview texture binding, angle label, saved name, updated dropdown/footer behavior, Saved confirmation,
local character switching and New Character reset. It fails on captured
Error/Exception/Assert logs. It does not claim that a bound RenderTexture contains
the correct model; inspect the PNGs too.

This is a **rendered smoke test**, not a visual golden-image test: animations and
the background are still live. Synthetic Click/NavigationSubmit events and field
changes exercise UI callbacks, but do not establish OS input, hit testing, focus
arbitration, multiplayer synchronization, or layout at other resolutions. Initial
startup messages before the EnteredPlayMode callback and teardown after the report
are outside its error collection window; consult `artifacts/ui/editor.log` too.

## First verified baseline — 2026-10-27

Run `20261027-134342-128`: 18 assertions passed, six 1920×1080 screenshots captured
and visually inspected. Overall result **failed**, correctly, on three existing
preview stripping errors:

- `InteractionController` required by `CombatInteractionNetwork`.
- `Human` required by `LocalSpeechEmitter`.
- `HumanoidBodyStateMachine` required by movement/combat/body bridge components.

These originate in `CharacterPreviewBooth.StripNetworking` when opening the creator.
Do not allowlist them to make the test green. Visual review also found the Front
view showing the dummy's back, other UI/background content behind the lobby, and
raw hair catalog identifiers wrapping across lines. No gameplay/UI redesign fixes
are bundled into the tooling setup.

## Preview fix verification — 2026-10-30

Run `20261030-130709-268`: **passed**, all 18 assertions, zero captured errors,
six 1920×1080 screenshots. Preview stripping now removes combat/speech/movement
and body-bridge dependents before the generic network-component pass. Front uses
base yaw 0° toward the +Z camera; Front and Side screenshots were inspected.
Background/overlapping UI and raw hair identifiers remain follow-up work. After
the report, editor teardown logged an unrelated `TileMap` `MissingReferenceException`
in `TileSubSystem.WaitForResourcesLoad`; teardown is outside the pilot collection window.

## Updated creator verification — 2026-10-30

Run `20261030-133826-904`: **passed**, 40 assertions, zero captured errors and
six 1920×1080 PNGs. Identity, Body, Style and Review were visually reviewed during
the refresh; final captures confirm the red slider fill and clean catalog labels.
The pilot additionally checks the Species dropdown, absence of Uniform Color,
one footer Save action, Saved confirmation, local character switching, new-draft
reset, and restoration of saved name/body values. See the
[lobby effort](../../Documents/architecture/2026-08_lobby-uitk-redesign.md#2026-10-30-character-creator-refresh)
for the source reference and agreed body-control exception.

The pilot refuses to start while scripts are compiling or compilation has failed,
and restores editor settings left by a prior compiler-blocked Play request before
retrying. Real pointer/keyboard input, other resolutions, and multiplayer remain
outside this fixed-resolution smoke test.

## Style palette relocation — 2026-10-30

Run `20261030-134939-028`: **passed**, 60 assertions, zero captured errors,
six 1920×1080 PNGs. Hair Color is absent from Body and sits below the Style tiles.
Hair, Facial Hair and Eyebrows show their own labelled palettes with independent
saved selections. The pilot changes all three colors and checks restoration after
character switching; the Style screenshot was visually reviewed. Hair and beard
use separate live tints; eyebrow rendering remains deferred.

## Player View fidelity pass — 2026-10-30

Run `20261030-140336-222`: **passed**, 84 assertions, zero captured errors,
14 screenshots at 1920×1080. Player tabs and Settings were visually reviewed.
Coverage now checks the 1180px maximum shell / 280px sidebar, player-tab surfaces,
three underlined Settings subtabs, resolution/fullscreen and display sliders,
volume readouts, mute disabling/re-enabling sliders, eight key-binding buttons,
local next-key capture, retained values, and the existing creator flow.

Settings changes and keyboard capture affect session-local UI state only: they do
not change the editor/Game View resolution, fullscreen, game audio, rendering or
Input System actions. Prototype keyboard labels do not claim to be the fork's
actual default bindings. See the [effort scope](../../Documents/architecture/2026-08_lobby-uitk-redesign.md#2026-10-30-player-view-shell-refresh).

Rounded dropdown/preview borders produced white output on the tested Linux path;
square corners restored content. Toggle input needs an automatic growing basis.
Volume checks must filter `lobby-shell__settings-slider` because `ScrollView`
scrollers also contain sliders. A green assertion report still requires PNG review.

## Scroll-free lobby and live startup preview — 2026-10-30

Run `20261030-142251-600`: **passed**, 89 assertions, zero captured errors,
14 screenshots at 1920×1080. The full server information and all four Change Log
entries fit without an outer ScrollView. The 840px shell is viewport-capped, banner
height follows sprite aspect and may shrink, and Ready retains bottom alignment
with top padding. `00-server-info.png` verifies the live RT before any creator
interaction; `01-lobby.png` was visually inspected with the lit character and layout.

New checks cover RT binding at startup, both preview lights surviving station
initialization, absence of Server Info scrolling, content bounds and action spacing.
Early booth creation exposed the orphan-light sweep deactivating its lights;
`GameplayLightGuard` now preserves lights owned by the preview booth as well as
station fixtures. Do not restore a placeholder or delay booth creation to hide this.

## Default preview outfits — 2026-10-30

Run `20261030-144343-787`: **passed**, 100 assertions, zero captured errors,
14 screenshots at 1920×1080. The selected security jumpsuit and jackboots were
visually inspected in the startup lobby, creator and side/body-edit captures.
Assertions check all three worn renderers and stable meshes through edits and
saved-character restoration. The curated catalog also contains civilian,
engineering and botany combinations; choice occurs once per new session-local draft.
No networked clothing Items or authoritative player equipment are spawned.

The umbrella catalog's interactive menu opens a blocking success dialog. Automation
should call `UiCatalogRebuildAll.TryRebuildAll` from editor code rather than invoking
that interactive menu; otherwise the dialog must be dismissed before MCP can continue.

## Lobby lifecycle pilot — 2026-10-30

**SS3D → Testing → Run Lobby Lifecycle Pilot** (native MCP: `execute_menu_item` with that
path) boots a single editor Host from Boot and drives: ready/unready, round start with the
creator open, spawn hiding, stop/return, Admin → Start Round with an unready player, late
join, a second return, then local client disconnect/reconnect 70s after server start. It writes
`artifacts/ui/lifecycle-<UTC timestamp>/status.json` (checks, per-step timeline with session
state, captured errors) and `lobby-after-reconnect.png`. Each step has its own timeout; a
failure names the step and the current session state.

Run `lifecycle-20261030-153833-081`: **passed**, 28 checks, zero captured errors, 72s.
Since the E2 character slice it also edits name/height/fat/hair in the creator, saves, and
checks the round-start and late-join bodies (name, armature height, Fat shape key, hair mesh;
`spawned-appearance.png`): `lifecycle-20261030-184836-089`, 58 checks. It
does not cover a separate client process, non-admin permissions or OS input. Buttons are
driven with `NavigationSubmitEvent` (a synthetic `ClickEvent` does not fire `Button.clicked`).

Lobby boot now leaves the station unloaded. Choose an actual saved template in Server
Settings and press Start Round; a default selection prefers saved ministation, otherwise the
first available save. `ApplicationSettings.EditorStationTemplate` still applies to an explicit
parameterless `TileSubSystem.LoadAsync()` call, not lobby startup. Edit project settings through
the inspector or `SerializedObject`/`SetDirty` while the editor is open.

## Lobby remote client pilot — 2026-10-30

**SS3D → Testing → Run Lobby Remote Client Pilot** needs a current client build at
`Builds/Game/SS3D.x86_64`. `validate.sh --full` rebuilds it when stale; otherwise call
`SS3D.Editor.Automation.AutomationBuild.BuildClient()` through `execute_code` and wait on
`artifacts/ui/client-build-result.json` (not **SS3D → Build → Client (Linux)**, which opens a file
manager and has no preflight). The client is a separate player, not the editor's assemblies. The
first build compiles URP shader variants (~13 min), later ones ~30s; the editor is blocked meanwhile.

The pilot boots the editor Host, launches the client windowed as non-admin `remoteprobe` with
`-testscript=Testing/multiplayer/scenarios/lobby-remote-client.txt`, and drives Admin → Start/Stop
Round twice. It writes `artifacts/ui/remote-<UTC>/status.json`, `client.log`, six client
screenshots (`remote-0*.png`) and `host-after-remote.png`. A pass needs the client's
`ScriptComplete` signal and no errors in either log.

**Real OS input:** the client's Ready click, the Chat tab and chat-field clicks, the typed chat
line and Return are real OS input. The client scenario's `os_click <selector>` resolves the element
to window pixels and emits an `OsClick` test signal; the pilot reads `client.log` and sends it to
`Testing/ui/uinput_input.py serve`, a virtual uinput pointer+keyboard that lives for the whole run
(`os_type`, `os_key` likewise). This moves the desktop pointer and types into the focused window
during the run; don't use the desktop meanwhile. A transparent overlay over the shell makes the
click miss and fails the pilot at the Ready step; synthetic events would have passed.

Requirements: write access to `/dev/uinput` for the session user (on Rutger's machine Steam's
`60-steam-input.rules` grants it via uaccess; otherwise add a udev rule such as
`KERNEL=="uinput", TAG+="uaccess"`), `xdotool` for the client window lookup only, and every monitor
at scale 1 (the absolute pointer spans the X root, which then equals GNOME's logical desktop).

Why not xdotool for input: on GNOME Wayland its XTEST events go through Xwayland's libei portal,
which shows an "allow remote interaction" dialog on every run. uinput sits below the compositor and
needs no dialog. Pitfalls found: udev tags the device as a keyboard only when keycodes 1–31 are all
declared (otherwise clicks work but typing silently does nothing), and keys from a second, freshly
created device did not reach the window the first device clicked, hence one device per run.

Run `remote-20261030-162400-944`: **passed**, 15 host checks, zero errors, 37s. The client may
still be tearing down after `ScriptComplete` (`Environment.Exit` stalls in a windowed player); the
pilot kills it and notes that in the timeline.

Before building, make sure `Game.unity` is not dirty: `BuildPlayer` opens a blocking
"Scene(s) Have Been Modified" dialog. If it is dirty after a playtest, look for root
`Chunk [x,z]` objects (leaked tile load, see the tile map) rather than saving blindly.

## Troubleshooting

- Missing license: sign in and reactivate via Unity Hub. The initial license failure
  on this machine cleared after sign-in; no cipher/security-policy changes were needed.
- No instances: check the bridge and server logs; a healthy MCP server alone does
  not prove Unity connected. Keep it local to this machine.
- Compilation: automation has its own editor-only asmdef. `Assets` has a parent
  asmdef; merely placing scripts in an `Editor` folder does not isolate them.
- Null UI root: a `UIDocument` may exist before its `rootVisualElement` does.
- UI events: set the pooled event's `target` before `SendEvent`.
- Game View sizing uses Unity's internal editor API, confined to one helper. If an
  editor upgrade changes it, repair that helper; don't silently capture another size.
- Validation step timed out: read the watchdog lines in the output. `busy: …` is a progress
  window; `DIALOG: …` names a modal and its PNG. Dismiss it deliberately (never Save `Game.unity`
  blindly; look for leaked `Chunk [x,z]` objects first).
- No final report: the external driver times out after 180 seconds; the in-editor
  scenario times out after 120 seconds once Play Mode has started. Inspect the logs
  and exit Play Mode before retrying. A compiler-blocked editor cannot run the pilot.

## Enlarged preview framing — 2026-10-30

Run `20261030-150133-789`: **passed**, 102 assertions, zero captured errors, 14 screenshots. Lobby, creator Identity and Body side-view captures were visually reviewed. The pilot now measures foreground occupancy in the preview RT: at least 72% height, with head and feet clear of the edges. This run measured 84%. The shared camera fits visible baked geometry rather than padded animation bounds or empty equipment slots.

## Validation loop baseline — 2026-10-30

`validate.sh --full` run `validate-20261030-173633`: **passed** unattended on the restored tree in
~4.5 min — compile clean, 2093/2093 EditMode, visual 122 checks (`20261030-173731-178`), lifecycle
28 (`lifecycle-20261030-173840-591`), build current (skipped), remote 17 host checks with real
Ready/chat input (`remote-20261030-174007-995`). Each acceptance check in the
[plan](../../Documents/plans/agent_validation_loop.plan.md#implementation-notes-2026-10-30) was
demonstrated by a deliberate break, then reverted.


## Server Settings coverage — 2026-10-30

The lifecycle pilot opens Admin and checks the real authorized snapshot, live ready ratio,
map/mode options, disabled explanations for unsupported rules, cap/length/mode/late-join edits,
invalid server-side rejection, settings lock during countdown, Extend Timer (+60 seconds),
Cancel Countdown (no spawn) and Force Start. It captures `server-settings-waiting.png` and
`server-settings-countdown.png` and `server-settings-rules.png` in its lifecycle output folder.
It also restores ministation through the map selector and runs a real one-minute Extended round
to verify server-enforced late-join denial, active-round edit rejection and automatic expiry. These are synthetic UI callback
checks; inspect the screenshots as well. The remote client sends unauthorized settings
read/write/action RPCs with `probe_server_settings_permissions`; the host verifies that the
cap and join policy remain unchanged, and the client verifies that no admin snapshot arrives.


Verification: runtime validation `validate-20261030-203641` passed compile, lifecycle
(96 checks, no captured errors), client rebuild and remote (19 host checks, client
permission probe + real Ready/chat input). The preceding full run
`validate-20261030-203255` passed EditMode (2089 completed tests) and visual (122 checks);
its lifecycle failure exposed the stale-snapshot edit race fixed by per-field server merges.
Waiting/countdown/rules screenshots from `lifecycle-20261030-203711-067` were reviewed.


## Deferred round-map loading — 2026-10-30

The lobby no longer restores a station at boot. Start Round loads the selected saved map,
waits for world readiness, then counts down. `lobby-map-load-status` and
`server-map-load-status` expose loading / initializing / ready / failed states. The lifecycle
pilot checks empty startup, captures `server-settings-loading.png`, validates a temporary second
map selection/restore and captures `server-settings-running.png` with Stop visible and Start hidden.
The remote scenario verifies no map is loaded on join and a ready status after the host starts.


Deferred-loading verification: full `validate-20261030-205054` passed compile,
2089/2089 EditMode tests, visual (122 checks), lifecycle (107 checks), rebuild and remote
(21 checks). After the long-name status layout adjustment, `validate-20261030-205721`
passed compile, visual (122), lifecycle (108), rebuilt client and remote (21). Loading,
waiting, countdown and running screenshots were reviewed; the running form shows Stop
without Start, and the long map status no longer displaces the sidebar actions.


## Playtest fix review coverage — 2026-10-30

The lifecycle pilot exercises B while typing in lobby chat, releases text capture on tab rebuild,
opens/closes the Escape menu over a spawned body and verifies gameplay input suppression and
restoration. It also waits for a rendered character-selector portrait and checks that head-crop
blitting preserves a sentinel `RenderTexture.active` (`character-selector-portrait.png`).
`SelectionRendererSetupTests` checks repeated renderer wiring retains the same selection-feature
instance/local ID and a matching feature recovery map. Generated glyph atlases, build-prefilter
flags and runtime-only catalog entries are not authored feature changes.


Review verification: `validate-20261030-211254` passed preflight, compile, all 2090 EditMode
tests (including renderer setup identity/recovery mapping), 122 visual checks, 110 lifecycle
checks (including portrait target preservation and Escape/focus behavior), client rebuild and
21 remote checks. Selector portrait and in-round menu captures were visually reviewed.

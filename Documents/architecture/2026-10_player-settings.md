> Implements: Documents/design/audio.md §7 (volume categories), §10 (levels are a settings concern)
> Touches systems: rounds-lobby, ui-shell, audio, main-hud (panel scale), rendering (URP quality assets), input
> Status: planned

**Progress:** not started. Phases 1–2 count toward [test-server.md](../milestones/test-server.md) T6. Phase 3 is polish outside the gates.

# Player settings: display, audio, controls (Oct 2026)

Wire up the player **Settings** tab in the lobby shell. It also serves as the in-round Escape menu. Today the tab is a session-local mockup with three sub-tabs: Display, Sound and Keyboard ([rounds-lobby](systems/rounds-lobby.md) Overview; `LobbyShellView.BuildSettingsTab`). None of its values are applied to the game, and none survive a restart. Scope was settled in a grilling session on 2026-10-31.

No design doc covers player settings: `main-hud.md` lists settings menus as out of scope, and `audio.md` §10 treats volume levels as a settings concern. The only design premise is audio.md §7's four volume categories. Brightness is deliberately left out (see Decisions).

This effort **absorbs** [2026-07_audio-foundation](2026-07_audio-foundation.md) Phase 0 (mixer groups and routing) and Phase 5's slider binding. audio-foundation keeps the lobby music bed.

## Starting state (2026-10-31)

- `LobbyShellView` holds settings in view-owned dictionaries (`_settingsValues`, `_keybinds`, `_resolution`, `_fullscreen`, `_muted`). The view applies nothing and persists nothing.
- The resolution list is hardcoded to four entries. Fullscreen is a plain bool. A Brightness slider exists. The 8 keybind rows are placeholders (Jump, Inventory…) that don't match `Assets/Content/Systems/Input/Controls.inputactions`.
- That asset has **no control schemes** and only keyboard/mouse bindings, across 7 maps: Camera, Movement, Console, Hotkeys, Other, Tile Creator and Interactions. Some actions are debug-only (Spawn Cans, Swap Minds, Toggle Atmos Debug, Toggle Selection Debug, Toggle Runtime Editor). No rebinding or override code exists.
- `MainMixer.mixer` has Master, SFX, Music and Ambience groups but exposes **no volume parameters**; the only exposed parameters are the SFX muffle ones. There's no Personal group. Ambience and Personal `AudioSource`s output to Master (audio-foundation's known gap).
- All six quality levels (Very Low through Ultra; Ultra is the default) point at the single `Assets/Settings/URP/SS3D_URPAsset.asset`. URP ignores the built-in shadow, pixel-light and AA fields, so the levels differ only in LOD bias, texture mip limit, anisotropic filtering, realtime reflection probes and VSync. The URP asset itself is fixed at high cost: 450 m shadow distance, 4 cascades, 2048 maps, high soft shadows, 8 lights per object, MSAA off, render scale 1, upscaling filter Auto.
- Local persistence precedent: `CharacterProfileStore` writes `persistentDataPath/Lobby/characters.json`, with a path override for pilots.

## Decisions

| # | Decision |
|---|---|
| 1 | Tabs: **Display, Audio, Controls**. Interface and Accessibility are parked. |
| 2 | Storage is client-local JSON at `persistentDataPath/Settings/settings.json`, with a `version` field. Keybinds are stored as `SaveBindingOverridesAsJson` output in the same file. Not server-side: `persistence-save.md`'s player layer covers playtime and lobby preferences, not machine options. |
| 3 | Audio and keybinds apply immediately. Resolution and window mode apply immediately, then show a **15 s "Keep these settings?"** prompt that reverts unless the player confirms. There is no global Apply button. |
| 4 | Audio: **Master, Music, SFX, Ambience, Personal** sliders, plus **Mute All** and **Mute when unfocused** (default on). |
| 5 | Display: resolution from `Screen.resolutions`; **Fullscreen / Borderless / Windowed**; VSync; frame cap (Off / 30 / 60 / 120 / 144 / 240); quality preset; render scale; upscaler; anti-aliasing; UI Scale. **Brightness is dropped** so darkness keeps its gameplay meaning (owner's call). |
| 6 | Quality presets are **Low / Medium / High / Ultra**, **one URP asset per tier**: shadow distance and resolution, cascades, soft-shadow quality, additional-light shadows and per-object light limit, plus each level's LOD, mip and anisotropic fields. VSync and frame cap are re-applied after every tier switch, because each quality level carries its own `vSyncCount`. |
| 7 | Upscaling and AA stay outside the presets. Render scale is 100 / 77 / 67 / 50 %. The upscaler applies below 100 %: **STP** (default; needs SM5 compute, falls back to FSR 1 if unsupported), FSR 1 or Bilinear. AA is Off / FXAA / SMAA / TAA, shown locked while STP is active (STP forces TAA). DLSS and FSR 2/3 are parked: URP 17.3 has no built-in support, so they would need a third-party plugin. |
| 8 | UI Scale is 75–150 % in 5 % steps and applies when the slider is released. It affects the screen-space panels: lobby shell (`Assets/UI Toolkit/PanelSettings.asset`), HUD overlay and comms overlay. The machine interface is excluded because it's diegetic; confirm whether its panel is screen-space before excluding it. |
| 9 | Controls are **keyboard and mouse only**. Rebindable: Movement (four rows: Forward/Back/Left/Right), Toggle Run, Camera, Hotkeys, Interactions, chat and Attack, plus a **Map Editor** group (Tile Creator). Debug and Console actions are hidden. Headings follow the action map names. |
| 10 | Read-only rows: the `main-hud.md` §7 intent modifiers (Ctrl/Alt/Shift; their hints print literal key names), Toggle Menu (Escape), and Primary/Secondary Click. For modifier+key bindings only the key is rebindable; the modifier shows as a fixed prefix. |
| 11 | One key per action, no alternate slot and no unbinding. A conflict within maps that can be active together **swaps** the two bindings and shows a "Use is now Q" notice. Maps that are never active together (Tile Creator vs gameplay) may share keys. Escape cancels capture. Mouse buttons 3–5 can be bound. |
| 12 | Mouse **sensitivity** and **invert vertical** are processor overrides on the Camera actions. Zoom speed is parked. |
| 13 | Each tab gets a **Reset to defaults** button with confirmation. There is no per-row reset. |
| 14 | First-launch defaults: native resolution, Borderless, VSync on, **High**, 100 % render scale, SMAA, all volumes 80 %, mute-when-unfocused on. Volume values are placeholders; balancing them is out of scope (audio.md §10). |
| 15 | No Settings entry in the launcher yet. Boot-time apply means saved settings are already in effect before the player connects. |
| 16 | A corrupt file is copied to `settings.json.bak`, defaults load, and a warning is logged. Missing keys get their defaults; unknown keys are ignored. Overrides for removed actions are dropped by the Input System. |

## Shape

- **`PlayerSettingsSubSystem`** (client-only, not networked) owns a plain `PlayerSettings` model. Loading and saving go through a store with the same path-override pattern as `CharacterProfileStore`. The subsystem applies the settings **at boot, before the intro and lobby**, and saves after a short debounce. It does nothing on a dedicated or headless server. Apply logic is split per area: audio (mixer), display (`Screen`, `QualitySettings`, URP camera/asset), input (binding overrides, processors) and UI scale (PanelSettings).
- **`LobbyShellView`** raises "set X" intents and rebuilds the tab from the model. Its settings dictionaries are deleted. This follows the `ServerSettings` / `LobbyServerSettingsView` split ([rounds-lobby](systems/rounds-lobby.md)).
- Conflict detection and swapping is pure logic over a binding table, so it can be tested without the Input System runtime.
- Design premises are respected: audio-foundation's one-clip-loader rule, agent-first composition's asset-over-code rule (quality tiers are URP assets), and the UiShell view/subsystem split.

## Phases

### Phase 1 — Model, persistence, boot apply, audio *(T6)*

- Write the `PlayerSettings` model and JSON store test-first (`tdd`): round-trip, version field, corrupt-file → `.bak` + defaults, missing and unknown keys.
- `PlayerSettingsSubSystem`: load and apply at boot, debounced save, skipped when headless.
- Mixer (absorbs audio-foundation Phase 0): add a **Personal** group and expose volume parameters for Master, Music, SFX, Ambience and Personal. Route `AmbienceSubSystem` and `PersonalAudioSubSystem` sources to their groups through a runtime-loadable mixer reference (closes audio-foundation's "outputs to Master" gap). The SFX/Music pool prefabs already target their groups; verify.
- Sound tab: five sliders, Mute All, mute-when-unfocused. Convert linear values to dB, with 0 mapped to −80.
- Move `LobbyShellView`'s settings state into the model.

### Phase 2 — Display, quality, upscaling *(T6)*

- Replace the six quality levels with four, each with its own URP asset; default High.
- Display tab per Decisions 5–8: resolution list, window mode with revert prompt, VSync and frame cap re-applied after tier changes, render scale + upscaler + AA with the STP lock and fallback, UI Scale on the screen-space panels.
- Remove the Brightness slider.

### Phase 3 — Controls *(outside the gates)*

- Controls tab generated from `Controls.inputactions`, using the visible map and action list from Decisions 9–10.
- Interactive rebinding (`PerformInteractiveRebinding`, Escape cancels), conflict swap and notice, overrides saved to the model, sensitivity and invert-Y processors.
- Reset to defaults on each tab (Decision 13; Audio and Display get theirs in Phases 1–2).
- Check that the lobby's text-field keybind masking still holds while a binding is being captured.

### Phase 4 — Verification

- EditMode: model and store (Phase 1), conflict swap (Phase 3).
- Lobby pilot: change one setting per tab, reload the store, and assert the value persisted; assert that the mixer parameter, `QualitySettings` level and binding override actually changed.
- `Testing/ui/validate.sh --full`: screenshots of all three tabs and the revert prompt.
- Then run `update-system-docs` to update the rounds-lobby and audio maps and add pitfalls.

## Parked

DLSS / FSR 2–3 plugins · Interface tab (chat font size, main-hud §7 hint toggle) · Accessibility tab (colorblind palette, shake/flash reduction, subtitles) · monitor selection · gamepad · launcher Settings entry · rebindable intent modifiers (needs main-hud §7 hints to follow bindings) · zoom speed.

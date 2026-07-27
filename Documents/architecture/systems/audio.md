> Code paths: Assets/Scripts/SS3D/Systems/Audio/
> Entry points: AudioSubSystem, AmbienceSubSystem, PersonalAudioSubSystem
> Status: partial (personal heartbeat/breathing/alert clips registered; health hit one-shots wired)
> Verified: ee090d4f3 — 2026-07-26

# Audio

## Overview

Networked positional SFX/music playback, split out of [chat-audio-screens](chat-audio-screens.md)
once Phase 1 of [audio-foundation](../2026-07_audio-foundation.md) shipped client-local occlusion.
`AudioSubSystem` owns a per-client pool of `AudioSource`s; the server calls `[Server] PlayAudioSource`,
which fans out via `[ObserversRpc]` so each client plays its own pooled source at the given world
position with Unity's own distance falloff (`minDistance`/`maxDistance`). The server itself runs no
pool (`#if !UNITY_SERVER` — playback is observer-only). Per [audio.md](../../design/audio.md) §1,
occlusion and (later) ambience are **client-local presentation**, not server state — a wall muffles a
sound *for a given listener*, which the server's "play clip X at position P" RPC has no notion of.

## Start here

- `Assets/Scripts/SS3D/Systems/Audio/AudioSubSystem.cs` — pool entry point: `PlayAudioSource` /
  `StopAudioSource` / `SetTimeAudioSource` (server) → `RpcPlayAudioSource` et al. (observers); pool
  sizing (`Min/MaxSfxAudioSources`, `Min/MaxMusicAudioSources`), periodic purge
- `Assets/Scripts/SS3D/Systems/Audio/AudioSourceOcclusion.cs` — per-pooled-source client-local occlusion
  (audio.md §3, the seventh `SS3D.Utils.LineOfSight` consumer); added at runtime to every Sfx/Music
  pool `AudioSource` by `AudioSubSystem.CreateNewAudioSource`, primed by `PrepareForPlayback` right
  before each `Play()`
- `Assets/Scripts/SS3D/Systems/Audio/AudioOcclusionState.cs` — pure cutoff/volume lerp math, kept
  separate from the `MonoBehaviour` so it's unit-testable without a scene
  (`Assets/Scripts/Tests/EditMode/AudioOcclusionStateTests.cs`)
- `Assets/Scripts/SS3D/Systems/Audio/Boombox.cs`, `NoisyCollision.cs`, `ChangeMusicInteraction.cs`,
  `ListenerPosition.cs`, `AudioType.cs` — diegetic jukebox (§5), server-triggered collision SFX (§3
  example, gains occlusion for free), music-swap interaction, local-player `AudioListener` follow,
  `Sfx`/`Music`/`Ambient` enum
- `Assets/Scripts/SS3D/Systems/Audio/AmbienceHandler.cs` — **legacy, superseded, do not extend.**
  Manual per-scene `_air`/`_windiness`/`_power` knobs + global mixer lowpass predating Area/electricity.
- `Assets/Scripts/SS3D/Systems/Audio/AmbienceSubSystem.cs` — **Phase 2**, `AmbienceHandler`'s
  replacement: client-local per-Area ambience crossfade (§2). Self-bootstrapped (same pattern as
  `ScreenEffectsSubSystem`), not scene/prefab-placed. Polls the local player's position every 0.5s via
  `AreaSubSystem.TryResolveAreaIdForWorldPosition`; crossfades two non-positional `AudioSource`s
  between the old/new `AreaSubSystem.TryGetAmbienceTrackId` clip. No occlusion (non-positional, §2 vs §3).
- `Assets/Scripts/SS3D/Systems/Audio/PersonalAudioSubSystem.cs` — **Phase 3**: personal, internal
  audio (§4) — heartbeat + heavy breathing, non-positional, no occlusion, owner-only.
  Self-bootstrapped like `AmbienceSubSystem`; owns playback only. Breathing takes
  `SetStaminaBreathingIntensity` / `SetHealthBreathingIntensity` (max-merge). Heartbeat via
  `SetHeartbeatIntensity`. Alert ding via `PlayAlertCue`.
- `Assets/Scripts/SS3D/Systems/Health/HealthPersonalAudioMapper.cs`,
  `Assets/Scripts/SS3D/Systems/Stamina/StaminaPersonalAudioMapper.cs` — `HealthSnapshot`/stamina
  ratio → heartbeat + labored breathing intensity, unit-tested. Called from each domain's existing local-owner
  hook (`HumanHealthController.ApplyScreenEffectsFromSnapshot`/`ClearScreenEffectsIfDriving` alongside
  `HealthScreenEffectMapper`; `StaminaController.SyncCurrentStamina`'s `IsOwner` gate) — not a new
  subscription. Health also drives positional gasp/choke/scream/flesh-hit via `AudioSubSystem`
  (`HealthAudioTrackIds`, `CombatAudioTrackIds.FleshHit`).
- `Assets/Scripts/SS3D/Systems/Audio/AudioTrackIds.cs` — fixed personal-audio clip ids
  (`Heartbeat`/`HeavyBreathing`/`AlertCue`) — Unity audio GUIDs once registered in `Sounds.asset`.
  FreeSound heartbeat/heavy breathing + SS14 beep1.
- `Assets/Scripts/SS3D/UI/MainHud/Components/AlertStackAudioMapper.cs` — **Phase 4** (§6): pure
  `HasNewAlert(previous, current)` diff over `AlertStackState` — true only when a hazard goes
  None → any severity, not on an escalation already showing. Unit-tested
  (`AlertStackAudioMapperTests`). `MainHudSubSystem.PushAlertState` is the single funnel every
  `AlertStackState` push goes through (health-driven, debug override, or clear) and calls
  `PersonalAudioSubSystem.PlayAlertCue()` (one-shot, cooldown-debounced) on a new alert.
- `Assets/Scripts/SS3D/Systems/Audio/FootstepAudio.cs`, `FootstepAudioTrackIds.cs` — footwear
  footsteps (§3): Socks / Shoes / Boots under `Assets/Art/Sound/Entities/Humanoid/Footsteps/`.
  **Client-local** `AudioSource` loop (not the networked pool) — host `Move(default)` clears server
  locomotion every tick, so a server-pooled loop never stayed audible. Owner uses predicted
  locomotion velocity; remotes use transform delta. Skips while `BodyAnimationSnapshot.IsFloating`
  (space coast). Runtime-added from `HumanoidPredictedMovement`.
- `Assets/Scripts/SS3D/Systems/Audio/VendorAudioTrackIds.cs` — vending dispense clip id
  (`VendingMachine.wav` from SS3D-Art); `VendingMachineController.PlayVendSound` uses it (replacing
  the old Can1 placeholder).
- `Assets/Scripts/SS3D/Systems/Furniture/AirlockAudioTrackIds.cs` — airlock deny clip id
  (`AirlockDeny.ogg` from SS14 Machines); `AirLockOpener.ServerPlayAccessDenied` plays it. Open/close
  still use generated `Sounds.AirlockOpen` / `Sounds.AirlockClose`.
- `Assets/Scripts/SS3D/Systems/Comms/CommsAudioTrackIds.cs` — announcement chimes
  (`Announce.ogg` always first; optional `Welcome.ogg` follow-up with the banner); `CommsFeedController`
  plays `StationAnnounce`, then reveals the ALL-STATION banner and any `CommsMessage.SoundId`
  follow-up in parallel. Third-party credits:
  [Sound ATTRIBUTIONS](../../../Assets/Art/Sound/ATTRIBUTIONS.md).
- `Assets/Content/Systems/Audio/MainMixer.mixer` — `Ambience`/`SFX`/`Music` groups exist; `Personal`
  group + per-group exposed Volume are Phase 0/5 work. `AmbienceSubSystem`/`PersonalAudioSubSystem`
  output to Master for now (no runtime-loadable `AudioMixerGroup` reference for a prefab-less
  subsystem) — route them once Phase 0 lands.
- `Assets/Content/Systems/Audio/SFXAudioSource.prefab`, `MusicAudioSource.prefab` — pool prefabs,
  routed to the `SFX`/`Music` mixer groups respectively
- `Assets/Scripts/SS3D/Systems/IngameConsoleSystem/Commands/AreaAmbienceCommand.cs` — `areaambience`
  dev console command, the thin authoring surface for `AreaSubSystem.SetAreaAmbienceTrackId`

## Extension points

- Play a positional sound: `SubSystems.Get<AudioSubSystem>().PlayAudioSource(AudioType, clipId,
  position, parent, ...)` (server-only). Do not `Instantiate`/`AudioSource.Play` ad hoc for anything
  that should occlude — route it through the pool so `AudioSourceOcclusion` applies.
- Sound clips resolve via `Assets.Get<AudioClip>(AssetDatabases.Sounds, id)` — the single clip-lookup
  path; don't add a second loader.
- **Finding/adding clips:** check `Documents/art-asset-index.md` → `art-available-for-import.json`
  first (SS3D-Art has hundreds of unimported sounds, and some imported ones aren't yet registered in
  `Assets/Content/Data/Databases/Sounds.asset`). Registering a new clip: create an
  `ObjectAssetReference` asset under `Assets/Content/Data/ObjectAssetReferences/` with
  `Id = <clip's own asset GUID>`, `Database = AssetDatabases.Sounds`'s `DatabaseID`, then add a
  matching `_key`/`_value` entry to `Sounds.asset`'s `Assets._list` (`_value` is `{fileID: 8300000,
  guid: <clip guid>, type: 3}`). `CombatAudioTrackIds` (per-domain, mirrors `AudioTrackIds`) is the
  pattern for naming registered ids in code rather than inlining raw GUID strings. Third-party
  imports: add a row to [Sound ATTRIBUTIONS](../../../Assets/Art/Sound/ATTRIBUTIONS.md).
- Ad-hoc `AudioSource` users outside the pool (`AirlockStateMachine`, `StructuralIntegrityPresenter`,
  `BlastExplosionEffect`, `BikeHorn`, `VendingMachineController`, `FuelPowerGenerator`,
  `CommsFeedController` non-diegetic announce/welcome) bypass occlusion today — consolidating
  positional ones onto `PlayAudioSource` is content-roster work ([audio-foundation](../2026-07_audio-foundation.md),
  deferred). UI chimes stay client-local / non-spatial.
- Author an area's ambience track: `AreaSubSystem.SetAreaAmbienceTrackId(areaId, trackId)` (server,
  thin — no Map Editor UI yet). Reachable in-game via the `areaambience (trackId|clear)` dev console
  command (`AreaAmbienceCommand`), which always targets the calling player's own current area (same
  "no mouse to click a tile with" reasoning as `AtmosDebugCommand`). Syncs to observers via
  `RpcSyncAreaAmbience` (BufferLast), same pattern as the departmental-tint snapshot.
- **31 ambience/collision clips registered, content-availability only (no default assignment):**
  `StationAmbience1-15`, `SpaceAmbience1-3`, `AirStagnant`, `WindHeavy/Light/Violent`, `PowerHum` (all
  candidate `areaambience` track ids — content decides which area gets which, per audio.md §2/§10);
  `MetalHit1-2`/`GrilleHit`/`Rod1`/`Tap`/`TrayHit1-2`/`WoodHit1` (more `NoisyCollision` variety — **no
  prefab uses `NoisyCollision` yet**, so these are registered for whenever one does, not wired to
  anything today).
- **Footwear footsteps (Socks/Shoes/Boots)** are registered *and* wired via `FootstepAudio` — not the
  availability-only pattern above.
- Personal cue seam: call `PersonalAudioSubSystem.SetHeartbeatIntensity` /
  `SetStaminaBreathingIntensity` / `SetHealthBreathingIntensity` (max-merge) for systemic cues
  (virology's symptomatic-stage cue is this same category, per audio.md §4) rather than building a
  parallel non-positional playback path. Do not call a single shared `SetBreathingIntensity` — stamina
  and health would overwrite each other.
- **Not yet built:** power-gating power-dependent ambience tracks off `AreaLightingState` (needs a
  per-track "requires power" data field — deferred, see Pitfalls), PDA notification cues (no PDA
  notification chip system exists yet to hook — same gap [chat-audio-screens](chat-audio-screens.md)
  already records for the non-diegetic feed), lobby music + volume-slider settings (Phase 5). See
  [audio-foundation](../2026-07_audio-foundation.md).

## Pitfalls

- **Occlusion is client-local — do not gate it behind `[Server]`/RPC.** The listener position only
  exists on each client; baking a muffled/clear decision into the server RPC would require per-observer
  variants of every play call. `AudioSourceOcclusion.Update()` runs unconditionally on whichever machine
  owns the `AudioSource` instance (clients only, since the server never builds a pool).
- **Reuse `SS3D.Utils.LineOfSight`, do not fork a raycast.** Same shared occluder mask
  (`LayerMask.GetMask("Default")`) as Drop, combat LOS, and comms occlusion — solid geometry that
  should occlude sits on the `Default` layer in this project, not a dedicated `Walls` layer.
- **Occlusion rays must not cast at feet height.** `ListenerPosition` follows the body root, and
  plenum/airlock-tile colliders on `Default` top out near y=0 — a root-height ray false-positives on
  the floor and muffles every pooled SFX. Sample planar LOS at chest height
  (`AudioSourceOcclusion.OcclusionSampleHeight`) so walls still block and floors don't.
- **`MainMixer` Master defaults to 0 dB.** The inherited snapshot had Master at ≈−20 dB (~10% linear);
  pooled SFX/Music route through it, so that alone made every diegetic sound inaudibly quiet even with
  clear LOS. Per-category slider balancing stays Phase 5 — don't re-bury Master as a stand-in for it.
- **Do not call `LayerMask.GetMask` / `NameToLayer` from a MonoBehaviour field initializer or static
  ctor.** Unity throws `UnityException` during `AddComponent` type init (pool create path hit this on
  `AudioSourceOcclusion`). Resolve lazily or in `Awake` — same rule `VisionSubSystem` already documents.
- **Pooled sources are never destroyed between plays** — the same `GameObject` is reused, so
  `AudioSourceOcclusion` state (cutoff, volume scale, occlusion flag) persists across unrelated clips
  unless reset. `AudioSubSystem.RpcPlayAudioSource` must call `PrepareForPlayback(volume)` before every
  `Play()` — skipping this leaves a newly played sound inheriting the previous clip's muffle.
- **`AmbienceHandler` is legacy — do not wire new features onto it.** No `AreaSubSystem` awareness, no
  power gating; `AmbienceSubSystem` (Phase 2) replaces it wholesale rather than extending its
  `_air`/`_windiness` fields.
- **`AreaRecord.AmbienceTrackId` is not persisted.** Absent from `SavedAreaRecord` /
  `BuildSavedAreaRecords` / `RestoreFromSave` — authoring via `SetAreaAmbienceTrackId` resets on the
  next map load/restore until a persistence contributor is added ([persistence](persistence.md)).
- **No per-track power-gating data field yet.** `audio.md` §2 says "some tracks are honestly
  power-dependent, not all of them" — that needs a per-track opt-in (mirroring
  `AreaRecord.HasDepartmentalLightTint`) before `AmbienceSubSystem` can silence a hum on `Dark`. Not
  built here on purpose: inventing the flag without an authoring surface or content decision on which
  tracks use it would be dead schema. `AreaSubSystem.OnAreaLightingStateChanged` /
  `TryGetLightingStateForTile` are the signal to consume once the flag exists.
- **Breathing contributors must max-merge:** stamina and health both drive heavy breathing. Use
  `SetStaminaBreathingIntensity` / `SetHealthBreathingIntensity` — a single setter lets the last
  writer silence the other domain's cue.
- **"New alert" means None → any severity, not any change.** `AlertStackAudioMapper.HasNewAlert`
  deliberately does not fire on Warning → Critical escalation — the visual chip is already showing,
  so re-cueing it would contradict §6's "not a continuous loop, only draws attention when something's
  actually wrong" restraint. Route any future alert-audio change through this diff, not a raw
  field-by-field equality check.
- **`StopAudioSource(parent)` stops the first child `AudioSource` under that NetworkObject.** Footsteps
  parent their pooled loop to the player body — don't parent unrelated one-shots to the same
  NetworkObject or a stop (idle / footwear swap) can kill the wrong clip. Prefer `parent: null` for
  transient SFX (gunfire already does).
- **A process-wide DDOL subsystem outlives any one player body.** `AmbienceSubSystem` never gets
  destroyed/recreated across disconnect/respawn/map-reload the way a `NetworkSubSystem` on the hub
  does, so it clears its own `_lastResolvedAreaId`/`_currentTrackId` when `LocalPlayerObjectChanged`
  reports no body — otherwise ambience loops the last live area's track forever with nothing left to
  poll, and a fresh map's `AreaId`s can numerically collide with the old ones. `PersonalAudioSubSystem`
  sidesteps this the way `ScreenEffectsSubSystem` does: it never watches for disconnect itself — the
  driving consumer (`HumanHealthController.ClearScreenEffectsIfDriving`, same call site as the
  screen-effect clear) zeroes the intensity on ownership loss instead.

## Depends on / Used by

- **Depends on:** [area](area.md) (`AreaRecord.AmbienceTrackId`, `TryResolveAreaIdForWorldPosition`, `TryGetAmbienceTrackId`), [electricity](electricity.md) (`MachinePowerConsumer` gates Boombox), [entities](entities.md) (`LocalPlayerObjectChanged` — `ListenerPosition` / `AmbienceSubSystem`)
- **Used by:** [chat-audio-screens](chat-audio-screens.md) (shared domain until fully split); [health](health.md) (`HealthPersonalAudioMapper`, hit/gasp/blood via pool); [stamina](stamina.md) (`StaminaPersonalAudioMapper`); [inventory](inventory.md) (`AlertStackAudioMapper` / `MainHudSubSystem.PushAlertState`); [combat](combat.md) (gunfire/reload + `FleshHit`); [furniture](furniture.md) (airlock open/close/deny + boombox; structural-destruction ad-hoc emitters are consolidation candidates)

## Related docs

- Design (read-only): [Documents/design/audio.md](../../design/audio.md)
- Architecture effort: [2026-07_audio-foundation](../2026-07_audio-foundation.md); health feel clip registration also in [2026-07_health-env-feel](../2026-07_health-env-feel.md)
- Precedent: [2026-07_screen-space-effects](../2026-07_screen-space-effects.md) (client-local snapshot-mapper pattern)

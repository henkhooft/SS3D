> Code paths: Assets/Scripts/SS3D/Systems/Audio/
> Entry points: AudioSubSystem, AmbienceSubSystem
> Status: partial
> Verified: defdd0f6a — 2026-07-23

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
- `Assets/Scripts/SS3D/Systems/Audio/Boombox.cs` — diegetic jukebox: `MachinePowerConsumer`-gated,
  plays/stops Music through the pool (audio.md §5's in-round music source)
- `Assets/Scripts/SS3D/Systems/Audio/NoisyCollision.cs` — server-triggered positional collision SFX
  through the pool (audio.md §3 example); gains occlusion for free
- `Assets/Scripts/SS3D/Systems/Audio/ChangeMusicInteraction.cs`, `ListenerPosition.cs`, `AudioType.cs`
  — Boombox music-swap interaction, local-player `AudioListener` follow, `Sfx`/`Music`/`Ambient` enum
- `Assets/Scripts/SS3D/Systems/Audio/AmbienceHandler.cs` — **legacy, unwired, superseded.** Manual
  per-scene `_air`/`_windiness`/`_power` knobs and a global mixer lowpass "muffle" predating
  Area/electricity. Not per-Area, not driven by `AreaRecord.AmbienceTrackId`. Do not extend.
- `Assets/Scripts/SS3D/Systems/Audio/AmbienceSubSystem.cs` — **Phase 2 replacement** for
  `AmbienceHandler`: client-local per-Area ambience crossfade (audio.md §2). Self-bootstrapped by
  `SystemsBootstrap.EnsureProcessWideServices` (same pattern as `ScreenEffectsSubSystem`), not a
  scene/prefab placement. Polls the local player's world position every 0.5s via
  `AreaSubSystem.TryResolveAreaIdForWorldPosition`, and on an area change crossfades two
  non-positional (`spatialBlend = 0`) `AudioSource`s between the old and new
  `AreaSubSystem.TryGetAmbienceTrackId` clip (`AssetDatabases.Sounds` lookup, matching the pool's clip
  path). No occlusion (ambience isn't positional, per audio.md §2 vs §3).
- `Assets/Content/Systems/Audio/MainMixer.mixer` — `Ambience` / `SFX` / `Music` groups exist; a
  `Personal` group and per-group exposed Volume parameters are Phase 0/5 work (not yet done).
  `AmbienceSubSystem`'s sources currently output to Master (no runtime-loadable `AudioMixerGroup`
  reference exists yet for a self-bootstrapped, prefab-less subsystem) — route them once Phase 0 lands.
- `Assets/Content/Systems/Audio/SFXAudioSource.prefab`, `MusicAudioSource.prefab` — pool prefabs,
  routed to the `SFX` / `Music` mixer groups respectively

## Extension points

- Play a positional sound: `SubSystems.Get<AudioSubSystem>().PlayAudioSource(AudioType, clipId,
  position, parent, ...)` (server-only). Do not `Instantiate`/`AudioSource.Play` ad hoc for anything
  that should occlude — route it through the pool so `AudioSourceOcclusion` applies.
- Sound clips resolve via `Assets.Get<AudioClip>(AssetDatabases.Sounds, id)` — the single clip-lookup
  path; don't add a second loader.
- Ad-hoc `AudioSource` users outside the pool (`AirlockStateMachine`, `StructuralIntegrityPresenter`,
  `BlastExplosionEffect`, `BikeHorn`, `VendingMachineController`, `FuelPowerGenerator`) bypass
  occlusion today — consolidating them onto `PlayAudioSource` is content-roster work
  ([audio-foundation](../2026-07_audio-foundation.md), deferred).
- Author an area's ambience track: `AreaSubSystem.SetAreaAmbienceTrackId(areaId, trackId)` (server,
  thin — no Map Editor UI yet, dev-console/content driven). Syncs to observers via
  `RpcSyncAreaAmbience` (BufferLast), same pattern as the departmental-tint snapshot.
- **Not yet built:** power-gating power-dependent ambience tracks off `AreaLightingState` (needs a
  per-track "requires power" data field — deferred, see Pitfalls), personal heartbeat/breathing
  (Phase 3), alert cues (Phase 4), lobby music + volume-slider settings (Phase 5). See
  [audio-foundation](../2026-07_audio-foundation.md) for the phase plan.

## Pitfalls

- **Occlusion is client-local — do not gate it behind `[Server]`/RPC.** The listener position only
  exists on each client; baking a muffled/clear decision into the server RPC would require per-observer
  variants of every play call. `AudioSourceOcclusion.Update()` runs unconditionally on whichever machine
  owns the `AudioSource` instance (clients only, since the server never builds a pool).
- **Reuse `SS3D.Utils.LineOfSight`, do not fork a raycast.** Same shared occluder mask
  (`LayerMask.GetMask("Default")`) as Drop, combat LOS, and comms occlusion — solid geometry that
  should occlude sits on the `Default` layer in this project, not a dedicated `Walls` layer.
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
- **Ambience sources are pure-runtime `AudioSource`s with no mixer group assigned.** `AmbienceSubSystem`
  self-bootstraps with no prefab/scene placement, so there's no Editor-assigned
  `OutputAudioMixerGroup` reference to give them (unlike the pool's `SFXAudioSource.prefab` /
  `MusicAudioSource.prefab`, which are pre-wired in the Editor). They output to Master until Phase 0
  wires a runtime-loadable mixer group reference.
- **A process-wide DDOL subsystem outlives any one player body.** `AmbienceSubSystem` never gets
  destroyed/recreated across disconnect/respawn/map-reload the way a `NetworkSubSystem` on the hub
  does, so it must clear its own `_lastResolvedAreaId`/`_currentTrackId` when
  `LocalPlayerObjectChanged` reports no body — otherwise (a) ambience keeps looping the last live
  area's track forever with nothing to poll, and (b) a fresh map's `AreaId`s can numerically collide
  with the old ones, silently skipping the correct crossfade on respawn. Mirrors
  `HumanHealthController.ClearScreenEffectsIfDriving`: the driving consumer clears shared
  presentation state on ownership loss, not the shared subsystem watching for disconnect.

## Depends on / Used by

- **Depends on:** [area](area.md) (`AreaRecord.AmbienceTrackId`, `TryResolveAreaIdForWorldPosition`, `TryGetAmbienceTrackId`), [electricity](electricity.md) (`MachinePowerConsumer` gates Boombox), [player-control](player-control.md) (local player for `ListenerPosition` / `AmbienceSubSystem`'s `LocalPlayerObjectChanged`)
- **Used by:** [chat-audio-screens](chat-audio-screens.md) (shared domain until fully split); furniture/combat/structural-destruction ad-hoc `AudioSource` users (candidates for pool consolidation)

## Related docs

- Design (read-only): [Documents/design/audio.md](../../design/audio.md)
- Architecture effort: [2026-07_audio-foundation](../2026-07_audio-foundation.md)
- Precedent: [2026-07_screen-space-effects](../2026-07_screen-space-effects.md) (client-local snapshot-mapper pattern)

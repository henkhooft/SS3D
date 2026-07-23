> Code paths: Assets/Scripts/SS3D/Systems/Audio/
> Entry points: AudioSubSystem
> Status: partial
> Verified: 220ac4d48 — 2026-07-23

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
- `Assets/Scripts/SS3D/Systems/Audio/AmbienceHandler.cs` — **legacy, unwired.** Manual per-scene
  `_air`/`_windiness`/`_power` knobs and a global mixer lowpass "muffle" predating Area/electricity.
  Not per-Area, not driven by `AreaRecord.AmbienceTrackId`. Superseded by
  [audio-foundation](../2026-07_audio-foundation.md) Phase 2 — do not extend, replace per that plan.
- `Assets/Content/Systems/Audio/MainMixer.mixer` — `Ambience` / `SFX` / `Music` groups exist; a
  `Personal` group and per-group exposed Volume parameters are Phase 0/5 work (not yet done)
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
- **Not yet built:** per-area ambience crossfade (Phase 2), personal heartbeat/breathing (Phase 3),
  alert cues (Phase 4), lobby music + volume-slider settings (Phase 5). See
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
  power gating; Phase 2 replaces it wholesale rather than extending its `_air`/`_windiness` fields.

## Depends on / Used by

- **Depends on:** [area](area.md) (`AreaRecord.AmbienceTrackId`, lighting snapshot — Phase 2), [electricity](electricity.md) (`MachinePowerConsumer` gates Boombox), [player-control](player-control.md) (local player for `ListenerPosition`)
- **Used by:** [chat-audio-screens](chat-audio-screens.md) (shared domain until fully split); furniture/combat/structural-destruction ad-hoc `AudioSource` users (candidates for pool consolidation)

## Related docs

- Design (read-only): [Documents/design/audio.md](../../design/audio.md)
- Architecture effort: [2026-07_audio-foundation](../2026-07_audio-foundation.md)
- Precedent: [2026-07_screen-space-effects](../2026-07_screen-space-effects.md) (client-local snapshot-mapper pattern)

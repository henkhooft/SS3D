> Parent map: [audio.md](audio.md)
> Code paths: Assets/Content/Data/Databases/Sounds.asset, Assets/Content/Data/ObjectAssetReferences/, `*AudioTrackIds.cs` per domain
> Status: partial
> Verified: ee090d4f3 — 2026-07-26

# Audio content (clips and track ids)

## Overview

How sound clips are found, registered and named in code, plus which registered clips are wired to anything. Playback mechanics (pool, occlusion, ambience, personal audio) live in the parent [audio](audio.md) map.

## Start here

- `Assets/Content/Data/Databases/Sounds.asset` — the sound database; clips resolve via `Assets.Get<AudioClip>(AssetDatabases.Sounds, id)`, the single clip-lookup path
- Per-domain id classes name registered clips instead of inlining GUIDs:
  - `Systems/Audio/AudioTrackIds.cs` — personal audio (`Heartbeat`, `HeavyBreathing`, `AlertCue`)
  - `Systems/Audio/FootstepAudioTrackIds.cs` — Socks / Shoes / Boots under `Assets/Art/Sound/Entities/Humanoid/Footsteps/`
  - `Systems/Audio/AmbientLoopTrackIds.cs` — machine loops, with each `AmbientLoopKind`'s level and reach
  - `Systems/Area/LightSwitchAudioTrackIds.cs` — wall switch click (`LightSwitchController.Toggle`)
  - `Systems/Audio/VendorAudioTrackIds.cs` — vending dispense (`VendingMachineController.PlayVendSound`)
  - `Systems/Furniture/AirlockAudioTrackIds.cs` — airlock deny (`AirLockOpener.ServerPlayAccessDenied`); open/close use generated `Sounds.AirlockOpen` / `AirlockClose`
  - `Systems/Comms/CommsAudioTrackIds.cs` — `Announce.ogg` chime, optional `Welcome.ogg` follow-up
  - `CombatAudioTrackIds` (incl. `FleshHit`), `HealthAudioTrackIds` (gasp / choke / scream)
  - `Systems/Audio/ItemCollisionAudioTrackIds.cs` — clip sets per `ItemCollisionMaterial` (metal, heavy metal, can, wood, plastic, glass) for `NoisyCollision`
  - `Systems/StructuralDamage/StructuralAudioTrackIds.cs` — hit, strain and break sets per material, wreckage thud, blast near and far, under `Assets/Art/Sound/World/Destruction/`
- Third-party credits: [Sound ATTRIBUTIONS](../../../Assets/Art/Sound/ATTRIBUTIONS.md)

## Extension points

- **Find a clip first** in [art-asset-index.md](../../art-asset-index.md) → `art-available-for-import.json`. SS3D-Art has hundreds of unimported sounds, and some imported ones aren't registered yet.
- **Register a clip:** create an `ObjectAssetReference` under `Assets/Content/Data/ObjectAssetReferences/` with `Id = <clip asset GUID>` and `Database = AssetDatabases.Sounds`'s `DatabaseID`. Then add a matching `_key`/`_value` entry to `Sounds.asset`'s `Assets._list` (`_value` is `{fileID: 8300000, guid: <clip guid>, type: 3}`). Name it in the owning domain's `*AudioTrackIds` class. For a third-party import, add a row to ATTRIBUTIONS.
- **Registered but not wired (content decides):** `StationAmbience1-15`, `SpaceAmbience1-3`, `AirStagnant` and `WindHeavy/Light/Violent` are candidate `areaambience` track ids. `GrilleHit`, `Rod1` and `TrayHit1-2` aren't in any `NoisyCollision` set.
- **Ad-hoc `AudioSource` users that skip occlusion:** `AirlockStateMachine`, `StructuralIntegrityPresenter`, `BikeHorn`, `VendingMachineController`, `FuelPowerGenerator`. Moving the positional ones onto `PlayAudioSource` is deferred content work ([audio-foundation](../2026-07_audio-foundation.md)). UI chimes (`CommsFeedController` announce/welcome) stay client-local and non-spatial on purpose.

## Depends on / Used by

- **Depends on:** [data-codegen](data-codegen.md) (asset databases)
- **Used by:** every domain with an `*AudioTrackIds` class above

## Related docs

- Parent: [audio](audio.md)
- Design (read-only): [audio.md](../../design/audio.md) §2, §3, §10

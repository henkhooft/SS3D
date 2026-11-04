> Implements: Documents/design/audio.md §1 (wind, sound needs air), §2 (layered ambience), §3 (impacts, destruction, machines); Documents/design/area.md §2, §3, §5 (departments, ambience override); Documents/design/atmospherics.md §4 (an alarm alarms)
> Touches systems: audio, audio-content, area, structural-destruction, combat, atmospherics, electricity, map-editor-creative-hooks, persistence
> Status: planned

**Progress:** No phase started. Phase order is D, A, B, C, one PR each.

# Ambience and impact audio (Nov 2026)

The station sounds empty. Explosions, wall breaks and melee hits on structures make no world sound, machines are mostly silent, and no area has ambience. This effort adds those sounds in the order that pays off soonest: impacts first because they need no map authoring and C4 is silent in a Playtest 1 slice, room tone third because it depends on tagged areas. [2026-07_audio-foundation](2026-07_audio-foundation.md) built the mechanisms and keeps only its lobby music phase.

## Starting state (2026-11-04, `a2f75673d`)

| Area | Today |
|---|---|
| Explosion | Silent. `_boomClip` is empty in `Assets/Resources/BlastVfxCatalog.asset`; `BlastExplosionEffect` would play it through its own `AudioSource` |
| Wall or table breaking | Visuals only, through `TileSubSystem.RpcSteelBreakup` |
| Melee on a structure | No positional sound. `ServerNotifyMeleeConnectHit` only reaches the attacker |
| Bullet on a non-living target | The same six clips for every material (`CombatAudioTrackIds.SurfaceHit`) |
| Material data | `RpcStructuralImpact` carries a byte, but only for overlay-capable targets and only wood and glass tables differ from the default |
| Items hitting things | `NoisyCollision` exists; no prefab uses it |
| Machines | Air alarm and vents have real state and no sound. `FireAlarm.prefab` has no controller |
| Room tone | `AmbienceSubSystem` crossfades one looped clip per area. No area has a track |
| Authoring | `AmbienceTrackId` isn't saved. `areaambience` needs a raw GUID. Department is free text |
| Live changes | `PollCurrentArea` returns early when the area is unchanged, so a new track waits until the player leaves the room |
| Maps | `ministation` has one area. Metastation has 82 named `APC · ENGINEERING BAY`. No department tags on either |
| Atmos on clients | `FlowData` is CPU-side. Pressure is only a GPU texture in `AtmosRenderContext.Snapshot` |
| Attribution | `StationAmbience1–15` and `SpaceAmbience2` are SS14 copies with no rows in `ATTRIBUTIONS.md` |

`StationAmbience1` and `StationAmbience2` are byte-identical, so there are 14 distinct station clips.

## Decisions

| # | Decision |
|---|---|
| 1 | Destruction sounds play on clients from existing messages (`RpcStructuralImpact`, `RpcSteelBreakup`, the blast RPC), through a new client-side play path on the occluded pool. Breaks and blasts add no messages, which protects [test-server.md](../milestones/test-server.md) T3. Only a client can choose the debris nearest its listener. |
| 2 | `RpcStructuralImpact` fires for every landed hit, not only overlay-capable targets. Measure the traffic. A changed signature bumps `ProtocolVersion`. |
| 3 | One pure function derives metal, glass or wood from `genericType` and `specificType`. `TileObjectSo` gets an optional override for exceptions. Unknown objects are metal. |
| 4 | Machine loops use a client-local `AmbientLoopSource`, added by prefab recipes and driven by synced power or alarm state. Only the nearest 12 or so play. It reuses `AudioSourceOcclusion`. |
| 5 | Room tone lives in `AmbienceProfile` assets in a catalog: bed, occasional clips, a power-gated flag. Each has a readable id such as `engineering`, which also fixes the `areaambience` GUID problem. |
| 6 | `ParentTag` stays a string, validated against the fixed list from [area.md](../design/area.md) §2. Saved values match case-insensitively; anything else loads as no department. |
| 7 | A power-gated bed follows the Area's Equipment channel. `SyncedAreaLighting` is the existing client path and can carry it. |
| 8 | Clients get a CPU copy of pressure beside `FlowData`. Wind and pressure are sampled locally with no new messages. |
| 9 | Muffling is per source in `AudioSourceOcclusion`, because a mixer filter can't exempt sounds within one tile. Ambience takes the same factor. Personal audio doesn't. |
| 10 | Ad-hoc emitters that should muffle move onto the pool, including `BlastExplosionEffect` and the cracked-wall hiss. |
| 11 | Sound effects may be CC0, CC-BY, CC-BY-SA or CC-BY-NC, each with an `ATTRIBUTIONS.md` row. A commercial release would have to replace the NC clips. Custom-licensed music stays out. |

## Clips

| Use | First cut | License |
|---|---|---|
| Blast | SS14 `explosion1–3`, `explosionfar` | CC-BY-SA-3.0 |
| Metal break | SS14 `metal_break*`, `metal_crunch`, `metal_glass_break*` | CC-BY-NC-4.0 |
| Glass break | SS14 `glass_break*`, `window_shatter*` | CC-BY-SA-3.0 |
| Wood break | SS14 `wood_destroy*` | CC-BY-SA-3.0 |
| Item collisions | SS3D-Art `World/Collisions` metal, wood, plastic | Project assets |
| Machine loops | SS14 `Ambience/Objects` | Check per file |
| Engineering | `ambitech*`, `ambiatmos*` | CC-BY-SA-3.0 |
| Medical | `ambinice` | CC-BY-SA-3.0 |
| Maintenance | `ambimaint*`, `maintambience` | CC-BY-SA-3.0 |
| Morgue override | `ambimo*` | CC-BY-SA-3.0 |
| Everything else | `StationAmbience*` | CC-BY-SA-3.0 |
| Bed candidates | `PowerHum`, `AirStagnant`, SS14 `shipambience` | Check per file |
| Wind | `WindLight`, `WindHeavy`, `WindViolent`, SS14 `Effects/Weather/wind_*` | Check per file |

Confirm each license in SS14's `attributions.yml` at import, and confirm `metal_slam*` there before using it. The owner auditions beds in-game, because seams and repetition need a human ear.

## Phases

| Phase | Work | Verify |
|---|---|---|
| D1 | Material function. Client-side pool play. Hits, breaks and blasts play material sounds; Cracked adds strain; wreckage landing adds a thud. One boom with a capped debris tail; a distant variant by range. `BlastExplosionEffect` moves to the pool. Attribution rows, including the 16 missing ones. | EditMode for material and the debris cap; `mppm:breach`; targeted smoke |
| D2 | Thrown and dropped items through `NoisyCollision`, with a rate cap so a breach doesn't turn into clatter. | EditMode for the cap; `scenarios:Environment` |
| A | `AmbientLoopSource` on air alarms, vendors, APCs, SMES, vents, scrubbers, gas pumps and generators, power-gated and occluded. Confirm each has a real state and cut any that doesn't. The fire alarm is out until it has a controller. | EditMode for nearest-source selection; targeted smoke |
| B1 | Fixed department list and Map Editor dropdown. Starting areas get neutral names instead of the APC title. `AmbienceProfile` catalog, `SavedAreaRecord` override, friendly `areaambience`, live track changes. Generic bed for untagged areas. | EditMode for save round trip and profile lookup; map editor scenario |
| B2 | Bed plus occasional clips in `AmbienceSubSystem`, the thin air bed, Equipment-channel fading, then department profiles. Paint and tag `ministation`. | EditMode for the scheduler; owner audition on `ministation` |
| C | CPU pressure copy. Wind bed from `FlowData`. Per-source muffling with the one-tile exception. Space bed away from the hull, near-silence and breathing elsewhere. | EditMode for the muffle curve; `mppm:breach`; T3 8-client line unchanged |

Validate each phase with `Testing/ui/validate.sh --steps compile,editmode` plus the focused steps above ([testing philosophy](2026-11_testing-philosophy.md)). Prefab and asset work needs the Editor open with UnityMCP connected. Run `update-system-docs` as each PR lands.

## Not in this effort

- Lobby music, which stays in [2026-07_audio-foundation](2026-07_audio-foundation.md) Phase 5
- Tagging Metastation's 82 areas
- Sounds for lights, which stay silent by design
- Fire alarm behaviour
- Volume balancing beyond what's needed to audition ([audio.md](../design/audio.md) §10)

## Related docs

- Maps: [audio](systems/audio.md), [audio-content](systems/audio-content.md), [area](systems/area.md), [structural-destruction](systems/structural-destruction.md), [atmospherics](systems/atmospherics.md)
- Milestone: [mvp1-nuke-ops.md](../milestones/mvp1-nuke-ops.md) M1c, M8

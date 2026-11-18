---
name: Disk pinpointer
overview: MVP1 M14. Operatives and the Captain carry a pinpointer; holding it in either hand shows a HUD chip above the hands with an arrow and SS14's distance band toward the nearest nuclear authentication disk, computed on the server and sent only to the holder
todos:
  - id: pinpointer-rules
    content: "blind: pure rules + EditMode tests: planar bearing, bands (Reached ≤ 1.5 m or the disk's root carrier is the holder, Close ≤ 8 m, Medium ≤ 25 m, Far beyond, Unknown with no disk), nearest-disk pick, either-hand visibility, per-holder sequence check (stale bearing and stale stop dropped)"
    status: pending
  - id: pinpointer-component
    content: "blind: Pinpointer component on the item: server holder set driven by the hand containers' OnContentsChanged; 4 Hz TargetRpc of world planar yaw + band to holders only; start/stop RPCs carrying the NetworkObject id and sequence; fresh start on owner change (reconnect); nothing on disconnect/SSD; no disk position ever leaves the server; static examine line 'It is tracking: the nuclear authentication disk'"
    status: pending
  - id: hud-chip
    content: "blind: MainHud bottom-center chip above the hands strip (UXML/USS through the MainHud catalog, no scene edits): shown only while the local hand check finds a Pinpointer, Unknown face until the first bearing, arrow at worldYaw − cameraYaw every frame, centred dot for Reached; arrow glyph from icon-index.json, or a new SVG under Assets/Art/Icons/"
    status: pending
  - id: loadouts
    content: "blind: add the pinpointer to the OperativeLoadout and CaptainLoadout backpacks; ItemSizeWeightTable row (Small, 0.3); review the loadout tests for the extra item"
    status: pending
  - id: item-import
    content: "rig: add Locator to Tools/export_item_meshes.py ITEMS and export with Blender from ~/Dev/SS3D-Art-main; ImportedItemPrefab creates the Pinpointer prefab (keep a separate screen object as a named child if the model has one); Run Content Prefab Recipes; python3 Tools/generate_art_index.py"
    status: pending
  - id: rig-proof
    content: "rig: verify the disk's effective NetworkObserver conditions (prefab has none; note the result); PlayMode cases in NukeOpsScenarios (disk in a bag inside a backpack; disk loaded via LoadDiskAndArm); MPPM: a remote holder gets start, bearing and stop matching a server reference; visual: chip shows and hides"
    status: pending
  - id: docs-sync
    content: "blind: update-system-docs — inventory.md 'Design deviation from main-hud.md' paragraph for the chip (next to the existing main-hud deviation), gamemodes-roles-traits Device-deviations Pitfall (pinpointer now built), M14 status, catalog N3/C2; separately fix machine-interface.md's dangling '§ Fork deviation' link"
    status: pending
isProject: false
---

# Disk pinpointer

Written by the `autoplan` skill on 2026-11-18: three self-grill rounds with a blind questioner, ending with an empty frontier.

## Implements

- [mvp1-nuke-ops.md](../milestones/mvp1-nuke-ops.md) **M14**; [expected-content.md](../milestones/expected-content.md) **N3**, **C2** (owner Decision 2: `must`, a HUD compass while it's in hand).
- `Documents/design/antagonist-content.md` §6 first pass: "every operative carries a pinpointer that points at the authentication disk wherever it is".
- **Deviates from `main-hud.md` §4** by adding a chip to the bottom-center zone that §4's zone list doesn't name. Recorded in the inventory map, and proposed to the owner below.
- Keeps MVP1's "disk pinpointing beyond thin" deferral. Here "thin" means an arrow and a band toward the disk, and nothing more.

## Starting state

No pinpointer exists. The disk (`NuclearAuthenticationDisk.prefab`, a `NukeCard`) starts in the vault and can be loaded into the nuke (`Nuke.cs`). `NukeOpsRules` already walks containers to find who carries it. SS3D-Art has a `Locator` model that isn't imported yet. The disk prefab has no NetworkObserver of its own.

## Owner questions

None of these block the build. It follows each recommendation.

1. **The chip's place.** A small chip above the hands strip, shown only while a pinpointer is held. **Recommend** promoting it into `main-hud.md` §4 as a held-item readout of the bottom-center zone. → `hud-chip`
2. **M14 moves the Playtest 1 finish line.** It's a new slice, but it implements the first pass that `antagonist-content.md` §6 already specifies and your Decision 2. **Recommend** keeping it before Playtest 1. → none
3. **Feel numbers.** Bands Reached ≤ 1.5 m, Close ≤ 8 m, Medium ≤ 25 m; 4 Hz updates. **Recommend** these as first guesses, tuned at the hand test. → `pinpointer-rules`

## Proposed design rules

| # | Rule | Source | Belongs in |
|---|---|---|---|
| P1 | Holding a pinpointer in either hand shows a chip above the hands with an arrow and a distance band. | owner Decision 2; SS14 `pinpointer.yml` | `main-hud.md` §4 |
| P2 | Anyone holding it can read it; it tracks the nearest disk. | `antagonist-content.md` §4 "physical possession is the gate" | `antagonist-content.md` §6 |
| P3 | The Captain carries one in every mode. | SS14 `heads.yml` always stocks it | `antagonist-content.md` §6 |

## Decision log

| # | Decision | Tag | Source |
|---|---|---|---|
| 1 | Arrow plus SS14's bands, no metres | owner + SS14 | Decision 2; `pinpointer.yml` TargetDistance |
| 2 | Chip above the hands strip, hidden when not relevant | taste | `main-hud.md` §1, §4, §9 |
| 3 | Shown while in either hand; one chip for two; no toggle | owner + taste | Decision 2 |
| 4 | Anyone can read it; one prefab, Locator look | SS3D + SS14 + taste | `antagonist-content.md` §4; SS14 variants are cosmetic |
| 5 | Nearest `NukeCard`, re-picked each tick; gamemode-agnostic; Unknown with no disk | SS3D + taste | `SceneRegistry<NukeCard>`, `NukeOpsWorld` |
| 6 | Position from the disk's transform | SS3D | `AttachedContainer._attachItems` (default true) |
| 7 | Any range; planar only | SS3D | `vision.md` §3 |
| 8 | Band thresholds; Reached includes "carried by the holder" via the container walk | taste + SS3D | `NukeOpsRules` |
| 9 | Shown while Downed; drops with the item in Critical or death; hidden with MainHud | SS3D | `health.md` §4; `HealthSimulation.cs:319/344`; `MainHudSubSystem.ApplyVisibility` |
| 10 | Static examine line, no band | SS14 | `pinpointer.ftl` |
| 11 | Operatives and Captain carry it in the backpack, in every mode | SS3D + SS14 + adaptation | `antagonist-content.md` §6; `nukeops.yml`; `heads.yml` |
| 12 | Server computes; holder-only TargetRpc of yaw + band at 4 Hz; no position leaves the server | SS3D + adaptation + taste | `networking.md` §10; gamemodes grid-observer Pitfall |
| 13 | Holder set from hand `OnContentsChanged`; fresh start on reconnect | SS3D | `networking.md` §3 |
| 14 | Client draws `worldYaw − cameraYaw` per frame | adaptation | — |
| 15 | The local hand check gates the chip; sequence numbers drop stale messages | adaptation | — |
| 16 | MainHud element, no scene edits | SS3D | `ui-shell.md`; agent-first composition |
| 17 | Verify the disk's effective observers (changes only a note) | SS3D | disk prefab |
| 18 | Locator import through `export_item_meshes.py`; Small, 0.3 | SS3D | inventory map; `ItemSizeWeightTable.cs` l.88–90 |
| 19 | Tests reuse `NukeOpsScenarios` | SS3D | `NukeOpsScenarios.cs` |
| 20 | Deviation recorded in `inventory.md`'s "Design deviation" paragraphs | SS3D | AGENTS.md; Documents/SKILL.md |
| 21 | New slice M14 before Playtest 1 | owner + SS3D | Decision 2; `antagonist-content.md` §6 |
| 22 | Out of scope: arrow on the model, toggle and sound, other targets, universal pinpointer, Syndicate look | SS3D | — |

## Phases

**Phase 1 (blind):** `pinpointer-rules` first, then `pinpointer-component`, `hud-chip` and `loadouts` in parallel, then `docs-sync`. The loadouts reference the prefab that `item-import` creates. If it doesn't exist yet, `loadouts` lands its table row and test changes and wires the asset in Phase 2.

**Phase 2 (rig):** `item-import` (Blender and SS3D-Art are on the owner's machine), finishing `loadouts`, then `rig-proof`. Then the owner's hand test of the bands.

## Done when

Every operative and the Captain spawn with a pinpointer, and holding it in either hand shows a chip above the hands with an arrow and a distance band (Close, Medium, Far, or Reached when the disk is within 1.5 m or carried by the holder), pointing at the disk wherever it is (pocket, nested bag, locker, loaded in the nuke) and turning with the camera; a remote client holding it sees the same.

| Check | Lane | Tier |
|---|---|---|
| Bearing, bands, nearest disk, either-hand rule, stale messages | blind | EditMode |
| The band is right with the disk in a nested bag and loaded in the nuke | rig | PlayMode (`NukeOpsScenarios`) |
| A remote holder gets start, bearing and stop | rig | MPPM |
| The chip shows and hides, and turns with the camera | rig | visual |
| Both loadouts carry it | blind | EditMode loadout test |
| The bands feel right | rig | owner hand test |

## Risks

1. **The Locator export.** The model may need a rest pose or icon fix (inventory Pitfalls for long items). It shows up in the first `ImportedItemPrefab` run.
2. **MainHud layout.** The chip may collide with the gear strip at small resolutions. Check it in the visual step.
3. **Camera yaw source.** If the camera rig has no single yaw, the arrow drifts. The PlayMode check rotates the camera and asserts the arrow.

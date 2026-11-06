> Parent map: [structural-destruction.md](structural-destruction.md)
> Code paths: Assets/Scripts/SS3D/Systems/StructuralDamage/
> Status: partial
> Verified: 7785df8dc — 2026-11-06

# Explosive items

## Overview

Implements [explosives-destruction.md §6–§7](../../design/explosives-destruction.md#6-explosive-items). The three items share `TimedCharge`; only the server arms, defuses and detonates them. `ExplosiveKind` selects the content. Protocol 28 covers the new behaviour state and the small-blast sound flag. Persistence format is unchanged.

| Item | Fuse | Gameplay |
|---|---|---|
| C4 | 15 seconds | Place and arm from its menu, or hold and use on a wall/door to attach and arm. Radial yield 180, falloff 45. |
| X4 | 15 seconds | Hold and use on a wall/door to attach and arm. Radial yield 240/falloff 60, plus yield 180/falloff 40 in a 90-degree cone through the far side of its target. |
| Syndicate minibomb | 5 seconds | Arm in hand using Use, or choose Throw minibomb, then a landing point. Throwing pulls the pin if needed; an already running fuse is preserved. Small radial yield 90/falloff 45. |

Floor and plenum damage is applied after all blast passes; see [floor and hull damage](structural-destruction-floors.md).

The values above are provisional content tuning. X4's additional force uses the same BFS, structural apply path and crew damage as radial blasts; it still stops at surviving structure. Both passes produce one audiovisual detonation. The direction is captured from the attached surface, pointing into the target. Picking up an attached charge dislodges it: its blast then follows the item, without the attached X4 cone.

C4/X4 have physical LED countdowns and a three-second wirecutter defuse. Movement, range loss or losing the tool cancels; expiry wins a same-frame defuse. Minibombs cannot be defused. Throws use server Rigidbody physics, cap the target distance at 10 m and start clear of nearby walls. No new throw key or permanent HUD surface is added.

`Effects/minibombcountdown.ogg` is imported as `ExplosiveCountdown.ogg` for the five-second minibomb. C4/X4 use `ChargeCountdown.ogg`, a fifteen-second version with ten steady opening cues followed by the original accelerating five-second finale. One non-looping recording starts at arming; a client seeks from the replicated deadline when it first observes an armed charge. Playback follows the item through the SFX pool with normal occlusion. Defuse/despawn stops and detaches that voice. C4/X4 reuse registered SS14 `explosion1.ogg`; minibombs use `explosion_small1.ogg`. Distant listeners retain the existing blast rumble.

Remote triggers, a proximity alert, other grenades and separate-client acceptance remain outstanding. Moving airlock leaves do not carry the attached model; attachment is fixed at the selected world contact.

## Start here

- `Assets/Scripts/SS3D/Systems/StructuralDamage/TimedCharge.cs` — profiles, server fuse, arm/attach/throw/defuse interactions and local countdown voice
- `Assets/Scripts/SS3D/Systems/StructuralDamage/ChargeCountdownDisplay.cs` — physical LED segments
- `Assets/Scripts/SS3D/Systems/StructuralDamage/Editor/ExplosivePrefabSetup.cs` — repeatable FBX mesh bake, prefabs, item/sound database registration and examine text
- `Assets/Scripts/SS3D/Systems/StructuralDamage/StructuralDamageSubSystem.cs` — radial blast and `ResolveShapedCharge`
- `Assets/Scripts/SS3D/Systems/StructuralDamage/BlastResolutionService.cs` — common BFS and `ResolveCone`
- `Assets/Scripts/SS3D/Systems/StructuralDamage/StructuralAudioPresenter.cs` — near/small/far explosion sound selection

## Extension points

- Run **SS3D → Structural Damage → Run Content Prefab Recipes** to rebuild content. It creates `C4`, `X4`, and `SyndicateMiniBomb` under `Assets/Content/WorldObjects/Items/Weapons/` and registers their item refs/Addressables.
- The admin console can give any of them with `spawninhand <name>`. Map Editor uses the same registered items.
- New sound files belong under `Assets/Art/Sound/Items/Weapons/Explosive/SS14/`, with source/licence rows in Sound ATTRIBUTIONS. Registration lives in the recipe.

## Pitfalls

- **RPC discards the client normal:** attachment must raycast the target's colliders again from the interaction origin; using `InteractionEvent.Normal` alone works in discovery and fails server revalidation.
- **A completed pooled voice can already belong to a new sound:** stop/detach the countdown only while its source is still parented to that charge, or despawn can cut off the explosion that reused it.
- **Held/attached items have special poses:** apply the wall contact after `PlaceHeldItemOutOfHand` (its helper lifts floor drops), keep the collider selectable, and use the server-authoritative item NetworkTransform.
- **FBX root scale/axis conversion is part of the pose:** bake the source's full `localToWorldMatrix` into the mesh. Dropping the imported root makes a tiny upright charge.

## Tests

- `TimedChargeTests`: profile fuse lengths, physical model size, LED presence, registered countdown clips, defuse tool and expiry gates, interface priority.
- `BlastResolutionTests`: directional force reaches behind a breached target, excludes rear/side tiles and stops at a surviving barrier.
- Host acceptance exercises minibomb arming/throw/fuse preservation and X4 attachment, countdown seek and target breach. Separate-client/late-observer acceptance remains pending.
- Run `Testing/ui/validate.sh --steps compile,editmode`; visual changes also require `visual` plus inspecting item renders.

## Depends on / Used by

- **Depends on:** [interactions-framework](interactions-framework.md), [inventory](inventory.md), [audio](audio.md), [tile](tile.md)
- **Used by:** [structural-destruction](structural-destruction.md)

## Related docs

- [Station structural damage plan](../../plans/station_structural_damage.plan.md)
- [MVP1 combat core](../../milestones/mvp1-nuke-ops.md)

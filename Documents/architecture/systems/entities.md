> Code paths: Assets/Scripts/SS3D/Systems/Entities/
> Entry points: EntitySubSystem, MindSubSystem, HumanoidBodyStateMachine
> Status: partial
> Verified: b00382c9c — 2026-07-20

# Entities

## Overview

Humanoid/silicon entity spawning, minds, and join/round ordering with [rounds-lobby](rounds-lobby.md). Humanoid body animation is driven by a packed `BodyAnimationSnapshot` SyncVar, not ad-hoc Animator calls.

**Prefab composition debt:** `Human.prefab` is a mega-prefab (~15k lines, ~120 script refs). Do not hand-add features on it. Target is a thin visual/network anchor; health Phase 0d is strip-and-rewire, not grow. See [2026-07_agent-first-composition.md](../2026-07_agent-first-composition.md).

**Body presentation debt:** collapse / death / ragdoll / walk-cycle ownership is fragmented across Health, `Ragdoll`, `AnimationOrchestrator`, body-state bridge, and movement. Interim collapse APIs exist; **do not add another path** — refactor per [2026-07_body-presentation-authority.md](../2026-07_body-presentation-authority.md).

## Start here

- `Assets/Scripts/SS3D/Systems/Entities/EntitySubSystem.cs` — entity spawn/management
- `Assets/Scripts/SS3D/Systems/Entities/MindSubSystem.cs` — mind/player mind assignment
- `Assets/Scripts/SS3D/Systems/Entities/Humanoid/Body/HumanoidBodyStateMachine.cs` — authoritative body/combat snapshot
- `Assets/Scripts/SS3D/Systems/Entities/Humanoid/Body/AnimationOrchestrator.cs` — snapshot → Animator; Melee Upper Body weight; `SetPosingSuppressed` for collapse
- `Assets/Scripts/SS3D/Systems/Entities/Humanoid/Ragdoll.cs` — knockdown / death collapse visuals (`ApplyCollapseVisuals`)
- `Assets/Scripts/SS3D/Systems/Entities/Humanoid/Body/HumanoidIkController.cs` — combat look-at; torso IK off during Attack Swing
- `Assets/Scripts/SS3D/Systems/Entities/Humanoid/Body/HumanoidBodyStateBridge.cs` — holds, stance, limp + `InjuredLeg` / arms, rare hurt Emote, `MirrorUpperBody`
- `Assets/Content/WorldObjects/Entities/Humanoids/Human/HumanCharacterAnimator.controller` — Peaceful/Melee/Ranged/Injured blends + limp oneshots
- `Assets/Scripts/SS3D/Editor/HumanoidLocomotionBlendSetup.cs` — **SS3D → Animation → Rebuild Combat Stance Blend Trees**
- `Assets/Scripts/SS3D/Systems/Inventory/Containers/Hand.cs` — `HandSide` on left/right hand prefabs (Upper Body mirror)

## Extension points

- Stance packs: Peaceful (Locomotion), Melee (Pro Melee Axe), Ranged (Basic Shooter), Injured (Male Injured Pack). Rebuild after reimporting Mix_* clips.
- Shelved clips (not wired): `Assets/Art/Animations/Misc/`, `Assets/Art/Animations/Probably Not/` — future collapse / cough / crawl / drag content.
- `HumanoidCombatMode` is 2 bits; `C` toggles Peaceful ↔ inventory-derived combat. `LimpSide != 0` → Injured locomotion; `InjuredLeg` drives idle severity + additive weight.
- **Animator vs code:** swing exit times, limp transitions, masks are animator-owned ([animation-polish](../2026-07_animation-polish.md)). Code sets parameters/triggers and look-at only — no swing duration constants.
- **Collapse / death:** `Ragdoll.ApplyCollapseVisuals` / death reinforce RPCs until body-presentation authority ships.

## Pitfalls

- **Ghost spawn stack-overflow:** `HumanoidGhostController.OnAwake` must call `base.OnAwake()`, never `base.Awake()`.
- **Do not redeclare `_bodyStateMachine` on `HumanoidGhostController`:** field already on `HumanoidController`; use `BodyStateMachine` from the base.
- **Walk cycle while “collapsed”:** Coimbra `UpdateEvent` keeps firing after `enabled=false`; limp bridge can still publish snapshots. Use `SetPosingSuppressed` + shared collapse visuals — see [body-presentation-authority](../2026-07_body-presentation-authority.md).
- **`Ragdoll.OnDisable` must not `Recover()`:** ownership/network teardown would stand a corpse back into locomotion.
- **Melee swing torso fight:** Upper Body mask must include spine/chest; head stays unmasked for look-at. Do not reintroduce C# swing duration timers — use AttackSwing + `AttackVariant` (0–2: horizontal / downward / backhand).
- **Left-hand Mixamo mirror:** Upper Body Hold* / Attack Swing* use `MirrorUpperBody` (`Hand.Side`). Do not mirror Base Layer FreeformCartesian — that flips strafes. Set `_side` on `HumanHandLeft`/`HumanHandRight`, not mega `Human.prefab`.
- **Injured oneshots:** Jump / Turn90 / Emote while limping → Injured Jump / Turn / Wave (Base Layer); healthy oneshots require `LimpSide == 0`. **Jump/Turn90 are not bound in `Controls.inputactions`** and nothing calls `PlayLocomotionTrigger` yet — presentation only.
- **Combat walk→run surge:** predicted movement must ease world speed and anim `VelZ` together (`GetAnimSpeedForScale`). Do not let `ProcessPlayerInput` publish snapped Speed while predicted movement owns loco.
- **Batch rebuild while Editor open:** run the rebuild menu, or drop a **user-writable** `artifacts/force-rebuild-animator.flag` (root-owned flags fail to delete and skip rebuild). Batchmode cannot open a held project.

## Depends on / Used by

- **Used by:** [rounds-lobby](rounds-lobby.md), [player-control](player-control.md), [health](health.md), [combat](combat.md)

## Related docs

- [2026-07_animation-polish](../2026-07_animation-polish.md) — **shipped** melee/limp/mirror/severity polish
- [2026-07_body-presentation-authority](../2026-07_body-presentation-authority.md) — **planned** collapse/death presentation refactor
- [2026-07_player-body-animation](../2026-07_player-body-animation.md)
- [2026-07_agent-first-composition](../2026-07_agent-first-composition.md) (prefab debt)
- [animation_system_design plan](../../plans/animation_system_design_250de599.plan.md)
- [INDEX.md](../INDEX.md)

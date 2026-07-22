> Code paths: Assets/Scripts/SS3D/Systems/Entities/
> Entry points: EntitySubSystem, MindSubSystem, HumanoidBodyStateMachine
> Status: partial
> Verified: 39135e03b — 2026-07-22

# Entities

## Overview

Humanoid/silicon entity spawning, minds, and join/round ordering with [rounds-lobby](rounds-lobby.md). Humanoid body animation is driven by a packed `BodyAnimationSnapshot` SyncVar, not ad-hoc Animator calls.

**Prefab composition debt:** `Human.prefab` is a mega-prefab (~15k lines, ~120 script refs). Do not hand-add features on it. Target is a thin visual/network anchor; health Phase 0d is strip-and-rewire, not grow. See [2026-07_agent-first-composition.md](../2026-07_agent-first-composition.md) and the scheduled paydown [2026-07_human-prefab-decomposition.md](../2026-07_human-prefab-decomposition.md) (planned).

**Body presentation debt:** collapse / death / ragdoll / walk-cycle ownership is fragmented across Health, `Ragdoll`, `AnimationOrchestrator`, body-state bridge, and movement. Interim collapse APIs exist; **do not add another path** — refactor per [2026-07_body-presentation-authority.md](../2026-07_body-presentation-authority.md).

## Start here

- `Assets/Scripts/SS3D/Systems/Entities/EntitySubSystem.cs` — entity spawn/management; `ServerSpawnCombatDummy` for mindless test Humans
- `Assets/Scripts/SS3D/Systems/Entities/MindSubSystem.cs` — mind/player mind assignment
- `Assets/Scripts/SS3D/Systems/Entities/Humanoid/Body/HumanoidBodyStateMachine.cs` — authoritative body/combat snapshot
- `Assets/Scripts/SS3D/Systems/Entities/Humanoid/Body/AnimationOrchestrator.cs` — snapshot → Animator; Melee Upper Body weight; `SetPosingSuppressed` for collapse
- `Assets/Scripts/SS3D/Systems/Entities/Humanoid/Ragdoll.cs` — knockdown / death collapse visuals (`ApplyCollapseVisuals`)
- `Assets/Scripts/SS3D/Systems/Entities/Humanoid/Body/HumanoidIkController.cs` — combat look-at; torso IK off during Attack Swing
- `Assets/Scripts/SS3D/Systems/Entities/Humanoid/Body/HumanoidBodyStateBridge.cs` — holds, stance, limp + `InjuredLeg` / arms, rare hurt Emote, `MirrorUpperBody`; must not pose while collapsed
- `Assets/Content/WorldObjects/Entities/Humanoids/Human/HumanCharacterAnimator.controller` — Peaceful/Melee/Ranged/Injured blends + limp oneshots
- `Assets/Scripts/SS3D/Editor/HumanoidLocomotionBlendSetup.cs` — **SS3D → Animation → Rebuild Combat Stance Blend Trees**
- `Assets/Scripts/SS3D/Systems/Inventory/Containers/Hand.cs` — `HandSide` on left/right hand prefabs (Upper Body mirror)
- Combat test dummy: [combat](combat.md) (`spawndummy` / `CombatDummyBootstrap`) — reuses Human prefab, no mind, do not grow `Human.prefab`
- `Assets/Scripts/SS3D/Systems/Entities/Editor/HumanPrefabRecipes.cs` — **SS3D → Entities → Run All Human Prefab Recipes**, the single entry point for every `Human.prefab`-targeting recipe tool (dev-hack removal, ContainerInteractive strip, hands wiring); add new Human-prefab recipes here rather than leaving them as undiscoverable one-off menu items

## Extension points

- Stance packs: Peaceful (Locomotion), Melee (Pro Melee Axe), Ranged (Basic Shooter), Injured (Male Injured Pack). Rebuild after reimporting Mix_* clips.
- Shelved clips (not wired): `Assets/Art/Animations/Misc/`, `Assets/Art/Animations/Probably Not/` — future collapse / cough / crawl / drag content.
- `HumanoidCombatMode` is 2 bits; **`C` toggles Help/Harm intent** (combat stance follows Harm via `InteractionController`). Inventory still picks Melee vs Ranged while in combat. `LimpSide != 0` → Injured locomotion; `InjuredLeg` drives idle severity + additive weight.
- **Animator vs code:** swing exit times, limp transitions, masks are animator-owned ([animation-polish](../2026-07_animation-polish.md)). Code sets parameters/triggers and look-at only — no swing duration constants.
- **Collapse / death:** `Ragdoll.ApplyCollapseVisuals` / death reinforce RPCs until body-presentation authority ships.

## Pitfalls

- **Ghost spawn stack-overflow:** `HumanoidGhostController.OnAwake` must call `base.OnAwake()`, never `base.Awake()`.
- **Do not redeclare `_bodyStateMachine` on `HumanoidGhostController`:** field already on `HumanoidController`; use `BodyStateMachine` from the base.
- **Walk cycle while “collapsed”:** Coimbra `UpdateEvent` keeps firing after `enabled=false`; limp bridge can still publish snapshots. Use `SetPosingSuppressed` + shared collapse visuals — see [body-presentation-authority](../2026-07_body-presentation-authority.md).
- **`Ragdoll.OnDisable` must not `Recover()`:** ownership/network teardown would stand a corpse back into locomotion.
- **Melee swing torso fight:** Upper Body mask must include spine/chest; head stays unmasked for look-at. Do not reintroduce C# swing duration timers — use AttackSwing + `AttackVariant` (0–2: horizontal / downward / backhand).
- **Left-hand Mixamo mirror:** Upper Body Hold* / Attack Swing* use `MirrorUpperBody` (`Hand.Side`). Do not mirror Base Layer FreeformCartesian — that flips strafes. Set `_side` on `HumanHandLeft`/`HumanHandRight`, not mega `Human.prefab`.
- **Every body part is its own nested `NetworkObject`, not a flat component list:** `HumanTorso`/`HumanHead`/each limb prefab under `HumanBodyParts/` carries its own `NetworkObject` with `IsNested = true`. The root `NetworkObject`'s `_networkBehaviours` flattens across those nested boundaries (it lists behaviours living inside body-part prefabs directly). A recipe tool that reuses `StorageContainerPrefabSetup`-style behaviour collection (which stops descending at any child with a `NetworkObject`) will silently drop every nested body-part behaviour from the rebuilt list — only stop at a child Nob when `!IsNested`. See `HumanPrefabHygiene.CollectNetworkBehaviours` ([2026-07_human-prefab-decomposition.md](../2026-07_human-prefab-decomposition.md)).
- **Stale `m_Script` on stripped nested-prefab mirrors is cosmetic, not a broken reference:** a `stripped` MonoBehaviour placeholder's authoritative type comes from `m_CorrespondingSourceObject` in the source prefab, not its own cached `m_Script` GUID — the cache can go stale (and even reference a since-deleted class) after the source prefab's component type changes without Human.prefab being resaved in the Editor. Fix by updating the cached GUID to match the source's current type; do not delete the stripped block — it is a live entry in the root `NetworkObject`'s `_networkBehaviours` array and deleting it (versus correcting it) shifts every subsequent behaviour's index.
- **Editing a nested body-part prefab asset directly leaves `Human.prefab`'s own mirror of it stale:** running a recipe that destroys a component on `HumanHead.prefab`/`HumanTorso.prefab` (e.g. `BodyPartContainerInteractiveStrip`) does not retroactively update `Human.prefab`'s stripped mirror of that instance — that only refreshes the next time `Human.prefab` itself is reloaded and resaved. `HumanPrefabRecipes.RunAllMenu` always calls `HumanPrefabHygiene.ResyncNestedPrefabInstances()` last, regardless of what the other recipes changed, specifically to converge this. Any new recipe that removes a component from a nested body-part prefab must trigger the same resync.
- **Injured oneshots:** Jump / Turn90 / Emote while limping → Injured Jump / Turn / Wave (Base Layer); healthy oneshots require `LimpSide == 0`. **Jump/Turn90 are not bound in `Controls.inputactions`** and nothing calls `PlayLocomotionTrigger` yet — presentation only.
- **Combat walk→run surge:** predicted movement must ease world speed and anim `VelZ` together (`GetAnimSpeedForScale`). Do not let `ProcessPlayerInput` publish snapped Speed while predicted movement owns loco.
- **Batch rebuild while Editor open:** run the rebuild menu, or drop a **user-writable** `artifacts/force-rebuild-animator.flag` (root-owned flags fail to delete and skip rebuild). Batchmode cannot open a held project.
- **Never assign injury SyncVars on pure clients:** `HumanoidBodyStateBridge` runs `Update` everywhere and calls `SetInjuredArms`/`SetInjuredLeg`. Those SyncVars are server-only — writing them on a client spam-logs FishNet `Cannot complete operation as server when server is not active` (thousands/sec after embark). Guard with `IsServer` before assigning; clients apply via SyncVar OnChange.

## Depends on / Used by

- **Used by:** [rounds-lobby](rounds-lobby.md), [player-control](player-control.md), [health](health.md), [combat](combat.md)

## Related docs

- [2026-07_animation-polish](../2026-07_animation-polish.md) — **shipped** melee/limp/mirror/severity polish
- [2026-07_body-presentation-authority](../2026-07_body-presentation-authority.md) — **planned** collapse/death presentation refactor
- [2026-07_player-body-animation](../2026-07_player-body-animation.md)
- [2026-07_agent-first-composition](../2026-07_agent-first-composition.md) (prefab debt)
- [2026-07_human-prefab-decomposition](../2026-07_human-prefab-decomposition.md) — **planned** Human.prefab paydown
- [animation_system_design plan](../../plans/animation_system_design_250de599.plan.md)
- [INDEX.md](../INDEX.md)

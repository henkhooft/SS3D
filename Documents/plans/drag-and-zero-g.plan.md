---
name: Dragging bodies and zero-g movement
overview: MVP1 M13. Drag Downed, Critical and dead bodies (and furniture) on a rope that turns corners and holds airlocks; move in zero-g by traction near structures, braking and throw-to-propel; a knockdown over open space recovers into floating instead of an endless ragdoll
todos:
  - id: tether-rules
    content: "blind: pure static rules + EditMode tests: rope target point (dragger→target line, rest 1.0 m bodies, start distance clamped to 1.5 m furniture), break rule (server dragger-to-target > 2.5 m for 1 s), who may be dragged (Downed/Critical/dead only), throw Δv curve, reach classification (static tile geometry yes; lockers, items, bodies no)"
    status: completed
  - id: space-recovery
    content: "blind: Ragdoll — a wind/blast knockdown that expires over open space recovers into upright Floating (drop the floor gate at Ragdoll.cs ~l.209 for that case); while Floating, ServerApplyWind never knocks down"
    status: completed
  - id: coast-impulse
    content: "blind: HumanoidLivingController — AddCoastImpulse(Vector3) as the single writer into _coastVelocity, capped at run speed; Ragdoll.TickWind calls it instead of MoveUpright while Floating; WASD traction within reach (25% accel, walk cap) and 0.5 s braking on release, via an owner-side overlap on the tile-collider layer"
    status: completed
  - id: throw-propel
    content: "blind: CmdThrow resolves Δv for a Floating thrower and TargetRpcs it to the owner, which calls AddCoastImpulse"
    status: completed
  - id: body-drag-core
    content: "blind: body drag component (generalised Draggable or BodyDraggable): server computes the tether point and routes it to the target's physics authority (owner while alive, server for corpses and disconnected owners); CC/Downed mover with collision; ragdoll hips force for Critical/dead; re-send when > 1.5 m off; server-set IsBeingDragged snapshot bit (InteractWork pattern); Downed owner skips the crawl drive while it's set; end rules wired (dragger downed/knocked/dead, break, despawn, target stands, either party Floating, release)"
    status: completed
  - id: drag-interaction
    content: "blind: body-drag interaction filtered out (not greyed) on standing and knockdown bodies, 60° cone to the hips bone; no sprint while dragging, walk cap"
    status: completed
  - id: hand-reserve
    content: "blind: dragging hand refuses CanContainItem and shows the reserved badge (TwoHandedWeaponRules pattern); Drop on the dragging hand releases in either intent (bypass the Help-only gate for this case only); clicking the badge releases"
    status: completed
  - id: furniture-sweep
    content: "blind: Draggable furniture follows the rope instead of a fixed world offset; each server step sweeps its collider (SweepTest/BoxCast) against static geometry and closed door solids and stops at the hit"
    status: pending
  - id: airlock-hold
    content: "blind: AirLockOpener.IsDoorwayBlocked also counts dragged objects and dragged corpses, not only SpawnedPlayers"
    status: pending
  - id: drag-command
    content: "blind: console command `drag <dragger> <target>` running the interaction's server path, for scenarios, MPPM and the smoke line"
    status: pending
  - id: human-recipe
    content: "rig: tier-B PrefabUtility recipe adds the body-drag component to Human.prefab, registered on the domain Run All aggregator; never hand-edit the prefab"
    status: pending
  - id: rig-proof
    content: "rig: PlayMode scenarios (extend EnvironmentScenarios): knockdown over space → Floating, wind push without knockdown, push-off back to plenum, throw while Floating gives Δv, wall/window/grille/airlock colliders on the reach layer; MPPM: remote drag through an airlock, remote float, owner disconnect mid-drag; run_smoketest.sh separate-client line for the remote drag"
    status: pending
  - id: docs-sync
    content: "blind: update-system-docs — entities, furniture (sweep Pitfall), interactions maps; M13 status; catalog U7/U31; animation-pass drag note (lockers draggable, IsDragging reads the SyncVar)"
    status: pending
isProject: false
---

# Dragging bodies and zero-g movement

Written by the `autoplan` skill on 2026-11-18: four self-grill rounds with a blind questioner.

## Implements

- [mvp1-nuke-ops.md](../milestones/mvp1-nuke-ops.md) **M13**; [expected-content.md](../milestones/expected-content.md) **U7**, **U31** (owner Decision 1).
- `Documents/design/health.md` §4: Downed bodies can be dragged by others.
- `Documents/design/armor.md` §3: magboots work over floor, "do nothing over open space".
- `Documents/design/atmospherics.md` §2: wind pushes people toward low pressure.
- **Deviates from `armor.md` §8**, which lists moving through open space as out of scope. The owner brought it in on 2026-11-18. The rules are proposals below.
- **Deviates from `combat.md` §5** for this pass: "a grab" during a knockdown window is deferred, because the M13 Done-when line doesn't need it.

## Starting state

- **Drag.** `Draggable` (`Assets/Scripts/SS3D/Systems/Furniture/Draggable.cs`) is on the unbolted girder, the Syndicate bomb and three locker prefabs. It holds a fixed world offset from the dragger, moved on the server. `HumanoidLivingController.IsDragging` reads the `Dragged` SyncVar. No body can be dragged.
- **Zero-g.** Over a tile with no plenum, a living body floats and coasts with WASD off (`HumanoidLivingController.TryProcessSpaceFloat`). A wind knockdown recovers only with floor support (`Ragdoll.cs` ~l.209), so a body blown over open space ragdolls forever. That's the owner's "endless ragdoll".

## Owner questions

**The owner accepted every recommendation on 2026-11-18.** Questions 1–4 are settled as recommended; question 5 (promoting the design rules) is the owner's own follow-up.

1. **Drag escape by an unresponsive or modified client.** The dragged body's own client moves it (server authority is kept by re-sending the target). A client that ignores the target drifts off and the snag rule ends the drag. **Recommend accepting this for Playtest 1** and revisiting with a swept server move if players abuse it. → `body-drag-core`
2. **A stranded spacewalker.** A suited player who drifts off with nothing to throw stays stuck for the round. **Recommend accepting it** as a real SS13 death, since vacuum or air runs out eventually. → `coast-impulse`
3. **Dragging standing players and knocked-down bodies.** SS14 allows both, and `combat.md` §5 mentions "a grab". **Recommend deferring both** to the handcuffs work (Decision 3). → `drag-interaction`, `tether-rules`
4. **Feel numbers.** Traction 25% of walking acceleration, walk top speed and 0.5 s braking; reach 1.5 m; throw Δv = clamp(1.0 + 1.5 × kg, 1.0, 2.5) m/s; drag at walk speed; break at 2.5 m for 1 s. **Recommend these as first guesses**, tuned at the hand test. → `tether-rules`, `coast-impulse`
5. **Promote the proposed design rules** below into a design doc, and lift the `armor.md` §8 exclusion. **Recommend a "Moving in zero gravity" subsection** in `armor.md` §3, and the drag rules in `health.md` §4. → none (docs)

## Proposed design rules

Design docs are owner-only, so these are the plan's assumptions until the owner promotes them.

| # | Rule | Source | Belongs in |
|---|---|---|---|
| R1 | Zero-g means a tile with no plenum. Lattices and catwalks are support, and a depressurised room with a floor walks normally. | SS3D `TileQueryService` + `entities.md`; departs from SS14's lattice | `armor.md` §3 |
| R2 | A knockdown that ends over open space recovers into floating upright. Downed, Critical and dead bodies drift. | adaptation | `armor.md` §3 |
| R3 | While floating, wind pushes but never knocks down. | adaptation of `atmospherics.md` §2 | `atmospherics.md` §2 |
| R4 | Within reach of a structure you can move with low traction, and letting go of the keys brakes you: that's "grabbing on". | SS14 `Controls.xml` l.49 | `armor.md` §3 |
| R5 | Reach means static station geometry only (walls, windows, grilles, airlocks, girders, lattices, floor edges). Lockers, items and bodies don't count. | adaptation | `armor.md` §3 |
| R6 | Throwing something while floating pushes you the other way, harder for heavier items. | SS14 `Controls.xml` l.51 | `armor.md` §3 |
| R9 | Downed, Critical and dead bodies can be dragged with an empty hand, which stays busy until you let go. | `health.md` §4 + SS14 `Controls.xml` l.68 | `health.md` §4 |
| R13 | A drag ends when the dragger goes down, the body snags, either party floats, or the dragger lets go. | adaptation | `health.md` §4 |

## Decision log

| # | Decision | Tag | Source |
|---|---|---|---|
| 1 | Zero-g = no plenum; lattice and catwalk are support | SS3D | `TileQueryService.cs:49`; Lattice/Catwalk `layer: 0`; `entities.md` |
| 2 | A knockdown over open space recovers into Floating | adaptation | `Ragdoll.cs` ~l.209 gate |
| 3 | While Floating, wind adds to the coast and never knocks down | adaptation | `atmospherics.md` §2 |
| 4 | Traction within reach, brake on release (= grab) | SS14 + taste | `Controls.xml` l.49 |
| 5 | Reach is 1.5 m to static tile geometry, including the breach rim | adaptation + taste | `LineOfSight` Walls layer |
| 6 | Throw Δv = clamp(1.0 + 1.5 × kg, 1.0, 2.5) m/s, added, capped at run speed | SS14 direction; taste numbers | `Controls.xml` l.51; item weights |
| 7 | No horizontal limit on drifting | SS14 | `StationBoundsRescue` stays height-only |
| 8 | Reaching plenum ends Floating; magboots work again | SS3D | `armor.md` §3; `TryProcessSpaceFloat` |
| 9 | Downed, Critical and dead can be dragged; standing and knockdown can't | SS3D + taste | `health.md` §4; owner question 3 |
| 10 | The drag needs an empty hand and reserves it | SS14 + SS3D | `Controls.xml` l.68; `Hands.cs:315`; `TwoHandedWeaponRules` |
| 11 | No sprint while dragging, walk cap; airlocks and ID still work | adaptation + taste | `AirLockOpener`, `AccessCredentialResolver` |
| 12 | Rope rule, independent of facing | adaptation | — |
| 13 | End rules (dragger down, 2.5 m for 1 s, despawn, target stands, Floating, release); a second dragger is refused | SS3D + SS14 + adaptation | `Draggable.MayChangeDrag`; `Controls.xml` l.73 |
| 14 | A dragged Downed body holds still, with no crawl | adaptation | `Ragdoll.WantsToCrawl` |
| 15 | Dragged objects and corpses hold an airlock open | SS3D | `AirLockOpener.IsDoorwayBlocked` |
| 16 | Wind keeps acting on the target; past the break, the drag ends | adaptation | supersedes a held-target rule |
| 17 | No drag animation or gait this pass | SS3D | MVP1 Explicitly deferred |
| 18 | Nobody sees who is dragging | taste | later idea |
| 19 | `AddCoastImpulse` is the single coast writer, owner-side | SS3D | `entities.md` Pitfalls |
| 20 | The server resolves the throw Δv and TargetRpcs it to the owner; aim falls back to 20 m along the ray | SS3D | `HumanoidController.cs:466` |
| 21 | Owner-side reach overlap; the Unknown-support guard prevents false reach | SS3D | `TryProcessSpaceFloat` |
| 22 | The tether point goes to the target's physics authority; the server re-sends it; no teleport | SS3D | `networking.md` §2; `entities.md` Pitfalls |
| 23 | Movers: CC collision, ragdoll hips force, furniture sweep test | adaptation | furniture map has no rule (new Pitfall) |
| 24 | Server-set `IsBeingDragged` snapshot bit, following the `InteractWork` pattern | SS3D | `HumanoidBodyStateMachine` |
| 25 | Human.prefab wiring through a tier-B recipe | SS3D | AGENTS.md |
| 26 | The interaction is filtered out on standing bodies; 60° cone to the hips | taste | `DragInteraction.IsInFront` (20°) |
| 27 | Console `drag <dragger> <target>`; Down a body with `hurt` | SS3D | `HurtCommand` |
| 28 | Test tiers below | SS3D | testing philosophy D11 |
| 29 | Drag escape by an unresponsive client accepted for Playtest 1 | owner | owner question 1, 2026-11-18 |
| 30 | Build-time decisions below (30+) were made during the M13 build session, 2026-11-18, without the owner; each is open to review | SS3D | owner brief: record every decision |
| 31 | TDD seams: the pure static classes `DragRules` (Furniture) and `ZeroGravityRules` (Entities/Humanoid), EditMode only | SS3D | owner brief ("EditMode tests for every pure rule") |
| 32 | The 60° cone is a half-angle (±60° of facing), measured on the deck plane; a target right underfoot counts | taste | hips on the floor sit below the pivot; a 3D angle would miss bodies at your feet |
| 33 | Knockdown vs Critical: a ragdolled body is draggable only when health also wants it down (`BodyPresentationIntent`); a timed knockdown has health intent Locomotion | SS3D | `Ragdoll._isKnockdownTimed` is server-only; the health snapshot replicates |
| 34 | "Walking acceleration" = `_movementSpeed / _accelerationTime` (the locomotion ramp, ~50 m/s²), so traction is ~12.5 m/s² | SS3D | `HumanoidController.MoveMovementTarget` |
| 35 | Braking is linear at walk speed / 0.5 s; a faster coast takes longer than 0.5 s to stop | taste | stateless per-frame rule |
| 36 | Traction never slows a coast faster than walk speed, but can steer it | taste | a thrown-off spacewalker keeps their throw speed |
| 37 | Throw recoil is planar and opposite the throw's planar heading; a throw straight up or down gives none | adaptation | coast is planar (`TryProcessSpaceFloat`) |
| 38 | End rules are checked in a fixed order: release, despawn, dragger down, snag, target stood, floating | SS3D | one reason logged per end |
| 39 | A combat (untimed-by-wind) knockdown keeps its old recovery; only wind and blast knockdowns use the open-space rule, and only an Unknown map still holds them down | SS3D | `Ragdoll._windKnockdown` |
| 40 | Blast knockdowns over open space are unchanged: R3 covers wind only | adaptation | `Ragdoll.ServerApplyBlast` |
| 41 | `Ragdoll` was at its G3 class-size ratchet; its static floor probe moved to `HumanoidSpaceSupport.TryGetFloorBelow` instead of raising the entry | SS3D | `ClassSizeRatchetTests` |
| 42 | Wind on a floating owner is air drag into the coast (`AtmosWind.DragAcceleration`), not a position nudge; unowned floating bodies keep the server's `MoveUpright` | adaptation | `Ragdoll.TickWind` |
| 43 | Traction follows camera-relative input like walking; it doesn't turn the body | taste | `HumanoidController.InputToWorld` |
| 44 | Braking applies whenever a floating body is in reach with no keys held, also for one that never pressed any (R4 read literally: not holding keys = holding on) | taste | R4 |
| 45 | Getting up over open space keeps the ragdoll hips' velocity as the coast, so a blown-out body keeps drifting instead of stopping dead | adaptation | R2 "bodies drift" |
| 46 | Reach probe: a 1.5 m overlap sphere at the body's centre on the Default and Walls layers; "tile object" = a `PlacedTileObject` in the collider's parents | SS3D | `TagManager` layers 0 and 8 |
| 47 | The throw recoil is resolved at release (`ServerLaunch`), not in `CmdThrow`, so a cancelled throw pushes nobody; the weight is read before the item leaves the hand | SS3D | Decision 13 of the animation pass (interrupted throws keep the item) |
| 48 | A floating thrower with no owning client (combat dummy) gets no recoil | SS3D | owner-side movement |
| 49 | One protocol bump (65 → 66) covers every wire change on this branch | SS3D | `ProtocolVersion` |
| 50 | One drag per body: a dragger already dragging something can't start a second drag with the other hand | taste | `DragRegistry` maps one target per dragger |
| 51 | The drag contract (`DragRules`, `DragRegistry`, `IDragTarget`) lives in `Systems/Entities/Dragging`, not Furniture: Furniture already depends on Entities, and the other way would add a G4 folder cycle | SS3D | `SystemsFolderCycleTests` |
| 52 | The ragdoll pull servoes every free bone's planar velocity toward the rope's (gain 4 /s, response 10 /s, cap 2.5 m/s) rather than forcing the hips alone, so a corpse slides whole | adaptation | plan Risk 2 |
| 53 | A dragged Downed body's controller catches up with the tether at up to 2.5 m/s (walk is 1.5), stepping slightly down to stay on the deck | taste | `BodyDraggable.StepController` |
| 54 | The server re-sends the tether when it moved > 5 cm (at most 20 Hz), or every 0.5 s while the body lies > 1.5 m off it; corpses, orphans and the host's own body get it directly | SS3D | Decision 22 |
| 55 | "Either party floats" on the server: the dragger's Floating snapshot, or no plenum under the dragger or under the body's hips | SS3D | `HumanoidSpaceSupport.IsUnsupportedAt` |
| 56 | `Ragdoll.IsRagdollPhysicsAuthority` is public and delegates to the shared `BodyPhysicsAuthority` rule that the drag movers also use | SS3D | Decision 22 |
| 57 | A body's position for the rope, the start range and the break rule is its hips while ragdolled, its root otherwise | SS3D | the ragdolled root lags the bones |
| 58 | Only a standing (Locomotion) body may start a drag | SS3D | end rule "dragger down" |
| 59 | `IsBeingDragged` takes bit 31, the last free bit of the packed body snapshot | SS3D | `BodyAnimationSnapshot.Pack` |
| 60 | `DragInteraction` moved to `Systems/Entities/Dragging` with the rest of the contract, so `BodyDraggable` builds it without a Furniture dependency | SS3D | G4 |
| 61 | Starting any drag, furniture included, needs an empty selected hand; furniture keeps its 20° cone around its pivot | SS3D + taste | Decision 10 |
| 62 | Letting go of your own drag from the menu needs no range, cone or empty hand | taste | — |
| 63 | The walk cap is `IsRunning` false while dragging, so the run toggle survives; in Harm the combat walk factor still applies | SS3D | `HumanoidLivingController.IsRunning` |
| 64 | The dragging hand stays selectable, unlike a two-handed rifle's off-hand: it only refuses items, so Drop on it can let go | SS3D | `Hands.CanSelectHand` |
| 65 | The rope's hand is the hand that started the drag, replicated on the target (`IDragTarget.DraggerHand`) | SS3D | — |
| 66 | Drop (Q) on the dragging hand and a click on its badge both send one owner ServerRpc, `HumanoidLivingController.CmdReleaseDrag` | SS3D | networking §2: the server resolves the caller's own drag |
| 67 | The HUD refreshes the badge on `DragRegistry.Changed`, since starting a drag changes no hand contents | SS3D | `MainHudSubSystem.RefreshHandReservedState` |

## Phases

**Phase 1 (blind)**, buildable in a cloud session with CI for compile and EditMode: `tether-rules`, `space-recovery`, `coast-impulse`, `throw-propel`, `body-drag-core`, `drag-interaction`, `hand-reserve`, `furniture-sweep`, `airlock-hold`, `drag-command`, `docs-sync`. Order: `tether-rules` first (it holds the rules every other todo calls), then zero-g (`space-recovery`, `coast-impulse`, `throw-propel`) and drag (the rest) in parallel.

**Phase 2 (rig)**, one visit to the owner's machine: `human-recipe`, then `rig-proof`. Fix whatever it turns up, then the owner's two-client hand test of the feel numbers.

## Done when

| Check | Lane | Tier |
|---|---|---|
| A crew member drags a Downed body down a corridor and through an airlock | rig | MPPM + smoke line |
| A crew member drags a locker (crates don't exist yet, catalog U29) | rig | PlayMode |
| A body knocked off the floor in zero-g without magboots recovers into floating, not an endless ragdoll | blind rule, rig proof | EditMode + PlayMode scenario |
| It can push off or brake against a structure and reach the floor again | rig | PlayMode scenario |
| It can throw an item to change direction | blind rule, rig proof | EditMode + PlayMode |
| A remote client sees both | rig | MPPM; smoke line on the built `UNITY_SERVER` path |
| The feel numbers are right | rig | owner two-client hand test |

## Risks

1. **Owner-side tether jitter on remotes.** Shows up early as rubber-banding in the first MPPM drag. The fallback is a server-swept move for Downed bodies (owner question 1).
2. **Ragdoll hips force fights the limbs.** A corpse spins or stretches, visible in the first PlayMode drag of a dead body.
3. **The reach layer is missing on some structures.** Covered by the layer test in `rig-proof`; a grille or window that doesn't register fails it.
4. **The `drag` console command slips.** The remote proof then falls back to the owner's two-client hand test.
5. **Breaking the space-float Pitfalls** ("Remote client stops dead at the ledge", Floating vs gait modes). Re-run `HumanoidSpaceCoastTests` and `scenarios:Environment` on every change to `TryProcessSpaceFloat`.

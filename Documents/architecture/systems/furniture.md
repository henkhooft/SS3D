> Code paths: Assets/Scripts/SS3D/Systems/Furniture/, Assets/Content/WorldObjects/Furniture/
> Entry points: (various world object behaviours); AirLockProximityService
> Status: partial
> Verified: f58c3b9fa — 2026-07-28

# Furniture / world objects

## Overview

Station furniture and interactable world objects — airlocks, lockers, disposal units, vending machines, jukebox, and related prefab behaviours. Power consumption and visuals delegate to [electricity](electricity.md) consumers; airlock opening respects area APC power. Vending machines open a diegetic panel via [machine-interface](machine-interface.md) instead of a direct dispense interaction. Disposal chute/outlet gameplay lives under [disposal](disposal.md).

## Start here

- `Assets/Scripts/SS3D/Systems/Furniture/Locker.cs` — door + ID lock; implements `IStorageAccessGate` so view/store only while open; closing door closes storage panels
- `Assets/Scripts/SS3D/Systems/Furniture/AirLockProximityService.cs` — server HashGrid index; player-centric proximity tick (not per-door FixedUpdate)
- `Assets/Scripts/SS3D/Systems/Furniture/AirLockOpener.cs` — padded proximity auto-open; click Open/Close (`AirLockDoorInteraction`); access-denied SFX + door-light blink; SyncVar→animator; power-gated; `IDynamicTileOccupant` notifies [atmospherics](atmospherics.md)
- `Assets/Scripts/SS3D/Systems/Furniture/AirLockDoorInteraction.cs` — Open requires ID; Close does not; manual open still auto-closes
- `Assets/Scripts/SS3D/Systems/Furniture/AirlockAudioTrackIds.cs` — `AirlockDeny` clip id ([audio](audio.md))
- `Assets/Scripts/SS3D/Systems/Furniture/AirlockStateMachine.cs` — open/close SFX + door-light colors via `AirLockOpener.SetDoorLightColor`
- `Assets/Scripts/SS3D/UI/MachineInterface/VendingMachineController.cs` — vending machines (machine-interface controller)
- `Assets/Scripts/SS3D/Systems/Audio/Boombox.cs` — jukebox toggle; stops audio on power loss
- `Assets/Content/WorldObjects/Furniture/Machines/Vendors/` — vending machine prefabs
- Disposal: see [disposal](disposal.md) (`DisposalBin`, `DisposalOutlet`, `DisposalSubSystem`)

## Extension points

- New furniture interactions: add `InteractionTargetNetworkBehaviour` (or `IInteractionTarget` on an existing networked behaviour) + domain interactions per [interactions-framework](interactions-framework.md).
- Powered machines: attach `MachinePowerConsumer` or `BasicPowerConsumer` + `ElectricDeviceAdjacencyConnector`; add `ConsumerPowerVisual` when emissive meshes should dim unpowered (see [electricity](electricity.md)).
- Panel-driven machines: subclass `MachineInterfaceBehaviour` and register UI per [machine-interface](machine-interface.md) extension recipe.
- Wall light switches: use [area](area.md) `LightSwitchController`, not furniture scripts.

## Pitfalls

- **SS13-scale airlock FixedUpdate:** hundreds of doors each calling `SpawnedPlayers.ToList()` + `GetComponent` every physics tick (~814× on Metastation). Proximity is owned by `AirLockProximityService` (HashGrid neighborhood of players only). Empty close passes use `_pendingEmptyPass` (doors with occupants), not a walk of all `_openerCells` — `EmptyPassSweep` must stay O(pending). Do not re-add per-door `FixedUpdate`. Hit 2026-07-28 (perf capture).
- **Airlock `CreateTargetInteractions` alloc on hover:** returning `new IInteraction[] { new AirLockDoorInteraction(...) }` every outline probe GC-spikes when looking at doors. Cache the interaction instance + array on the opener; only refresh `Name`. Hit 2026-07-28.
- **Client airlocks never open:** remotes are NetworkTransformed on the server — `CharacterController.Move` never runs for them, so Unity triggers / CC Overlap miss. Disable CC when `!IsOwner` so NT can drive the server transform. Probe `EntitySubSystem.SpawnedPlayers` with `Collider.ClosestPoint` + padding (~0.85m): closed door solids stop characters just outside the trigger OBB, so a strict contains check never fires. Drive `Animator.Open` from the `_isOpen` SyncVar OnChange. Hit 2026-07-26.
- **Access-denied spam at a locked door:** unauthorized players in the padded proximity volume must only trigger deny once per approach (`_deniedOccupants` edge). Calling `ServerPlayAccessDenied` every FixedUpdate floods SFX/blinks. Clear the denied set when they leave or on power loss. Hit 2026-07-26 (design).
- **Security cannot open civilian airlocks:** Security role preset omitted `AccessLevel.Civilian` (bit 128). Civilian doors require that bit; host and client both fail access. SecurityOfficer preset + `Security.asset` include Civilian. Hit 2026-07-26.
- **Airlocks stuck open after leaving the trigger:** `AirLockOpener` closes on `OnPowerStatusUpdated(Inactive)`. If [electricity](electricity.md) `PowerAreaConsumers` writes `Inactive` then `Powered` every tick (~0.2s), the SyncVar OnChange restarts the 2s close timer forever. Assign final `PowerStatus` once per consumer; never clear-then-set. Defense: `ScheduleCloseAfterDelay` must not restart an already-running timer. Regression test: `PowerAreaConsumers_AssignsFinalStatusOnceWithoutFlicker`. Hit three times (91b053c1d, Jul 16 uncommitted, 2026-07-18).

## Depends on / Used by

- **Depends on:** [interactions-framework](interactions-framework.md), [interactions-runtime](interactions-runtime.md), [tile](tile.md), [electricity](electricity.md), [area](area.md) (airlock/switch area resolution), [atmospherics](atmospherics.md) (airlock passability), [audio](audio.md) (deny/open/close SFX), [id-access](id-access.md), [machine-interface](machine-interface.md) (vending), [disposal](disposal.md) (chute/outlet behaviours)
- **Used by:** world scenes and construction content

## Related docs

- [disposal](disposal.md) — disposal item network
- [machine-interface](machine-interface.md) — vending UI and diegetic shell
- Effort: [2026-07_metastation-scale-perf.md](../2026-07_metastation-scale-perf.md) — airlock proximity invert + Map Editor sim suspend
- Plan: [areas_implementation_plan_c0639343.plan.md](../../plans/areas_implementation_plan_c0639343.plan.md)
- [INDEX.md](../INDEX.md)

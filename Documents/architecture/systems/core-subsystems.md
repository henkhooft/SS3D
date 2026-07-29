> Code paths: Assets/Scripts/SS3D/Core/, Assets/Scripts/SS3D/Systems/Bootstrap/, Assets/Scripts/SS3D/Systems/WorldReadiness/, Assets/Scripts/SS3D/Networking/NetworkSystemsHub.cs
> Entry points: SubSystem, NetworkSubSystem, SubSystems, SystemsBootstrap, WorldReadinessSubSystem, NetworkSystemsHub
> Status: partial
> Verified: 671460029 — 2026-07-29 (Coimbra GetChecked/EventSystem Editor GC note)

# Core / SubSystems

## Overview

Base actor/subsystem pattern and runtime service locator. All gameplay domains expose a `*SubSystem` registered via `SubSystems.Get<T>()`. `NetworkSubSystem` extends FishNet `NetworkActor` for networked subsystems.

Process-wide services: `SystemsBootstrap` (DDOL). World/session networked systems: `NetworkSystemsHub` Resources prefab (spawned Online). Boot/Game do not place per-system SubSystem GameObjects ([session-world-lifecycle](../2026-07_session-world-lifecycle.md)).

## Start here

- `Assets/Scripts/SS3D/Core/Behaviours/SubSystem.cs` — non-networked subsystem base (bootstrap-owned)
- `Assets/Scripts/SS3D/Core/Behaviours/NetworkSubSystem.cs` — networked subsystem base (hub prefab only)
- `Assets/Scripts/SS3D/Core/Subsystems.cs` — locator; FindObject fallback skipped while quitting / WaitingForServer
- `Assets/Scripts/SS3D/Core/WorldReadiness/IWorldReady.cs` + `WorldReadyPhase.cs` — readiness contract
- `Assets/Scripts/SS3D/Systems/WorldReadiness/WorldReadinessSubSystem.cs` — phase coordinator
- `Assets/Scripts/SS3D/Systems/Bootstrap/SystemsBootstrap.cs` — DDOL process-wide services
- `Assets/Scripts/SS3D/Networking/NetworkSystemsHub.cs` + `Assets/Resources/NetworkSystemsHub.prefab` — Online hub
- `Assets/Scripts/SS3D/Editor/Bootstrap/SessionWorldLifecycleEditorMenus.cs` — rebuild hub / strip scenes

## Extension points

- New process-wide SubSystem: add to `SystemsBootstrap.EnsureProcessWideServices`.
- New networked domain: add type to hub rebuild menu list, run `SS3D/Bootstrap/Rebuild NetworkSystemsHub Prefab` — do not edit Game.unity.
- World sim: implement `IWorldReady`; notify via `WorldReadinessSubSystem` after TileMapLoaded / AreasFlooded — never treat `OnMapCreated` as ready.
- Consumers: `IsReady` / `WhenReady` / `WaitUntilAsync` — not `Get` in Update.

## Pitfalls

- **Registration ≠ readiness.** Await `WorldReadyPhase` before round start / sim ticks that need flooded areas.
- **Missing Get during Disconnecting / WaitingForServer is silent** — use `TryGet`.
- **Game content prefabs may still host SubSystems** (PlayerCamera, Radial/Armed overlays, MapEditor) that register before hub Online — Phase 3 only stripped Boot/Game systems roots. Consumers must `TryGet` / lazy-resolve; relocating those components onto bootstrap/hub is residual cleanup ([session-world-lifecycle](../2026-07_session-world-lifecycle.md) post-ship note).
- **Do not AddComponent NetworkSubSystems at runtime** — edit-time on hub prefab only (FishNet behaviour list).
- Hub despawn on disconnect unregisters via `OnDestroyed` — no extra teardown required.
- **Coimbra `GetChecked` / `EventSystem` Editor GC:** PackageCache `ServiceLocator.GetChecked` interpolates assert strings every call; deep profiles also show `String.Format` paired with every `EventSystem.Invoke`. Prefer caching `IEventService` and `SubSystems` refs on hot loops we own; do not edit PackageCache. Tracked in [TECH_DEBT.md](../TECH_DEBT.md) §2.

## Related docs

- [2026-07_session-world-lifecycle](../2026-07_session-world-lifecycle.md)
- [2026-07_agent-first-composition](../2026-07_agent-first-composition.md)
- [INDEX.md](../INDEX.md)

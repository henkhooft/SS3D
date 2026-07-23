> Code paths: Assets/Scripts/SS3D/Core/, Assets/Scripts/SS3D/Systems/Bootstrap/, Assets/Scripts/SS3D/Systems/WorldReadiness/, Assets/Scripts/SS3D/Networking/NetworkSystemsHub.cs
> Entry points: SubSystem, NetworkSubSystem, SubSystems, SystemsBootstrap, WorldReadinessSubSystem, NetworkSystemsHub
> Status: partial
> Verified: 79239122d — 2026-07-23

# Core / SubSystems

## Overview

Base actor/subsystem pattern and runtime service locator. All gameplay domains expose a `*SubSystem` registered via `SubSystems.Get<T>()`. `NetworkSubSystem` extends FishNet `NetworkActor` for networked subsystems.

Scene-placed registration on Boot/Game actors is **legacy**. Process-wide services use `SystemsBootstrap`; networked hub scaffolding is `NetworkSystemsHub` (dual-runs with scene systems until migration empties Game).

## Start here

- `Assets/Scripts/SS3D/Core/Behaviours/SubSystem.cs` — non-networked subsystem base
- `Assets/Scripts/SS3D/Core/Behaviours/NetworkSubSystem.cs` — networked subsystem base
- `Assets/Scripts/SS3D/Core/Subsystems.cs` — locator; FindObject fallback skipped while quitting / WaitingForServer
- `Assets/Scripts/SS3D/Core/WorldReadiness/IWorldReady.cs` + `WorldReadyPhase.cs` — readiness contract
- `Assets/Scripts/SS3D/Systems/WorldReadiness/WorldReadinessSubSystem.cs` — phase coordinator
- `Assets/Scripts/SS3D/Systems/Bootstrap/SystemsBootstrap.cs` — DDOL WorldReadiness / ScreenEffects / Automation / Vision
- `Assets/Scripts/SS3D/Networking/NetworkSystemsHub.cs` — spawnable hub NetworkObject

## Extension points

- New domain subsystem: subclass `SubSystem`/`NetworkSubSystem`. Prefer bootstrap/hub over Boot/Game YAML.
- World sim: implement `IWorldReady`; notify via `WorldReadinessSubSystem` after TileMapLoaded / AreasFlooded — never treat `OnMapCreated` as ready.
- Consumers: `IsReady` / `WhenReady` / `WaitUntilAsync` — not `Get` in Update.

## Pitfalls

- **Registration ≠ readiness.** Await `WorldReadyPhase` before round start / sim ticks that need flooded areas.
- **Missing Get during WaitingForServer is silent** — use `TryGet`.

## Related docs

- [2026-07_session-world-lifecycle](../2026-07_session-world-lifecycle.md)
- [2026-07_agent-first-composition](../2026-07_agent-first-composition.md)
- [INDEX.md](../INDEX.md)

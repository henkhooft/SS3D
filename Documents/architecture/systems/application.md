> Code paths: Assets/Scripts/SS3D/Application/, Assets/Scripts/SS3D/Systems/Bootstrap/
> Entry points: ApplicationInitializerSubSystem, SystemsBootstrap
> Status: stub
> Verified: 66ba71066 — 2026-10-31

# Application

## Overview

App bootstrap and startup initialization. `ApplicationInitializerSubSystem` is DDOL via `SystemsBootstrap` (created last so ApplicationPreInitializing / ApplicationInitializing listeners Awake first). Boot/Game are thin launch pads ([agent-first composition](../2026-07_agent-first-composition.md) follow-on (a) shipped).

## Start here

- `Assets/Scripts/SS3D/Application/ApplicationInitializerSubSystem.cs` — application startup subsystem
- `Assets/Scripts/SS3D/Systems/Bootstrap/SystemsBootstrap.cs` — owns ApplicationInitializer + other process-wide services

## Extension points

- New process-wide services: register in `SystemsBootstrap`, not Boot YAML.

## Pitfalls

- **Boot storm if offline reloads Boot after Online.** Session owner arms Empty — see [networking-session](networking-session.md).
- **PlayMode tests start in the Test Runner's `InitTestScene<guid>`.** `SystemsBootstrap` must not cold-start there: `SceneSubSystem` would load Intro over the runner's scene and no test starts. The bootstrap waits for the first other scene (the tests load Boot themselves). The MCP test runner enters Play Mode without a domain reload, so the handler is cleared on `SubsystemRegistration`.
- **Listener order:** anything that subscribes to ApplicationInitializing must exist before ApplicationInitializer.OnStart — bootstrap creates ApplicationInitializer last in the AfterSceneLoad batch.

## Depends on / Used by

- **Depends on:** [core-subsystems](core-subsystems.md)
- **Used by:** All systems at startup

## Related docs

- [2026-07_agent-first-composition](../2026-07_agent-first-composition.md)
- [2026-07_session-world-lifecycle](../2026-07_session-world-lifecycle.md)
- [INDEX.md](../INDEX.md)

> Code paths: Assets/Scripts/SS3D/Application/, Assets/Scripts/SS3D/Systems/Bootstrap/
> Entry points: ApplicationInitializerSubSystem, SystemsBootstrap
> Status: stub
> Verified: 90e26cdc2 — 2026-07-23

# Application

## Overview

App bootstrap and startup initialization. Boot/Game remain scene launch pads; process-wide services move to `SystemsBootstrap` ([agent-first composition](../2026-07_agent-first-composition.md) follow-on (a) — scaffolding shipped, scene empty still open).

## Start here

- `Assets/Scripts/SS3D/Application/ApplicationInitializerSubSystem.cs` — application startup subsystem (Boot scene)
- `Assets/Scripts/SS3D/Systems/Bootstrap/SystemsBootstrap.cs` — DDOL WorldReadiness / ScreenEffects / Automation / Vision

## Extension points

- New process-wide services: register in `SystemsBootstrap`, not Boot YAML (until Phase 3h empties scenes).

## Pitfalls

- **Boot storm if offline reloads Boot after Online.** Session owner arms Empty — see [networking-session](networking-session.md).

## Depends on / Used by

- **Depends on:** [core-subsystems](core-subsystems.md)
- **Used by:** All systems at startup

## Related docs

- [2026-07_agent-first-composition](../2026-07_agent-first-composition.md)
- [2026-07_session-world-lifecycle](../2026-07_session-world-lifecycle.md)
- [INDEX.md](../INDEX.md)

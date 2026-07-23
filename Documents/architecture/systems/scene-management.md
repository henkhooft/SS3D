> Code paths: Assets/Scripts/SS3D/SceneManagement/
> Entry points: SceneSubSystem
> Status: stub
> Verified: 84401b2fe — 2026-07-23

# Scene management

## Overview

Scene loading and switching. Integrates with editor toolbar Scene Switcher.

`SceneSubSystem` is DDOL via `SystemsBootstrap`. Scenes are launch pads, not system composition roots ([agent-first composition](../2026-07_agent-first-composition.md)). FishNet offline after first Online is `Scenes.Empty` — see [networking-session](networking-session.md).

## Start here

- `Assets/Scripts/SS3D/SceneManagement/SceneSubSystem.cs` — scene loading subsystem (bootstrapped)
- `Assets/Scripts/SS3D/Data/Generated/Scenes.cs` — codegen scene refs (`Boot`, `Empty`, `EmptyPath`, `BootPath`, Game, …)

## Extension points

(stub)

## Pitfalls

- **Duplicate EventSystem when Game loads additively over Intro:** Intro Objects prefab and Game both have an EventSystem. Unload is async — disable Intro/Launcher EventSystems synchronously when Game becomes active, then unload. Do not leave both enabled.
- **Disconnect must not reload Boot after first Online.** CCR arms Empty; see [networking-session](networking-session.md) Pitfalls.

## Depends on / Used by

- **Depends on:** [data-codegen](data-codegen.md)
- **Used by:** [networking-session](networking-session.md), [application](application.md)

## Related docs

- [2026-07_agent-first-composition](../2026-07_agent-first-composition.md)
- [2026-07_session-world-lifecycle](../2026-07_session-world-lifecycle.md)
- [INDEX.md](../INDEX.md)

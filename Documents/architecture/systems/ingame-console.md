> Code paths: Assets/Scripts/SS3D/Systems/IngameConsoleSystem/
> Entry points: CommandsController
> Status: partial
> Verified: 1ddd6404a — 2026-07-21

# In-game console

## Overview

Dev/admin in-game console commands routed through `CommandsController`. Commands are discovered by reflection from `Command` subclasses across all loaded assemblies (not only `SS3D.Systems`), so UI assemblies can host commands that would otherwise create an asmdef cycle. Server commands check `PermissionSubSystem` role before executing.

**Condemned UI:** console panel uGUI — do not extend; move to UITK debug layer when UiShell lands ([agent-first composition](../2026-07_agent-first-composition.md)). Command dispatch is **not** condemned.

## Start here

- `Assets/Scripts/SS3D/Systems/IngameConsoleSystem/CommandsController.cs` — command dispatch (offline, server, client)
- `Assets/Scripts/SS3D/Systems/IngameConsoleSystem/Commands/Command.cs` — base class; subclasses auto-register
- `Assets/Scripts/SS3D/Systems/IngameConsoleSystem/Commands/IdAccessCommands/` — `accesscheck`, `accessgrant`, `accessrevoke`, `accesspreset` dev helpers
- `Assets/Scripts/SS3D/Systems/IngameConsoleSystem/Commands/ScreenEffectCommand.cs` — client `screeneffect` intensity setter
- `Assets/Scripts/SS3D/Systems/IngameConsoleSystem/Commands/ScreenEffectHitFlashCommand.cs` — client hit-flash trigger
- `Assets/Scripts/SS3D/UI/MainHud/Commands/AlertStackCommand.cs` — client `alertstack` hazard override (MainHud asm)

## Extension points

**New server command:** subclass `Command`, set `Type = Server`, `AccessLevel`, implement `Perform` and `CheckArgs`; name class with `Command` suffix. Prefer `Assets/Scripts/SS3D/Systems/IngameConsoleSystem/Commands/` when the command only needs Systems types.

**Commands that call UI assemblies (MainHud, etc.):** put the `Command` subclass in that UI assembly (MainHud → Systems is fine; Systems → MainHud cycles). `CommandsController` reflects across all loaded assemblies.

**ID access dev commands:** use `IdAccessCommandUtilities` for target resolution and level/preset parsing.

**Screen-effect debug:** client commands call [screen-effects](screen-effects.md); F2 menu is an alternate path on the same subsystem.

**Alert-stack debug:** client `alertstack` lives in [inventory](inventory.md) MainHud; F3 menu is the alternate path.

## Depends on / Used by

- **Depends on:** [permissions](permissions.md), [id-access](id-access.md) (access dev commands), [entities](entities.md), [inventory](inventory.md) (incl. MainHud-hosted `alertstack`), [screen-effects](screen-effects.md)

## Related docs

- [id-access](id-access.md)
- [screen-effects](screen-effects.md)
- [2026-07_agent-first-composition](../2026-07_agent-first-composition.md)
- [ui-shell](ui-shell.md)
- [INDEX.md](../INDEX.md)

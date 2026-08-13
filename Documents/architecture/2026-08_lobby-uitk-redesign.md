> Implements: Documents/design/lobby.md
> Touches systems: rounds-lobby, ui-shell
> Status: in-progress (Phase A/B visual + Phase E1 live preview/draft; C/D/E2/F pending)

# Lobby UITK redesign

Replace the condemned uGUI lobby with a UiShell Modal UITK Lobby Shell matching the attached
pre-round lobby design. Character Creator is a **separate** modal screen opened from the lobby
character preview.

## Phases

| Phase | Scope | Status |
|---|---|---|
| A | Lobby Shell layout + USS + mock data; catalog; preview placeholder click stub | **shipped** |
| B | Character Creator separate screen (guided-steps visual) | **shipped** (visual) |
| C | Wire Ready / round state / admin start-stop / player list / show-hide | pending |
| D | Jobs H/M/L/N + antag prefs sync + resolution | pending |
| E1 | Live booth preview + local Save/Return draft (name) | **shipped** |
| E2 | Appearance apply/sync (hair/skin/catalog, network, spawn) | pending |
| F | Purge condemned LobbyCanvas / uGUI views; docs finalize | pending |

## Design divergences (do not edit design docs)

- Jobs UI uses **H/M/L/N priority chips** from the attached design, not `lobby.md` §3 drag-ranked list.
- Character Creator follows design option **1A Guided Steps** (Identity → Body → Style → Review).
- Character Creator is in scope for this effort (separate screen); `lobby.md` §6/§11 name-only identity is deferred only until Phase E2 lands.

## Entry points

- `Assets/Scripts/SS3D/UI/Lobby/LobbyUiSubSystem.cs` — DDOL bootstrap; shell + creator + booth lifecycle
- `Assets/Scripts/SS3D/UI/Lobby/LobbyShellView.cs` — full shell chrome
- `Assets/Scripts/SS3D/UI/Lobby/CharacterCreatorView.cs` — guided-steps Character Creator
- `Assets/Scripts/SS3D/Systems/Entities/Character/CharacterPreviewBooth.cs` — off-map RT booth (layer 22)
- `Assets/Content/Systems/UI/Lobby/LobbyShell.uss` (+ `@import CharacterCreator.uss`) + `Resources/LobbyAssetCatalog.asset`
- Rebuild: **SS3D → Data → Rebuild All UI Catalogs**

## Visual QA

Play Mode: click character preview → Character Creator with live humanoid RT; Front/Side/Back rotates
the dummy; **Save Character** updates lobby name; **Return to Lobby** restores the shell with the
same RT. Disable `LobbyCanvas` in the Hierarchy if the old uGUI lobby fights for input/visibility.

## Related

- Design: [lobby.md](../design/lobby.md)
- Maps: [rounds-lobby](systems/rounds-lobby.md), [ui-shell](systems/ui-shell.md)

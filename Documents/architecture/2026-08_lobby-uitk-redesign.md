> Implements: Documents/design/lobby.md
> Touches systems: rounds-lobby, ui-shell
> Status: in-progress (Phase A shell + Phase B Character Creator visual; wiring + purge pending)

# Lobby UITK redesign

Replace the condemned uGUI lobby with a UiShell Modal UITK Lobby Shell matching the attached
pre-round lobby design. Character Creator is a **separate** modal screen opened from the lobby
character preview (placeholder until Phase E).

## Phases

| Phase | Scope | Status |
|---|---|---|
| A | Lobby Shell layout + USS + mock data; catalog; preview placeholder click stub | **shipped** |
| B | Character Creator separate screen (guided-steps visual) | **shipped** (visual) |
| C | Wire Ready / round state / admin start-stop / player list / show-hide | pending |
| D | Jobs H/M/L/N + antag prefs sync + resolution | pending |
| E | Character Creator functional + live preview | pending |
| F | Purge condemned LobbyCanvas / uGUI views; docs finalize | pending |

## Design divergences (do not edit design docs)

- Jobs UI uses **H/M/L/N priority chips** from the attached design, not `lobby.md` §3 drag-ranked list.
- Character Creator follows design option **1A Guided Steps** (Identity → Body → Style → Review).
- Character Creator is in scope for this effort (separate screen); `lobby.md` §6/§11 name-only identity is deferred only until Phase E lands.

## Entry points

- `Assets/Scripts/SS3D/UI/Lobby/LobbyUiSubSystem.cs` — DDOL bootstrap; attaches shell + creator to `UiLayer.Modal`
- `Assets/Scripts/SS3D/UI/Lobby/LobbyShellView.cs` — full shell chrome
- `Assets/Scripts/SS3D/UI/Lobby/CharacterCreatorView.cs` — guided-steps Character Creator
- `Assets/Content/Systems/UI/Lobby/LobbyShell.uss` (+ `@import CharacterCreator.uss`) + `Resources/LobbyAssetCatalog.asset`
- Rebuild: **SS3D → Data → Rebuild All UI Catalogs**

## Visual QA

Play Mode shows the UITK shell with mock data. Click the character preview to open the Character
Creator; **Return to Lobby** restores the shell. Disable `LobbyCanvas` in the Hierarchy if the old
uGUI lobby fights for input/visibility.

## Related

- Design: [lobby.md](../design/lobby.md)
- Maps: [rounds-lobby](systems/rounds-lobby.md), [ui-shell](systems/ui-shell.md)

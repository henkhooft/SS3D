> Code paths: Assets/Scripts/SS3D/Systems/Gamemodes/, Assets/Scripts/SS3D/Systems/Roles/, Assets/Scripts/SS3D/Systems/Traits/
> Entry points: GamemodeSubSystem, RoleSubSystem
> Status: partial
> Verified: 2fcfc9fa8 — 2026-10-30

# Gamemodes / roles / traits

## Overview

Round objectives, job roles/loadouts, and character traits (e.g. ID permissions). Coordinates with [rounds-lobby](rounds-lobby.md) embark flow.

## Start here

- `Assets/Scripts/SS3D/Systems/Gamemodes/GamemodeSubSystem.cs` — gamemode subsystem
- `Assets/Scripts/SS3D/Systems/Roles/RoleSubSystem.cs` — replicated job slots (`Jobs`), lobby preferences (`JobPreferencesMessage`), assignment, ID/PDA + loadout spawn
- `Assets/Scripts/SS3D/Systems/Roles/JobResolver.cs` — pure round-start (High → Medium → Low, fallback Assistant) and latejoin resolution; `JobResolverTests`
- `Assets/Scripts/SS3D/Systems/Roles/AntagonistCategories.cs` — lobby antagonist opt-in keys
- `Assets/Content/Data/Roles/StartingRoles.asset` — available roles and slot caps (0 = unlimited)
- `GamemodeObjectivePanelView` — legacy uGUI panel hidden (no Tab hotkey; PDA tab later)

Server Settings selects Secret / Extended / Nuclear Emergency. Extended skips objective
initialization; Secret uses the sole existing antagonist mode. The runtime gamemode reference
is server-side (no SyncVar); authorized settings use the rounds subsystem's TargetRpc.
Weighted mode pools remain deferred; see [rounds-lobby](rounds-lobby.md).

## Extension points

- Active gamemode name: `GamemodeSubSystem.CurrentGamemodeName` (used by round-history persistence).
- New job: add a `RoleData` to `StartingRoles.asset` and map its department in `IdAccessSubSystem.DepartmentForRole`.
- Antagonist selection: choose among `RoleSubSystem.IsAntagonistOptedIn(ckey, AntagonistCategories.*)`; nothing consumes it yet.

## Pitfalls

- Assignment happens at round start (`ResolveRoundStartRoles`, called by `EntitySubSystem.SpawnReadyPlayers`) or at latejoin spawn, not on connect. `TryGetRole` is empty in the lobby.

## Depends on / Used by

- **Depends on:** [rounds-lobby](rounds-lobby.md), [entities](entities.md)
- **Used by:** [persistence](persistence.md) (round-end history entry)

## Related docs

- [INDEX.md](../INDEX.md)

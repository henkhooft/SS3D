> Code paths: Assets/Scripts/SS3D/Systems/Furniture/Disposal/, Assets/Scripts/SS3D/Systems/Furniture/DisposalBin.cs, DisposalOutlet.cs, Assets/Scripts/SS3D/Systems/Tile/Connections/Disposal*
> Entry points: DisposalSubSystem, DisposalBin, DisposalOutlet, DisposalPipeConnectivity
> Status: partial
> Verified: f82ca45bf — 2026-07-19

# Disposal

## Overview

Server-authoritative **item** disposal network: pipe segments on `TileLayer.Disposal` form BFS-connected components; chutes inject held items as capsules that travel a routed path to a tagged or main outlet. Mid-transit tile clear spills the capsule at the cut. Player pipe craft recipes, Cargo export, and player-body transit are deferred — see [2026-07_disposal-item-network.md](../2026-07_disposal-item-network.md).

## Start here

- `Assets/Scripts/SS3D/Systems/Furniture/Disposal/DisposalSubSystem.cs` — registry owner; capsule tick; spill-on-cut
- `Assets/Scripts/SS3D/Systems/Furniture/Disposal/DisposalPipeConnectivity.cs` — BFS walk + `TryFindRoute`
- `Assets/Scripts/SS3D/Systems/Furniture/DisposalBin.cs` — chute; `AcceptsSize` / `MaxSizeClass`; drop-in + tagger interactions
- `Assets/Scripts/SS3D/Systems/Furniture/DisposalOutlet.cs` — arrival hold / main-outlet grace (eject only if `_spaceEjectionPoint` or `IDisposalSweepable` is wired)
- `Assets/Scripts/SS3D/Systems/Furniture/Disposal/DisposalDropInInteraction.cs` — Combine-tier dispose
- `Assets/Scripts/SS3D/Systems/Tile/Connections/DisposalPipeAdjacencyConnector.cs` — pipe mesh adjacency
- Prefabs: `Assets/Content/WorldObjects/Furniture/Machines/Supply/DisposalBin.prefab`, `DisposalOutlet.prefab`; pipes under `.../Structures/Pipes/Disposals/`; open clips/controllers beside the prefabs
- Scene: `DisposalSystem` under networked systems root in `Game.unity`

## Extension points

- New outlet destinations: place `DisposalOutlet` with `TargetDepartment` set; ensure a pipe under it joins the network.
- Cargo hook: implement `IDisposalSweepable` on the main outlet (or sibling) — no appraisal yet.
- Size-restricted chutes: lower `DisposalBin._maxSizeClass` in the inspector (same ceiling pattern as `AttachedContainer`).

## Pitfalls

- **Dispose fails silently if `DisposalSubSystem` missing:** `DisposalBin.TryEnterDisposalNetwork` returns false when `SubSystems.TryGet` misses — register `DisposalSystem` on `Game.unity` (already present on this branch).
- **Dispose loses to Drop on primary-click:** Dispose defaulted to Priority 0 while Drop is 5. Dispose is now 40 (TagDisposal 20) so chute click prefers Dispose.
- **Dispose becomes a floor drop:** enter used to `RemoveItem` before routing; on failure the item stayed out of hand. Now it restores to the hand. Root cause of empty networks: observer only rebuilt on **pipe** place — bin/outlet placed after pipes never joined `Terminals`. Fixed to rebuild on disposal furniture place/clear and after `OnMapLoaded`.
- **Outlet arrivals spawn inside the mesh:** arrivals are spat along `transform.forward` by `_spitDistance` (default 0.85m), not at the outlet origin.
- **Main-outlet items vanished after grace:** prefab defaults to `Department.None` with a 30s grace, then `EjectIntoSpace` despawned in place when `_spaceEjectionPoint` was unset. Grace/eject now only runs if an ejection point or `IDisposalSweepable` is present; otherwise arrivals sit for pickup.
- **Lid/door anim never plays:** controllers need an Idle default (Open-as-default plays on spawn). Drive `NetworkAnimator.Play` from successful chute enter / outlet arrive — not raw `Animator.Play` alone if clients should see it. NetworkAnimator must be in `NetworkObject._networkBehaviours` with `_clientAuthoritative: 0`.
- **Items visible mid-pipe:** design wants visible transit later (glass sections). Until then capsules hide via `Item.SetVisibility` (ObserversRpc); enable `DisposalSubSystem._debugShowTransitItems` to watch routes. Reveal on spit / pipe-cut spill.
- **`Object.Destroy` on items:** Coimbra forbids it — use FishNet `Despawn` when `ServerManager` exists, else `gameObject.Dispose(true)` (`using Coimbra`).
- **Pipe place/cut in play:** no player recipes yet; clearing a disposal tile (map editor / `TryClearTile`) is what triggers sabotage spill.

## Depends on / Used by

- **Depends on:** [tile](tile.md), [inventory](inventory.md) (`Item.SizeClass`, hands), [interactions-framework](interactions-framework.md), [examine](examine.md) (tag readout), [id-access](id-access.md) (optional chute gate)
- **Used by:** station maps with disposal furniture; future Cargo export

## Related docs

- Effort: [2026-07_disposal-item-network.md](../2026-07_disposal-item-network.md)
- Design (read-only): [disposal.md](../../design/disposal.md)
- [INDEX.md](../INDEX.md)

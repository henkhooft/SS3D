> Code paths: Assets/Scripts/SS3D/Systems/Furniture/Disposal/, Assets/Scripts/SS3D/Systems/Furniture/DisposalBin.cs, DisposalOutlet.cs, Assets/Scripts/SS3D/Systems/Tile/Connections/Disposal*
> Entry points: DisposalSubSystem, DisposalBin, DisposalOutlet, DisposalPipeConnectivity
> Status: partial
> Verified: 32cb1634b — 2026-07-19

# Disposal

## Overview

Server-authoritative **item** disposal network: pipe segments on `TileLayer.Disposal` form BFS-connected components; chutes inject held items as capsules that travel a routed path to a tagged or main outlet. Mid-transit tile clear spills the capsule at the cut. Player pipe craft recipes, Cargo export, and player-body transit are deferred — see [2026-07_disposal-item-network.md](../2026-07_disposal-item-network.md).

## Start here

- `Assets/Scripts/SS3D/Systems/Furniture/Disposal/DisposalSubSystem.cs` — registry owner; capsule tick; spill-on-cut
- `Assets/Scripts/SS3D/Systems/Furniture/Disposal/DisposalPipeConnectivity.cs` — BFS walk + `TryFindRoute`
- `Assets/Scripts/SS3D/Systems/Furniture/DisposalBin.cs` — chute; `AcceptsSize` / `MaxSizeClass`; drop-in + tagger interactions
- `Assets/Scripts/SS3D/Systems/Furniture/DisposalOutlet.cs` — arrival hold / main-outlet grace + despawn
- `Assets/Scripts/SS3D/Systems/Furniture/Disposal/DisposalDropInInteraction.cs` — Combine-tier dispose
- `Assets/Scripts/SS3D/Systems/Tile/Connections/DisposalPipeAdjacencyConnector.cs` — pipe mesh adjacency
- Prefabs: `Assets/Content/WorldObjects/Furniture/Machines/Supply/DisposalBin.prefab`, `DisposalOutlet.prefab`; pipes under `.../Structures/Pipes/Disposals/`
- Scene: `DisposalSystem` under networked systems root in `Game.unity`

## Extension points

- New outlet destinations: place `DisposalOutlet` with `TargetDepartment` set; ensure a pipe under it joins the network.
- Cargo hook: implement `IDisposalSweepable` on the main outlet (or sibling) — no appraisal yet.
- Size-restricted chutes: lower `DisposalBin._maxSizeClass` in the inspector (same ceiling pattern as `AttachedContainer`).

## Pitfalls

- **Dispose fails silently if `DisposalSubSystem` missing:** `DisposalBin.TryEnterDisposalNetwork` returns false when `SubSystems.TryGet` misses — register `DisposalSystem` on `Game.unity` (already present on this branch).
- **Grace eject after pickup:** main outlet must skip items in a container or far from the outlet; otherwise a held item was teleported/despawned on timer expiry.
- **`Object.Destroy` on items:** Coimbra forbids it — use FishNet `Despawn` when `ServerManager` exists, else `gameObject.Dispose(true)` (`using Coimbra`).
- **Pipe place/cut in play:** no player recipes yet; clearing a disposal tile (map editor / `TryClearTile`) is what triggers sabotage spill.

## Depends on / Used by

- **Depends on:** [tile](tile.md), [inventory](inventory.md) (`Item.SizeClass`, hands), [interactions-framework](interactions-framework.md), [examine](examine.md) (tag readout), [id-access](id-access.md) (optional chute gate)
- **Used by:** station maps with disposal furniture; future Cargo export

## Related docs

- Effort: [2026-07_disposal-item-network.md](../2026-07_disposal-item-network.md)
- Design (read-only): [disposal.md](../../design/disposal.md)
- [INDEX.md](../INDEX.md)

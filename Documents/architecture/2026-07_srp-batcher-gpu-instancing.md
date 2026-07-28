> Implements: (perf / scale — no single design §; playable floor for SS13-size maps)
> Touches systems: rendering, selection, tile, structural-destruction
> Status: shipped

# SRP Batcher + GPU Instancing

## Goal

Cut Metastation `Render.Mesh` / `ApplyShader` cost by unlocking URP **SRP Batcher** and **GPU Instancing** for station tile draws. Simple Toon already had the shader contract (`UnityPerMaterial` CBUFFER + `#pragma multi_compile_instancing`); this effort is content + runtime hygiene.

## Shipped

### Materials

- Enabled `m_EnableInstancingVariants` on ST floor mats: TileGrey, GreyDark, Bar, Kitchen, Wood, Reinforced (TilePlating / Palette already on).
- Left URP Lit overlays / TileWhite for a later shader migration.

### Adjacency `sharedMesh`

- [`TileAdjacencyView`](../../Assets/Scripts/SS3D/Systems/Tile/Connections/TileAdjacencyView.cs), [`AbstractHorizontalConnector`](../../Assets/Scripts/SS3D/Systems/Tile/Connections/AbstractHorizontalConnector.cs), [`DirectionalAdjacencyConnector`](../../Assets/Scripts/SS3D/Systems/Tile/Connections/DirectionalAdjacencyConnector.cs), [`DisposalPipeAdjacencyConnector`](../../Assets/Scripts/SS3D/Systems/Tile/Connections/DisposalPipeAdjacencyConnector.cs) assign `_filter.sharedMesh` (not `.mesh`) so adjacency variants keep the shared FBX asset for instancing.

### Selection pick without permanent MeshRenderer MPBs

- [`Selectable`](../../Assets/Scripts/SS3D/Systems/Selection/Selectable.cs) registers as [`SelectionPickContext.ISelectionPickSource`](../../Assets/Scripts/SS3D/Rendering/URP/SelectionPickContext.cs); MeshRenderers stay MPB-free.
- [`SelectionPickRendererFeature`](../../Assets/Scripts/SS3D/Rendering/URP/SelectionPickRendererFeature.cs) draws registered meshes with `DrawMesh` + transient `MaterialPropertyBlock` (skinned still use permanent MPB + `DrawRenderer` — few characters).
- Pick collect frustum-culls with the request camera (`IsInPickFrustum`) — AOI alone is not enough after leaving MeshRenderer draws.
- Permanent `_SelectionColor` MPBs were the main SRP Batcher breaker on every floor/wall tile.

### Structural integrity Intact clear

- [`StructuralIntegrityPresenter.Apply(Intact)`](../../Assets/Scripts/SS3D/Systems/StructuralDamage/StructuralIntegrityPresenter.cs) calls `SetPropertyBlock(null)` instead of writing a white tint MPB.

### EditMode

- `SrpBatcherInstancingTests` — floor mat instancing flags, adjacency sharedMesh, Selectable no permanent MPB.
- `StructuralIntegrityPresentationTests.Presenter_ApplyIntact_ClearsMaterialPropertyBlock`.

## Verification (Play Mode)

- Frame Debugger on Metastation (Map Editor closed): identical TileGrey floors should GPU-instance / SRP-batch; damaged walls may still split.
- Re-export Profiler under `Logs/perf/` (`metastation-play`) — expect `Render.Mesh` / `ApplyShader` down vs `capture-20260728-164634` / post-underfloor baseline.
- Hover/examine/interaction pick still resolves on floors and doors.
- After frustum cull: `SS3D Selection Pick` should drop vs `capture-20260728-181845` when zoomed in (AOI still large).

## Explicitly deferred

- Fixture `.materials` clones (lights/doors).
- Chunk-/room-combined floor meshes.
- TileWhite / overlay Lit → STDefault.
- GPU Resident Drawer (stays off — Linux/OpenGL).

## Related docs

- Prior scale pass: [2026-07_metastation-scale-perf.md](2026-07_metastation-scale-perf.md)
- System maps: [rendering](systems/rendering.md), [selection](systems/selection.md), [tile](systems/tile.md), [structural-destruction](systems/structural-destruction.md)

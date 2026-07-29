> Code paths: Assets/Scripts/SS3D/Rendering/, Assets/Content/Resources/Simple Toon/, Assets/Scripts/SS3D/Systems/Vision/, Assets/Content/Resources/Vision/
> Entry points: SelectionPickRendererFeature, AtmosRendererFeature, VisionRendererFeature
> Status: partial
> Verified: 620ac632 — 2026-07-29 (Frame Debugger AI export tooling)

# Rendering

## Overview

URP rendering extensions for this fork. The selection pick pass ([selection](selection.md)) is gameplay-critical for interaction targeting. The atmospherics pass ([atmospherics](atmospherics.md)) composites gas scatter, plasma glow, and heat distortion from sim GPU textures via `AtmosRenderContext`. **Decal Renderer** is enabled on the forward renderer (Use Rendering Layers on) for health blood decals and future surface marks — see `DecalRenderingLayers`. **Atmos snapshot is server-built only** — clients render when a snapshot is present; multiplayer client sync is not implemented yet.

Station materials use the **Simple Toon** shader stack (`STDefault` / `STTransparent`). ST already has `UnityPerMaterial` + `#pragma multi_compile_instancing` — Metastation wins come from enabling material instancing and keeping MeshRenderers free of permanent MaterialPropertyBlocks (see [srp-batcher-gpu-instancing](../2026-07_srp-batcher-gpu-instancing.md)). Palette emission must sample `_EmissionMap` (same UV swatch pattern as albedo) — a flat `_EmissionColor` alone washes shared `PaletteEmission` materials white. `STDefault` includes DepthOnly + DepthNormals (with `_WRITE_RENDERING_LAYERS`) so URP Decal Layers can distinguish characters from tiles, and ForwardLit samples DBuffer (`ApplyDecalToBaseColor`) so health blood / surface `DecalProjector`s tint skin and worn clothing.

Client FOV / fog-of-war is a hard black mask driven by batched physics raycasts from `Entity.ViewPoint` (`VisionSubSystem` → `_VisionMap`) and composited by `VisionRendererFeature`. Unseen areas are fully opaque black, not soft fog. Rays advance in `RaycastCommand` waves, skipping furniture/props until the nearest wall/door (non-window); a capped multi-hit buffer previously filled with props and leaked vision through walls. Triggers and inventory preview cameras are ignored.

## Start here

- `Assets/Scripts/SS3D/Rendering/URP/SelectionPickRendererFeature.cs` — URP feature for shader-ID picking
- `Assets/Scripts/SS3D/Rendering/URP/SelectionPickContext.cs` — pick pass render context
- `Assets/Scripts/SS3D/Rendering/URP/SelectionRenderingLayers.cs` — layer bit to exclude outline shells from the pick pass
- `Assets/Scripts/SS3D/Rendering/URP/DecalRenderingLayers.cs` — floor vs character DecalProjector masks (`ReceiveWorldDecals`)
- `Assets/Scripts/SS3D/Rendering/URP/AtmosRendererFeature.cs` — gas scatter, glow, distortion passes
- `Assets/Scripts/SS3D/Rendering/URP/AtmosRenderContext.cs` — shared GPU snapshot for atmos shaders
- `Assets/Scripts/SS3D/Rendering/URP/VisionRendererFeature.cs` — FOV mask + hard black composite
- `Assets/Scripts/SS3D/Rendering/URP/UiBackdropBlurRendererFeature.cs` — Dual Kawase world blur behind diegetic machine UI
- `Assets/Scripts/SS3D/Systems/Vision/VisionSubSystem.cs` — client `RaycastCommand` waves → R16 `_VisionMap` (occluder cache; float dilate + `SetPixelData`)
- `Assets/Content/Resources/Simple Toon/Shaders/STLighting.hlsl` — half-toon lighting + palette emission sample
- `Assets/Content/Resources/Simple Toon/Shaders/STDefault.shader` — opaque toon (+ DepthNormals for Decal Layers)
- `Assets/Content/Resources/Simple Toon/Shaders/ObjectIcon.shader` — unlit bright full-toon for UI icon previews
- `Assets/Scripts/SS3D/Utils/IconPreviewGenerator.cs` — ObjectIcon material swap for UI icon previews
- `Assets/Settings/URP/` — pipeline asset and Forward+ renderer (includes Decal Renderer feature)

## Extension points

- New render features: add URP `ScriptableRendererFeature` under `Rendering/URP/`.
- Outline / auxiliary meshes that must not participate in pick: set rendering layer `SelectionRenderingLayers.ExcludeFromSelectionPick`.
- World surface marks: stamp `DecalRenderingLayers.ReceiveWorldDecals` on receiver renderers; point floor `DecalProjector`s at `WorldFloorProjectorMask`. Custom opaque shaders must implement DepthNormals with `_WRITE_RENDERING_LAYERS` or Decal Layers will not exclude them, and must sample DBuffer (`ApplyDecalToBaseColor` / `_DBUFFER_MRT*`) or Automatic→DBuffer projectors never tint albedo (worn jumpsuit / ST floors).

## Pitfalls

- **Permanent MaterialPropertyBlocks break SRP Batcher:** any MPB on a MeshRenderer (e.g. old Selectable `_SelectionColor`, Intact white integrity tint) drops that draw out of the SRP Batcher path. Selection pick uses transient MPB on `DrawMesh` via `SelectionPickContext`; intact integrity clears MPBs. Hit 2026-07-28 (Metastation).
- **Selection `DrawMesh` needs explicit frustum cull:** HashGrid AOI ≠ camera frustum. Pick collect must `TestPlanesAABB` against the request camera (`IsInPickFrustum`) or Metastation pays full-AOI pick draws. See [selection](selection.md). Hit 2026-07-28.
- **GPU Instancing needs the material flag:** ST shaders compile instancing variants, but assets need `enableInstancing` / `m_EnableInstancingVariants: 1`. Floor ST mats were flipped in [srp-batcher-gpu-instancing](../2026-07_srp-batcher-gpu-instancing.md).
- **GPU Resident Drawer on Linux/OpenGL:** `m_GPUResidentDrawerMode` must stay **Disabled** (`0`) on `SS3D_URPAsset`. Instanced Drawing requires `BatchBufferTarget.RawBuffer`; unsupported APIs spam the warning every rebuild. Do not re-enable in `URPFoundationSetup` without checking the active graphics API. Do not confuse with classic GPU Instancing / SRP Batcher (still on).
- **ST meshes ignore blood/floor DecalProjectors:** Automatic Decal technique is DBuffer on desktop. `STDefault` ForwardLit must keep `#pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3` and `ApplyDecalToBaseColor` under `#ifdef _DBUFFER` — calling it without the keyword samples an unbound buffer (weight 0 → black mesh). DepthNormals alone only fixes Decal Layers filtering, not albedo tint.
- **Decal Layers must stay enabled:** `SS3D_ForwardPlusRenderer` Decal feature `decalLayers: 1`. Floor/bullet projectors target `ReceiveWorldDecals` only; characters stay Default. Turning layers off makes Lit tiles look fine while masking breaks for the intended filter path — do not disable during lighting/SSAO retunes.
- **Item/tile icons dark or black under half-toon / fixture-only lighting:** do not render icons with live ST half-toon mats or bare scene lights. Use `IconPreviewGenerator` (`Unlit/ObjectIcon` overrides). `RuntimePreviewGenerator` still spawns temporary preview lights as a fallback path — do not rely on scene lighting. Keep `ObjectIcon` in Always Included Shaders (same pattern as InteractionOutline).
- **Shiny player head under PointFill:** close URP point lights create a bright N·L hotspot on bald/curved meshes (bloom amplifies it). Soft-near atten in `STLighting.hlsl` + keep character `_SpecIntensity: 0`; raise/dim fill rather than copying Built-in intensities.
- **Vision FOV must not use fixed multi-hit RaycastAll buffers:** a dense prop pile can exhaust the hit slots and report a clear line through walls. Keep iterative/wave single-hit casts that skip non-occluders (`VisionSubSystem` `RaycastCommand` waves + collider occluder cache).
- **Vision map upload:** do not `new Color[]` / `SetPixels` on the LateUpdate path — dilate with persistent float scratch and upload R16 via `SetPixelData` (`Vision.VisionMap` was a multi-MB/frame GC hotspot).

## Depends on / Used by

- **Used by:** [selection](selection.md), [atmospherics](atmospherics.md), [screen-effects](screen-effects.md) / [machine-interface](machine-interface.md) (UI backdrop blur)
- **Vision FOV depends on:** `PlacedTileObject` Wall/Door (or `Walls` layer) colliders; cast origin from [entities](entities.md) `Entity.ViewPoint` when present

## Related docs

- [FORK_STATUS.md](../../FORK_STATUS.md) § URP migration
- Plan: [urp_lighting_look_plan_d42c32f5.plan.md](../../plans/urp_lighting_look_plan_d42c32f5.plan.md)
- Polish handoff: [2026-07_urp-lighting-look-polish.md](../2026-07_urp-lighting-look-polish.md)
- Effort (shipped): [2026-07_srp-batcher-gpu-instancing.md](../2026-07_srp-batcher-gpu-instancing.md)
- Effort (shipped): [2026-07_unity-framedebug-ai-tooling.md](../2026-07_unity-framedebug-ai-tooling.md) — Frame Debugger → `Logs/framedebug/` markdown
- Effort (planned): [2026-07_atmos-client-visualization-sync.md](../2026-07_atmos-client-visualization-sync.md)

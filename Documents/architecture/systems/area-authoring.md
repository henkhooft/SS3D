> Parent map: [area.md](area.md)
> Status: partial
> Verified: c161137b2 — 2026-10-31

# Area authoring

## Overview

**Scripts → Rooms** uses the existing editor toolbar: Construct paints the room selected in the palette; the New room palette entry creates and paints a room as one undo step. Select opens properties, Dropper picks a room and returns to Construct, and Delete erases membership. Rooms need no APC. Drag paints a line; Shift-drag fills a rectangle; Escape or switching tools cancels. A colored preview precedes the server edit. Walls/space are skipped; floor and door tiles accept membership. Properties also use the shared history. Deleting/replacing a room’s last floor/door tile retires its metadata and residual wall-origin IDs; undo restores the whole room. Partial erasure keeps the room. Palette selection centers the orbital camera on the room, preserving angle and zoom.

The first room creation/paint freezes the existing APC-generated partition as authored data. Authored maps never geometrically reflood on wall damage, APC placement/removal or template restore. APCs link to the room at their floor origin / front-facing tile, with at most one per room. Removing an APC leaves the room and its metadata intact; its local power becomes unavailable. Conflicting paint strokes are rejected atomically.

Room controls and Layer View Mode both use `MapEditorView.SwitchRow` via `AddSwitchRow`, including the same track/thumb USS; department mode shows read-only access bits and preserves the custom override. Areas is a separate default-off layer with chunk meshes and room-name labels. Scripts markers follow the Scripts category; Rooms keeps Areas enabled across Construct/Select/Dropper/Delete; leaving the category hides it. Manual acceptance of painting is pending.

## Start here

- `Assets/Scripts/SS3D/Systems/Area/AreaPaintingService.cs` — membership validation, reversible strokes, APC linking, metadata capture/restore
- `Assets/Scripts/SS3D/Systems/Area/AreaAuthoring.cs` — AreaSubSystem authoring API, mutation invalidation, map reset
- `Assets/Scripts/SS3D/Systems/Tile/MapEditor/MapEditorAreaPainting.cs` — drag input, bounded stroke geometry, admin RPCs, creation/stroke undo wrappers
- `Assets/Scripts/SS3D/Systems/Tile/MapEditor/MapEditorArea.cs` — selection, room metadata RPCs and undo
- `Assets/Scripts/SS3D/Systems/Tile/MapEditor/UI/MapEditorAreaView.cs` — brush controls, room form, name labels
- `Assets/Scripts/SS3D/Systems/Tile/MapEditor/MapEditorAreaOverlay.cs` — membership mesh and stroke preview
- `Assets/Scripts/SS3D/Systems/Tile/SavedObjects/SavedAreaRecord.cs` — optional `authored` / `noApc` flags

## Extension points

- `AreaSubSystem.TryPaintArea` delegates to the production painting service; server RPCs authorize before accessing it.
- `AreaPaintingService.AreaPaintChange` reverts the complete stroke including whether the map was already authored.
- Chunk `areaIds` already persist tile membership. `SavedAreaRecord.authored` and the area chunk’s `authored` flag prevent restore-time reflood (the chunk flag survives deletion of the final room); `noApc` keeps unpowered rooms out of the APC-position lookup. The flat legacy map carries `hasAuthoredAreas` through migration. Empty authored maps remain empty; missing flags preserve legacy APC-default behavior; no envelope schema bump is required for these additive fields.
- Map clear resets registries, authored state, buffered area/name/ambience/lighting snapshots and client editor selection. Do not leave records from the previous map alive.
- Membership changes invalidate device, electricity and atmos membership indexes and refresh lighting/visual snapshots.
- Wire protocol is 3 (painting and selection RPC changes).

## Pitfalls

- **Saved painted IDs disappear on load:** chunks already store membership, but deferred APC flood used to overwrite it. Authored records must suppress all geometric rebuild entry points, including deferred restore and APC removal.
- **Room drafts reset on a toast:** `RefreshPopover` recreates controls; keep the room popover mounted during unrelated chrome refreshes.
- **Blank text / white controls in builds:** runtime panels do not inherit the Editor theme. Room styles explicitly provide font, text colors, field geometry and pill appearance.

## Tests

`Assets/Scripts/Tests/EditMode/Area/AreaPaintingTests.cs`: transfer/erase undo, walls/space, APC relinking/removal, atomic multi-APC rejection, metadata + chunk membership JSON round-trip, legacy defaults and bounded stroke geometry. `MapEditorAreaViewTests` covers labels, layer/category state, shared layer/room switches, room palette controls and read-only department access. Compile + EditMode passed (`artifacts/ui/validate-20261031-143332/summary.md`: succeeded; all 2146 tests completed). Manual playtesting remains owner-run.

## Depends on / Used by

- **Depends on:** [tile](tile.md), [persistence](persistence.md), [id-access](id-access.md), [area](area.md)
- **Used by:** [map-authoring milestone](../../milestones/map-authoring.md) A2

## Related docs

- Design (read-only): [area](../../design/area.md) §3; [creative-mode](../../design/creative-mode.md) §§3, 6–7

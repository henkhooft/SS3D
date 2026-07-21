using SS3D.Data.AssetDatabases;
using System.Collections.Generic;
using UnityEngine;

namespace SS3D.Systems.Tile.MapEditor.Commands
{
    /// <summary>
    /// Builds invertible commands from client DTOs. Previous occupants / decal ids are
    /// snapshotted from the live map so the client does not need to send prior state.
    /// </summary>
    public static class MapEditorCommandFactory
    {
        public static IMapEditorCommand FromDto(MapEditorCommandDto dto, MapEditorCommandContext ctx)
        {
            return dto.Kind switch
            {
                MapEditorCommandKind.PlaceTile => CreatePlaceTile(dto, ctx),
                MapEditorCommandKind.PlaceItem =>
                    new PlaceItemCommand(dto.AssetName, dto.Position, dto.Direction),
                MapEditorCommandKind.ClearTile =>
                    new ClearTileCommand(dto.AssetName, dto.Position, dto.Direction),
                MapEditorCommandKind.ClearItem =>
                    new ClearItemCommand(dto.AssetName, dto.Position, dto.Direction),
                MapEditorCommandKind.MoveTile =>
                    new MoveTileCommand(dto.AssetName, dto.PreviousPosition, dto.Position, dto.Direction),
                MapEditorCommandKind.SetFloorDecal =>
                    CreateSetFloorDecal(dto.Position, dto.DecalId, ctx),
                MapEditorCommandKind.ClearFloorDecal =>
                    CreateSetFloorDecal(dto.Position, 0, ctx),
                _ => CreatePlaceTile(dto, ctx),
            };
        }

        public static bool TryCreatePlaceCommands(
            MapEditorCommandDto dto,
            MapEditorCommandContext ctx,
            List<IMapEditorCommand> into)
        {
            if (dto.Kind is not (MapEditorCommandKind.PlaceTile or MapEditorCommandKind.PlaceItem
                or MapEditorCommandKind.SetFloorDecal))
            {
                into.Add(FromDto(dto, ctx));
                return true;
            }

            if (dto.Kind == MapEditorCommandKind.PlaceTile)
            {
                GenericObjectSo asset = ctx.ResolveAsset(dto.AssetName);
                if (asset is not TileObjectSo tile || ctx.Construction == null)
                    return false;

                PreviewResult preview = ctx.Construction.TryPreviewTile(
                    tile, dto.Position, dto.Direction, dto.ReplaceExisting);
                if (!preview.CanBuild)
                    return false;

                into.Add(CreatePlaceTile(dto, ctx));
                return true;
            }

            if (dto.Kind == MapEditorCommandKind.SetFloorDecal)
            {
                if (ctx.Map == null)
                    return false;

                if (!ctx.Map.TryGetTileLocation(TileLayer.Plenum, dto.Position, out ITileLocation plenum)
                    || plenum.IsFullyEmpty())
                    return false;

                into.Add(CreateSetFloorDecal(dto.Position, dto.DecalId, ctx));
                return true;
            }

            into.Add(new PlaceItemCommand(dto.AssetName, dto.Position, dto.Direction));
            return true;
        }

        private static PlaceTileCommand CreatePlaceTile(MapEditorCommandDto dto, MapEditorCommandContext ctx)
        {
            string previousName = null;
            Direction previousDir = Direction.North;

            if (dto.ReplaceExisting && ctx?.Map != null)
            {
                GenericObjectSo asset = ctx.ResolveAsset(dto.AssetName);
                if (asset is TileObjectSo tile
                    && ctx.Map.TryGetTileLocation(tile.layer, dto.Position, out ITileLocation location)
                    && location.TryGetPlacedObject(out PlacedTileObject placed, dto.Direction)
                    && placed != null)
                {
                    previousName = placed.NameString;
                    previousDir = placed.Direction;
                }
            }

            return new PlaceTileCommand(
                dto.AssetName,
                dto.Position,
                dto.Direction,
                dto.ReplaceExisting,
                previousName,
                previousDir);
        }

        private static SetFloorDecalCommand CreateSetFloorDecal(
            Vector3 position, ushort decalId, MapEditorCommandContext ctx)
        {
            ushort previous = 0;
            if (ctx?.Map != null && ctx.Map.TryGetFloorDecalId(position, out ushort existing))
                previous = existing;

            return new SetFloorDecalCommand(position, decalId, previous);
        }
    }
}

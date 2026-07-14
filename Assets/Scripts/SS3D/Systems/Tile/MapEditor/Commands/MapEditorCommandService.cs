using SS3D.Data.AssetDatabases;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SS3D.Systems.Tile.MapEditor.Commands
{
    /// <summary>
    /// Executes invertible map editor mutations on the server tilemap.
    /// </summary>
    public sealed class MapEditorCommandService
    {
        private readonly MapEditorUndoStack _undoStack = new();
        private MapEditorCommandContext _context;
        private MapEditorPlacementMode _placementMode = MapEditorPlacementMode.Normal;

        public int UndoDepth => _undoStack.UndoDepth;
        public int RedoDepth => _undoStack.RedoDepth;

        public MapEditorPlacementMode PlacementMode
        {
            get => _placementMode;
            set => _placementMode = value;
        }

        public void Bind(TileMap map, TileResourceLoader loader, ConstructionService construction)
        {
            _context = new MapEditorCommandContext(map, loader, construction)
            {
                PlacementMode = _placementMode,
            };
        }

        public void ClearHistory() => _undoStack.Clear();

        public bool Execute(IMapEditorCommand command)
        {
            if (_context == null)
                return false;

            _context.PlacementMode = _placementMode;
            command.Apply(_context);
            _undoStack.Push(command);
            return true;
        }

        public bool ExecuteCompound(IReadOnlyList<IMapEditorCommand> commands)
        {
            if (commands == null || commands.Count == 0)
                return false;

            if (commands.Count == 1)
                return Execute(commands[0]);

            return Execute(new CompoundCommand(System.Array.ConvertAll(commands.ToArray(), c => c)));
        }

        public bool Undo()
        {
            if (_context == null)
                return false;

            _context.PlacementMode = _placementMode;
            return _undoStack.TryUndo(_context);
        }

        public bool Redo()
        {
            if (_context == null)
                return false;

            _context.PlacementMode = _placementMode;
            return _undoStack.TryRedo(_context);
        }

        public static IMapEditorCommand FromPlacement(string assetName, Vector3 position, Direction direction, bool replaceExisting, bool isItem)
        {
            return isItem
                ? new PlaceItemCommand(assetName, position, direction)
                : new PlaceTileCommand(assetName, position, direction, replaceExisting);
        }

        public static IMapEditorCommand FromClear(string assetName, Vector3 position, Direction direction, bool isItem)
        {
            return isItem
                ? new ClearItemCommand(assetName, position)
                : new ClearTileCommand(assetName, position, direction);
        }
    }
}

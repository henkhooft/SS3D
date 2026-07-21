using System.Collections.Generic;

namespace SS3D.Systems.Tile.MapEditor.Commands
{
    /// <summary>
    /// Server-side undo/redo stacks for map editor mutations.
    /// </summary>
    public sealed class MapEditorUndoStack
    {
        private const int MaxDepth = 50;

        private readonly Stack<IMapEditorCommand> _undo = new();
        private readonly Stack<IMapEditorCommand> _redo = new();

        public int UndoDepth => _undo.Count;
        public int RedoDepth => _redo.Count;

        public void Push(IMapEditorCommand command)
        {
            _undo.Push(command);
            _redo.Clear();

            while (_undo.Count > MaxDepth)
            {
                Stack<IMapEditorCommand> trimmed = new();
                int keep = MaxDepth;
                foreach (IMapEditorCommand item in _undo)
                {
                    if (keep-- <= 0)
                        break;
                    trimmed.Push(item);
                }

                _undo.Clear();
                foreach (IMapEditorCommand item in trimmed)
                    _undo.Push(item);
            }
        }

        public bool TryUndo(MapEditorCommandContext ctx)
        {
            if (_undo.Count == 0)
                return false;

            IMapEditorCommand command = _undo.Pop();
            command.Revert(ctx);
            _redo.Push(command);
            return true;
        }

        public bool TryRedo(MapEditorCommandContext ctx)
        {
            if (_redo.Count == 0)
                return false;

            IMapEditorCommand command = _redo.Pop();
            command.Apply(ctx);
            _undo.Push(command);
            return true;
        }

        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
        }
    }
}

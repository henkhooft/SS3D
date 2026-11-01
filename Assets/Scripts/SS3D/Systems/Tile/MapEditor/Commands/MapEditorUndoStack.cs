using System.Collections.Generic;

namespace SS3D.Systems.Tile.MapEditor.Commands
{
    /// <summary>
    /// Server-side undo/redo stacks for map editor mutations.
    /// </summary>
    public sealed class MapEditorUndoStack
    {
        private const int MaxDepth = 50;

        private readonly Stack<Entry> _undo = new();
        private readonly Stack<Entry> _redo = new();

        private sealed class Entry
        {
            public IMapEditorCommand Command;
            public long Before;
            public long After;
        }

        private long _nextRevision;
        private long _revision;
        private long _savedRevision;
        public long Revision => _revision;
        public bool HasUnsavedChanges => _revision != _savedRevision;
        public void MarkSaved() => _savedRevision = _revision;
        public void MarkNewMap()
        {
            Clear();
            _revision = ++_nextRevision;
        }

        public int UndoDepth => _undo.Count;
        public int RedoDepth => _redo.Count;

        public void Push(IMapEditorCommand command)
        {
            _undo.Push(new Entry { Command = command, Before = _revision, After = ++_nextRevision });
            _revision = _nextRevision;
            _redo.Clear();

            while (_undo.Count > MaxDepth)
            {
                Stack<Entry> trimmed = new();
                int keep = MaxDepth;
                foreach (Entry item in _undo)
                {
                    if (keep-- <= 0)
                        break;
                    trimmed.Push(item);
                }

                _undo.Clear();
                foreach (Entry item in trimmed)
                    _undo.Push(item);
            }
        }

        public bool TryUndo(MapEditorCommandContext ctx)
        {
            if (_undo.Count == 0)
                return false;

            Entry entry = _undo.Peek();
            entry.Command.Revert(ctx);
            _undo.Pop();
            _revision = entry.Before;
            _redo.Push(entry);
            return true;
        }

        public bool TryRedo(MapEditorCommandContext ctx)
        {
            if (_redo.Count == 0)
                return false;

            Entry entry = _redo.Peek();
            entry.Command.Apply(ctx);
            _redo.Pop();
            _revision = entry.After;
            _undo.Push(entry);
            return true;
        }

        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
            _revision = ++_nextRevision;
            _savedRevision = _revision;
        }
    }
}

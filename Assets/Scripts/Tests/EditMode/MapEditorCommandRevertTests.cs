using NUnit.Framework;
using SS3D.Systems.Tile;
using SS3D.Systems.Tile.MapEditor.Commands;

namespace SS3D.Tests.EditMode
{
    public sealed class MapEditorCommandRevertTests
    {
        [Test]
        public void UndoStack_PushUndoRedo_UpdatesDepth()
        {
            var stack = new MapEditorUndoStack();
            var map = TileMap.Create("undo-test");
            var ctx = new MapEditorCommandContext(map, null, new ConstructionService(map, new TileQueryService(map)));

            stack.Push(new CompoundCommand(System.Array.Empty<IMapEditorCommand>()));
            Assert.AreEqual(1, stack.UndoDepth);

            stack.TryUndo(ctx);
            Assert.AreEqual(0, stack.UndoDepth);
            Assert.AreEqual(1, stack.RedoDepth);

            stack.TryRedo(ctx);
            Assert.AreEqual(1, stack.UndoDepth);
            Assert.AreEqual(0, stack.RedoDepth);
        }
    }
}

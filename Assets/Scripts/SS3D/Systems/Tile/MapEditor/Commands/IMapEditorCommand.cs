namespace SS3D.Systems.Tile.MapEditor.Commands
{
    public interface IMapEditorCommand
    {
        MapEditorCommandDto ToDto();
        void Apply(MapEditorCommandContext ctx);
        void Revert(MapEditorCommandContext ctx);
    }
}

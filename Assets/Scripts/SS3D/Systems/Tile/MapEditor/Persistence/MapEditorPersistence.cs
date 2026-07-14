using SS3D.Data.Management;
using System.Collections.Generic;
using System.Linq;

namespace SS3D.Systems.Tile.MapEditor.Persistence
{
    public sealed class MapEditorMapEntry
    {
        public string Name { get; init; }
        public string DisplayLabel { get; init; }
        public bool IsQuicksave { get; init; }
    }

    /// <summary>
    /// Abstracts map template persistence for the editor save/load UI.
    /// </summary>
    public interface IMapEditorPersistence
    {
        IReadOnlyList<MapEditorMapEntry> ListMaps();
        bool Save(string mapName, bool overwrite);
        bool Load(string mapName);
        void Delete(string mapName);
        bool Rename(string oldName, string newName);
        bool Exists(string mapName);
    }

    /// <summary>
    /// v1 persistence via existing TileSubSystem + LocalStorage.
    /// </summary>
    public sealed class MapEditorLocalPersistence : IMapEditorPersistence
    {
        private readonly TileSubSystem _tileSystem;
        private const string QuicksavePrefix = "quicksave_";

        public MapEditorLocalPersistence(TileSubSystem tileSystem) => _tileSystem = tileSystem;

        public IReadOnlyList<MapEditorMapEntry> ListMaps()
        {
            List<string> files = LocalStorage.GetAllObjectsNameInFolder(_tileSystem.SavePath);
            return files
                .Select(f => f[..f.LastIndexOf('.')])
                .OrderByDescending(n => n.StartsWith(QuicksavePrefix))
                .ThenByDescending(n => n)
                .Select(n => new MapEditorMapEntry
                {
                    Name = n,
                    DisplayLabel = n,
                    IsQuicksave = n.StartsWith(QuicksavePrefix),
                })
                .ToList();
        }

        public bool Save(string mapName, bool overwrite)
        {
            _tileSystem.Save(_tileSystem.SavePath + "/" + mapName, overwrite);
            return true;
        }

        public bool Load(string mapName)
        {
            _tileSystem.Load(_tileSystem.SavePath + "/" + mapName);
            return true;
        }

        public void Delete(string mapName) =>
            LocalStorage.DeleteFile(_tileSystem.SavePath + "/" + mapName);

        public bool Rename(string oldName, string newName)
        {
            if (Exists(newName))
                return false;

            LocalStorage.RenameFile(_tileSystem.SavePath + "/" + oldName, _tileSystem.SavePath + "/" + newName);
            return true;
        }

        public bool Exists(string mapName) => _tileSystem.MapNameAlreadyExist(mapName);
    }
}

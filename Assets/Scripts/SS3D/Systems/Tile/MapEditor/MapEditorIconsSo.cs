using UnityEngine;

namespace SS3D.Systems.Tile.MapEditor
{
    /// <summary>
    /// Toolbar and library icon sprites for the map editor UI.
    /// </summary>
    [CreateAssetMenu(menuName = "SS3D/Map Editor/Icons", fileName = "MapEditorIcons")]
    public sealed class MapEditorIconsSo : ScriptableObject
    {
        [Header("Tools")]
        [SerializeField] private Sprite _select;
        [SerializeField] private Sprite _edit;
        [SerializeField] private Sprite _move;
        [SerializeField] private Sprite _undo;
        [SerializeField] private Sprite _redo;
        [SerializeField] private Sprite _quicksave;
        [SerializeField] private Sprite _openMap;

        [Header("View")]
        [SerializeField] private Sprite _resetView;
        [SerializeField] private Sprite _layers;
        [SerializeField] private Sprite _eye;
        [SerializeField] private Sprite _eyeOff;
        [SerializeField] private Sprite _settings;
        [SerializeField] private Sprite _exit;
        [SerializeField] private Sprite _search;
        [SerializeField] private Sprite _showUi;

        [Header("Modes")]
        [SerializeField] private Sprite _modeUpper;
        [SerializeField] private Sprite _modeLower;
        [SerializeField] private Sprite _modeItems;
        [SerializeField] private Sprite _modeScripting;

        [Header("Subcategories — Upper")]
        [SerializeField] private Sprite _subFlooring;
        [SerializeField] private Sprite _subTurfs;
        [SerializeField] private Sprite _subWalls;
        [SerializeField] private Sprite _subDoors;
        [SerializeField] private Sprite _subTileObjects;
        [SerializeField] private Sprite _subWallAttachments;

        [Header("Subcategories — Lower")]
        [SerializeField] private Sprite _subPiping;
        [SerializeField] private Sprite _subDisposals;
        [SerializeField] private Sprite _subBaseTiles;

        [Header("Subcategories — Items")]
        [SerializeField] private Sprite _subFoodDrink;
        [SerializeField] private Sprite _subTools;
        [SerializeField] private Sprite _subMedical;
        [SerializeField] private Sprite _subSecurity;
        [SerializeField] private Sprite _subMisc;

        [Header("Subcategories — Scripting")]
        [SerializeField] private Sprite _subAtmospherics;
        [SerializeField] private Sprite _subSpawnPlacements;
        [SerializeField] private Sprite _subRandomSpawners;
        [SerializeField] private Sprite _subTriggers;

        public Sprite GetToolIcon(MapEditorTool tool) =>
            tool switch
            {
                MapEditorTool.Select => _select,
                MapEditorTool.Edit => _edit,
                MapEditorTool.Move => _move,
                _ => null,
            };

        public Sprite GetModeIcon(MapEditorMode mode) =>
            mode switch
            {
                MapEditorMode.Upper => _modeUpper,
                MapEditorMode.Lower => _modeLower,
                MapEditorMode.Items => _modeItems,
                MapEditorMode.Scripting => _modeScripting,
                _ => null,
            };

        public Sprite GetSubcategoryIcon(MapEditorSubcategory subcategory) =>
            subcategory switch
            {
                MapEditorSubcategory.Flooring => _subFlooring,
                MapEditorSubcategory.Turfs => _subTurfs,
                MapEditorSubcategory.Walls => _subWalls,
                MapEditorSubcategory.Doors => _subDoors,
                MapEditorSubcategory.TileObjects => _subTileObjects,
                MapEditorSubcategory.WallAttachments => _subWallAttachments,
                MapEditorSubcategory.Piping => _subPiping,
                MapEditorSubcategory.Disposals => _subDisposals,
                MapEditorSubcategory.BaseTiles => _subBaseTiles,
                MapEditorSubcategory.FoodDrink => _subFoodDrink,
                MapEditorSubcategory.Tools => _subTools,
                MapEditorSubcategory.Medical => _subMedical,
                MapEditorSubcategory.Security => _subSecurity,
                MapEditorSubcategory.Misc => _subMisc,
                MapEditorSubcategory.Atmospherics => _subAtmospherics,
                MapEditorSubcategory.SpawnPlacements => _subSpawnPlacements,
                MapEditorSubcategory.RandomSpawners => _subRandomSpawners,
                MapEditorSubcategory.Triggers => _subTriggers,
                _ => null,
            };

        public Sprite Undo => _undo;
        public Sprite Redo => _redo;
        public Sprite Quicksave => _quicksave;
        public Sprite OpenMap => _openMap;
        public Sprite ResetView => _resetView;
        public Sprite Layers => _layers;
        public Sprite Eye => _eye;
        public Sprite EyeOff => _eyeOff;
        public Sprite Settings => _settings;
        public Sprite Exit => _exit;
        public Sprite Search => _search;
        public Sprite ShowUi => _showUi;
    }
}

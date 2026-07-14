using UnityEngine;
using UnityEngine.UIElements;

namespace SS3D.Systems.Tile.MapEditor
{
    /// <summary>
    /// Toolbar and library icon paths for the map editor UI.
    /// Icons are SVG assets imported as UI Toolkit <see cref="VectorImage"/>s.
    /// </summary>
    [CreateAssetMenu(menuName = "SS3D/Map Editor/Icons", fileName = "MapEditorIcons")]
    public sealed class MapEditorIconsSo : ScriptableObject
    {
        public static class Paths
        {
            public const string Select = "Assets/Art/Icons/external icons/sbed/select.svg";
            public const string Edit = "Assets/Art/Icons/external icons/delapouite/pencil.svg";
            public const string Move = "Assets/Art/Icons/external icons/delapouite/move.svg";
            public const string Undo = "Assets/Art/Icons/external icons/lorc/return-arrow.svg";
            public const string Redo = "Assets/Art/Icons/external icons/delapouite/share.svg";
            public const string Quicksave = "Assets/Art/Icons/external icons/delapouite/save.svg";
            public const string OpenMap = "Assets/Art/Icons/external icons/delapouite/open-folder.svg";
            public const string ResetView = "Assets/Art/Icons/external icons/lorc/compass.svg";
            public const string Layers = "Assets/Art/Icons/external icons/delapouite/stack.svg";
            public const string Eye = "Assets/Art/Icons/external icons/lorc/eyeball.svg";
            public const string EyeOff = "Assets/Art/Icons/external icons/delapouite/invisible.svg";
            public const string Settings = "Assets/Art/Icons/external icons/lorc/cog.svg";
            public const string Exit = "Assets/Art/Icons/external icons/delapouite/exit-door.svg";
            public const string Search = "Assets/Art/Icons/external icons/lorc/magnifying-glass.svg";
            public const string ModeUpper = "Assets/Art/Icons/external icons/delapouite/expand.svg";
            public const string ModeLower = "Assets/Art/Icons/external icons/delapouite/contract.svg";
            public const string ModeItems = "Assets/Art/Icons/external icons/delapouite/cube.svg";
            public const string ModeScripting = "Assets/Art/Icons/external icons/delapouite/plain-arrow.svg";
            public const string SubFlooring = "Assets/Art/Icons/external icons/delapouite/domino-tiles.svg";
            public const string SubTurfs = "Assets/Art/Icons/external icons/lorc/mountains.svg";
            public const string SubOverlays = "Assets/Art/Icons/external icons/delapouite/stack.svg";
            public const string SubWalls = "Assets/Art/Icons/external icons/delapouite/square.svg";
            public const string SubDoors = "Assets/Art/Icons/external icons/delapouite/door.svg";
            public const string SubTileObjects = "Assets/Art/Icons/external icons/delapouite/cube.svg";
            public const string SubWallAttachments = "Assets/Art/Icons/external icons/delapouite/pin.svg";
            public const string SubPiping = "Assets/Art/Icons/external icons/delapouite/pipes.svg";
            public const string SubDisposals = "Assets/Art/Icons/external icons/delapouite/trash-can.svg";
            public const string SubBaseTiles = "Assets/Art/Icons/external icons/delapouite/plain-square.svg";
            public const string SubFoodDrink = "Assets/Art/Icons/external icons/delapouite/cube.svg";
            public const string SubTools = "Assets/Art/Icons/external icons/lorc/cog.svg";
            public const string SubMedical = "Assets/Art/Icons/external icons/delapouite/cube.svg";
            public const string SubSecurity = "Assets/Art/Icons/external icons/lorc/heavy-lightning.svg";
            public const string SubMisc = "Assets/Art/Icons/external icons/delapouite/plain-circle.svg";
            public const string SubAtmospherics = "Assets/Art/Icons/external icons/delapouite/pipes.svg";
            public const string SubSpawnPlacements = "Assets/Art/Icons/external icons/delapouite/pin.svg";
            public const string SubRandomSpawners = "Assets/Art/Icons/external icons/delapouite/rolling-dices.svg";
            public const string SubTriggers = "Assets/Art/Icons/external icons/lorc/focused-lightning.svg";
        }

        [Header("Baked references (filled by SS3D/Map Editor/Bake Icons for player builds)")]
        [SerializeField] private VectorImage _select;
        [SerializeField] private VectorImage _edit;
        [SerializeField] private VectorImage _move;
        [SerializeField] private VectorImage _undo;
        [SerializeField] private VectorImage _redo;
        [SerializeField] private VectorImage _quicksave;
        [SerializeField] private VectorImage _openMap;
        [SerializeField] private VectorImage _resetView;
        [SerializeField] private VectorImage _layers;
        [SerializeField] private VectorImage _eye;
        [SerializeField] private VectorImage _eyeOff;
        [SerializeField] private VectorImage _settings;
        [SerializeField] private VectorImage _exit;
        [SerializeField] private VectorImage _search;
        [SerializeField] private VectorImage _modeUpper;
        [SerializeField] private VectorImage _modeLower;
        [SerializeField] private VectorImage _modeItems;
        [SerializeField] private VectorImage _modeScripting;
        [SerializeField] private VectorImage _subFlooring;
        [SerializeField] private VectorImage _subTurfs;
        [SerializeField] private VectorImage _subOverlays;
        [SerializeField] private VectorImage _subWalls;
        [SerializeField] private VectorImage _subDoors;
        [SerializeField] private VectorImage _subTileObjects;
        [SerializeField] private VectorImage _subWallAttachments;
        [SerializeField] private VectorImage _subPiping;
        [SerializeField] private VectorImage _subDisposals;
        [SerializeField] private VectorImage _subBaseTiles;
        [SerializeField] private VectorImage _subFoodDrink;
        [SerializeField] private VectorImage _subTools;
        [SerializeField] private VectorImage _subMedical;
        [SerializeField] private VectorImage _subSecurity;
        [SerializeField] private VectorImage _subMisc;
        [SerializeField] private VectorImage _subAtmospherics;
        [SerializeField] private VectorImage _subSpawnPlacements;
        [SerializeField] private VectorImage _subRandomSpawners;
        [SerializeField] private VectorImage _subTriggers;

        public VectorImage GetToolIcon(MapEditorTool tool) =>
            tool switch
            {
                MapEditorTool.Select => Resolve(_select, Paths.Select),
                MapEditorTool.Edit => Resolve(_edit, Paths.Edit),
                MapEditorTool.Move => Resolve(_move, Paths.Move),
                _ => null,
            };

        public VectorImage GetModeIcon(MapEditorMode mode) =>
            mode switch
            {
                MapEditorMode.Upper => Resolve(_modeUpper, Paths.ModeUpper),
                MapEditorMode.Lower => Resolve(_modeLower, Paths.ModeLower),
                MapEditorMode.Items => Resolve(_modeItems, Paths.ModeItems),
                MapEditorMode.Scripting => Resolve(_modeScripting, Paths.ModeScripting),
                _ => null,
            };

        public VectorImage GetSubcategoryIcon(MapEditorSubcategory subcategory) =>
            subcategory switch
            {
                MapEditorSubcategory.Flooring => Resolve(_subFlooring, Paths.SubFlooring),
                MapEditorSubcategory.Turfs => Resolve(_subTurfs, Paths.SubTurfs),
                MapEditorSubcategory.Overlays => Resolve(_subOverlays, Paths.SubOverlays),
                MapEditorSubcategory.Walls => Resolve(_subWalls, Paths.SubWalls),
                MapEditorSubcategory.Doors => Resolve(_subDoors, Paths.SubDoors),
                MapEditorSubcategory.TileObjects => Resolve(_subTileObjects, Paths.SubTileObjects),
                MapEditorSubcategory.WallAttachments => Resolve(_subWallAttachments, Paths.SubWallAttachments),
                MapEditorSubcategory.Piping => Resolve(_subPiping, Paths.SubPiping),
                MapEditorSubcategory.Disposals => Resolve(_subDisposals, Paths.SubDisposals),
                MapEditorSubcategory.BaseTiles => Resolve(_subBaseTiles, Paths.SubBaseTiles),
                MapEditorSubcategory.FoodDrink => Resolve(_subFoodDrink, Paths.SubFoodDrink),
                MapEditorSubcategory.Tools => Resolve(_subTools, Paths.SubTools),
                MapEditorSubcategory.Medical => Resolve(_subMedical, Paths.SubMedical),
                MapEditorSubcategory.Security => Resolve(_subSecurity, Paths.SubSecurity),
                MapEditorSubcategory.Misc => Resolve(_subMisc, Paths.SubMisc),
                MapEditorSubcategory.Atmospherics => Resolve(_subAtmospherics, Paths.SubAtmospherics),
                MapEditorSubcategory.SpawnPlacements => Resolve(_subSpawnPlacements, Paths.SubSpawnPlacements),
                MapEditorSubcategory.RandomSpawners => Resolve(_subRandomSpawners, Paths.SubRandomSpawners),
                MapEditorSubcategory.Triggers => Resolve(_subTriggers, Paths.SubTriggers),
                _ => null,
            };

        public VectorImage Undo => Resolve(_undo, Paths.Undo);
        public VectorImage Redo => Resolve(_redo, Paths.Redo);
        public VectorImage Quicksave => Resolve(_quicksave, Paths.Quicksave);
        public VectorImage OpenMap => Resolve(_openMap, Paths.OpenMap);
        public VectorImage ResetView => Resolve(_resetView, Paths.ResetView);
        public VectorImage Layers => Resolve(_layers, Paths.Layers);
        public VectorImage Eye => Resolve(_eye, Paths.Eye);
        public VectorImage EyeOff => Resolve(_eyeOff, Paths.EyeOff);
        public VectorImage Settings => Resolve(_settings, Paths.Settings);
        public VectorImage Exit => Resolve(_exit, Paths.Exit);
        public VectorImage Search => Resolve(_search, Paths.Search);
        public VectorImage ShowUi => Resolve(_eye, Paths.Eye);

        private static VectorImage Resolve(VectorImage baked, string path) =>
            baked != null ? baked : MapEditorIconLoader.Load(path);
    }
}

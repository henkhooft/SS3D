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
            private const string Root = "Assets/Art/Icons/map-editor/";

            public const string Select = Root + "select.svg";
            public const string Construct = Root + "construct.svg";
            public const string Move = Root + "move.svg";
            public const string Dropper = Root + "dropper.svg";
            public const string Delete = Root + "trash.svg";
            public const string Undo = Root + "undo.svg";
            public const string Redo = Root + "redo.svg";
            public const string SaveMap = Root + "saveAs.svg";
            public const string OpenMap = Root + "openMap.svg";
            public const string RotateLeft = Root + "rotateLeft.svg";
            public const string RotateRight = Root + "rotateRight.svg";
            public const string ZoomIn = Root + "zoomIn.svg";
            public const string ZoomOut = Root + "zoomOut.svg";
            public const string PanelExpand = Root + "chevronUp.svg";
            public const string PanelCollapse = Root + "chevronDown.svg";
            public const string ResetView = Root + "resetPosition.svg";
            public const string Layers = Root + "layers.svg";
            public const string EyeOff = Root + "eyeOff.svg";
            public const string Camera = Root + "camera.svg";
            public const string Settings = Root + "settings.svg";
            public const string Search = Root + "search.svg";
            public const string ModeUpper = Root + "chevronUp.svg";
            public const string ModeLower = Root + "chevronDown.svg";
            public const string ModeItems = Root + "box.svg";
            public const string ModeScripting = Root + "script.svg";
            public const string SubFlooring = Root + "grid.svg";
            public const string SubTurfs = Root + "terrain.svg";
            public const string SubOverlays = Root + "layers.svg";
            public const string SubWalls = Root + "bracket.svg";
            public const string SubDoors = Root + "door.svg";
            public const string SubTileObjects = Root + "cube.svg";
            public const string SubWallAttachments = Root + "bracket.svg";
            public const string SubPiping = Root + "pipe.svg";
            public const string SubDisposals = Root + "trash.svg";
            public const string SubBaseTiles = Root + "dots9.svg";
            public const string SubFoodDrink = Root + "box.svg";
            public const string SubTools = Root + "settings.svg";
            public const string SubMedical = Root + "cube.svg";
            public const string SubSecurity = Root + "bolt.svg";
            public const string SubMisc = Root + "dots9.svg";
            public const string SubAtmospherics = Root + "pipe.svg";
            public const string SubSpawnPlacements = Root + "pin.svg";
            public const string SubRandomSpawners = Root + "dice.svg";
            public const string SubTriggers = Root + "bolt.svg";
        }

        [Header("Baked references (filled by SS3D/Map Editor/Bake Icons for player builds)")]
        [SerializeField] private VectorImage _select;
        [SerializeField] private VectorImage _construct;
        [SerializeField] private VectorImage _move;
        [SerializeField] private VectorImage _dropper;
        [SerializeField] private VectorImage _delete;
        [SerializeField] private VectorImage _undo;
        [SerializeField] private VectorImage _redo;
        [SerializeField] private VectorImage _saveMap;
        [SerializeField] private VectorImage _openMap;
        [SerializeField] private VectorImage _rotateLeft;
        [SerializeField] private VectorImage _rotateRight;
        [SerializeField] private VectorImage _zoomIn;
        [SerializeField] private VectorImage _zoomOut;
        [SerializeField] private VectorImage _panelExpand;
        [SerializeField] private VectorImage _panelCollapse;
        [SerializeField] private VectorImage _resetView;
        [SerializeField] private VectorImage _layers;
        [SerializeField] private VectorImage _eyeOff;
        [SerializeField] private VectorImage _camera;
        [SerializeField] private VectorImage _settings;
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
                MapEditorTool.Edit => Resolve(_construct, Paths.Construct),
                MapEditorTool.Move => Resolve(_move, Paths.Move),
                MapEditorTool.Dropper => Resolve(_dropper, Paths.Dropper),
                MapEditorTool.Delete => Resolve(_delete, Paths.Delete),
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
        public VectorImage SaveMap => Resolve(_saveMap, Paths.SaveMap);
        public VectorImage OpenMap => Resolve(_openMap, Paths.OpenMap);
        public VectorImage RotateLeft => Resolve(_rotateLeft, Paths.RotateLeft);
        public VectorImage RotateRight => Resolve(_rotateRight, Paths.RotateRight);
        public VectorImage ZoomIn => Resolve(_zoomIn, Paths.ZoomIn);
        public VectorImage ZoomOut => Resolve(_zoomOut, Paths.ZoomOut);
        public VectorImage PanelExpand => Resolve(_panelExpand, Paths.PanelExpand);
        public VectorImage PanelCollapse => Resolve(_panelCollapse, Paths.PanelCollapse);
        public VectorImage ResetView => Resolve(_resetView, Paths.ResetView);
        public VectorImage Layers => Resolve(_layers, Paths.Layers);
        public VectorImage Camera => Resolve(_camera, Paths.Camera);
        public VectorImage Settings => Resolve(_settings, Paths.Settings);
        public VectorImage Search => Resolve(_search, Paths.Search);

        /// <summary>
        /// Icon for the floating reveal button, visible only while the HUD is hidden (toggled via
        /// the F7 hotkey — the toolbar no longer has an explicit Hide UI button).
        /// </summary>
        public VectorImage ShowUi => Resolve(_eyeOff, Paths.EyeOff);

        private static VectorImage Resolve(VectorImage baked, string path) =>
            baked != null ? baked : MapEditorIconLoader.Load(path);
    }
}

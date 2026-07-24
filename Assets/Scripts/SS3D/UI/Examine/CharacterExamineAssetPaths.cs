namespace SS3D.UI.Examine
{
    /// <summary>
    /// Stable asset paths for the character-examine UI. Rebuild the committed catalog with
    /// <c>SS3D → Examine → Rebuild Character Examine Asset Catalog</c>.
    /// </summary>
    public static class CharacterExamineAssetPaths
    {
        public const string ResourcesCatalogName = "CharacterExamineAssetCatalog";
        public const string CatalogAssetPath =
            "Assets/Content/Systems/UI/Examine/Resources/CharacterExamineAssetCatalog.asset";

        public const string StyleSheet = "Assets/Content/Systems/UI/Examine/CharacterExamine.uss";
        public const string InventorySlotStyle =
            "Assets/Content/Systems/UI/MachineInterface/Components/InventorySlot.uss";
        public const string MachineWindowStyle =
            "Assets/Content/Systems/UI/MachineInterface/Components/MachineWindow.uss";

        // InventorySlot.uss reads --ss3d-diegetic-* custom properties defined only here — included so
        // the paperdoll's slot wells render their fill/border regardless of what else is loaded.
        public const string DiegeticTokensStyle =
            "Assets/Content/Systems/UI/MachineInterface/Tokens/diegetic-tokens.uss";

        public const string IconRoot = "Assets/Art/Icons/Inventory/";
    }
}

using SS3D.Systems.Tile;

namespace SS3D.Systems.StructuralDamage
{
    /// <summary>
    /// Provisional integrity thresholds (balance deferred). Stage from remaining HP fraction.
    /// </summary>
    public static class StructuralIntegrityRules
    {
        public const float DefaultWallMaxIntegrity = 100f;
        public const float DefaultDoorMaxIntegrity = 80f;
        public const float DefaultWindowMaxIntegrity = 50f;

        /// <summary>Remaining fraction at or below which the tile becomes Damaged.</summary>
        public const float DamagedFraction = 0.7f;

        /// <summary>Remaining fraction at or below which the tile becomes Cracked/venting.</summary>
        public const float CrackedFraction = 0.35f;

        public static bool IsStructural(PlacedTileObject placed)
        {
            if (placed == null || placed.Layer != TileLayer.Turf)
                return false;

            TileObjectGenericType generic = placed.GenericType;
            return generic == TileObjectGenericType.Wall || generic == TileObjectGenericType.Door;
        }

        public static bool IsStructural(TileObjectSo tileObjectSo)
        {
            if (tileObjectSo == null || tileObjectSo.layer != TileLayer.Turf)
                return false;

            return tileObjectSo.genericType == TileObjectGenericType.Wall
                || tileObjectSo.genericType == TileObjectGenericType.Door;
        }

        public static float ResolveMaxIntegrity(TileObjectSo tileObjectSo)
        {
            if (tileObjectSo == null)
                return DefaultWallMaxIntegrity;

            if (tileObjectSo.structuralMaxIntegrity > 0f)
                return tileObjectSo.structuralMaxIntegrity;

            if (tileObjectSo.genericType == TileObjectGenericType.Door)
                return DefaultDoorMaxIntegrity;

            if (TileOccupancyEvaluator.IsWindowName(tileObjectSo.NameString))
                return DefaultWindowMaxIntegrity;

            return DefaultWallMaxIntegrity;
        }

        public static StructuralIntegrityStage StageFromRemaining(float remaining, float maxIntegrity)
        {
            if (maxIntegrity <= 0f || remaining <= 0f)
                return StructuralIntegrityStage.Destroyed;

            float fraction = remaining / maxIntegrity;
            if (fraction <= CrackedFraction)
                return StructuralIntegrityStage.Cracked;

            if (fraction <= DamagedFraction)
                return StructuralIntegrityStage.Damaged;

            return StructuralIntegrityStage.Intact;
        }

        public static bool LeaksAtmosphere(StructuralIntegrityStage stage)
        {
            return stage == StructuralIntegrityStage.Cracked
                || stage == StructuralIntegrityStage.Destroyed;
        }
    }
}

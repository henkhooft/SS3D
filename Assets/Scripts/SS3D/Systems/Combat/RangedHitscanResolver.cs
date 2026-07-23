using SS3D.Systems.Health;
using SS3D.Systems.Tile;
using SS3D.Utils;
using UnityEngine;

namespace SS3D.Systems.Combat
{
    /// <summary>
    /// Server hitscan resolve: accuracy-cone sample → LOS → living zone or structural turf.
    /// </summary>
    public static class RangedHitscanResolver
    {
        private static readonly LayerMask DefaultOcclusionMask = LayerMask.GetMask("Default");

        public static bool TryResolveShot(
            Ray aimRay,
            in RangedWeaponProfile profile,
            float spreadDegrees,
            System.Random rng,
            HumanHealthController excludeHealth,
            out HumanHealthController health,
            out BodyZone zone,
            out TileCoord structuralCoord,
            out bool hitLiving,
            out bool hitStructural)
        {
            health = null;
            zone = BodyZone.Chest;
            structuralCoord = default;
            hitLiving = false;
            hitStructural = false;

            float maxRange = Mathf.Max(1f, profile.MaxRangeMeters);
            Vector3 shotDir = AccuracyCone.SampleDirection(aimRay.direction, spreadDegrees, rng);
            Ray shotRay = new(aimRay.origin, shotDir);

            // Occlusion: wall before anything else stops the round (whiff).
            if (RangedStructuralHitResolver.IsOccluded(shotRay, maxRange, DefaultOcclusionMask, out float blockDistance))
            {
                // Prefer structural damage on the blocking wall when it is structural turf.
                if (RangedStructuralHitResolver.TryResolve(shotRay, blockDistance + 0.05f, out structuralCoord, out _))
                {
                    hitStructural = true;
                    return true;
                }

                return false;
            }

            if (ZoneTargetResolver.TryResolveHoverZone(
                    shotRay,
                    excludeHealth,
                    maxRange,
                    out zone,
                    out health,
                    out Vector3 hitPoint,
                    out _))
            {
                // Default-layer occluder between muzzle and limb absorbs the round.
                float limbDistance = Vector3.Distance(shotRay.origin, hitPoint);
                if (!LineOfSight.HasLineOfSight(shotRay.origin, hitPoint, DefaultOcclusionMask, out _))
                {
                    if (RangedStructuralHitResolver.TryResolve(shotRay, limbDistance, out structuralCoord, out _))
                    {
                        health = null;
                        hitStructural = true;
                        return true;
                    }

                    health = null;
                    return false;
                }

                hitLiving = true;
                return true;
            }

            if (RangedStructuralHitResolver.TryResolve(shotRay, maxRange, out structuralCoord, out _))
            {
                hitStructural = true;
                return true;
            }

            return false;
        }
    }
}

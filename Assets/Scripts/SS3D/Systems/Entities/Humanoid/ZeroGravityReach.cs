using SS3D.Systems.Inventory.Items;
using SS3D.Systems.Tile;
using UnityEngine;

namespace SS3D.Systems.Entities.Humanoid
{
    /// <summary>
    /// The physics side of R5: whether static station geometry lies within <see cref="ZeroGravityRules.ReachRadius"/> of
    /// a floating body. Walls, windows, grilles, airlocks, girders, lattices and the floor's edge count; lockers, items
    /// and bodies don't (<see cref="ZeroGravityRules.CountsAsReach"/>). Owner-side: a pure client whose tiles haven't
    /// arrived finds no colliders and so no reach, never a false one (Decision 21).
    /// </summary>
    public static class ZeroGravityReach
    {
        private static readonly Collider[] Hits = new Collider[16];
        private static int _mask = -1;

        /// <summary>Tile colliders live on these layers (Decision 46).</summary>
        public static int Mask => _mask != -1 ? _mask : _mask = LayerMask.GetMask("Default", "Walls");

        public static bool IsStructureInReach(Vector3 centre)
        {
            int count = Physics.OverlapSphereNonAlloc(centre, ZeroGravityRules.ReachRadius, Hits, Mask,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (CountsAsReach(Hits[i]))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool CountsAsReach(Collider hit) => ZeroGravityRules.CountsAsReach(
            hit.GetComponentInParent<PlacedTileObject>() != null,
            hit.GetComponentInParent<Draggable>() != null,
            hit.GetComponentInParent<Item>() != null,
            hit.GetComponentInParent<Entity>() != null);
    }
}

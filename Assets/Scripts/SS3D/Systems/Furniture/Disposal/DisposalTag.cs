using SS3D.Systems.IdAccess;
using UnityEngine;

namespace SS3D.Systems.Furniture.Disposal
{
    /// <summary>
    /// Runtime destination label attached to an item at a chute's tagger interaction (design doc §4).
    /// A real, examine-readable property on the object itself — not hidden metadata.
    /// </summary>
    public sealed class DisposalTag : MonoBehaviour
    {
        public Department Destination { get; private set; } = Department.None;

        public bool IsTagged => Destination != Department.None;

        public void SetDestination(Department department)
        {
            Destination = department;
        }

        public void Clear()
        {
            Destination = Department.None;
        }
    }
}

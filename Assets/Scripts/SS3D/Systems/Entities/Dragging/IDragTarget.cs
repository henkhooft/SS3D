using FishNet.Object;
using UnityEngine;

namespace SS3D.Systems.Entities.Dragging
{
    /// <summary>
    /// Something a body can drag on a rope: furniture (<see cref="Draggable"/>) or a Downed, Critical or dead body
    /// (<c>BodyDraggable</c>). The dragger is replicated, so every peer knows who drags what (<see cref="DragRegistry"/>).
    /// </summary>
    public interface IDragTarget
    {
        /// <summary>Replicated: someone drags this.</summary>
        bool Dragged { get; }

        /// <summary>Replicated: the dragging body's <see cref="NetworkObject"/>, or null.</summary>
        NetworkObject DraggerObject { get; }

        GameObject GetGameObject();

        /// <summary>Whether <paramref name="dragger"/> may start dragging this now. Shared by the menu and the server.</summary>
        bool CanBeDraggedBy(Transform dragger);

        /// <summary>Whether this lies in the start cone in front of <paramref name="dragger"/> (Decision 26).</summary>
        bool IsInStartCone(Transform dragger);

        /// <summary>Server: start dragging with <paramref name="dragger"/>, or let go with false.</summary>
        void ServerSetDrag(bool drag, Transform dragger);
    }
}

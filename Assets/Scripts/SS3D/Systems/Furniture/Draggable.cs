using Coimbra.Services.Events;
using Coimbra.Services.PlayerLoopEvents;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using SS3D.Core;
using SS3D.Core.Behaviours;
using SS3D.Interactions;
using SS3D.Interactions.Interfaces;
using SS3D.Systems.Entities;
using SS3D.Systems.Entities.Dragging;
using SS3D.Systems.Furniture;
using SS3D.Systems.Inventory.Interactions;
using SS3D.Systems.Selection;
using UnityEngine;
using SS3D.Systems.Inventory.Containers;

/// <summary>
/// Put this script on things that can be dragged by a player, such as unbolted furnitures.
/// </summary>
[RequireComponent(typeof(Selectable))]
public class Draggable : NetworkActor, IInteractionTarget, IGameObjectProvider, IDragTarget
{
    [SerializeField]
    private Transform _dragger;

    private Vector3 _draggerToDragged;

    /// <summary>The dragging body. Replicated so every peer's <see cref="DragRegistry"/> knows who drags what.</summary>
    [SyncVar(OnChange = nameof(OnDraggerObjectChanged))]
    private NetworkObject _draggerObject;

    public bool Dragged => _draggerObject != null;

    [SyncVar]
    private HandSide _draggerHand;

    public HandSide DraggerHand => _draggerHand;

    public NetworkObject DraggerObject => _draggerObject;

    public GameObject GetGameObject() => gameObject;

    private void OnDraggerObjectChanged(NetworkObject previous, NetworkObject next, bool asServer) =>
        DragRegistry.OnDraggerChanged(this, previous, next);

    public override void OnStopNetwork()
    {
        DragRegistry.Forget(this);
        base.OnStopNetwork();
    }

    public bool CanBeDraggedBy(Transform dragger) =>
        MayChangeDrag(true, dragger, Dragged ? _dragger : null, transform.position) && (Dragged || !IsLocked);

    /// <summary>Furniture keeps the narrow 20° cone it always had: its pivot is the thing in front of you.</summary>
    public bool IsInStartCone(Transform dragger)
    {
        Vector3 toTarget = transform.position - dragger.position;
        return Vector3.Angle(dragger.forward, toTarget.normalized) <= 20f;
    }

    public void ServerSetDrag(bool drag, Transform dragger, HandSide hand = HandSide.Right)
    {
        if (drag && !Dragged)
            _draggerHand = hand;

        if (drag && !Dragged && IsLocked)
            return;

        // Only the body dragging it lets go; a second body can't take it over (Decision 13).
        if (Dragged && dragger != null && dragger != _dragger)
            return;

        ApplyDrag(drag, dragger);
    }

    /// <summary>
    /// How far (horizontally) the server lets a client's body be from this object when it starts a drag. Generous,
    /// because the object's pivot can sit well past the edge the player clicked, and the server's copy of the body lags.
    /// </summary>
    public const float MaxDragStartDistance = 3f;

    public IInteraction[] CreateTargetInteractions(InteractionEvent interactionEvent)
    {
        if (!Dragged && IsLocked)
            return System.Array.Empty<IInteraction>();

        DragInteraction dragInteraction = new DragInteraction();
        return new IInteraction[] { dragInteraction };
    }

    /// <summary>True when a sibling <see cref="IDragLock"/> forbids starting a drag.</summary>
    public bool IsLocked => TryGetComponent(out IDragLock dragLock) && dragLock.DragLocked;

    protected override void OnStart()
    {
        AddHandle(UpdateEvent.AddListener(HandleUpdate));
    }

    private void HandleUpdate(ref EventContext context, in UpdateEvent e)
    {
        if (!IsServer || !Dragged)
            return;

        if (_dragger == null)
        {
            // The dragger despawned.
            ApplyDrag(false, null);
            return;
        }

        transform.position =
            new Vector3(_dragger.position.x, transform.position.y, _dragger.position.z) + _draggerToDragged;
    }

    public void SetDrag(bool drag, Transform dragger)
    {
        if (IsServer)
        {
            ServerSetDrag(drag, dragger);
            return;
        }

        CmdSetDrag(drag);
    }

    /// <summary>
    /// The dragger is always the caller's own body, resolved on the server (networking §2). Starting needs that body
    /// within <see cref="MaxDragStartDistance"/>; only the body dragging the object may let go of it.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void CmdSetDrag(bool drag, NetworkConnection conn = null)
    {
        if (!SubSystems.TryGet(out EntitySubSystem entities) || !entities.TryGetOwnedEntity(conn, out Entity entity))
            return;

        if (!MayChangeDrag(drag, entity.transform, Dragged ? _dragger : null, transform.position))
            return;

        ServerSetDrag(drag, entity.transform);
    }

    /// <summary>Server rule for a client's drag request; pure, so EditMode can test it.</summary>
    public static bool MayChangeDrag(bool drag, Transform caller, Transform currentDragger, Vector3 draggedPosition)
    {
        if (caller == null)
            return false;

        if (!drag)
            return currentDragger == null || currentDragger == caller;

        if (currentDragger != null && currentDragger != caller)
            return false;

        Vector3 offset = draggedPosition - caller.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= MaxDragStartDistance * MaxDragStartDistance;
    }

    private void ApplyDrag(bool drag, Transform dragger)
    {
        _dragger = drag ? dragger : null;
        _draggerObject = _dragger != null ? _dragger.GetComponent<NetworkObject>() : null;

        if (_dragger == null)
            return;

        _draggerToDragged = new Vector3(
            transform.position.x - _dragger.position.x,
            0f,
            transform.position.z - _dragger.position.z);
    }
}

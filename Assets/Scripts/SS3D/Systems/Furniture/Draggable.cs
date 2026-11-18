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
using SS3D.Systems.Entities.Humanoid;
using SS3D.Systems.Entities.Humanoid.Body;
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

    /// <summary>The dragging body. Replicated so every peer's <see cref="DragRegistry"/> knows who drags what.</summary>
    [SyncVar(OnChange = nameof(OnDraggerObjectChanged))]
    private NetworkObject _draggerObject;

    public bool Dragged => _draggerObject != null;

    [SyncVar]
    private HandSide _draggerHand;

    public HandSide DraggerHand => _draggerHand;

    public NetworkObject DraggerObject => _draggerObject;

    public GameObject GetGameObject() => gameObject;

    public Vector3 FootprintPosition => transform.position;

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

    /// <summary>Rope length, set when the drag starts (<see cref="DragRules.RestDistance"/>).</summary>
    private float _restDistance;
    private float _breakOverSeconds;

    private static readonly RaycastHit[] SweepHits = new RaycastHit[16];
    private static int _sweepMask = -1;

    /// <summary>
    /// The object trails on the rope (Decision 12) and never passes through walls, windows or closed door leaves: each
    /// step sweeps its colliders' box along the move and stops at the first hit (furniture map Pitfalls). The server runs
    /// the end rules here too (Decision 13).
    /// </summary>
    private void HandleUpdate(ref EventContext context, in UpdateEvent e)
    {
        if (!IsServer || !Dragged)
            return;

        DragEndReason reason = EvaluateEnd(Time.deltaTime);
        if (reason != DragEndReason.None)
        {
            ApplyDrag(false, null);
            return;
        }

        Vector3 tether = DragRules.TetherPoint(_dragger.position, transform.position, _restDistance);
        Vector3 delta = tether - transform.position;
        delta.y = 0f;
        if (delta.sqrMagnitude < 0.000001f)
            return;

        transform.position += DragRules.SweptStep(delta, TrySweep(delta, out float hitDistance), hitDistance);
    }

    private DragEndReason EvaluateEnd(float seconds)
    {
        bool draggerGone = _dragger == null;
        Vector3 apart = draggerGone ? Vector3.zero : _dragger.position - transform.position;
        apart.y = 0f;
        return DragRules.EvaluateEnd(new DragEndInputs
        {
            DraggerGone = draggerGone,
            DraggerCanAct = draggerGone || !_dragger.TryGetComponent(out Ragdoll body)
                || body.Presentation == BodyPresentationState.Locomotion,
            Snagged = DragRules.TickBreak(ref _breakOverSeconds, apart.magnitude, seconds),
            TargetStillDraggable = true,
            DraggerFloating = !draggerGone && HumanoidSpaceSupport.IsUnsupportedAt(_dragger.position),
            TargetFloating = HumanoidSpaceSupport.IsUnsupportedAt(transform.position),
        });
    }

    /// <summary>
    /// Casts the box around this object's solid colliders along <paramref name="delta"/>, lifted and trimmed off the
    /// floor it stands on. Bodies and items (other layers) and this object's own colliders don't stop it.
    /// </summary>
    private bool TrySweep(Vector3 delta, out float hitDistance)
    {
        hitDistance = 0f;
        if (!TryGetSolidBounds(out Bounds bounds))
            return false;

        if (_sweepMask == -1)
            _sweepMask = LayerMask.GetMask("Default", "Walls");

        const float floorClearance = 0.06f;
        Vector3 extents = bounds.extents;
        extents.y = Mathf.Max(0.01f, extents.y - floorClearance);
        Vector3 centre = bounds.center + Vector3.up * floorClearance;
        float distance = delta.magnitude;
        int count = Physics.BoxCastNonAlloc(centre, extents, delta / distance, SweepHits, Quaternion.identity, distance,
            _sweepMask, QueryTriggerInteraction.Ignore);

        bool hit = false;
        hitDistance = distance;
        for (int i = 0; i < count; i++)
        {
            RaycastHit candidate = SweepHits[i];
            if (candidate.collider == null || candidate.collider.transform.IsChildOf(transform) || candidate.distance <= 0f)
                continue;

            if (candidate.distance < hitDistance)
            {
                hitDistance = candidate.distance;
                hit = true;
            }
        }

        return hit;
    }

    private bool TryGetSolidBounds(out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        foreach (Collider solid in GetComponentsInChildren<Collider>())
        {
            if (!solid.enabled || solid.isTrigger)
                continue;

            if (any)
                bounds.Encapsulate(solid.bounds);
            else
                bounds = solid.bounds;

            any = true;
        }

        return any;
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
        _breakOverSeconds = 0f;

        if (_dragger == null)
            return;

        Vector3 apart = transform.position - _dragger.position;
        apart.y = 0f;
        _restDistance = DragRules.RestDistance(isBody: false, apart.magnitude);
    }
}

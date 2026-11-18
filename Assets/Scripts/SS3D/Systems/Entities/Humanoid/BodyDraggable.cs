using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using SS3D.Interactions;
using SS3D.Interactions.Extensions;
using SS3D.Interactions.Interfaces;
using SS3D.Logging;
using SS3D.Systems.Entities.Humanoid.Body;
using SS3D.Systems.Entities.Dragging;
using SS3D.Systems.Health;
using SS3D.Systems.Inventory.Containers;
using UnityEngine;

namespace SS3D.Systems.Entities.Humanoid
{
    /// <summary>
    /// Lets a Downed, Critical or dead body be dragged on a rope (Documents/plans/drag-and-zero-g.plan.md, M13). The server
    /// runs the drag: it checks the end rules, works out the rope's tether point and hands it to whichever peer simulates
    /// the body (<see cref="BodyPhysicsAuthority"/>: the owner while alive, the server for corpses and orphans), which
    /// moves it with collision. A Downed body's controller steps toward the tether; a ragdoll's bones are pulled along.
    /// The server re-sends the tether instead of teleporting the body, so a client that ignores it drifts off and snags
    /// (owner question 1).
    /// </summary>
    public sealed class BodyDraggable : NetworkBehaviour, IDragTarget, IInteractionTarget
    {
        /// <summary>The menu entry, built once: outline probes ask on every hover (interactions-framework Pitfalls).</summary>
        private IInteraction[] _dragInteractions;

        /// <summary>The server sends a moved tether at most this often.</summary>
        private const float SendInterval = 0.05f;

        /// <summary>A tether that moved less than this isn't worth a message.</summary>
        private const float SendDistance = 0.05f;

        /// <summary>How long the server waits before sending an unchanged tether to a body that ignores it.</summary>
        private const float ResendInterval = 0.5f;

        /// <summary>How quickly the dragged bones' velocity follows the rope, per second.</summary>
        private const float PullResponse = 10f;

        [SyncVar(OnChange = nameof(OnDraggerObjectChanged))]
        private NetworkObject _draggerObject;

        private Ragdoll _ragdoll;
        private HumanHealthController _health;
        private HumanoidBodyStateMachine _bodyState;
        private CharacterController _characterController;
        private Transform _hips;
        private Rigidbody[] _bones;

        // Server.
        private Transform _dragger;
        private float _breakOverSeconds;
        private Vector3 _lastSentTether;
        private float _lastSentAt = float.NegativeInfinity;
        private bool _sentToOwner;

        // Physics authority.
        private bool _hasTether;
        private Vector3 _tether;

        [SyncVar]
        private HandSide _draggerHand;

        public bool Dragged => _draggerObject != null;

        public HandSide DraggerHand => _draggerHand;

        public NetworkObject DraggerObject => _draggerObject;

        public GameObject GetGameObject() => gameObject;

        /// <summary>Downed, Critical or dead right now (<see cref="DragRules.MayBeDragged"/>).</summary>
        public bool IsDraggableNow => Cache() && DragRules.MayBeDragged(_ragdoll.Presentation,
            _health != null ? BodyPresentationIntent.FromSnapshot(_health.Snapshot) : _ragdoll.Presentation);

        /// <summary>Where the body is: its hips while ragdolled (the root lags there), else its root.</summary>
        public Vector3 BodyPosition => Cache() && _ragdoll.IsKnockedDown && _hips != null ? _hips.position : transform.position;

        public Vector3 FootprintPosition
        {
            get
            {
                Vector3 body = BodyPosition;
                return new Vector3(body.x, transform.position.y, body.z);
            }
        }

        /// <summary>The hips, for the start cone (Decision 26).</summary>
        public Transform Hips => Cache() && _hips != null ? _hips : transform;

        public bool IsInStartCone(Transform dragger) =>
            dragger != null && DragRules.IsInDragCone(dragger.position, dragger.forward, BodyPosition);

        /// <summary>
        /// Offers Drag on a Downed, Critical or dead body and Drop on your own drag; a standing or knocked-down body
        /// offers nothing at all (Decision 26).
        /// </summary>
        public IInteraction[] CreateTargetInteractions(InteractionEvent interactionEvent)
        {
            Entity source = interactionEvent.Source?.GetComponentInParent<Entity>();
            bool draggedBySource = Dragged && source != null && _draggerObject == source.NetworkObject;
            if (DragRules.Offer(Dragged, draggedBySource, IsDraggableNow) == DragOffer.None)
            {
                return System.Array.Empty<IInteraction>();
            }

            return _dragInteractions ??= new IInteraction[] { new DragInteraction() };
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            Cache();
        }

        public override void OnStopNetwork()
        {
            DragRegistry.Forget(this);
            _hasTether = false;
            base.OnStopNetwork();
        }

        public override void OnStopServer()
        {
            if (Dragged)
            {
                ServerEnd(DragEndReason.Despawned);
            }

            base.OnStopServer();
        }

        private void OnDraggerObjectChanged(NetworkObject previous, NetworkObject next, bool asServer) =>
            DragRegistry.OnDraggerChanged(this, previous, next);

        public bool CanBeDraggedBy(Transform dragger)
        {
            if (dragger == null || dragger == transform || !IsDraggableNow)
            {
                return false;
            }

            if (dragger.TryGetComponent(out Ragdoll draggerBody) && draggerBody.Presentation != BodyPresentationState.Locomotion)
            {
                return false;
            }

            // One drag per body (plan Decision 50).
            if (dragger.TryGetComponent(out NetworkObject draggerObject)
                && DragRegistry.TryGetTarget(draggerObject, out object held) && !ReferenceEquals(held, this))
            {
                return false;
            }

            Transform current = Dragged ? _draggerObject.transform : null;
            return Draggable.MayChangeDrag(true, dragger, current, BodyPosition);
        }

        [Server]
        public void ServerSetDrag(bool drag, Transform dragger, HandSide hand = HandSide.Right)
        {
            if (!drag)
            {
                if (Dragged && (dragger == null || dragger == _dragger))
                {
                    ServerEnd(DragEndReason.Released);
                }

                return;
            }

            if (Dragged || !CanBeDraggedBy(dragger))
            {
                return;
            }

            _dragger = dragger;
            _draggerHand = hand;
            _draggerObject = dragger.GetComponent<NetworkObject>();
            _breakOverSeconds = 0f;
            _lastSentAt = float.NegativeInfinity;
            _sentToOwner = false;
            _bodyState?.SetBeingDragged(true);
        }

        [Server]
        private void ServerEnd(DragEndReason reason)
        {
            Log.Information(this, "Drag of {body} ended: {reason}", Logs.ServerOnly, name, reason);
            _dragger = null;
            _draggerObject = null;
            _hasTether = false;
            if (_bodyState != null)
            {
                _bodyState.SetBeingDragged(false);
            }

            if (_sentToOwner && Owner.IsValid && !Owner.IsLocalClient)
            {
                TargetSetTether(Owner, false, Vector3.zero);
            }

            _sentToOwner = false;
        }

        private void Update()
        {
            if (IsServer && Dragged)
            {
                ServerTick(Time.deltaTime);
            }

            if (_hasTether && IsMover() && _ragdoll.IsDowned && !_ragdoll.IsFallingDowned)
            {
                StepController(Time.deltaTime);
            }
        }

        private void FixedUpdate()
        {
            if (_hasTether && IsMover() && _ragdoll.IsKnockedDown)
            {
                PullBones();
            }
        }

        private bool IsMover() => Cache() && _ragdoll.IsRagdollPhysicsAuthority();

        [Server]
        private void ServerTick(float seconds)
        {
            Vector3 body = BodyPosition;
            bool draggerGone = _dragger == null || _draggerObject == null || !_draggerObject.IsSpawned;
            Vector3 dragger = draggerGone ? body : _dragger.position;
            Vector3 apart = dragger - body;
            apart.y = 0f;

            DragEndInputs inputs = new()
            {
                DraggerGone = draggerGone,
                DraggerCanAct = draggerGone || CanDraggerAct(_dragger),
                Snagged = DragRules.TickBreak(ref _breakOverSeconds, apart.magnitude, seconds),
                TargetStillDraggable = IsDraggableNow,
                DraggerFloating = !draggerGone && IsFloating(_dragger, dragger),
                TargetFloating = HumanoidSpaceSupport.IsUnsupportedAt(body),
            };

            DragEndReason reason = DragRules.EvaluateEnd(inputs);
            if (reason != DragEndReason.None)
            {
                ServerEnd(reason);
                return;
            }

            ServerRoute(DragRules.TetherPoint(dragger, body, DragRules.BodyRestDistance), body);
        }

        private static bool CanDraggerAct(Transform dragger) =>
            !dragger.TryGetComponent(out Ragdoll body) || body.Presentation == BodyPresentationState.Locomotion;

        private static bool IsFloating(Transform dragger, Vector3 position) =>
            (dragger.TryGetComponent(out HumanoidBodyStateMachine state) && state.Snapshot.IsFloating)
            || HumanoidSpaceSupport.IsUnsupportedAt(position);

        /// <summary>The tether goes to the physics authority: set here when that's the server (or the host's own body).</summary>
        [Server]
        private void ServerRoute(Vector3 tether, Vector3 body)
        {
            bool dead = _ragdoll.Presentation == BodyPresentationState.Dead;
            if (dead || !Owner.IsValid || Owner.IsLocalClient)
            {
                _tether = tether;
                _hasTether = true;
                return;
            }

            float now = Time.time;
            bool moved = (tether - _lastSentTether).sqrMagnitude > SendDistance * SendDistance && now - _lastSentAt >= SendInterval;
            bool ignored = DragRules.NeedsResend(body, tether) && now - _lastSentAt >= ResendInterval;
            if (!_sentToOwner || moved || ignored)
            {
                _lastSentTether = tether;
                _lastSentAt = now;
                _sentToOwner = true;
                TargetSetTether(Owner, true, tether);
            }
        }

        [TargetRpc]
        private void TargetSetTether(NetworkConnection connection, bool active, Vector3 tether)
        {
            if (this == null)
            {
                return;
            }

            _hasTether = active;
            _tether = tether;
        }

        /// <summary>A Downed body slides on its controller, so walls and doors stop it.</summary>
        private void StepController(float seconds)
        {
            if (_characterController == null)
            {
                return;
            }

            Vector3 step = DragRules.MoverStep(transform.position, _tether, DragRules.CatchUpSpeed, seconds);
            step.y = -2f * seconds; // stay on the deck
            if (IsOwner)
            {
                if (_characterController.enabled)
                {
                    _characterController.Move(step);
                }

                return;
            }

            // Nobody owns it: the server moves it, with the controller on only for the move (see Ragdoll.MoveUpright).
            _characterController.enabled = true;
            _characterController.Move(step);
            _characterController.enabled = false;
        }

        /// <summary>
        /// A ragdoll slides as a whole: every free bone's planar velocity follows the rope's, so the joints don't stretch
        /// or spin the corpse the way a pull on the hips alone would (plan Risk 2).
        /// </summary>
        private void PullBones()
        {
            if (_hips == null || _bones == null
                || !DragRules.TryGetRagdollPullVelocity(_hips.position, _tether, DragRules.CatchUpSpeed, out Vector3 velocity))
            {
                return;
            }

            foreach (Rigidbody bone in _bones)
            {
                if (bone == null || bone.isKinematic)
                {
                    continue;
                }

                Vector3 planar = bone.linearVelocity;
                planar.y = 0f;
                bone.AddForce((velocity - planar) * PullResponse, ForceMode.Acceleration);
            }
        }

        private bool Cache()
        {
            if (_ragdoll != null)
            {
                return true;
            }

            _ragdoll = GetComponent<Ragdoll>();
            _health = GetComponent<HumanHealthController>();
            _bodyState = GetComponent<HumanoidBodyStateMachine>();
            _characterController = GetComponent<CharacterController>();
            Animator animator = GetComponent<Animator>();
            _hips = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
            RagdollPart[] parts = GetComponentsInChildren<RagdollPart>(true);
            _bones = new Rigidbody[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                _bones[i] = parts[i].GetComponent<Rigidbody>();
            }

            return _ragdoll != null;
        }
    }
}

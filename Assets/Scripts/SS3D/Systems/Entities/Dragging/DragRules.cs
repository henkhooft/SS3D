using SS3D.Systems.Entities.Humanoid;
using SS3D.Systems.Inventory.Containers;
using UnityEngine;

namespace SS3D.Systems.Entities.Dragging
{
    /// <summary>Why a drag ended; <see cref="None"/> while it goes on.</summary>
    public enum DragEndReason
    {
        None,
        Released,
        Despawned,
        DraggerDown,
        Snagged,
        TargetStood,
        Floating,
    }

    /// <summary>What the drag entry on a target's menu does, if it's there at all.</summary>
    public enum DragOffer
    {
        /// <summary>No entry: filtered out, not greyed (Decision 26).</summary>
        None,
        Start,
        Release,
    }

    /// <summary>What the server knows about a running drag each step, for <see cref="DragRules.EvaluateEnd"/>.</summary>
    public struct DragEndInputs
    {
        public bool Released;
        public bool TargetGone;
        public bool DraggerGone;
        /// <summary>False once the dragger is Downed, knocked down, Critical or dead.</summary>
        public bool DraggerCanAct;
        /// <summary>The break rule fired (<see cref="DragRules.TickBreak"/>).</summary>
        public bool Snagged;
        /// <summary>False once a dragged body stands up (<see cref="DragRules.MayBeDragged"/>). Furniture stays true.</summary>
        public bool TargetStillDraggable;
        public bool DraggerFloating;
        public bool TargetFloating;
    }

    /// <summary>
    /// Pure rules for dragging bodies and furniture (Documents/plans/drag-and-zero-g.plan.md, Decisions 9, 12, 13, 22,
    /// 26; feel numbers from owner question 4, first guesses for the hand test). Everything here is planar: the rope
    /// pulls along the deck and never lifts.
    /// </summary>
    public static class DragRules
    {
        /// <summary>A dragged body trails this far behind the dragger.</summary>
        public const float BodyRestDistance = 1f;

        /// <summary>Furniture keeps the distance it was picked up at, up to this.</summary>
        public const float FurnitureMaxRestDistance = 1.5f;

        /// <summary>The drag snags when the dragger and the target are further apart than this ...</summary>
        public const float BreakDistance = 2.5f;

        /// <summary>... for this long.</summary>
        public const float BreakSeconds = 1f;

        /// <summary>The server sends the tether again when the target lies further than this from it.</summary>
        public const float ResendDistance = 1.5f;

        /// <summary>Half-angle of the cone in front of the dragger that the target's hips must be in to start.</summary>
        public const float StartConeDegrees = 60f;

        /// <summary>
        /// Where the rope puts the target: on the line from the dragger to the target, <paramref name="restDistance"/>
        /// from the dragger, at the target's own height. A slack rope (target already closer) leaves it where it is, so
        /// the drag never pushes, and the target trails behind whatever way the dragger faces.
        /// </summary>
        public static Vector3 TetherPoint(Vector3 dragger, Vector3 target, float restDistance)
        {
            Vector3 planar = target - dragger;
            planar.y = 0f;
            float distance = planar.magnitude;
            if (distance <= restDistance || distance < 0.0001f)
            {
                return target;
            }

            Vector3 point = dragger + planar * (restDistance / distance);
            point.y = target.y;
            return point;
        }

        /// <summary>The rope length for a new drag; <paramref name="startDistance"/> is planar.</summary>
        public static float RestDistance(bool isBody, float startDistance) =>
            isBody ? BodyRestDistance : Mathf.Min(startDistance, FurnitureMaxRestDistance);

        /// <summary>
        /// Advances the break timer by <paramref name="seconds"/> at the current planar dragger-to-target
        /// <paramref name="distance"/>. Coming back within <see cref="BreakDistance"/> resets it. True once it snags.
        /// </summary>
        public static bool TickBreak(ref float overSeconds, float distance, float seconds)
        {
            if (distance <= BreakDistance)
            {
                overSeconds = 0f;
                return false;
            }

            overSeconds += seconds;
            return overSeconds >= BreakSeconds;
        }

        /// <summary>
        /// Downed, Critical and dead bodies can be dragged; standing ones and timed knockdowns can't (Decision 9).
        /// <paramref name="presentation"/> is the body's <c>Ragdoll.Presentation</c>; <paramref name="healthIntent"/> is
        /// what health wants it to be (<c>BodyPresentationIntent.FromSnapshot</c>). A knockdown ragdolls a body that
        /// health still has standing, which is how the two tell a knockdown from Critical.
        /// </summary>
        public static bool MayBeDragged(BodyPresentationState presentation, BodyPresentationState healthIntent)
        {
            if (presentation == BodyPresentationState.Dead)
            {
                return true;
            }

            if (presentation == BodyPresentationState.Locomotion)
            {
                return false;
            }

            return healthIntent is BodyPresentationState.Downed or BodyPresentationState.Collapsed or BodyPresentationState.Dead;
        }

        /// <summary>
        /// Whether <paramref name="target"/> (the hips) lies within <see cref="StartConeDegrees"/> of the dragger's
        /// facing, measured on the deck plane: hips on the floor sit well below the dragger's pivot.
        /// </summary>
        public static bool IsInDragCone(Vector3 dragger, Vector3 forward, Vector3 target)
        {
            Vector3 toTarget = target - dragger;
            toTarget.y = 0f;
            forward.y = 0f;
            if (toTarget.sqrMagnitude < 0.01f)
            {
                // Right underfoot: there's no direction to be out of.
                return true;
            }

            if (forward.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            return Vector3.Angle(forward, toTarget) <= StartConeDegrees;
        }

        /// <summary>
        /// Whether a target's menu shows the drag entry for this source: start on a draggable target nobody drags, let go
        /// of your own drag (even once the body stands), and nothing otherwise, so a standing body shows no greyed entry.
        /// </summary>
        public static DragOffer Offer(bool dragged, bool draggedBySource, bool targetDraggable)
        {
            if (dragged)
            {
                return draggedBySource ? DragOffer.Release : DragOffer.None;
            }

            return targetDraggable ? DragOffer.Start : DragOffer.None;
        }

        /// <summary>The hand that holds the rope is busy until you let go (Decision 10).</summary>
        public static bool IsHandReservedByDrag(bool dragging, HandSide dragHand, HandSide hand) =>
            dragging && dragHand == hand;

        /// <summary>The first end rule that applies, in a fixed order, or <see cref="DragEndReason.None"/> (Decision 13).</summary>
        public static DragEndReason EvaluateEnd(in DragEndInputs inputs)
        {
            if (inputs.Released)
            {
                return DragEndReason.Released;
            }

            if (inputs.TargetGone || inputs.DraggerGone)
            {
                return DragEndReason.Despawned;
            }

            if (!inputs.DraggerCanAct)
            {
                return DragEndReason.DraggerDown;
            }

            if (inputs.Snagged)
            {
                return DragEndReason.Snagged;
            }

            if (!inputs.TargetStillDraggable)
            {
                return DragEndReason.TargetStood;
            }

            if (inputs.DraggerFloating || inputs.TargetFloating)
            {
                return DragEndReason.Floating;
            }

            return DragEndReason.None;
        }

        /// <summary>How fast a dragged Downed body's mover may catch up with the tether, so it keeps up with a walking dragger.</summary>
        public const float CatchUpSpeed = 2.5f;

        /// <summary>
        /// One frame of a character-controller body (Downed) following the tether: planar, at most
        /// <paramref name="maxSpeed"/>, never past the tether.
        /// </summary>
        public static Vector3 MoverStep(Vector3 position, Vector3 tether, float maxSpeed, float seconds)
        {
            Vector3 toTether = tether - position;
            toTether.y = 0f;
            return Vector3.ClampMagnitude(toTether, maxSpeed * seconds);
        }

        /// <summary>
        /// The velocity a taut rope asks of a ragdoll (Critical or dead): planar, toward the tether, capped at
        /// <paramref name="maxSpeed"/>. False on a slack rope, which leaves the body to its own physics.
        /// </summary>
        public static bool TryGetRagdollPullVelocity(Vector3 hips, Vector3 tether, float maxSpeed, out Vector3 velocity)
        {
            Vector3 toTether = tether - hips;
            toTether.y = 0f;
            if (toTether.sqrMagnitude < 0.0025f)
            {
                velocity = Vector3.zero;
                return false;
            }

            velocity = Vector3.ClampMagnitude(toTether * RagdollPullGain, maxSpeed);
            return true;
        }

        /// <summary>Metres per second of pull per metre the hips lie off the tether.</summary>
        public const float RagdollPullGain = 4f;

        /// <summary>True when the target has drifted far enough off the tether that its authority needs it again.</summary>
        public static bool NeedsResend(Vector3 target, Vector3 tether)
        {
            Vector3 off = target - tether;
            off.y = 0f;
            return off.sqrMagnitude > ResendDistance * ResendDistance;
        }
    }
}

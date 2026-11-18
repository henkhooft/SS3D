using System.Collections.Generic;

namespace SS3D.Systems.Entities.Dragging
{
    /// <summary>
    /// Who drags what, on every peer: fed by each drag target's replicated dragger SyncVar, so a remote client knows
    /// its own body is dragging without a second SyncVar on the dragger (plan Decision 24's pattern). Keys are the
    /// dragger's <c>NetworkObject</c> and values the target behaviour; plain objects keep it testable.
    /// </summary>
    public static class DragRegistry
    {
        private static readonly Dictionary<object, object> TargetByDragger = new();

        /// <summary>Any drag started or ended on this peer (the HUD's reserved badge listens).</summary>
        public static event System.Action Changed;

        /// <summary>A target's dragger changed from <paramref name="previous"/> to <paramref name="next"/> (either may be null).</summary>
        public static void OnDraggerChanged(object target, object previous, object next)
        {
            if (previous != null && TargetByDragger.TryGetValue(previous, out object held) && ReferenceEquals(held, target))
            {
                TargetByDragger.Remove(previous);
            }

            if (next != null)
            {
                TargetByDragger[next] = target;
            }

            Changed?.Invoke();
        }

        /// <summary>Everything being dragged right now (airlocks hold open for them, Decision 15).</summary>
        public static Dictionary<object, object>.ValueCollection Targets => TargetByDragger.Values;

        public static bool TryGetTarget(object dragger, out object target)
        {
            target = null;
            return dragger != null && TargetByDragger.TryGetValue(dragger, out target) && target != null;
        }

        /// <summary>The target went away (despawn): drop any drag of it.</summary>
        public static void Forget(object target)
        {
            object found = null;
            foreach (KeyValuePair<object, object> pair in TargetByDragger)
            {
                if (ReferenceEquals(pair.Value, target))
                {
                    found = pair.Key;
                    break;
                }
            }

            if (found != null)
            {
                TargetByDragger.Remove(found);
                Changed?.Invoke();
            }
        }

        /// <summary>Tests and domain reloads.</summary>
        public static void Clear() => TargetByDragger.Clear();
    }
}

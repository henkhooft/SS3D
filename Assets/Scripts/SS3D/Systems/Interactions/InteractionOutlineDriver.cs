using System.Collections.Generic;
using SS3D.Interactions;
using SS3D.Interactions.Interfaces;
using SS3D.Systems.Entities;
using SS3D.Systems.Selection;
using Unity.Profiling;
using UnityEngine;

namespace SS3D.Systems.Interactions
{
    /// <summary>
    /// Owner-client hover outline feedback (green/yellow/blue). Extracted from
    /// <see cref="InteractionController"/> — LateUpdate must use
    /// <see cref="InteractionPipeline.TryEvaluateOutlineInteractability"/> (no full Discover).
    /// </summary>
    public sealed class InteractionOutlineDriver
    {
        private static readonly ProfilerMarker OutlinePerformanceMarker = new("SS3D.Interactions.Outline");

        private Selectable _activeOutlineSelectable;
        private InteractionOutlineView _activeOutlineView;
        private readonly List<IInteractionTarget> _outlineTargets = new(8);

        public void Refresh(
            SelectionSubSystem selectionSystem,
            Camera camera,
            IntentType intent,
            IInteractionSource source)
        {
            using (OutlinePerformanceMarker.Auto())
            {
                RefreshUnguarded(selectionSystem, camera, intent, source);
            }
        }

        public void Clear()
        {
            // Unity fake-null: destroyed views compare unequal to null via ==.
            if (_activeOutlineView)
            {
                _activeOutlineView.SetState(InteractionOutlineView.OutlineState.Hidden);
            }

            _activeOutlineView = null;
            _activeOutlineSelectable = null;
        }

        private void RefreshUnguarded(
            SelectionSubSystem selectionSystem,
            Camera camera,
            IntentType intent,
            IInteractionSource source)
        {
            Selectable current = selectionSystem != null ? selectionSystem.GetCurrentSelectable() : null;
            InteractionOutlineView.ClearPendingExcept(current);

            if (current == null || IsEntityOutlineExcluded(current))
            {
                Clear();
                return;
            }

            if (InteractionOutlineView.IsPending(current))
            {
                if (current != _activeOutlineSelectable)
                {
                    Clear();
                    _activeOutlineSelectable = current;
                    _activeOutlineView = InteractionOutlineView.GetOrCreate(current);
                }

                if (_activeOutlineView)
                {
                    _activeOutlineView.SetState(InteractionOutlineView.OutlineState.Pending);
                }

                return;
            }

            if (current != _activeOutlineSelectable)
            {
                Clear();
                _activeOutlineSelectable = current;
                _activeOutlineView = InteractionOutlineView.GetOrCreate(current);
            }

            if (!_activeOutlineView)
            {
                return;
            }

            if (!TryEvaluateInteractability(current, camera, intent, source, out bool hasViableInteractions))
            {
                _activeOutlineView.SetState(InteractionOutlineView.OutlineState.Hidden);
                return;
            }

            InteractionOutlineView.OutlineState state = hasViableInteractions
                ? InteractionOutlineView.OutlineState.Available
                : InteractionOutlineView.OutlineState.Unavailable;

            _activeOutlineView.SetState(state);
        }

        /// <summary>
        /// Player-controlled entities use dedicated UIs (e.g. medical) instead of world interaction outlines.
        /// </summary>
        private static bool IsEntityOutlineExcluded(Selectable selectable)
        {
            return selectable.GetComponentInParent<Entity>() != null;
        }

        private bool TryEvaluateInteractability(
            Selectable selectable,
            Camera camera,
            IntentType intent,
            IInteractionSource source,
            out bool hasViableInteractions)
        {
            hasViableInteractions = false;

            if (source == null)
            {
                return false;
            }

            bool hasPoint = SelectionTargetUtility.TryResolveInteractionPoint(camera, selectable, out Vector3 point, out Vector3 normal);
            InteractionDiscovery.CollectTargetsInto(source, selectable.gameObject, _outlineTargets);

            InteractionEvent outlineEvent = hasPoint
                ? new InteractionEvent(source, null, point, normal)
                : new InteractionEvent(source, null);

            // Outline LateUpdate must not run full Discover (source-only Drop, ToArray, Filter lists).
            return InteractionPipeline.TryEvaluateOutlineInteractability(
                source,
                _outlineTargets,
                outlineEvent,
                intent,
                out hasViableInteractions);
        }
    }
}

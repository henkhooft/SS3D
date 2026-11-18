using System.Collections.Generic;
using SS3D.Interactions;
using SS3D.Permissions;
using SS3D.Systems.Entities.Dragging;
using SS3D.Systems.Entities.Humanoid;
using SS3D.Systems.Inventory.Containers;
using UnityEngine;

namespace SS3D.Systems.IngameConsoleSystem.Commands
{
    /// <summary>
    /// Starts a body drag through the Drag interaction's own checks and server path, or lets go with no target
    /// (Documents/plans/drag-and-zero-g.plan.md Decision 27). For scenarios, MPPM and the smoke line: Down the body with
    /// <c>hurt</c> first. Range and the start cone are skipped, the 3 m start distance and an empty selected hand are not.
    /// </summary>
    public class DragCommand : Command
    {
        private static readonly IReadOnlyList<CommandParameter> Params = new[]
        {
            P.Target("dragger"),
            P.Target("target").Optional(),
        };

        public override string Name => "drag";
        public override CommandCategory Category => CommandCategory.Admin;
        public override string Description => "Drag a Downed, Critical or dead body with the dragger's selected hand; no target lets go";
        public override ServerRoleTypes AccessLevel => ServerRoleTypes.Administrator;
        public override CommandType Type => CommandType.Server;
        public override IReadOnlyList<CommandParameter> Parameters => Params;
        public override string Example => "me nearest";

        public override CommandResult Perform(CommandContext context, CommandArgs args)
        {
            CommandTarget dragger = args.Target("dragger");
            if (!dragger.TryGetComponent(out HumanoidLivingController controller))
            {
                return CommandResult.Error($"{dragger.DisplayName} can't drag");
            }

            if (!args.Has("target"))
            {
                IDragTarget held = controller.DragTarget;
                if (held == null)
                {
                    return CommandResult.Error($"{dragger.DisplayName} isn't dragging anything");
                }

                held.ServerSetDrag(false, controller.transform);
                return CommandResult.Success($"{dragger.DisplayName} let go");
            }

            CommandTarget target = args.Target("target");
            if (!target.TryGetComponent(out BodyDraggable body))
            {
                return CommandResult.Error($"{target.DisplayName} can't be dragged");
            }

            Hand hand = controller.GetComponentInChildren<Hands>()?.SelectedHand;
            if (hand == null)
            {
                return CommandResult.Error($"{dragger.DisplayName} has no hand to drag with");
            }

            if (!body.IsDraggableNow)
            {
                return CommandResult.Error($"{target.DisplayName} isn't Downed, Critical or dead");
            }

            if (!hand.IsEmpty())
            {
                return CommandResult.Error($"{dragger.DisplayName}'s selected hand isn't empty");
            }

            DragInteraction drag = new() { RangeCheck = false, ConeCheck = false };
            InteractionEvent interactionEvent = new(hand, body, body.FootprintPosition, Vector3.up);
            if (!drag.CanInteract(interactionEvent))
            {
                return CommandResult.Error(
                    $"{dragger.DisplayName} can't drag {target.DisplayName} now (more than {Draggable.MaxDragStartDistance} m apart, already dragged, or not standing)");
            }

            drag.Start(interactionEvent, null);
            return body.Dragged
                ? CommandResult.Success($"{dragger.DisplayName} drags {target.DisplayName}")
                : CommandResult.Error($"{dragger.DisplayName} couldn't start dragging {target.DisplayName}");
        }
    }
}

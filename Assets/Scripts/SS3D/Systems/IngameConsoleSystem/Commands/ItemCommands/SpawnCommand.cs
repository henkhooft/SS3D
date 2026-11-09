using System.Collections.Generic;
using SS3D.Core;
using SS3D.Permissions;
using SS3D.Systems.Entities;
using SS3D.Systems.Inventory.Containers;
using SS3D.Systems.Inventory.Items;
using UnityEngine;

namespace SS3D.Systems.IngameConsoleSystem.Commands.ItemCommands
{
    /// <summary>Spawns an item at the caller's feet or into their selected hand; scenarios use <c>hand</c> to arm a body.</summary>
    public class SpawnCommand : Command
    {
        private const string Hand = "hand";

        private static readonly IReadOnlyList<CommandParameter> Params = new[]
        {
            P.Item(),
            P.Choice("at", "feet", Hand).Default("feet"),
        };

        public override string Name => "spawn";
        public override CommandCategory Category => CommandCategory.Admin;
        public override string Description => "Spawn an item by name or asset id at your feet or in your empty selected hand";
        public override ServerRoleTypes AccessLevel => ServerRoleTypes.Administrator;
        public override CommandType Type => CommandType.Server;
        public override IReadOnlyList<CommandParameter> Parameters => Params;
        public override string Example => "MedicalPatch hand";

        public override CommandResult Perform(CommandContext context, CommandArgs args)
        {
            Entity entity = context.CallerEntity;
            if (entity == null)
            {
                return CommandResult.Error("you have no body to spawn next to");
            }

            CommandItem item = args.Item();
            ItemSubSystem items = SubSystems.Get<ItemSubSystem>();

            if (args.Text("at") == Hand)
            {
                HumanInventory inventory = entity.GetComponentInParent<HumanInventory>();
                Hand hand = inventory != null ? inventory.Hands.SelectedHand : null;
                if (hand == null)
                {
                    return CommandResult.Error("you have no hands");
                }

                if (!hand.IsEmpty())
                {
                    return CommandResult.Error("your selected hand is not empty");
                }

                Item spawned = items.SpawnItemInContainer(item.Prefab.GameObject, hand.Container);
                return CommandResult.Success($"item {spawned.Name} spawned in hand");
            }

            Vector3 position = entity.transform.position;
            Quaternion rotation = item.Prefab.GetWorldFacing(entity.transform.eulerAngles.y);
            items.SpawnItem(item.AssetId, position, rotation);
            return CommandResult.Success($"item {item.Prefab.Name} spawned at {position}");
        }
    }
}

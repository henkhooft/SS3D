using SS3D.Engine.Interactions;
using SS3D.Engine.Interactions.Extensions;
using SS3D.Engine.Tiles;
using UnityEngine;

namespace SS3D.Content.Systems.Interactions
{
    public class TableConstructionInteraction : ConstructionInteraction
    {
        public FurnitureFloorFixture TableToConstruct { get; set; }

        public override string GetName(InteractionEvent interactionEvent)
        {
            TileObject tileObject = (interactionEvent.Target as IGameObjectProvider)?.GameObject?.GetComponentInParent<TileObject>();
            if (tileObject != null && tileObject.Tile.fixtures.GetFixture(TileLayers.FurnitureMain) == TableToConstruct)
            {
                return "Deconstruct";
            }

            return "Construct";
        }

        public override bool CanInteract(InteractionEvent interactionEvent)
        {
            if (!base.CanInteract(interactionEvent))
            {
                return false;
            }

            return TargetTile.Tile.turf?.isWall != true;
        }

        public override void Cancel(InteractionEvent interactionEvent, InteractionReference reference)
        {
            
        }

        protected override void StartDelayed(InteractionEvent interactionEvent)
        {
            var targetBehaviour = (IGameObjectProvider) interactionEvent.Target;
            TileManager tileManager = Object.FindObjectOfType<TileManager>();
            TileObject targetTile = targetBehaviour.GameObject.GetComponentInParent<TileObject>();
            var tile = targetTile.Tile;

            
            if (tile.fixtures.GetFixture(TileLayers.FurnitureMain) != null) // If there is a fixture on the place
            {
                if (tile.fixtures.GetFixture(TileLayers.FurnitureMain) == TableToConstruct) // If the fixture is a table
                {
                    tile.fixtures.SetFixture(null, TileLayers.FurnitureMain); // Deconstruct
                }
            }
            else // If there is no fixture on place
            {
                // TODO: Change default rotation to north
                tile.fixtures.SetFixture(TableToConstruct, TileLayers.FurnitureMain); // Construct
            }
            
            // TODO: Make an easier way of doing this.
            // tile.subStates = new object[2];

            tileManager.UpdateTile(targetTile.transform.position, tile);
        }
    }
}
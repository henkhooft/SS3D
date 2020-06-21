﻿using SS3D.Engine.Interactions;
using SS3D.Engine.Interactions.Extensions;
using SS3D.Engine.Tiles;
using UnityEngine;

namespace SS3D.Content.Systems.Interactions
{
    public class TableConstructionInteraction : DelayedInteraction
    {
        public FurnitureFloorFixture TableToConstruct { get; set; }

        public override string GetName(InteractionEvent interactionEvent)
        {
            TileObject tileObject = (interactionEvent.Target as IGameObjectProvider)?.GameObject?.GetComponentInParent<TileObject>();
            if (tileObject != null && tileObject.Tile.fixtures.GetFloorFixtureAtLayer(FloorFixtureLayers.FurnitureFixture1) == TableToConstruct)
            {
                return "Deconstruct";
            }

            return "Construct";
        }

        public override bool CanInteract(InteractionEvent interactionEvent)
        {
            if (interactionEvent.Target is IGameObjectProvider targetBehaviour)
            {
                TileObject targetTile = targetBehaviour.GameObject.GetComponentInParent<TileObject>();
                if (targetTile == null)
                {
                    return false;
                }

                if (!InteractionExtensions.RangeCheck(interactionEvent))
                {
                    return false;
                }

                if (targetTile.Tile.turf?.isWall == true)
                {
                    return false;
                }

                return true;
            }
            
            return false;
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

            
            if (tile.fixtures.GetFloorFixtureAtLayer(FloorFixtureLayers.FurnitureFixture1) != null) // If there is a fixture on the place
            {
                if (tile.fixtures.GetFloorFixtureAtLayer(FloorFixtureLayers.FurnitureFixture1) == TableToConstruct) // If the fixture is a table
                {
                    tile.fixtures.SetFloorFixtureAtLayer(null, FloorFixtureLayers.FurnitureFixture1); // Deconstruct
                }
            }
            else // If there is no fixture on place
            {
                tile.fixtures.SetFloorFixtureAtLayer(TableToConstruct, FloorFixtureLayers.FurnitureFixture1); // Deconstruct
            }
            
            // TODO: Make an easier way of doing this.
            tile.subStates = new object[2];

            tileManager.UpdateTile(targetTile.transform.position, tile);
        }
    }
}
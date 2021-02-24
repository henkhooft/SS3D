using System;
using SS3D.Content.Systems.Interactions;
using SS3D.Engine.Interactions;
using SS3D.Engine.Tiles;

namespace SS3D.Content.Systems.Construction
{
    public class FixtureConstructionInteraction : ConstructionInteraction
    {
        /// <summary>
        /// The fixture to construct
        /// </summary>
        public Fixture Fixture { get; set; }
        
        /// <summary>
        /// If any existing fixture should be overwritten
        /// </summary>
        public bool Overwrite { get; set; }

        public TileLayers TileLayer
        {
            get => tileLayer;
            set
            {
                tileLayer = value;
            }
        }

        private TileLayers tileLayer;


        public override string GetName(InteractionEvent interactionEvent)
        {
            return "Construct fixture";
        }

        public override void Cancel(InteractionEvent interactionEvent, InteractionReference reference)
        {
            
        }

        public override bool CanInteract(InteractionEvent interactionEvent)
        {
            if (!base.CanInteract(interactionEvent))
            {
                return false;
            }

            return Overwrite || !GetFixture(TargetTile.Tile.fixtures);
        }

        protected override void StartDelayed(InteractionEvent interactionEvent)
        {
            TileManager tileManager = UnityEngine.Object.FindObjectOfType<TileManager>();
            TileDefinition definition = TargetTile.Tile;
            // Set desired fixture
            SetFixture(definition.fixtures);
            
            // Required to get the tile to update fixtures
            // TODO: Add flag?
            // definition.fixtures = (FixturesContainer) definition.fixtures;
            
            // Apply change
            tileManager.UpdateTile(TargetTile.transform.position, definition);
        }

        private Fixture GetFixture(FixturesContainer container)
        {
            return container.GetFixture(tileLayer);
        }

        private void SetFixture(FixturesContainer container)
        {
            container.SetFixture(Fixture, tileLayer);
        }
    }
}

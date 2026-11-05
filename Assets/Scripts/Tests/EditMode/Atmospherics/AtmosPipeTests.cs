using NUnit.Framework;
using SS3D.Systems.Atmospherics;
using SS3D.Systems.Atmospherics.Pipes;
using SS3D.Systems.Tile;
using SS3D.Systems.Tile.Connections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EditorTests.Atmospherics
{
    public class AtmosPipeTests
    {
        private List<GameObject> _instantiated;

        [SetUp]
        public void SetUp() => _instantiated = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _instantiated)
                Object.DestroyImmediate(go);
            _instantiated.Clear();
        }

        [Test]
        public void StraightPipeRun_FormsSingleNetwork()
        {
            TileMapTestUtilities.MapContext context = TileMapTestUtilities.CreateContext(_instantiated);
            PlacedTileObject western = CreateGasPipeAt(new Vector2Int(5, 5), TileLayer.PipeMiddle);
            PlacedTileObject eastern = CreateGasPipeAt(new Vector2Int(6, 5), TileLayer.PipeMiddle);

            RegisterOnMap(context.Map, western, TileLayer.PipeMiddle, new Vector3(5, 0, 5));
            RegisterOnMap(context.Map, eastern, TileLayer.PipeMiddle, new Vector3(6, 0, 5));
            RecomputeAdjacency(context.Map, western, eastern);

            var registry = new GasPipeNetworkRegistry(AtmosConstants.DefaultGasCount);
            registry.RebuildAll(context.Map);

            Assert.AreEqual(1, registry.NetworkCount);
            Assert.IsTrue(registry.TryGetNetworkForSegment(
                new GasPipeSegmentKey(new TileCoord(context.Map.MapId, 5, 5), TileLayer.PipeMiddle, TileObjectSpecificType.None),
                out GasPipeNetworkId westernNetwork,
                out GasPipeNetworkRecord record));
            Assert.IsTrue(registry.TryGetNetworkForSegment(
                new GasPipeSegmentKey(new TileCoord(context.Map.MapId, 6, 5), TileLayer.PipeMiddle, TileObjectSpecificType.None),
                out GasPipeNetworkId easternNetwork,
                out _));
            Assert.AreEqual(westernNetwork.Value, easternNetwork.Value);
            Assert.AreEqual(2, record.Segments.Count);
            Assert.AreEqual(2 * AtmosPipeConstants.SegmentVolume, record.Volume, 0.001f);
        }

        [Test]
        public void LineFill_FillsOnlyItsNetwork_ToThePressureAsked()
        {
            TileMapTestUtilities.MapContext context = TileMapTestUtilities.CreateContext(_instantiated);
            PlacedTileObject mixed = CreateGasPipeAt(new Vector2Int(5, 5), TileLayer.PipeMiddle);
            PlacedTileObject waste = CreateGasPipeAt(new Vector2Int(5, 5), TileLayer.PipeRight);
            RegisterOnMap(context.Map, mixed, TileLayer.PipeMiddle, new Vector3(5, 0, 5));
            RegisterOnMap(context.Map, waste, TileLayer.PipeRight, new Vector3(5, 0, 5));
            RecomputeAdjacency(context.Map, mixed, waste);

            var registry = new GasPipeNetworkRegistry(AtmosConstants.DefaultGasCount);
            registry.RebuildAll(context.Map);
            Assert.IsTrue(registry.TryFillNetwork(GasPipeSegmentKey.From(mixed), AtmosSupplyMix.Air, 300f));

            registry.TryGetNetworkForSegment(GasPipeSegmentKey.From(mixed), out _, out GasPipeNetworkRecord filled);
            registry.TryGetNetworkForSegment(GasPipeSegmentKey.From(waste), out _, out GasPipeNetworkRecord other);
            Assert.That(filled.GetPressure(AtmosConstants.DefaultGasCount), Is.EqualTo(300f).Within(0.5f));
            Assert.AreEqual(0f, other.GetTotalMoles(AtmosConstants.DefaultGasCount));
        }

        [Test]
        public void OffsetPipeLayers_OnSameTile_DoNotShareNetwork()
        {
            TileMapTestUtilities.MapContext context = TileMapTestUtilities.CreateContext(_instantiated);
            PlacedTileObject leftLayer = CreateGasPipeAt(new Vector2Int(5, 5), TileLayer.PipeLeft);
            PlacedTileObject middleLayer = CreateGasPipeAt(new Vector2Int(5, 5), TileLayer.PipeMiddle);

            RegisterOnMap(context.Map, leftLayer, TileLayer.PipeLeft, new Vector3(5, 0, 5));
            RegisterOnMap(context.Map, middleLayer, TileLayer.PipeMiddle, new Vector3(5, 0, 5));

            var registry = new GasPipeNetworkRegistry(AtmosConstants.DefaultGasCount);
            registry.RebuildAll(context.Map);

            Assert.AreEqual(2, registry.NetworkCount);
        }

        [Test]
        public void RemovingMiddleSegment_SplitsNetwork()
        {
            TileMapTestUtilities.MapContext context = TileMapTestUtilities.CreateContext(_instantiated);
            PlacedTileObject western = CreateGasPipeAt(new Vector2Int(5, 5), TileLayer.PipeMiddle);
            PlacedTileObject center = CreateGasPipeAt(new Vector2Int(6, 5), TileLayer.PipeMiddle);
            PlacedTileObject eastern = CreateGasPipeAt(new Vector2Int(7, 5), TileLayer.PipeMiddle);

            RegisterOnMap(context.Map, western, TileLayer.PipeMiddle, new Vector3(5, 0, 5));
            RegisterOnMap(context.Map, center, TileLayer.PipeMiddle, new Vector3(6, 0, 5));
            RegisterOnMap(context.Map, eastern, TileLayer.PipeMiddle, new Vector3(7, 0, 5));
            RecomputeAdjacency(context.Map, western, center, eastern);

            var registry = new GasPipeNetworkRegistry(AtmosConstants.DefaultGasCount);
            registry.RebuildAll(context.Map);
            Assert.AreEqual(1, registry.NetworkCount);

            context.Map.GetOrCreateTileLocation(TileLayer.PipeMiddle, new Vector3(6, 0, 5)).ClearAllPlacedObject();
            registry.RebuildAround(context.Map, new TileCoord(context.Map.MapId, 6, 5));

            Assert.AreEqual(2, registry.NetworkCount);
        }

        [Test]
        public void TransferTurfToNetwork_ConservesTotalMoles()
        {
            TileMapTestUtilities.MapContext context = TileMapTestUtilities.CreateContext(_instantiated);
            TileMapTestUtilities.PlacePlenum(context, new Vector3(5, 0, 5));

            PlacedTileObject pipe = CreateGasPipeAt(new Vector2Int(5, 5), TileLayer.PipeMiddle);
            RegisterOnMap(context.Map, pipe, TileLayer.PipeMiddle, new Vector3(5, 0, 5));

            using var turf = new AtmosSimulation(context.Query, context.Map.MapId, AtmosConstants.DefaultGasCount);
            turf.CreateChunk(new TileChunkRef
            {
                MapId = context.Map.MapId,
                ChunkKey = Vector2Int.zero,
                Origin = Vector3.zero,
            });
            turf.UpdateCell(new TileCoord(context.Map.MapId, 5, 5));

            var registry = new GasPipeNetworkRegistry(AtmosConstants.DefaultGasCount);
            registry.RebuildAll(context.Map);
            Assert.IsTrue(registry.TryGetNetworkForSegment(
                GasPipeSegmentKey.From(pipe),
                out GasPipeNetworkId networkId,
                out GasPipeNetworkRecord network));

            var observer = new AtmosPipeObserver(context.Map, registry);
            var pipeSimulation = new AtmosPipeSimulation(registry, turf, null, AtmosConstants.DefaultGasCount, observer);

            float initialTurfMoles = turf.GetTotalMoles();
            turf.DebugAddMoles(new TileCoord(context.Map.MapId, 5, 5), AtmosConstants.Oxygen, 25f);
            float afterAdd = turf.GetTotalMoles();

            Assert.IsTrue(pipeSimulation.TryTransferMoles(
                networkId,
                AtmosConstants.Oxygen,
                10f,
                new TileCoord(context.Map.MapId, 5, 5),
                PipeTransferDirection.ToNetwork,
                out float moved));
            Assert.AreEqual(10f, moved, 0.001f);
            Assert.AreEqual(afterAdd, turf.GetTotalMoles() + pipeSimulation.GetTotalMolesAcrossNetworks(), 0.01f);
            Assert.AreEqual(10f, network.Moles[AtmosConstants.Oxygen.Value], 0.001f);
            Assert.Less(turf.DebugGetMoles(new TileCoord(context.Map.MapId, 5, 5), AtmosConstants.Oxygen), afterAdd);
            Assert.Greater(initialTurfMoles, 0f);
        }

        [Test]
        public void TransferNetworkToTurf_ConservesTotalMoles()
        {
            TileMapTestUtilities.MapContext context = TileMapTestUtilities.CreateContext(_instantiated);
            TileMapTestUtilities.PlacePlenum(context, new Vector3(5, 0, 5));

            PlacedTileObject pipe = CreateGasPipeAt(new Vector2Int(5, 5), TileLayer.PipeMiddle);
            RegisterOnMap(context.Map, pipe, TileLayer.PipeMiddle, new Vector3(5, 0, 5));

            using var turf = new AtmosSimulation(context.Query, context.Map.MapId, AtmosConstants.DefaultGasCount);
            turf.CreateChunk(new TileChunkRef
            {
                MapId = context.Map.MapId,
                ChunkKey = Vector2Int.zero,
                Origin = Vector3.zero,
            });
            turf.UpdateCell(new TileCoord(context.Map.MapId, 5, 5));

            var registry = new GasPipeNetworkRegistry(AtmosConstants.DefaultGasCount);
            registry.RebuildAll(context.Map);
            Assert.IsTrue(registry.TryGetNetworkForSegment(
                GasPipeSegmentKey.From(pipe),
                out GasPipeNetworkId networkId,
                out GasPipeNetworkRecord network));

            network.Moles[AtmosConstants.CarbonDioxide.Value] = 15f;

            var observer = new AtmosPipeObserver(context.Map, registry);
            var pipeSimulation = new AtmosPipeSimulation(registry, turf, null, AtmosConstants.DefaultGasCount, observer);
            float totalBefore = turf.GetTotalMoles() + pipeSimulation.GetTotalMolesAcrossNetworks();

            Assert.IsTrue(pipeSimulation.TryTransferMoles(
                networkId,
                AtmosConstants.CarbonDioxide,
                8f,
                new TileCoord(context.Map.MapId, 5, 5),
                PipeTransferDirection.ToTurf,
                out float moved));
            Assert.AreEqual(8f, moved, 0.001f);
            Assert.AreEqual(totalBefore, turf.GetTotalMoles() + pipeSimulation.GetTotalMolesAcrossNetworks(), 0.01f);
            Assert.AreEqual(7f, network.Moles[AtmosConstants.CarbonDioxide.Value], 0.001f);
            Assert.AreEqual(8f, turf.DebugGetMoles(new TileCoord(context.Map.MapId, 5, 5), AtmosConstants.CarbonDioxide), 0.001f);
        }

        [Test]
        public void Transfer_FromEmptySource_MovesZeroMoles()
        {
            TileMapTestUtilities.MapContext context = TileMapTestUtilities.CreateContext(_instantiated);
            TileMapTestUtilities.PlacePlenum(context, new Vector3(5, 0, 5));

            PlacedTileObject pipe = CreateGasPipeAt(new Vector2Int(5, 5), TileLayer.PipeMiddle);
            RegisterOnMap(context.Map, pipe, TileLayer.PipeMiddle, new Vector3(5, 0, 5));

            using var turf = new AtmosSimulation(context.Query, context.Map.MapId, AtmosConstants.DefaultGasCount);
            turf.CreateChunk(new TileChunkRef
            {
                MapId = context.Map.MapId,
                ChunkKey = Vector2Int.zero,
                Origin = Vector3.zero,
            });
            turf.UpdateCell(new TileCoord(context.Map.MapId, 5, 5));

            var registry = new GasPipeNetworkRegistry(AtmosConstants.DefaultGasCount);
            registry.RebuildAll(context.Map);
            Assert.IsTrue(registry.TryGetNetworkForSegment(
                GasPipeSegmentKey.From(pipe),
                out GasPipeNetworkId networkId,
                out _));

            var observer = new AtmosPipeObserver(context.Map, registry);
            var pipeSimulation = new AtmosPipeSimulation(registry, turf, null, AtmosConstants.DefaultGasCount, observer);

            Assert.IsFalse(pipeSimulation.TryTransferMoles(
                networkId,
                AtmosConstants.Plasma,
                5f,
                new TileCoord(context.Map.MapId, 5, 5),
                PipeTransferDirection.ToTurf,
                out float moved));
            Assert.AreEqual(0f, moved);
        }

        [Test]
        public void AtmosPortFlow_ReturnsZeroWhenPressuresEqual()
        {
            float flow = AtmosPortFlow.ComputeFlowMoles(
                101f,
                101f,
                AtmosPortConstants.VentRatedFlowMolesPerSecond,
                AtmosPortConstants.PortMaxDifferentialKpa,
                AtmosConstants.TickInterval);

            Assert.AreEqual(0f, flow);
        }

        [Test]
        public void AtmosPortFlow_ScalesWithPressureDifferential()
        {
            float lowDelta = AtmosPortFlow.ComputeFlowMoles(
                110f,
                100f,
                AtmosPortConstants.VentRatedFlowMolesPerSecond,
                AtmosPortConstants.PortMaxDifferentialKpa,
                AtmosConstants.TickInterval);

            float highDelta = AtmosPortFlow.ComputeFlowMoles(
                200f,
                100f,
                AtmosPortConstants.VentRatedFlowMolesPerSecond,
                AtmosPortConstants.PortMaxDifferentialKpa,
                AtmosConstants.TickInterval);

            Assert.Greater(lowDelta, 0f);
            Assert.Greater(highDelta, lowDelta);
        }

        [Test]
        public void AtmosDevicePipeResolver_FindsNetworkUnderFurnitureTile()
        {
            TileMapTestUtilities.MapContext context = TileMapTestUtilities.CreateContext(_instantiated);
            PlacedTileObject pipe = CreateGasPipeAt(new Vector2Int(5, 5), TileLayer.PipeMiddle);
            RegisterOnMap(context.Map, pipe, TileLayer.PipeMiddle, new Vector3(5, 0, 5));

            var registry = new GasPipeNetworkRegistry(AtmosConstants.DefaultGasCount);
            registry.RebuildAll(context.Map);

            TileCoord deviceCoord = new TileCoord(context.Map.MapId, 5, 5);
            Assert.IsTrue(AtmosDevicePipeResolver.TryResolveNetwork(
                context.Map,
                registry,
                deviceCoord,
                AtmosDevicePipeResolver.VentLayers,
                out GasPipeNetworkId networkId,
                out GasPipeSegmentKey segmentKey));
            Assert.IsFalse(networkId.IsNone);
            Assert.AreEqual(TileLayer.PipeMiddle, segmentKey.Layer);
        }

        [Test]
        public void SimpleAdjacencyConnectorPipe_ParticipatesInGasNetwork()
        {
            TileMapTestUtilities.MapContext context = TileMapTestUtilities.CreateContext(_instantiated);
            PlacedTileObject pipe = CreateSimpleGasPipeAt(new Vector2Int(5, 5), TileLayer.PipeMiddle);
            RegisterOnMap(context.Map, pipe, TileLayer.PipeMiddle, new Vector3(5, 0, 5));

            Assert.IsTrue(PipeConnectionRule.ParticipatesInGasNetwork(pipe));

            var registry = new GasPipeNetworkRegistry(AtmosConstants.DefaultGasCount);
            registry.RebuildAll(context.Map);

            Assert.AreEqual(1, registry.NetworkCount);
        }

        [Test]
        public void AtmosDevicePipeResolver_IgnoresPipesOnNeighbouringTiles()
        {
            TileMapTestUtilities.MapContext context = TileMapTestUtilities.CreateContext(_instantiated);
            PlacedTileObject pipe = CreateGasPipeAt(new Vector2Int(6, 5), TileLayer.PipeMiddle);
            RegisterOnMap(context.Map, pipe, TileLayer.PipeMiddle, new Vector3(6, 0, 5));

            var registry = new GasPipeNetworkRegistry(AtmosConstants.DefaultGasCount);
            registry.RebuildAll(context.Map);

            TileCoord deviceCoord = new TileCoord(context.Map.MapId, 5, 5);
            Assert.IsFalse(AtmosDevicePipeResolver.TryResolveNetwork(
                context.Map,
                registry,
                deviceCoord,
                AtmosDevicePipeResolver.VentLayers,
                out _,
                out _));
            Assert.IsFalse(AtmosDevicePipeResolver.HasPipeSegment(context.Map, deviceCoord, AtmosDevicePipeResolver.VentLayers));
        }

        [Test]
        public void DeviceLines_VentsPreferSupplyAndScrubbersPreferWaste()
        {
            TileMapTestUtilities.MapContext context = TileMapTestUtilities.CreateContext(_instantiated);
            GasPipeNetworkRegistry registry = RegistryWithPipesOn(context, TileLayer.PipeLeft, TileLayer.PipeMiddle, TileLayer.PipeRight);

            Assert.AreEqual(TileLayer.PipeLeft, ResolvedLayer(context, registry, AtmosDevicePipeResolver.VentLayers));
            Assert.AreEqual(TileLayer.PipeRight, ResolvedLayer(context, registry, AtmosDevicePipeResolver.ScrubberLayers));
        }

        [Test]
        public void DeviceLines_VentsAndScrubbersBothFallBackToMixed()
        {
            TileMapTestUtilities.MapContext context = TileMapTestUtilities.CreateContext(_instantiated);
            GasPipeNetworkRegistry registry = RegistryWithPipesOn(context, TileLayer.PipeMiddle);

            Assert.AreEqual(TileLayer.PipeMiddle, ResolvedLayer(context, registry, AtmosDevicePipeResolver.VentLayers));
            Assert.AreEqual(TileLayer.PipeMiddle, ResolvedLayer(context, registry, AtmosDevicePipeResolver.ScrubberLayers));
        }

        [Test]
        public void DeviceLines_EachDeviceIgnoresTheOtherLines()
        {
            TileMapTestUtilities.MapContext context = TileMapTestUtilities.CreateContext(_instantiated);
            GasPipeNetworkRegistry registry = RegistryWithPipesOn(context, TileLayer.PipeRight, TileLayer.PipeSurface);
            TileCoord coord = new TileCoord(context.Map.MapId, 5, 5);

            Assert.IsFalse(AtmosDevicePipeResolver.TryResolveNetwork(
                context.Map, registry, coord, AtmosDevicePipeResolver.VentLayers, out _, out _));
            Assert.AreEqual(TileLayer.PipeSurface, ResolvedLayer(context, registry, AtmosDevicePipeResolver.InlineLayers));

            TileMapTestUtilities.MapContext other = TileMapTestUtilities.CreateContext(_instantiated);
            GasPipeNetworkRegistry supplyOnly = RegistryWithPipesOn(other, TileLayer.PipeLeft);
            Assert.IsFalse(AtmosDevicePipeResolver.TryResolveNetwork(
                other.Map, supplyOnly, new TileCoord(other.Map.MapId, 5, 5), AtmosDevicePipeResolver.ScrubberLayers, out _, out _));
        }

        [Test]
        public void JoiningTwoNetworks_AddsMolesAndBlendsTemperatureByHeatCapacity()
        {
            TileMapTestUtilities.MapContext context = TileMapTestUtilities.CreateContext(_instantiated);
            PlacedTileObject western = CreateGasPipeAt(new Vector2Int(5, 5), TileLayer.PipeMiddle);
            PlacedTileObject eastern = CreateGasPipeAt(new Vector2Int(7, 5), TileLayer.PipeMiddle);
            RegisterOnMap(context.Map, western, TileLayer.PipeMiddle, new Vector3(5, 0, 5));
            RegisterOnMap(context.Map, eastern, TileLayer.PipeMiddle, new Vector3(7, 0, 5));
            RecomputeAdjacency(context.Map, western, eastern);

            float[] specificHeats = { 1f, 3f, 1f, 1f };
            var registry = new GasPipeNetworkRegistry(AtmosConstants.DefaultGasCount, specificHeats);
            registry.RebuildAll(context.Map);
            Assert.AreEqual(2, registry.NetworkCount);

            registry.TryGetNetworkForSegment(GasPipeSegmentKey.From(western), out _, out GasPipeNetworkRecord westernNetwork);
            registry.TryGetNetworkForSegment(GasPipeSegmentKey.From(eastern), out _, out GasPipeNetworkRecord easternNetwork);
            westernNetwork.Moles[0] = 10f;
            westernNetwork.Temperature = 300f;
            easternNetwork.Moles[1] = 10f;
            easternNetwork.Temperature = 400f;

            PlacedTileObject center = CreateGasPipeAt(new Vector2Int(6, 5), TileLayer.PipeMiddle);
            RegisterOnMap(context.Map, center, TileLayer.PipeMiddle, new Vector3(6, 0, 5));
            RecomputeAdjacency(context.Map, western, center, eastern);
            registry.RebuildAround(context.Map, new TileCoord(context.Map.MapId, 6, 5));

            Assert.AreEqual(1, registry.NetworkCount);
            registry.TryGetNetworkForSegment(GasPipeSegmentKey.From(center), out _, out GasPipeNetworkRecord merged);
            Assert.AreEqual(10f, merged.Moles[0], 0.001f);
            Assert.AreEqual(10f, merged.Moles[1], 0.001f);
            Assert.AreEqual(3 * AtmosPipeConstants.SegmentVolume, merged.Volume, 0.001f);
            Assert.AreEqual(375f, merged.Temperature, 0.01f);
            Assert.AreEqual(0, registry.PendingReleases.Count);
        }

        [Test]
        public void SplittingANetwork_DividesGasBySegmentCount_AndReleasesTheRemovedShare()
        {
            TileMapTestUtilities.MapContext context = TileMapTestUtilities.CreateContext(_instantiated);
            var origins = new[] { 4, 5, 6, 7 };
            var pipes = new PlacedTileObject[origins.Length];
            for (int i = 0; i < origins.Length; i++)
            {
                pipes[i] = CreateGasPipeAt(new Vector2Int(origins[i], 5), TileLayer.PipeMiddle);
                RegisterOnMap(context.Map, pipes[i], TileLayer.PipeMiddle, new Vector3(origins[i], 0, 5));
                TileMapTestUtilities.PlacePlenum(context, new Vector3(origins[i], 0, 5));
            }

            RecomputeAdjacency(context.Map, pipes);

            AtmosSimulation turf = CreateTurf(context, origins);
            using (turf)
            {
                var registry = new GasPipeNetworkRegistry(AtmosConstants.DefaultGasCount);
                registry.RebuildAll(context.Map);
                registry.TryGetNetworkForSegment(GasPipeSegmentKey.From(pipes[0]), out _, out GasPipeNetworkRecord network);
                network.Moles[AtmosConstants.Plasma.Value] = 40f;

                var observer = new AtmosPipeObserver(context.Map, registry);
                var pipeSimulation = new AtmosPipeSimulation(registry, turf, null, AtmosConstants.DefaultGasCount, observer);

                GasPipeSegmentKey west = GasPipeSegmentKey.From(pipes[0]);
                GasPipeSegmentKey east = GasPipeSegmentKey.From(pipes[3]);
                TileCoord removed = new TileCoord(context.Map.MapId, 6, 5);
                float turfPlasmaBefore = turf.DebugGetMoles(removed, AtmosConstants.Plasma);

                context.Map.GetOrCreateTileLocation(TileLayer.PipeMiddle, new Vector3(6, 0, 5)).ClearAllPlacedObject();
                observer.OnTileCleared(null, removed, TileLayer.PipeMiddle);
                pipeSimulation.Tick(AtmosConstants.TickInterval);

                Assert.AreEqual(2, registry.NetworkCount);
                registry.TryGetNetworkForSegment(west, out _, out GasPipeNetworkRecord westernNetwork);
                registry.TryGetNetworkForSegment(east, out _, out GasPipeNetworkRecord easternNetwork);
                Assert.AreEqual(20f, westernNetwork.Moles[AtmosConstants.Plasma.Value], 0.001f, "two of four segments");
                Assert.AreEqual(10f, easternNetwork.Moles[AtmosConstants.Plasma.Value], 0.001f, "one of four segments");
                Assert.AreEqual(10f, turf.DebugGetMoles(removed, AtmosConstants.Plasma) - turfPlasmaBefore, 0.001f);
                Assert.AreEqual(0, registry.PendingReleases.Count);
            }
        }

        [Test]
        public void RemovingALoneSegment_ReleasesAllItsGasAndDropsTheNetwork()
        {
            TileMapTestUtilities.MapContext context = TileMapTestUtilities.CreateContext(_instantiated);
            TileMapTestUtilities.PlacePlenum(context, new Vector3(5, 0, 5));
            PlacedTileObject pipe = CreateGasPipeAt(new Vector2Int(5, 5), TileLayer.PipeMiddle);
            RegisterOnMap(context.Map, pipe, TileLayer.PipeMiddle, new Vector3(5, 0, 5));

            using AtmosSimulation turf = CreateTurf(context, 5);
            var registry = new GasPipeNetworkRegistry(AtmosConstants.DefaultGasCount);
            registry.RebuildAll(context.Map);
            registry.TryGetNetworkForSegment(GasPipeSegmentKey.From(pipe), out _, out GasPipeNetworkRecord network);
            network.Moles[AtmosConstants.Plasma.Value] = 12f;

            var observer = new AtmosPipeObserver(context.Map, registry);
            var pipeSimulation = new AtmosPipeSimulation(registry, turf, null, AtmosConstants.DefaultGasCount, observer);

            TileCoord coord = new TileCoord(context.Map.MapId, 5, 5);
            context.Map.GetOrCreateTileLocation(TileLayer.PipeMiddle, new Vector3(5, 0, 5)).ClearAllPlacedObject();
            observer.OnTileCleared(null, coord, TileLayer.PipeMiddle);
            pipeSimulation.Tick(AtmosConstants.TickInterval);

            Assert.AreEqual(0, registry.NetworkCount);
            Assert.AreEqual(12f, turf.DebugGetMoles(coord, AtmosConstants.Plasma), 0.001f);
        }

        private AtmosSimulation CreateTurf(TileMapTestUtilities.MapContext context, params int[] originsOnRow5)
        {
            var turf = new AtmosSimulation(context.Query, context.Map.MapId, AtmosConstants.DefaultGasCount);
            turf.CreateChunk(new TileChunkRef
            {
                MapId = context.Map.MapId,
                ChunkKey = Vector2Int.zero,
                Origin = Vector3.zero,
            });

            foreach (int x in originsOnRow5)
                turf.UpdateCell(new TileCoord(context.Map.MapId, x, 5));

            return turf;
        }

        private GasPipeNetworkRegistry RegistryWithPipesOn(TileMapTestUtilities.MapContext context, params TileLayer[] layers)
        {
            foreach (TileLayer layer in layers)
                RegisterOnMap(context.Map, CreateGasPipeAt(new Vector2Int(5, 5), layer), layer, new Vector3(5, 0, 5));

            var registry = new GasPipeNetworkRegistry(AtmosConstants.DefaultGasCount);
            registry.RebuildAll(context.Map);
            return registry;
        }

        private static TileLayer ResolvedLayer(TileMapTestUtilities.MapContext context, GasPipeNetworkRegistry registry, TileLayer[] layers)
        {
            Assert.IsTrue(AtmosDevicePipeResolver.TryResolveNetwork(
                context.Map,
                registry,
                new TileCoord(context.Map.MapId, 5, 5),
                layers,
                out _,
                out GasPipeSegmentKey segmentKey));
            return segmentKey.Layer;
        }

        private PlacedTileObject CreateGasPipeAt(Vector2Int worldOrigin, TileLayer layer)
        {
            GameObject go = new GameObject($"GasPipe_{worldOrigin}_{layer}");
            _instantiated.Add(go);

            PlacedTileObject placed = go.AddComponent<PlacedTileObject>();
            go.AddComponent<PipeAdjacencyConnector>();

            TileObjectSo so = ScriptableObject.CreateInstance<TileObjectSo>();
            so.genericType = TileObjectGenericType.Pipe;
            so.specificType = TileObjectSpecificType.None;
            so.layer = layer;

            SetPrivateField(placed, "_tileObjectSo", so);
            SetPrivateField(placed, "_connector", placed.GetComponent<IAdjacencyConnector>());
            SetPrivateField(placed, "_worldOrigin", worldOrigin);
            SetPrivateField(placed, "_mapId", 0);
            placed.transform.position = new Vector3(worldOrigin.x, 0, worldOrigin.y);
            return placed;
        }

        private PlacedTileObject CreateSimpleGasPipeAt(Vector2Int worldOrigin, TileLayer layer)
        {
            GameObject go = new GameObject($"SimpleGasPipe_{worldOrigin}_{layer}");
            _instantiated.Add(go);

            PlacedTileObject placed = go.AddComponent<PlacedTileObject>();
            go.AddComponent<SimpleAdjacencyConnector>();

            TileObjectSo so = ScriptableObject.CreateInstance<TileObjectSo>();
            so.genericType = TileObjectGenericType.Pipe;
            so.specificType = TileObjectSpecificType.None;
            so.layer = layer;

            SetPrivateField(placed, "_tileObjectSo", so);
            SetPrivateField(placed, "_connector", placed.GetComponent<IAdjacencyConnector>());
            SetPrivateField(placed, "_worldOrigin", worldOrigin);
            SetPrivateField(placed, "_mapId", 0);
            placed.transform.position = new Vector3(worldOrigin.x, 0, worldOrigin.y);
            return placed;
        }

        private static void RegisterOnMap(TileMap map, PlacedTileObject placed, TileLayer layer, Vector3 worldPosition)
        {
            ITileLocation location = map.GetOrCreateTileLocation(layer, worldPosition);
            location.AddPlacedObject(placed, Direction.North);
            placed.transform.SetParent(map.transform);
        }

        private static void RecomputeAdjacency(TileMap map, params PlacedTileObject[] segments)
        {
            foreach (PlacedTileObject segment in segments)
            {
                map.AdjacencyEngine.QueueCascadeFrom(segment);
            }

            map.AdjacencyEngine.ProcessQueue();
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            FieldInfo field = target.GetType().GetField(fieldName, flags);
            Assert.IsNotNull(field, $"Field {fieldName} not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }
    }
}

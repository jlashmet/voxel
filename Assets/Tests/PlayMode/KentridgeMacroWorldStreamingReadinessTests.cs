using System;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Showcase;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class KentridgeMacroWorldStreamingReadinessTests
    {
        private const uint Seed = 0x4B454E54u;
        private const int BuildingFoundationInsetDm = 6;
        private const int BuildingTerrainSamplesPerAxis = 5;
        private const double StreamingBudgetMs = 5000.0;
        private const int MaximumFeatureDrainSteps = 16;

        [Test]
        public void PresentationColumnReadinessWaitsForFeaturePublicationAndCommittedSettlementShell()
        {
            // Keep the complete macro physical/storage contract inside the final exact-SHA target,
            // then add the production streaming discriminator that was missing from the prior gate.
            new KentridgeMacroWorldPhysicalStorageAcceptanceTests()
                .PhysicalMacroWorldReachesProductionStorageWithSettlementShellAndRoof();

            TopDownWorldLayout layout = MountingForceTopDownWorldDefinition.Build(Seed);
            TopDownWorldPhysicalIntentSpec intent = KentridgeTopDownWorldPhysicalIntent.Build();
            VoxelWorldGenSettings settings = Settings();
            TopDownWorldPhysicalPlan physical = TopDownWorldPhysicalVoxelCatalogue.Plan(
                layout,
                intent,
                KentridgeDefinition.TownCentreDm,
                MountingForceTopDownWorldDefinition.CellSizeDm,
                settings);

            Assert.That(
                physical.TryGetSettlement(
                    MountingForceTopDownWorldDefinition.Moordell,
                    out TopDownWorldSettlementPlan settlement),
                Is.True);
            Assert.That(settlement.Buildings.Count, Is.GreaterThanOrEqualTo(4));

            TopDownWorldBuildingBlockoutPlan building = settlement.Buildings[0];
            int3 timberVoxel = TimberWallProbe(building, settings.VoxelsPerDecimetre);
            float3 presentationMetres = ToPresentationMetres(timberVoxel);

            TopDownWorldLayoutSelection.Select(
                layout,
                KentridgeDefinition.TownCentreDm.X,
                KentridgeDefinition.TownCentreDm.Y,
                MountingForceTopDownWorldDefinition.CellSizeDm);

            FeatureCatalogue combined = default;
            ShowcaseWorld world = null;
            try
            {
                combined = KentridgeCombinedVoxelCatalogue.Build(
                    Seed,
                    settings,
                    Allocator.Persistent);
                Assert.That(combined.IsCreated, Is.True);

                world = new ShowcaseWorld(
                    Seed,
                    brickPoolCapacity: 131072,
                    loadRadiusRegions: 1,
                    unloadRadiusRegions: 2);
                world.ConfigureGeneratedContentForGameplay(combined);
                combined = default;

                // Feature work is deliberately stepped before terrain. A fresh streaming call can
                // therefore finish every currently demanded terrain region and publish its terrain
                // meshes while the settlement feature publication for this column has not run yet.
                world.StepStreaming(presentationMetres, StreamingBudgetMs);

                Assert.That(world.PendingRegionLoads, Is.Zero,
                    "The discriminator requires all current terrain demand to be generated in the first streaming pass.");
                Assert.That(world.RegionsGenerated, Is.GreaterThan(0));
                Assert.That(
                    world.IsPresentationColumnContentSettled(presentationMetres),
                    Is.False,
                    "Terrain publication alone must not declare a presented settlement column stable while authored feature publication is pending.");

                int featureVoxelsBeforeDrain = world.FeatureVoxelsBuilt;
                int drainSteps = DrainUntilSettled(world, presentationMetres);

                Assert.That(
                    world.IsPresentationColumnContentSettled(presentationMetres),
                    Is.True,
                    "Presentation-column readiness must become true after that column's feature publication completes.");
                Assert.That(
                    world.FeatureVoxelsBuilt,
                    Is.GreaterThan(featureVoxelsBeforeDrain),
                    "The transition to ready must include real feature rasterization, not just queue bookkeeping.");

                VoxelCell timberCell = ReadCell(world.ReadStorage, timberVoxel);
                Assert.That(
                    timberCell.BaseMaterialId,
                    Is.EqualTo(settings.Materials.Resolve(MaterialRole.Timber)),
                    "The final feature publication must leave the Moordell settlement shell in the authoritative world consumed by rendering.");

                TestContext.WriteLine(
                    "MACRO_STREAMING_READINESS " +
                    $"regions={world.RegionsGenerated} featureVoxels={world.FeatureVoxelsBuilt} " +
                    $"lastFeatureMs={world.LastFeatureMs:0.00} drainSteps={drainSteps} timberVoxel={timberVoxel}");
            }
            finally
            {
                world?.Dispose();
                if (combined.IsCreated) combined.Dispose();
            }
        }

        [Test]
        public void NegativeZSettlementPublishesAfterStreamingDemandTransition()
        {
            TopDownWorldLayout layout = MountingForceTopDownWorldDefinition.Build(Seed);
            TopDownWorldPhysicalIntentSpec intent = KentridgeTopDownWorldPhysicalIntent.Build();
            VoxelWorldGenSettings settings = Settings();
            TopDownWorldPhysicalPlan physical = TopDownWorldPhysicalVoxelCatalogue.Plan(
                layout,
                intent,
                KentridgeDefinition.TownCentreDm,
                MountingForceTopDownWorldDefinition.CellSizeDm,
                settings);

            Assert.That(
                physical.TryGetSettlement(
                    MountingForceTopDownWorldDefinition.Moordell,
                    out TopDownWorldSettlementPlan moordell),
                Is.True);
            Assert.That(
                physical.TryGetSettlement(
                    MountingForceTopDownWorldDefinition.FairyVillage,
                    out TopDownWorldSettlementPlan fairy),
                Is.True);
            Assert.That(fairy.CentreDm.Y, Is.LessThan(0),
                "The signed-coordinate discriminator requires Fairy Village to remain in negative world Z.");

            int3 moordellTimber = TimberWallProbe(moordell.Buildings[0], settings.VoxelsPerDecimetre);
            int3 fairyTimber = TimberWallProbe(fairy.Buildings[0], settings.VoxelsPerDecimetre);
            float3 moordellMetres = ToPresentationMetres(moordellTimber);
            float3 fairyMetres = ToPresentationMetres(fairyTimber);
            Assert.That(fairyTimber.z, Is.LessThan(0));

            TopDownWorldLayoutSelection.Select(
                layout,
                KentridgeDefinition.TownCentreDm.X,
                KentridgeDefinition.TownCentreDm.Y,
                MountingForceTopDownWorldDefinition.CellSizeDm);

            FeatureCatalogue combined = default;
            ShowcaseWorld world = null;
            try
            {
                combined = KentridgeCombinedVoxelCatalogue.Build(
                    Seed,
                    settings,
                    Allocator.Persistent);
                world = new ShowcaseWorld(
                    Seed,
                    brickPoolCapacity: 131072,
                    loadRadiusRegions: 1,
                    unloadRadiusRegions: 2);
                world.ConfigureGeneratedContentForGameplay(combined);
                combined = default;

                // Match the built-player evidence lifecycle: first settle a positive-Z settlement,
                // then move demand across the origin and require the negative-Z settlement to be
                // generated, feature-published, and retained in authoritative streamed storage.
                world.StepStreaming(moordellMetres, StreamingBudgetMs);
                DrainUntilSettled(world, moordellMetres);
                Assert.That(world.IsPresentationColumnContentSettled(moordellMetres), Is.True);

                int featureVoxelsBeforeFairy = world.FeatureVoxelsBuilt;
                world.StepStreaming(fairyMetres, StreamingBudgetMs);
                int fairyDrainSteps = DrainUntilSettled(world, fairyMetres);

                Assert.That(
                    world.IsPresentationColumnContentSettled(fairyMetres),
                    Is.True,
                    "A negative-Z settlement column must not become stranded after demand crosses the world origin.");
                Assert.That(
                    world.FeatureVoxelsBuilt,
                    Is.GreaterThan(featureVoxelsBeforeFairy),
                    "Fairy demand must perform real feature rasterization after the positive-to-negative-Z transition.");

                VoxelCell fairyCell = ReadCell(world.ReadStorage, fairyTimber);
                Assert.That(
                    fairyCell.BaseMaterialId,
                    Is.EqualTo(settings.Materials.Resolve(MaterialRole.Timber)),
                    "Fairy Village must retain its authored timber shell in the streamed authoritative world.");

                TestContext.WriteLine(
                    "MACRO_NEGATIVE_Z_STREAMING " +
                    $"fairyVoxel={fairyTimber} drainSteps={fairyDrainSteps} " +
                    $"featureVoxels={world.FeatureVoxelsBuilt} regions={world.RegionsGenerated}");
            }
            finally
            {
                world?.Dispose();
                if (combined.IsCreated) combined.Dispose();
            }
        }

        private static int DrainUntilSettled(ShowcaseWorld world, float3 presentationMetres)
        {
            int steps = 0;
            while (!world.IsPresentationColumnContentSettled(presentationMetres)
                   && steps++ < MaximumFeatureDrainSteps)
                world.StepStreaming(presentationMetres, StreamingBudgetMs);
            return steps;
        }

        private static int3 TimberWallProbe(
            TopDownWorldBuildingBlockoutPlan building,
            int scale)
        {
            SampleTerrainRelief(building, scale, out _, out int maximumGround);
            return new int3(
                building.CentreDm.X * scale,
                maximumGround + 10 * scale,
                (building.CentreDm.Y - building.HalfExtentZDm + 1) * scale);
        }

        private static float3 ToPresentationMetres(int3 voxel)
        {
            return new float3(
                voxel.x * ShowcaseWorld.VoxelSize,
                voxel.y * ShowcaseWorld.VoxelSize,
                voxel.z * ShowcaseWorld.VoxelSize);
        }

        private static VoxelCell ReadCell(IRegionReadSource reads, int3 worldVoxel)
        {
            int edge = ShowcaseWorld.RegionVoxelEdge;
            var region = new int3(
                (int)math.floor((float)worldVoxel.x / edge),
                (int)math.floor((float)worldVoxel.y / edge),
                (int)math.floor((float)worldVoxel.z / edge));
            int3 local = worldVoxel - region * edge;

            Assert.That(reads.TryAcquireRegion(region, out RegionReadView view), Is.True,
                "The settlement shell region must remain resident while it is in current demand.");
            Assert.That(view.TryReadCell(local, out VoxelCell cell), Is.True);
            return cell;
        }

        private static void SampleTerrainRelief(
            TopDownWorldBuildingBlockoutPlan building,
            int scale,
            out int minimumGround,
            out int maximumGround)
        {
            int leftDm = building.CentreDm.X - building.HalfExtentXDm - BuildingFoundationInsetDm;
            int rightDm = building.CentreDm.X + building.HalfExtentXDm + BuildingFoundationInsetDm;
            int backDm = building.CentreDm.Y - building.HalfExtentZDm - BuildingFoundationInsetDm;
            int frontDm = building.CentreDm.Y + building.HalfExtentZDm + BuildingFoundationInsetDm;
            minimumGround = int.MaxValue;
            maximumGround = int.MinValue;

            for (var x = 0; x < BuildingTerrainSamplesPerAxis; x++)
            {
                int xDm = leftDm + (rightDm - leftDm) * x / (BuildingTerrainSamplesPerAxis - 1);
                for (var z = 0; z < BuildingTerrainSamplesPerAxis; z++)
                {
                    int zDm = backDm + (frontDm - backDm) * z / (BuildingTerrainSamplesPerAxis - 1);
                    int ground = TerrainSampler.HeightAt(xDm * scale, zDm * scale, Seed);
                    minimumGround = Math.Min(minimumGround, ground);
                    maximumGround = Math.Max(maximumGround, ground);
                }
            }
        }

        private static VoxelWorldGenSettings Settings()
        {
            return new VoxelWorldGenSettings(
                1,
                new VoxelMaterialMap(
                    foundationStone: 20,
                    masonry: 18,
                    darkMasonry: 6,
                    timber: 2,
                    glass: 4,
                    warmWindow: 15,
                    roofTile: 8,
                    slate: 7,
                    cloth: 9,
                    moss: 14,
                    water: 11,
                    roadSurface: 13));
        }
    }
}
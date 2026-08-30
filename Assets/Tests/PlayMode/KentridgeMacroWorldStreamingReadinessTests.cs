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
        public void CurrentDemandReadinessWaitsForFeaturePublicationAndCommittedSettlementShell()
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
                    out TopDownWorldSettlementPlan settlement),
                Is.True);
            Assert.That(settlement.Buildings.Count, Is.GreaterThanOrEqualTo(4));

            TopDownWorldBuildingBlockoutPlan building = settlement.Buildings[0];
            SampleTerrainRelief(building, settings.VoxelsPerDecimetre, out _, out int maximumGround);
            var timberVoxel = new int3(
                building.CentreDm.X * settings.VoxelsPerDecimetre,
                maximumGround + 10 * settings.VoxelsPerDecimetre,
                building.CentreDm.Y * settings.VoxelsPerDecimetre);
            var cameraMetres = new float3(
                timberVoxel.x * ShowcaseWorld.VoxelSize,
                timberVoxel.y * ShowcaseWorld.VoxelSize,
                timberVoxel.z * ShowcaseWorld.VoxelSize);

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
                // meshes while the settlement feature publications it just queued have not run yet.
                world.StepStreaming(cameraMetres, StreamingBudgetMs);

                Assert.That(world.PendingRegionLoads, Is.Zero,
                    "The discriminator requires all current terrain demand to be generated in the first streaming pass.");
                Assert.That(world.RegionsGenerated, Is.GreaterThan(0));
                Assert.That(
                    world.IsCurrentDemandContentSettled(cameraMetres),
                    Is.False,
                    "Terrain publication alone must not declare the current demand presentation-stable while authored feature publication is pending.");

                int featureVoxelsBeforeDrain = world.FeatureVoxelsBuilt;
                int drainSteps = 0;
                while (!world.IsCurrentDemandContentSettled(cameraMetres)
                       && drainSteps++ < MaximumFeatureDrainSteps)
                    world.StepStreaming(cameraMetres, StreamingBudgetMs);

                Assert.That(
                    world.IsCurrentDemandContentSettled(cameraMetres),
                    Is.True,
                    "Current-demand readiness must become true after all demanded feature publications complete.");
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
                    $"featureSteps={world.FeatureBuildSteps} drainSteps={drainSteps} timberVoxel={timberVoxel}");
            }
            finally
            {
                world?.Dispose();
                if (combined.IsCreated) combined.Dispose();
            }
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

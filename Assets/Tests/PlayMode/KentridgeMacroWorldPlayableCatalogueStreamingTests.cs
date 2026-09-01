using System;
using Game.Composition.WorldBuilderWorldGen.Runtime;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Hightown;
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
    /// <summary>
    /// Reproduces the playable slice's final catalogue composition boundary. The ordinary macro
    /// streaming fixture feeds Kentridge's combined catalogue directly to ShowcaseWorld, while the
    /// player appends Hightown and the regional corridor first. This fixture proves that second
    /// combine cannot drop or rebase the authored negative-Z macro settlement placements.
    /// </summary>
    public sealed class KentridgeMacroWorldPlayableCatalogueStreamingTests
    {
        private const uint Seed = 0x4B454E54u;
        private const int BuildingFoundationInsetDm = 6;
        private const int BuildingTerrainSamplesPerAxis = 5;
        private const double StreamingBudgetMs = 5000.0;
        private const int MaximumDrainSteps = 16;

        [Test]
        public void PlayableKentridgeCatalogueRequiresExplicitOneShotMacroSelection()
        {
            TopDownWorldLayout layout = MountingForceTopDownWorldDefinition.Build(Seed);
            VoxelWorldGenSettings settings = Settings(kentridge: true);

            // Establish a deterministic empty handoff before reproducing the playable catalogue call.
            // Select/TryConsume is intentionally one-shot; no scene-global reset API is needed.
            TopDownWorldLayoutSelection.Select(
                layout,
                KentridgeDefinition.TownCentreDm.X,
                KentridgeDefinition.TownCentreDm.Y,
                MountingForceTopDownWorldDefinition.CellSizeDm);
            Assert.That(TopDownWorldLayoutSelection.TryConsume(Seed, out _), Is.True);

            FeatureCatalogue withoutSelection = default;
            FeatureCatalogue withSelection = default;
            try
            {
                withoutSelection = KentridgeCombinedVoxelCatalogue.Build(
                    Seed,
                    settings,
                    Allocator.Temp);
                Assert.That(
                    ContainsDefinitionStarting(withoutSelection, "macro-town-building-fairy-village-"),
                    Is.False,
                    "The production Kentridge catalogue cannot contain the macro settlement unless composition selects the semantic macro layout first.");
                Assert.That(
                    ContainsDefinitionStarting(withoutSelection, "macro-town-building-orc-village-"),
                    Is.False);

                TopDownWorldLayoutSelection.Select(
                    layout,
                    KentridgeDefinition.TownCentreDm.X,
                    KentridgeDefinition.TownCentreDm.Y,
                    MountingForceTopDownWorldDefinition.CellSizeDm);
                withSelection = KentridgeCombinedVoxelCatalogue.Build(
                    Seed,
                    settings,
                    Allocator.Temp);
                Assert.That(
                    ContainsDefinitionStarting(withSelection, "macro-town-building-fairy-village-"),
                    Is.True,
                    "The same production catalogue must include Fairy once the explicit semantic handoff is present.");
                Assert.That(
                    ContainsDefinitionStarting(withSelection, "macro-town-building-orc-village-"),
                    Is.True,
                    "The same production catalogue must include Orc once the explicit semantic handoff is present.");

                TestContext.WriteLine(
                    "MACRO_PLAYABLE_SELECTION " +
                    $"withoutDefinitions={withoutSelection.Definitions.Length} " +
                    $"withDefinitions={withSelection.Definitions.Length}");
            }
            finally
            {
                if (withSelection.IsCreated) withSelection.Dispose();
                if (withoutSelection.IsCreated) withoutSelection.Dispose();
            }
        }

        [Test]
        public void PlayableCatalogueRetainsFairySettlementAfterHightownAndCorridorCombine()
        {
            TopDownWorldLayout layout = MountingForceTopDownWorldDefinition.Build(Seed);
            VoxelWorldGenSettings kentridgeSettings = Settings(kentridge: true);
            TopDownWorldPhysicalPlan physical = TopDownWorldPhysicalVoxelCatalogue.Plan(
                layout,
                KentridgeTopDownWorldPhysicalIntent.Build(),
                KentridgeDefinition.TownCentreDm,
                MountingForceTopDownWorldDefinition.CellSizeDm,
                kentridgeSettings);
            Assert.That(
                physical.TryGetSettlement(
                    MountingForceTopDownWorldDefinition.FairyVillage,
                    out TopDownWorldSettlementPlan fairy),
                Is.True);

            int3 timberVoxel = TimberWallProbe(fairy.Buildings[0], kentridgeSettings.VoxelsPerDecimetre);
            float3 presentationMetres = new float3(
                timberVoxel.x * ShowcaseWorld.VoxelSize,
                timberVoxel.y * ShowcaseWorld.VoxelSize,
                timberVoxel.z * ShowcaseWorld.VoxelSize);
            Assert.That(timberVoxel.z, Is.LessThan(0));

            SettlementPlan kentridge = KentridgeDefinition.Build(Seed);
            SettlementPlan hightown = HightownDefinition.Build(Seed);
            TopDownWorldLayoutSelection.Select(
                layout,
                KentridgeDefinition.TownCentreDm.X,
                KentridgeDefinition.TownCentreDm.Y,
                MountingForceTopDownWorldDefinition.CellSizeDm);

            FeatureCatalogue kentridgeCatalogue = default;
            FeatureCatalogue hightownCatalogue = default;
            FeatureCatalogue corridorCatalogue = default;
            FeatureCatalogue playableCatalogue = default;
            ShowcaseWorld world = null;
            try
            {
                kentridgeCatalogue = KentridgeCombinedVoxelCatalogue.Build(
                    Seed,
                    kentridgeSettings,
                    Allocator.Temp);
                hightownCatalogue = HightownVoxelCatalogue.Build(
                    hightown,
                    Settings(kentridge: false),
                    Allocator.Temp);
                corridorCatalogue = RegionCorridorCatalogue.Build(
                    Seed,
                    kentridgeSettings,
                    kentridge.CentreDm,
                    hightown.CentreDm,
                    Allocator.Temp);
                playableCatalogue = SettlementCatalogueCombiner.Combine(
                    Allocator.Persistent,
                    kentridgeCatalogue,
                    hightownCatalogue,
                    corridorCatalogue);

                world = new ShowcaseWorld(
                    Seed,
                    brickPoolCapacity: 131072,
                    loadRadiusRegions: 1,
                    unloadRadiusRegions: 2);
                world.ConfigureGeneratedContentForGameplay(playableCatalogue);
                playableCatalogue = default;

                world.StepStreaming(presentationMetres, StreamingBudgetMs);
                int steps = 0;
                while (!world.IsPresentationColumnContentSettled(presentationMetres)
                       && steps++ < MaximumDrainSteps)
                    world.StepStreaming(presentationMetres, StreamingBudgetMs);

                Assert.That(
                    world.IsPresentationColumnContentSettled(presentationMetres),
                    Is.True,
                    "The playable three-catalogue combine must still settle Fairy's negative-Z authored column.");
                VoxelCell timber = ReadCell(world.ReadStorage, timberVoxel);
                Assert.That(
                    timber.BaseMaterialId,
                    Is.EqualTo(kentridgeSettings.Materials.Resolve(MaterialRole.Timber)),
                    "Appending Hightown and the regional corridor must not drop or rebase Fairy's authored timber shell.");

                TestContext.WriteLine(
                    "MACRO_PLAYABLE_CATALOGUE_STREAMING " +
                    $"fairyVoxel={timberVoxel} drainSteps={steps} " +
                    $"featureVoxels={world.FeatureVoxelsBuilt} regions={world.RegionsGenerated}");
            }
            finally
            {
                world?.Dispose();
                if (playableCatalogue.IsCreated) playableCatalogue.Dispose();
                if (corridorCatalogue.IsCreated) corridorCatalogue.Dispose();
                if (hightownCatalogue.IsCreated) hightownCatalogue.Dispose();
                if (kentridgeCatalogue.IsCreated) kentridgeCatalogue.Dispose();
            }
        }

        private static bool ContainsDefinitionStarting(FeatureCatalogue catalogue, string prefix)
        {
            for (var i = 0; i < catalogue.Definitions.Length; i++)
                if (catalogue.Definitions[i].Name.ToString().StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static int3 TimberWallProbe(TopDownWorldBuildingBlockoutPlan building, int scale)
        {
            int leftDm = building.CentreDm.X - building.HalfExtentXDm - BuildingFoundationInsetDm;
            int rightDm = building.CentreDm.X + building.HalfExtentXDm + BuildingFoundationInsetDm;
            int backDm = building.CentreDm.Y - building.HalfExtentZDm - BuildingFoundationInsetDm;
            int frontDm = building.CentreDm.Y + building.HalfExtentZDm + BuildingFoundationInsetDm;
            int maximumGround = int.MinValue;
            for (var x = 0; x < BuildingTerrainSamplesPerAxis; x++)
            {
                int xDm = leftDm + (rightDm - leftDm) * x / (BuildingTerrainSamplesPerAxis - 1);
                for (var z = 0; z < BuildingTerrainSamplesPerAxis; z++)
                {
                    int zDm = backDm + (frontDm - backDm) * z / (BuildingTerrainSamplesPerAxis - 1);
                    maximumGround = Math.Max(
                        maximumGround,
                        TerrainSampler.HeightAt(xDm * scale, zDm * scale, Seed));
                }
            }

            return new int3(
                building.CentreDm.X * scale,
                maximumGround + 10 * scale,
                (building.CentreDm.Y - building.HalfExtentZDm + 1) * scale);
        }

        private static VoxelCell ReadCell(IRegionReadSource reads, int3 worldVoxel)
        {
            int edge = ShowcaseWorld.RegionVoxelEdge;
            var region = new int3(
                (int)math.floor((float)worldVoxel.x / edge),
                (int)math.floor((float)worldVoxel.y / edge),
                (int)math.floor((float)worldVoxel.z / edge));
            int3 local = worldVoxel - region * edge;
            Assert.That(reads.TryAcquireRegion(region, out RegionReadView view), Is.True);
            Assert.That(view.TryReadCell(local, out VoxelCell cell), Is.True);
            return cell;
        }

        private static VoxelWorldGenSettings Settings(bool kentridge)
        {
            return new VoxelWorldGenSettings(
                1,
                new VoxelMaterialMap(
                    foundationStone: kentridge ? (byte)20 : (byte)6,
                    masonry: kentridge ? (byte)18 : (byte)1,
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
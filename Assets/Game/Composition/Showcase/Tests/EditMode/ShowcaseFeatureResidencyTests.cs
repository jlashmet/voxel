using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Showcase.Tests
{
    public sealed class ShowcaseFeatureResidencyTests
    {
        private const uint Seed = 0x4B454E54u;
        private const double StreamingBudgetMs = 5000.0;
        private const int MaximumStreamingSteps = 64;

        [Test]
        public void OrdinaryStreamingPublishesAuthoredUpperLayerWithoutHorizontalRadiusWidening()
        {
            FeatureCatalogue catalogue = default;
            ShowcaseWorld world = null;
            try
            {
#pragma warning disable CS0618
                catalogue = ShowcaseCatalogue.Build(Seed, Allocator.Persistent);
#pragma warning restore CS0618
                Assert.That(catalogue.IsCreated, Is.True);

                PrepareBoundaryCrossingPlacement(
                    ref catalogue,
                    out ExplicitPlacement placement,
                    out FeatureDefinition definition,
                    out int3 upperRegion,
                    out int3 footprint);

                int presentationLayer = placement.Position.y >> VoxelGrid.RegionVoxelEdgeLog2;
                var presentationMetres = new float3(
                    (placement.Position.x + footprint.x / 2f) * ShowcaseWorld.VoxelSize,
                    placement.Position.y * ShowcaseWorld.VoxelSize,
                    (placement.Position.z + footprint.z / 2f) * ShowcaseWorld.VoxelSize);
                int3 presentationRegion = new int3(
                    upperRegion.x,
                    presentationLayer,
                    upperRegion.z);

                Assert.That(upperRegion.y, Is.GreaterThan(presentationLayer));
                Assert.That(upperRegion.x, Is.EqualTo(presentationRegion.x));
                Assert.That(upperRegion.z, Is.EqualTo(presentationRegion.z));

                world = new ShowcaseWorld(
                    Seed,
                    brickPoolCapacity: 131072,
                    loadRadiusRegions: 1,
                    unloadRadiusRegions: 2);
                world.ConfigureGeneratedContentForGameplay(catalogue);
                catalogue = default;

                world.GenerateRegionBlocking(presentationRegion);
                Assert.That(world.IsCurrentDemandContentSettled(presentationMetres), Is.True);
                Assert.That(world.IsGenerated(upperRegion), Is.False);
                Assert.That(world.IsPresentationColumnContentSettled(presentationMetres), Is.False);

                int featureVoxelsBefore = world.FeatureVoxelsBuilt;
                int steps = 0;
                while ((!world.IsGenerated(upperRegion)
                        || !world.IsPresentationColumnContentSettled(presentationMetres))
                       && steps++ < MaximumStreamingSteps)
                {
                    world.StepStreaming(presentationMetres, StreamingBudgetMs);
                }

                Assert.That(world.IsGenerated(upperRegion), Is.True,
                    "Ordinary streaming must generate the authored upper layer in the already-demanded X/Z column.");
                Assert.That(world.IsPresentationColumnContentSettled(presentationMetres), Is.True,
                    "Column readiness must wait for authored upper-layer publication.");
                Assert.That(world.FeatureVoxelsBuilt, Is.GreaterThan(featureVoxelsBefore),
                    "The transition must include real feature rasterization, not only queue bookkeeping.");
                Assert.That(world.ReadStorage.TryAcquireRegion(upperRegion, out RegionReadView _), Is.True,
                    "The authored upper layer must be readable from the production presentation source.");

                TestContext.WriteLine(
                    "SHOWCASE_FEATURE_RESIDENCY " +
                    $"definition={definition.Name} lower={presentationRegion} upper={upperRegion} " +
                    $"steps={steps} featureVoxelsDelta={world.FeatureVoxelsBuilt - featureVoxelsBefore} horizontalRadius=1");
            }
            finally
            {
                world?.Dispose();
                if (catalogue.IsCreated) catalogue.Dispose();
            }
        }

        private static void PrepareBoundaryCrossingPlacement(
            ref FeatureCatalogue catalogue,
            out ExplicitPlacement selectedPlacement,
            out FeatureDefinition selectedDefinition,
            out int3 selectedUpperRegion,
            out int3 selectedFootprint)
        {
            selectedPlacement = default;
            selectedDefinition = default;
            selectedUpperRegion = default;
            selectedFootprint = default;
            int selectedPlacementIndex = -1;
            int bestHeight = 1;

            for (var ruleIndex = 0; ruleIndex < catalogue.Rules.Length; ruleIndex++)
            {
                PlacementRule rule = catalogue.Rules[ruleIndex];
                if ((uint)rule.DefinitionId >= (uint)catalogue.Definitions.Length) continue;
                FeatureDefinition definition = catalogue.Definitions[rule.DefinitionId];
                if (definition.Footprint.y <= bestHeight || definition.ProgramLength <= 0) continue;

                int placementStart = math.max(0, rule.ExplicitOffset);
                int placementEnd = math.min(
                    rule.ExplicitOffset + rule.ExplicitCount,
                    catalogue.ExplicitPlacements.Length);
                if (placementStart >= placementEnd) continue;

                selectedPlacementIndex = placementStart;
                selectedDefinition = definition;
                bestHeight = definition.Footprint.y;
            }

            Assert.That(selectedPlacementIndex, Is.GreaterThanOrEqualTo(0),
                "Production Showcase catalogue must contain an explicit vertically authored feature.");

            ExplicitPlacement placement = catalogue.ExplicitPlacements[selectedPlacementIndex];
            int regionEdge = 1 << VoxelGrid.RegionVoxelEdgeLog2;
            int currentLayer = placement.Position.y >> VoxelGrid.RegionVoxelEdgeLog2;
            placement.Position.y = (currentLayer + 1) * regionEdge - 1;
            catalogue.ExplicitPlacements[selectedPlacementIndex] = placement;

            int3 footprint = selectedDefinition.Footprint;
            if ((placement.Orientation & 1) != 0)
                footprint = new int3(footprint.z, footprint.y, footprint.x);

            int lowerLayer = placement.Position.y >> VoxelGrid.RegionVoxelEdgeLog2;
            int upperLayer = (placement.Position.y + footprint.y - 1)
                             >> VoxelGrid.RegionVoxelEdgeLog2;
            Assert.That(upperLayer, Is.GreaterThan(lowerLayer));

            int centreX = placement.Position.x + footprint.x / 2;
            int centreZ = placement.Position.z + footprint.z / 2;
            selectedPlacement = placement;
            selectedFootprint = footprint;
            selectedUpperRegion = new int3(
                centreX >> VoxelGrid.RegionVoxelEdgeLog2,
                upperLayer,
                centreZ >> VoxelGrid.RegionVoxelEdgeLog2);
        }
    }
}

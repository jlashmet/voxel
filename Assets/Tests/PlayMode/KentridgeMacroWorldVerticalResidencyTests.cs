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

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class KentridgeMacroWorldVerticalResidencyTests
    {
        private const uint Seed = 0x4B454E54u;
        private const double StreamingBudgetMs = 5000.0;
        private const int MaximumStreamingSteps = 64;

        [Test]
        public void OrdinaryStreamingMakesTallAuthoredFeatureUpperRegionResidentWithoutTraversalForcing()
        {
            TopDownWorldLayout layout = MountingForceTopDownWorldDefinition.Build(Seed);
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
                    Settings(),
                    Allocator.Persistent);
                Assert.That(combined.IsCreated, Is.True);

                PrepareBoundaryCrossingExplicitPlacement(
                    ref combined,
                    out ExplicitPlacement placement,
                    out FeatureDefinition definition,
                    out int3 upperRegion);

                int3 footprint = definition.Footprint;
                if ((placement.Orientation & 1) != 0)
                    footprint = new int3(footprint.z, footprint.y, footprint.x);

                var presentationMetres = new float3(
                    (placement.Position.x + footprint.x / 2f) * ShowcaseWorld.VoxelSize,
                    placement.Position.y * ShowcaseWorld.VoxelSize,
                    (placement.Position.z + footprint.z / 2f) * ShowcaseWorld.VoxelSize);
                int presentationLayer = placement.Position.y >> VoxelGrid.RegionVoxelEdgeLog2;
                Assert.That(
                    upperRegion.y,
                    Is.GreaterThan(presentationLayer),
                    "The discriminator must keep the viewer below the authored upper shell so camera-layer residency cannot satisfy the assertion.");

                world = new ShowcaseWorld(
                    Seed,
                    brickPoolCapacity: 131072,
                    loadRadiusRegions: 1,
                    unloadRadiusRegions: 2);
                world.ConfigureGeneratedContentForGameplay(combined);
                combined = default;

                Assert.That(world.IsGenerated(upperRegion), Is.False);
                int featureVoxelsBefore = world.FeatureVoxelsBuilt;

                int steps = 0;
                while ((!world.IsGenerated(upperRegion)
                        || !world.IsPresentationColumnContentSettled(presentationMetres))
                       && steps++ < MaximumStreamingSteps)
                {
                    // StepStreaming is the production per-frame ShowcaseWorld update path. Do not
                    // call GenerateRegionBlocking or move the viewer into the upper region: this
                    // regression exists specifically to prove authored vertical residency does it.
                    world.StepStreaming(presentationMetres, StreamingBudgetMs);
                }

                Assert.That(
                    world.IsGenerated(upperRegion),
                    Is.True,
                    "Ordinary streaming must generate the authored feature's upper region while the viewer remains in its lower presentation layer.");
                Assert.That(
                    world.IsPresentationColumnContentSettled(presentationMetres),
                    Is.True,
                    "The authored column must finish feature publication through ordinary streaming.");
                Assert.That(
                    world.FeatureVoxelsBuilt,
                    Is.GreaterThan(featureVoxelsBefore),
                    "The residency transition must include real feature rasterization, not only terrain queue bookkeeping.");
                Assert.That(
                    world.ReadStorage.TryAcquireRegion(upperRegion, out RegionReadView _),
                    Is.True,
                    "The upper shell region must be resident in the same authoritative read source consumed by presentation rendering.");

                TestContext.WriteLine(
                    "KENTRIDGE_VERTICAL_RESIDENCY " +
                    $"definition={definition.Name} placement={placement.Position} footprint={footprint} upperRegion={upperRegion} " +
                    $"presentationLayer={presentationLayer} steps={steps} featureVoxels={world.FeatureVoxelsBuilt}");
            }
            finally
            {
                world?.Dispose();
                if (combined.IsCreated) combined.Dispose();
            }
        }

        private static void PrepareBoundaryCrossingExplicitPlacement(
            ref FeatureCatalogue catalogue,
            out ExplicitPlacement selectedPlacement,
            out FeatureDefinition selectedDefinition,
            out int3 selectedUpperRegion)
        {
            selectedPlacement = default;
            selectedDefinition = default;
            selectedUpperRegion = default;
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

            Assert.That(
                selectedPlacementIndex,
                Is.GreaterThanOrEqualTo(0),
                "The production catalogue must expose at least one real explicit feature with a nontrivial vertical raster program for the residency fixture.");

            ExplicitPlacement placement = catalogue.ExplicitPlacements[selectedPlacementIndex];
            int regionEdge = 1 << VoxelGrid.RegionVoxelEdgeLog2;
            int currentLayer = placement.Position.y >> VoxelGrid.RegionVoxelEdgeLog2;
            int nextBoundaryY = (currentLayer + 1) * regionEdge;
            placement.Position.y = nextBoundaryY - 1;
            catalogue.ExplicitPlacements[selectedPlacementIndex] = placement;

            int3 footprint = selectedDefinition.Footprint;
            if ((placement.Orientation & 1) != 0)
                footprint = new int3(footprint.z, footprint.y, footprint.x);

            int lowerLayer = placement.Position.y >> VoxelGrid.RegionVoxelEdgeLog2;
            int upperLayer = (placement.Position.y + footprint.y - 1)
                             >> VoxelGrid.RegionVoxelEdgeLog2;
            Assert.That(
                upperLayer,
                Is.GreaterThan(lowerLayer),
                "The fixture must deterministically reposition the production feature across a vertical region boundary.");

            int centreX = placement.Position.x + footprint.x / 2;
            int centreZ = placement.Position.z + footprint.z / 2;
            selectedPlacement = placement;
            selectedUpperRegion = new int3(
                centreX >> VoxelGrid.RegionVoxelEdgeLog2,
                upperLayer,
                centreZ >> VoxelGrid.RegionVoxelEdgeLog2);
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

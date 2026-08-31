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
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class KentridgeMacroWorldVerticalResidencyTests
    {
        private const uint Seed = 0x4B454E54u;
        private const double StreamingBudgetMs = 5000.0;
        private const int MaximumStreamingSteps = 64;
        private const byte MacroFoundationMaterial = 20;
        private const byte MacroTimberMaterial = 2;
        private const byte MacroRoofMaterial = 8;

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

                int3 presentationRegion = new int3(
                    upperRegion.x,
                    presentationLayer,
                    upperRegion.z);
                world.GenerateRegionBlocking(presentationRegion);

                Assert.That(
                    world.IsCurrentDemandContentSettled(presentationMetres),
                    Is.True,
                    "The fixture must first establish the historical race state: the bounded terrain/presentation layer is already final.");
                Assert.That(
                    world.IsGenerated(upperRegion),
                    Is.False,
                    "The fixture must keep the authored upper shell absent before ordinary streaming resumes.");
                Assert.That(
                    world.IsPresentationColumnContentSettled(presentationMetres),
                    Is.False,
                    "Presentation readiness must not report a ground column final while an authored upper feature layer in the same X/Z column is still absent.");

                int featureVoxelsBefore = world.FeatureVoxelsBuilt;
                int steps = 0;
                while ((!world.IsGenerated(upperRegion)
                        || !world.IsPresentationColumnContentSettled(presentationMetres))
                       && steps++ < MaximumStreamingSteps)
                {
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
                    "The residency transition must include real feature rasterization in the upper authored layer, not only terrain queue bookkeeping.");
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

        [Test]
        public void RossdamFourBlockoutBuildingsRasterizeIntoAuthoritativeStorage()
        {
            TopDownWorldLayout layout = MountingForceTopDownWorldDefinition.Build(Seed);
            VoxelWorldGenSettings settings = Settings();
            TopDownWorldPhysicalIntentSpec intent = KentridgeTopDownWorldPhysicalIntent.Build();
            var root = new Int2(
                KentridgeDefinition.TownCentreDm.X,
                KentridgeDefinition.TownCentreDm.Y);
            TopDownWorldPhysicalPlan physical = TopDownWorldPhysicalVoxelCatalogue.Plan(
                layout,
                intent,
                root,
                MountingForceTopDownWorldDefinition.CellSizeDm,
                settings);
            Assert.That(
                physical.TryGetSettlement(
                    KentridgeTopDownWorldLayout.Rossdam,
                    out TopDownWorldSettlementPlan rossdam),
                Is.True,
                "The production physical plan must expose Rossdam by its semantic graph id.");
            Assert.That(
                rossdam.Buildings.Count,
                Is.EqualTo(4),
                "Rossdam must retain the four generic production blockouts required by physical intent.");

            TopDownWorldLayoutSelection.Select(
                layout,
                root.X,
                root.Y,
                MountingForceTopDownWorldDefinition.CellSizeDm);

            FeatureCatalogue combined = default;
            var table = new RegionTable(256, Allocator.Persistent);
            var pool = new BrickPool(131072, Allocator.Persistent);
            try
            {
                combined = KentridgeCombinedVoxelCatalogue.Build(
                    Seed,
                    settings,
                    Allocator.Persistent);
                Assert.That(combined.IsCreated, Is.True);

                int scale = settings.VoxelsPerDecimetre;
                for (var buildingIndex = 0; buildingIndex < rossdam.Buildings.Count; buildingIndex++)
                {
                    TopDownWorldBuildingBlockoutPlan expected = rossdam.Buildings[buildingIndex];
                    Assert.That(
                        TryFindStructureAtCentre(
                            in combined,
                            expected.CentreDm.X * scale,
                            expected.CentreDm.Y * scale,
                            out PlacementRule rule,
                            out FeatureDefinition definition,
                            out ExplicitPlacement placement,
                            out int3 footprint),
                        Is.True,
                        $"Rossdam building {buildingIndex} at semantic centre {expected.CentreDm} must map to one production structure placement.");

                    GenerateIntersectingRegions(
                        in combined,
                        Seed,
                        placement.Position,
                        footprint,
                        ref table,
                        in pool);

                    int structureVoxels = 0;
                    int occupiedMinY = int.MaxValue;
                    int occupiedMaxY = int.MinValue;
                    int3 maxExclusive = placement.Position + footprint;
                    for (int y = placement.Position.y; y < maxExclusive.y; y++)
                    {
                        for (int z = placement.Position.z; z < maxExclusive.z; z++)
                        {
                            for (int x = placement.Position.x; x < maxExclusive.x; x++)
                            {
                                VoxelCell cell = VoxelAccess.GetCell(
                                    ref table,
                                    in pool,
                                    new int3(x, y, z));
                                if (!cell.IsSolid || !IsMacroStructureMaterial(cell.BaseMaterialId))
                                    continue;

                                structureVoxels++;
                                occupiedMinY = math.min(occupiedMinY, y);
                                occupiedMaxY = math.max(occupiedMaxY, y);
                            }
                        }
                    }

                    Assert.That(
                        structureVoxels,
                        Is.GreaterThan(0),
                        $"Rossdam building {buildingIndex} has no authoritative foundation/timber/roof voxels in its production footprint.");
                    Assert.That(
                        occupiedMinY,
                        Is.EqualTo(placement.Position.y),
                        $"Rossdam building {buildingIndex} must begin at its production minimum sampled ground; a different minimum indicates floating/buried/displaced voxelization.");
                    int occupiedSpan = occupiedMaxY - occupiedMinY + 1;
                    Assert.That(
                        occupiedSpan,
                        Is.GreaterThanOrEqualTo(expected.HeightDm * scale),
                        $"Rossdam building {buildingIndex} must retain meaningful above-ground vertical occupancy through its authored wall height.");

                    TestContext.WriteLine(
                        "KENTRIDGE_ROSSDAM_AUTHORITATIVE " +
                        $"building={buildingIndex} semanticCentreDm={expected.CentreDm} expectedHeightDm={expected.HeightDm} " +
                        $"definition={definition.Name} ruleDefinition={rule.DefinitionId} placement={placement.Position} footprint={footprint} " +
                        $"structureVoxels={structureVoxels} occupiedY={occupiedMinY}..{occupiedMaxY} span={occupiedSpan}");
                }
            }
            finally
            {
                pool.Dispose();
                table.Dispose();
                if (combined.IsCreated) combined.Dispose();
            }
        }

        private static bool TryFindStructureAtCentre(
            in FeatureCatalogue catalogue,
            int expectedCentreX,
            int expectedCentreZ,
            out PlacementRule selectedRule,
            out FeatureDefinition selectedDefinition,
            out ExplicitPlacement selectedPlacement,
            out int3 selectedFootprint)
        {
            selectedRule = default;
            selectedDefinition = default;
            selectedPlacement = default;
            selectedFootprint = default;
            int matches = 0;

            for (var ruleIndex = 0; ruleIndex < catalogue.Rules.Length; ruleIndex++)
            {
                PlacementRule rule = catalogue.Rules[ruleIndex];
                if ((uint)rule.DefinitionId >= (uint)catalogue.Definitions.Length
                    || rule.ExplicitCount != 1
                    || rule.ExplicitOffset < 0
                    || rule.ExplicitOffset >= catalogue.ExplicitPlacements.Length)
                    continue;

                FeatureDefinition definition = catalogue.Definitions[rule.DefinitionId];
                if (definition.Kind != FeatureKind.Structure) continue;

                ExplicitPlacement placement = catalogue.ExplicitPlacements[rule.ExplicitOffset];
                int3 footprint = definition.Footprint;
                if ((placement.Orientation & 1) != 0)
                    footprint = new int3(footprint.z, footprint.y, footprint.x);

                int centreX = placement.Position.x + footprint.x / 2;
                int centreZ = placement.Position.z + footprint.z / 2;
                if (centreX != expectedCentreX || centreZ != expectedCentreZ) continue;

                selectedRule = rule;
                selectedDefinition = definition;
                selectedPlacement = placement;
                selectedFootprint = footprint;
                matches++;
            }

            Assert.That(
                matches,
                Is.LessThanOrEqualTo(1),
                $"Semantic building centre ({expectedCentreX},{expectedCentreZ}) must map unambiguously to one production structure placement.");
            return matches == 1;
        }

        private static void GenerateIntersectingRegions(
            in FeatureCatalogue catalogue,
            uint seed,
            int3 minInclusive,
            int3 footprint,
            ref RegionTable table,
            in BrickPool pool)
        {
            int3 maxInclusive = minInclusive + footprint - 1;
            int minRegionX = minInclusive.x >> VoxelGrid.RegionVoxelEdgeLog2;
            int minRegionY = minInclusive.y >> VoxelGrid.RegionVoxelEdgeLog2;
            int minRegionZ = minInclusive.z >> VoxelGrid.RegionVoxelEdgeLog2;
            int maxRegionX = maxInclusive.x >> VoxelGrid.RegionVoxelEdgeLog2;
            int maxRegionY = maxInclusive.y >> VoxelGrid.RegionVoxelEdgeLog2;
            int maxRegionZ = maxInclusive.z >> VoxelGrid.RegionVoxelEdgeLog2;

            var reads = new RegionReadSource(in table, in pool);
            var mutations = new RegionMutationStore(in table, in pool);
            for (int ry = minRegionY; ry <= maxRegionY; ry++)
            {
                for (int rz = minRegionZ; rz <= maxRegionZ; rz++)
                {
                    for (int rx = minRegionX; rx <= maxRegionX; rx++)
                    {
                        FeatureGeneration.GenerateRegion(
                            in catalogue,
                            seed,
                            new int3(rx, ry, rz),
                            reads,
                            mutations);
                    }
                }
            }
        }

        private static bool IsMacroStructureMaterial(byte material) =>
            material == MacroFoundationMaterial
            || material == MacroTimberMaterial
            || material == MacroRoofMaterial;

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
                    foundationStone: MacroFoundationMaterial,
                    masonry: 18,
                    darkMasonry: 6,
                    timber: MacroTimberMaterial,
                    glass: 4,
                    warmWindow: 15,
                    roofTile: MacroRoofMaterial,
                    slate: 7,
                    cloth: 9,
                    moss: 14,
                    water: 11,
                    roadSurface: 13));
        }
    }
}

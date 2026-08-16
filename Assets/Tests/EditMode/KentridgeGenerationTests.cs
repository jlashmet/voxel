using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Structures.Runtime;

using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>Kentridge tests with no Showcase scene or ShowcaseWorld dependency.</summary>
    public sealed class KentridgeGenerationTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void CatalogueEvaluatesEveryPlannedFeatureWithoutShowcase()
        {
            SettlementPlan plan = KentridgeDefinition.Build(Seed);
            Assert.AreEqual(17, plan.Plots.Count);
            Assert.AreEqual(4, plan.Streets.Count);
            Assert.AreEqual("market-square", plan.Plaza.Id);

            FeatureCatalogue catalogue = KentridgeCombinedVoxelCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            var primitives = new NativeList<Primitive>(256, Allocator.Temp);
            var anchors = new NativeList<ResolvedAnchor>(8, Allocator.Temp);

            try
            {
                int instances = 0;
                int structures = 0;
                int primitiveCount = 0;

                for (int ruleIndex = 0; ruleIndex < catalogue.Rules.Length; ruleIndex++)
                {
                    PlacementRule rule = catalogue.Rules[ruleIndex];
                    FeatureDefinition definition = catalogue.Definitions[rule.DefinitionId];

                    for (int i = 0; i < rule.ExplicitCount; i++)
                    {
                        ExplicitPlacement placement =
                            catalogue.ExplicitPlacements[rule.ExplicitOffset + i];
                        primitives.Clear();
                        anchors.Clear();

                        ParameterSet parameters = FeatureGeneration.ResolveParameters(
                            in catalogue, in definition, in placement, rule.DefinitionId,
                            placement.Position, Seed);
                        ulong instanceSeed = FeatureGeneration.InstanceSeed(
                            Seed, rule.DefinitionId, placement.Position);

                        EvaluationResult result = ShapeProgram.Evaluate(
                            in catalogue, rule.DefinitionId, in parameters,
                            placement.Position, placement.Orientation,
                            Seed, instanceSeed, primitives, anchors);

                        Assert.AreEqual(EvaluationResult.Ok, result,
                            $"{definition.Name} failed at {placement.Position}");
                        Assert.Greater(primitives.Length, 0,
                            $"{definition.Name} emitted no geometry");

                        instances++;
                        primitiveCount += primitives.Length;
                        if (definition.Kind == FeatureKind.Structure) structures++;
                    }
                }

                Assert.AreEqual(17, structures,
                    "Every stable Kentridge building role should compile once.");
                Assert.Greater(instances, structures,
                    "District terraces, roads, foundation skirts, paths, and dressing should accompany buildings.");
                Assert.Greater(primitiveCount, 100,
                    "Kentridge emitted implausibly little geometry.");
            }
            finally
            {
                primitives.Dispose();
                anchors.Dispose();
                catalogue.Dispose();
            }
        }

        [Test]
        public void MacroVerticalProfileCreatesARealTownClimb()
        {
            const int scale = 1;
            int lower = KentridgeVerticalProfile.SurfaceYAtDm(
                KentridgeTownPlanner.MainSpineXDm, 950, Seed, scale);
            int market = KentridgeVerticalProfile.SurfaceYAtDm(
                KentridgeDefinition.TownCentreDm.X,
                KentridgeDefinition.TownCentreDm.Y,
                Seed, scale);
            int summit = KentridgeVerticalProfile.SurfaceYAtDm(
                KentridgeTownPlanner.MainSpineXDm, 150, Seed, scale);

            Assert.Greater(market - lower, 40,
                "The market should sit visibly above the lower residential tier.");
            Assert.Greater(summit - market, 90,
                "The civic summit should dominate the market terrace by many metres.");
            Assert.Greater(summit - lower, 150,
                "Kentridge needs macro verticality, not decorative height variation.");
        }

        [Test]
        public void DistrictTerracesCreateNeighbourhoodScaleShelves()
        {
            FeatureCatalogue terraces = KentridgeDistrictTerraceCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);

            try
            {
                Assert.AreEqual(14, terraces.Definitions.Length,
                    "The hillside plan should expose fourteen authored semantic shelf pieces.");
                Assert.AreEqual(14, terraces.ExplicitPlacements.Length);

                // The catalogue carries two kinds: the shelves themselves are Landform, and the
                // densified frontage pieces that sit on them are Infrastructure. Requiring every
                // definition to be Landform assumed the pre-densification catalogue, where the
                // five Infrastructure pieces did not exist yet — KentridgeInfrastructureTests
                // asserts that exact split.
                int landformShelves = 0;
                int infrastructurePieces = 0;
                int broadShelves = 0;
                int tallestFootprint = 0;
                for (int i = 0; i < terraces.Definitions.Length; i++)
                {
                    FeatureDefinition definition = terraces.Definitions[i];
                    if (definition.Kind == FeatureKind.Landform)
                    {
                        landformShelves++;
                        Assert.AreEqual(15, definition.Precedence,
                            "District terrain must run before roads and parcel grading.");
                    }
                    else if (definition.Kind == FeatureKind.Infrastructure)
                    {
                        infrastructurePieces++;
                        Assert.AreEqual(18, definition.Precedence,
                            "Frontage pieces grade after the shelves they sit on, "
                          + "and still before roads and parcel grading.");
                    }
                    else
                    {
                        Assert.Fail($"Unexpected terrace kind {definition.Kind}.");
                    }
                    if (definition.Footprint.x >= 300)
                        broadShelves++;
                    if (definition.Footprint.y > tallestFootprint)
                        tallestFootprint = definition.Footprint.y;
                }

                Assert.AreEqual(9, landformShelves, "The authored semantic shelves are Landform.");
                Assert.AreEqual(5, infrastructurePieces,
                    "Densified frontage pieces ride on the shelves as Infrastructure.");

                Assert.GreaterOrEqual(broadShelves, 6,
                    "Most terrace pieces should join multiple structures or public spaces.");
                Assert.Greater(tallestFootprint, 100,
                    "Upper shelves should have enough vertical extent to cut/fill the hillside.");
            }
            finally
            {
                terraces.Dispose();
            }
        }

        [Test]
        public void EverySemanticPlotIsSupportedByASharedDistrictShelf()
        {
            SettlementPlan plan = KentridgeDefinition.Build(Seed);
            FeatureCatalogue terraces = KentridgeDistrictTerraceCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);

            try
            {
                for (int p = 0; p < plan.Plots.Count; p++)
                {
                    BuildingPlot plot = plan.Plots[p];
                    Int3 footprint = KentridgeDefinition.FootprintDm(plot.Archetype);
                    int plotMinX = plot.PositionDm.X;
                    int plotMaxX = plot.PositionDm.X + footprint.X;
                    int plotMinZ = plot.PositionDm.Y;
                    int plotMaxZ = plot.PositionDm.Y + footprint.Z;
                    bool covered = false;

                    for (int i = 0; i < terraces.Definitions.Length; i++)
                    {
                        ExplicitPlacement placement = terraces.ExplicitPlacements[i];
                        FeatureDefinition definition = terraces.Definitions[i];
                        int terraceMinX = placement.Position.x;
                        int terraceMaxX = terraceMinX + definition.Footprint.x;
                        int terraceMinZ = placement.Position.z;
                        int terraceMaxZ = terraceMinZ + definition.Footprint.z;

                        if (plotMinX >= terraceMinX && plotMaxX <= terraceMaxX
                            && plotMinZ >= terraceMinZ && plotMaxZ <= terraceMaxZ)
                        {
                            covered = true;
                            break;
                        }
                    }

                    Assert.IsTrue(covered,
                        $"Kentridge role {plot.RoleId} is not fully supported by a district shelf.");
                }
            }
            finally
            {
                terraces.Dispose();
            }
        }

        [Test]
        public void ParcelSupportsAreShallowFoundationSkirtsNotTerrainColumns()
        {
            FeatureCatalogue supports = KentridgeTerraceSupportCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);

            try
            {
                Assert.AreEqual(16, supports.Definitions.Length,
                    "Every non-well building should retain a shallow foundation collar.");
                Assert.AreEqual(16, supports.ExplicitPlacements.Length);

                for (int i = 0; i < supports.Definitions.Length; i++)
                {
                    FeatureDefinition definition = supports.Definitions[i];
                    Assert.AreEqual(24, definition.Footprint.y,
                        "Parcel support must stay shallow now that district terraces own the hillside mass.");
                    StringAssert.StartsWith("kentridge-foundation-skirt-", definition.Name.ToString());
                }
            }
            finally
            {
                supports.Dispose();
            }
        }

        [Test]
        public void MarketDressingHasStablePropVocabularyAndPlacements()
        {
            FeatureCatalogue dressing = KentridgeTownDressingCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);

            try
            {
                Assert.AreEqual(4, dressing.Definitions.Length,
                    "Market dressing should compile four reusable prop definitions.");
                Assert.AreEqual(20, dressing.ExplicitPlacements.Length,
                    "The first market-square dressing pass should place twenty props.");

                int explicitCount = 0;
                for (int i = 0; i < dressing.Rules.Length; i++)
                    explicitCount += dressing.Rules[i].ExplicitCount;

                Assert.AreEqual(20, explicitCount,
                    "Every market dressing placement should be reachable by a rule.");
            }
            finally
            {
                dressing.Dispose();
            }
        }

        [Test]
        public void StreetscapeDressingMarksTheClimbWithoutInflatingBuildingCount()
        {
            FeatureCatalogue dressing = KentridgeStreetDressingCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);

            try
            {
                Assert.AreEqual(3, dressing.Definitions.Length,
                    "Street furnishing should reuse lamp, bench, and planter definitions.");
                Assert.AreEqual(30, dressing.ExplicitPlacements.Length,
                    "The streetscape pass should remain sparse and deliberately authored.");

                int explicitCount = 0;
                for (int i = 0; i < dressing.Rules.Length; i++)
                {
                    explicitCount += dressing.Rules[i].ExplicitCount;
                    Assert.AreEqual(FeatureKind.Landform,
                        dressing.Definitions[dressing.Rules[i].DefinitionId].Kind,
                        "Street furniture must not become semantic buildings.");
                }

                Assert.AreEqual(30, explicitCount);
                Assert.AreEqual(24, dressing.Rules[0].ExplicitCount,
                    "Most street furniture should be lamps that reveal the vertical road rhythm.");
                Assert.AreEqual(3, dressing.Rules[1].ExplicitCount);
                Assert.AreEqual(3, dressing.Rules[2].ExplicitCount);
            }
            finally
            {
                dressing.Dispose();
            }
        }

        [Test]
        public void PlotDressingFollowsDistrictsAndFrontages()
        {
            FeatureCatalogue dressing = KentridgePlotDressingCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);

            try
            {
                Assert.AreEqual(7, dressing.Definitions.Length,
                    "Plot dressing should reuse a compact seven-prop vocabulary.");
                Assert.AreEqual(56, dressing.ExplicitPlacements.Length,
                    "Every non-well plot should receive its district-specific dressing set.");

                int explicitCount = 0;
                int horizontalFenceCount = dressing.Rules[0].ExplicitCount;
                int verticalFenceCount = dressing.Rules[1].ExplicitCount;

                for (int i = 0; i < dressing.Rules.Length; i++)
                {
                    explicitCount += dressing.Rules[i].ExplicitCount;
                    Assert.AreEqual(FeatureKind.Landform,
                        dressing.Definitions[dressing.Rules[i].DefinitionId].Kind,
                        "Dressing must not inflate the semantic building count.");
                }

                Assert.AreEqual(56, explicitCount,
                    "Every plot-dressing placement should be reachable by a rule.");
                Assert.Greater(horizontalFenceCount, 0,
                    "Frontage rotation should produce horizontal fence segments.");
                Assert.Greater(verticalFenceCount, 0,
                    "Frontage rotation should produce vertical fence segments.");
            }
            finally
            {
                dressing.Dispose();
            }
        }

        [Test]
        public void PlanIsDeterministicForSameSeed()
        {
            SettlementPlan a = KentridgeDefinition.Build(Seed);
            SettlementPlan b = KentridgeDefinition.Build(Seed);

            Assert.AreEqual(a.Plots.Count, b.Plots.Count);
            for (int i = 0; i < a.Plots.Count; i++)
            {
                BuildingPlot left = a.Plots[i];
                BuildingPlot right = b.Plots[i];
                Assert.AreEqual(left.RoleId, right.RoleId);
                Assert.AreEqual(left.Archetype, right.Archetype);
                Assert.AreEqual(left.District, right.District);
                Assert.AreEqual(left.Frontage, right.Frontage);
                Assert.AreEqual(left.PositionDm.X, right.PositionDm.X);
                Assert.AreEqual(left.PositionDm.Y, right.PositionDm.Y);
            }
        }

        private static VoxelWorldGenSettings BuildSettings()
        {
            var materials = new VoxelMaterialMap(
                foundationStone: 1, masonry: 1, darkMasonry: 6,
                timber: 2, glass: 4, warmWindow: 15,
                roofTile: 8, slate: 7, cloth: 9,
                moss: 14, water: 11, roadSurface: 13);
            return new VoxelWorldGenSettings(1, materials);
        }
    }
}

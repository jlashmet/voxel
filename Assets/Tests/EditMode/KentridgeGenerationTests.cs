using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Core.Features;

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
                    "Roads, terrace supports, paths, and dressing should accompany the buildings.");
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
        public void RaisedPlotsReceiveVisibleMasonryTerraceSupport()
        {
            FeatureCatalogue supports = KentridgeTerraceSupportCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);

            try
            {
                Assert.AreEqual(16, supports.Definitions.Length,
                    "Every non-well parcel should have an independent retaining support.");
                Assert.AreEqual(16, supports.ExplicitPlacements.Length);

                int tallest = 0;
                int shallowest = int.MaxValue;
                for (int i = 0; i < supports.Definitions.Length; i++)
                {
                    int height = supports.Definitions[i].Footprint.y;
                    if (height > tallest) tallest = height;
                    if (height < shallowest) shallowest = height;
                }

                Assert.Greater(tallest, 100,
                    "At least one upper parcel should expose a retaining face over ten metres tall.");
                Assert.Less(shallowest, 30,
                    "Lower parcels should remain close to the underlying terrain.");
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

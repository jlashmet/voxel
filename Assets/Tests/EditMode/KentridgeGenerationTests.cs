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
                    "Road and plaza instances should accompany the buildings.");
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

using System;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeRoadShoulderRegressionTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void WorldBuilderRoadsLowerAsBoundedContinuousTerrainCorridors()
        {
            VoxelWorldGenSettings settings = BuildSettings();
            SettlementPlan plan = KentridgeDefinition.Build(Seed);
            WorldRoadNetwork network = KentridgeWorldRoadNetwork.Build(plan, Seed, settings);
            FeatureCatalogue roads = KentridgeDirectedTownSurfaceCatalogue.Build(
                Seed, settings, Allocator.Temp);

            try
            {
                Assert.AreEqual(plan.Routes.Count, network.Routes.Count,
                    "Modern Kentridge routes must remain the semantic road source of truth.");
                Assert.GreaterOrEqual(roads.Definitions.Length, network.Routes.Count,
                    "Long resolved segments may split only to satisfy the bounded feature footprint.");
                Assert.LessOrEqual(roads.Definitions.Length, FeatureBudget.MaxDefinitions);

                for (int i = 0; i < roads.Definitions.Length; i++)
                {
                    FeatureDefinition definition = roads.Definitions[i];
                    StringAssert.StartsWith("world-road-", definition.Name.ToString());
                    Assert.That(math.cmax(definition.Footprint),
                        Is.LessThanOrEqualTo(FeatureBudget.MaxFootprintVoxels));
                    Assert.AreEqual(1, definition.MaxPrimitives,
                        definition.Name + " should lower through one analytic corridor, not stamp stacks.");

                    int corridorOps = 0;
                    int legacyBoxOps = 0;
                    int pc = definition.ProgramOffset;
                    int end = pc + definition.ProgramLength;
                    while (pc < end)
                    {
                        ShapeOp op = (ShapeOp)roads.Program[pc];
                        if (op == ShapeOp.EmitTerrainCorridor) corridorOps++;
                        if (op == ShapeOp.EmitBox) legacyBoxOps++;
                        pc += ShapeOps.InstructionLength(op);
                        if (op == ShapeOp.End) break;
                    }

                    Assert.AreEqual(1, corridorOps,
                        definition.Name + " must contain exactly one generic terrain corridor.");
                    Assert.AreEqual(0, legacyBoxOps,
                        definition.Name + " must not restore overlapping road/shoulder box stamps.");
                }

                AssertPhysicalInfluenceMatchesSemanticRoad(roads, network.Routes[0], settings);
            }
            finally
            {
                roads.Dispose();
            }
        }

        [Test]
        public void ShoulderInfluenceRecoversContinuouslyIntoClearance()
        {
            VoxelWorldGenSettings settings = BuildSettings();
            SettlementPlan plan = KentridgeDefinition.Build(Seed);
            WorldRoadNetwork network = KentridgeWorldRoadNetwork.Build(plan, Seed, settings);
            WorldRoadNetworkRoute route = network.Routes[0];
            ResolvedWorldRoadPoint point = route.Road.Points[0];

            int previousCoverage = 32;
            bool observedTransition = false;
            for (int offset = 0; offset <= route.GradeRadiusDm; offset++)
            {
                Assert.IsTrue(network.TrySample(
                    point.Xdm + offset,
                    point.Zdm,
                    out WorldRoadNetworkSample sample));
                Assert.LessOrEqual(sample.Influence.Coverage31, previousCoverage,
                    "Road influence must recover monotonically toward local terrain.");
                if (sample.Influence.Coverage31 > 0 && sample.Influence.Coverage31 < 31)
                    observedTransition = true;
                previousCoverage = sample.Influence.Coverage31;
            }

            Assert.IsTrue(observedTransition,
                "The shoulder needs continuous intermediate coverage, not only hard core/outside states.");
            Assert.IsTrue(network.TrySampleClearance(
                point.Xdm + route.ClearanceRadiusDm,
                point.Zdm,
                out WorldRoadNetworkSample clearance));
            Assert.Greater(clearance.ClearanceCoverage31, 0,
                "Vegetation/placement clearance must extend through the authored road corridor.");
        }

        private static void AssertPhysicalInfluenceMatchesSemanticRoad(
            FeatureCatalogue roads,
            WorldRoadNetworkRoute route,
            VoxelWorldGenSettings settings)
        {
            int matchingDefinition = -1;
            Primitive corridor = default;

            for (int i = 0; i < roads.Definitions.Length; i++)
            {
                ExplicitPlacement placement = roads.ExplicitPlacements[i];
                using var primitives = new NativeList<Primitive>(Allocator.Temp);
                using var anchors = new NativeList<ResolvedAnchor>(Allocator.Temp);
                ParameterSet parameters = default;
                EvaluationResult evaluation = ShapeProgram.Evaluate(
                    in roads,
                    i,
                    in parameters,
                    placement.Position,
                    placement.Orientation,
                    Seed,
                    route.Road.Intent.Seed,
                    primitives,
                    anchors);
                Assert.AreEqual(EvaluationResult.Ok, evaluation);
                Assert.AreEqual(1, primitives.Length);

                Primitive candidate = primitives[0];
                if (candidate.Shape != PrimitiveShape.TerrainCorridor
                    || unchecked((uint)candidate.D.y) != route.Road.Intent.Seed)
                    continue;
                matchingDefinition = i;
                corridor = candidate;
                break;
            }

            Assert.GreaterOrEqual(matchingDefinition, 0,
                "A semantic Kentridge road must remain traceable to its physical corridor primitive.");

            int scale = settings.VoxelsPerDecimetre;
            int midpointX = DivideRounded(corridor.A.x + corridor.B.x, scale * 2);
            int midpointZ = DivideRounded(corridor.A.z + corridor.B.z, scale * 2);
            var semantic = new WorldRoadInfluence(route.Road);

            bool matchedIntermediateCoverage = false;
            for (int offset = 0; offset <= route.GradeRadiusDm; offset++)
            {
                int xdm = midpointX + offset;
                int zdm = midpointZ;
                if (!semantic.TrySample(xdm, zdm, out WorldRoadInfluenceSample semanticSample))
                    continue;
                if (!TerrainCorridorRasteriser.TrySample(
                        in corridor,
                        xdm * scale,
                        zdm * scale,
                        out TerrainCorridorSample physicalSample))
                    continue;
                if (semanticSample.Coverage31 == 0 || semanticSample.Coverage31 == 31)
                    continue;

                Assert.AreEqual(semanticSample.DistanceDm, physicalSample.DistanceDm);
                Assert.AreEqual(semanticSample.TargetHeightDm * scale,
                    physicalSample.TargetHeightVoxels);
                Assert.AreEqual(semanticSample.Coverage31, physicalSample.Coverage31,
                    "Terrain grading/material coverage must consume the same 0..31 semantic influence.");
                matchedIntermediateCoverage = true;
                break;
            }

            Assert.IsTrue(matchedIntermediateCoverage,
                "Regression needs an intermediate shoulder sample, not only full/zero coverage.");
        }

        private static int DivideRounded(int numerator, int denominator)
        {
            if (numerator >= 0) return (numerator + denominator / 2) / denominator;
            return -((-numerator + denominator / 2) / denominator);
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

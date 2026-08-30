using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
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
            var profile = new WorldRoadProfile(
                "isolated-shoulder-regression",
                "road-surface",
                carriagewayWidthDm: 20,
                transitionWidthDm: 12,
                maximumGradePermille: 220,
                maximumCutFillDm: 12,
                edgeVariationDm: 2,
                vegetationSuppressionPermille: 1000,
                traversalCostPermille: 1000,
                crossingPolicy: WorldRoadCrossingPolicy.AllowPass);
            var intent = new WorldRoadIntent(
                "isolated:west-east",
                "west",
                "east",
                Seed,
                profile,
                "isolated shoulder continuity regression",
                new[]
                {
                    new WorldRoadPlanPoint(0, 0),
                    new WorldRoadPlanPoint(200, 0),
                });
            var terrain = new ResolverFixtureTerrain(
                (x, z) => 0,
                (x, z) => WorldRoadTerrainFlags.None);
            ResolvedWorldRoad resolved = WorldRoadResolver.Resolve(
                intent, terrain, sampleSpacingDm: 40, searchMarginCells: 0);
            Assert.AreEqual(WorldRoadResolutionStatus.Resolved, resolved.Status, resolved.FailureReason);

            var route = new WorldRoadNetworkRoute(
                resolved,
                WorldRoadSemanticClass.Vehicle,
                shoulderWidthDm: 6,
                clearanceWidthDm: 10);
            var network = new WorldRoadNetwork(new[] { route });

            const int midpointX = 100;
            int previousCoverage = 32;
            bool observedTransition = false;
            bool observedOutside = false;
            for (int offset = 0; offset <= route.GradeRadiusDm; offset++)
            {
                if (!network.TrySample(midpointX, offset, out WorldRoadNetworkSample sample))
                {
                    observedOutside = true;
                    break;
                }

                Assert.LessOrEqual(sample.Influence.Coverage31, previousCoverage,
                    "An isolated road shoulder must recover monotonically toward local terrain.");
                if (sample.Influence.Coverage31 > 0 && sample.Influence.Coverage31 < 31)
                    observedTransition = true;
                previousCoverage = sample.Influence.Coverage31;
            }

            Assert.IsTrue(observedTransition,
                "The shoulder needs continuous intermediate coverage, not only hard core/outside states.");
            Assert.IsTrue(observedOutside || previousCoverage < 31,
                "The isolated cross-section must leave the full-strength road core.");
            Assert.IsTrue(network.TrySampleClearance(
                midpointX,
                route.ClearanceRadiusDm,
                out WorldRoadNetworkSample clearance));
            Assert.Greater(clearance.ClearanceCoverage31, 0,
                "Vegetation/placement clearance must extend through the authored road corridor.");
        }

        [Test]
        public void VegetationSuppressionUsesSharedInfluenceAndRecoversThroughShoulder()
        {
            VoxelWorldGenSettings settings = BuildSettings();
            SettlementPlan plan = KentridgeDefinition.Build(Seed);
            WorldRoadNetwork network = KentridgeWorldRoadNetwork.Build(plan, Seed, settings);
            WorldRoadNetworkRoute route = network.Routes[0];
            ResolvedWorldRoadPoint point = route.Road.Points[0];

            int strongestShoulder = 0;
            int weakestShoulder = 32;
            for (int offset = 0; offset <= route.GradeRadiusDm; offset++)
            {
                Assert.IsTrue(network.TrySample(
                    point.Xdm + offset,
                    point.Zdm,
                    out WorldRoadNetworkSample sample));
                int suppression = sample.Influence.VegetationSuppression31;
                if (suppression <= 0 || suppression >= 31) continue;
                strongestShoulder = Math.Max(strongestShoulder, suppression);
                weakestShoulder = Math.Min(weakestShoulder, suppression);
            }

            Assert.That(strongestShoulder, Is.GreaterThan(weakestShoulder),
                "The shared road influence must expose more than one intermediate ecology state.");

            const int population = 3100;
            int coreSuppressed = CountSuppressed(31, population);
            int innerShoulderSuppressed = CountSuppressed((byte)strongestShoulder, population);
            int outerShoulderSuppressed = CountSuppressed((byte)weakestShoulder, population);
            int localTerrainSuppressed = CountSuppressed(0, population);

            Assert.AreEqual(population, coreSuppressed,
                "Full road influence must suppress incompatible core vegetation.");
            Assert.That(innerShoulderSuppressed, Is.LessThan(coreSuppressed));
            Assert.That(innerShoulderSuppressed, Is.GreaterThan(outerShoulderSuppressed),
                "Vegetation density must recover as the same road influence falls through the shoulder.");
            Assert.That(outerShoulderSuppressed, Is.GreaterThan(localTerrainSuppressed));
            Assert.AreEqual(0, localTerrainSuppressed,
                "Road ecology influence must not suppress the regional ecology outside its footprint.");
        }

        [Test]
        public void KentridgeVegetationPlannerConsumesInfluenceRatherThanBinaryClearance()
        {
            VoxelWorldGenSettings settings = BuildSettings();
            SettlementPlan plan = KentridgeDefinition.Build(Seed);
            var candidates = KentridgeVegetationLayoutPlanner.Build(plan);
            WorldRoadNetwork network = KentridgeWorldRoadNetwork.Build(plan, Seed, settings);

            int expectedSurvivors = 0;
            int legacyClearanceSurvivors = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                VegetationCandidate candidate = candidates[i];
                bool suppressed = network.TrySample(
                        candidate.X, candidate.Z, out WorldRoadNetworkSample sample)
                    && WorldRoadVegetationSuppression.ShouldSuppress(
                        sample.Influence.VegetationSuppression31,
                        Seed,
                        candidate.X,
                        candidate.Z,
                        candidate.Ordinal);
                if (!suppressed) expectedSurvivors++;
                if (!network.TrySampleClearance(candidate.X, candidate.Z, out _))
                    legacyClearanceSurvivors++;
            }

            Assert.AreNotEqual(legacyClearanceSurvivors, expectedSurvivors,
                "This fixture must discriminate shared shoulder recovery from the legacy hard-clearance behavior.");
            Assert.IsTrue(KentridgeVegetationPlanner.TryBuild(
                Seed, settings, new AlwaysSolidSurfaceQuery(), out var instances));
            Assert.AreEqual(expectedSurvivors, instances.Count,
                "Production Kentridge vegetation must consume the exact shared road suppression scalar.");
        }

        [Test]
        public void GenericResolverIsDeterministicAndHonorsGradeAndCutFillLimits()
        {
            var profile = new WorldRoadProfile(
                "regression-road",
                "road-surface",
                carriagewayWidthDm: 20,
                transitionWidthDm: 12,
                maximumGradePermille: 220,
                maximumCutFillDm: 12,
                edgeVariationDm: 0,
                vegetationSuppressionPermille: 1000,
                traversalCostPermille: 1000,
                crossingPolicy: WorldRoadCrossingPolicy.AllowPass);
            var intent = new WorldRoadIntent(
                "regression:a-b",
                "a",
                "b",
                Seed,
                profile,
                "generic resolver regression",
                new[]
                {
                    new WorldRoadPlanPoint(0, 0),
                    new WorldRoadPlanPoint(160, 0),
                });
            var terrain = new ResolverFixtureTerrain(
                (x, z) => x / 8 + Math.Abs(z) / 20,
                (x, z) => x == 80 && z == 0 ? WorldRoadTerrainFlags.Blocked : WorldRoadTerrainFlags.None);

            ResolvedWorldRoad first = WorldRoadResolver.Resolve(intent, terrain, 40, 3);
            ResolvedWorldRoad second = WorldRoadResolver.Resolve(intent, terrain, 40, 3);
            Assert.AreEqual(WorldRoadResolutionStatus.Resolved, first.Status, first.FailureReason);
            Assert.AreEqual(first.Status, second.Status);
            Assert.AreEqual(first.Points.Count, second.Points.Count);
            Assert.Greater(first.Points.Count, 2,
                "Blocked direct cell should force a deterministic detour rather than a synthetic straight line.");

            for (int i = 0; i < first.Points.Count; i++)
            {
                Assert.AreEqual(first.Points[i], second.Points[i],
                    "Fixed road intent/seed/terrain must resolve stable geometry.");
                int terrainHeight = terrain.HeightAtDm(first.Points[i].Xdm, first.Points[i].Zdm);
                Assert.LessOrEqual(Math.Abs(first.Points[i].Ydm - terrainHeight), profile.MaximumCutFillDm,
                    "Resolved road exceeded its authored cut/fill envelope.");
                if (i == 0) continue;
                int dx = first.Points[i].Xdm - first.Points[i - 1].Xdm;
                int dz = first.Points[i].Zdm - first.Points[i - 1].Zdm;
                int run = Math.Max(1, IntegerSqrt((long)dx * dx + (long)dz * dz));
                int rise = Math.Abs(first.Points[i].Ydm - first.Points[i - 1].Ydm);
                Assert.LessOrEqual((long)rise * 1000L, (long)profile.MaximumGradePermille * run,
                    "Resolved road exceeded its authored maximum grade.");
            }
        }

        [Test]
        public void GenericResolverRejectsWaterBarrierUnlessCrossingPolicyAllowsIt()
        {
            var terrain = new ResolverFixtureTerrain(
                (x, z) => 0,
                (x, z) => x == 80 ? WorldRoadTerrainFlags.Water : WorldRoadTerrainFlags.None);
            WorldRoadIntent dryOnly = BarrierIntent(WorldRoadCrossingPolicy.AllowPass);
            WorldRoadIntent crossing = BarrierIntent(
                WorldRoadCrossingPolicy.AllowPass | WorldRoadCrossingPolicy.AllowWaterCrossing);

            ResolvedWorldRoad rejected = WorldRoadResolver.Resolve(dryOnly, terrain, 40, 2);
            ResolvedWorldRoad accepted = WorldRoadResolver.Resolve(crossing, terrain, 40, 2);

            Assert.AreEqual(WorldRoadResolutionStatus.Blocked, rejected.Status,
                "A full water barrier must not be silently crossed without authored crossing policy.");
            Assert.AreEqual(WorldRoadResolutionStatus.Resolved, accepted.Status, accepted.FailureReason);
        }

        [Test]
        public void KentridgeAuthorsReusablePlacementClearanceOnGenericNetworkRoutes()
        {
            VoxelWorldGenSettings settings = BuildSettings();
            SettlementPlan plan = KentridgeDefinition.Build(Seed);
            WorldRoadNetwork network = KentridgeWorldRoadNetwork.Build(plan, Seed, settings);
            Assert.Greater(network.Routes.Count, 0);

            WorldRoadNetworkRoute route = network.Routes[0];
            Assert.Greater(route.ClearanceWidthDm, 0,
                "Kentridge must author placement clearance on the generic route contract.");
            Assert.Greater(route.ClearanceRadiusDm, route.GradeRadiusDm,
                "Placement keep-clearance must extend beyond the physical grading shoulder.");

            ResolvedWorldRoadPoint point = route.Road.Points[0];
            Assert.IsTrue(network.TrySampleClearance(point.Xdm, point.Zdm, out WorldRoadNetworkSample sample));
            Assert.AreSame(route, sample.Route,
                "Kentridge placement consumers must query the generic aggregate, not duplicate a settlement-local distance field.");
            Assert.AreEqual(31, sample.ClearanceCoverage31);
        }

        private static WorldRoadIntent BarrierIntent(WorldRoadCrossingPolicy policy)
        {
            var profile = new WorldRoadProfile(
                "barrier-road-" + (int)policy,
                "road-surface",
                carriagewayWidthDm: 20,
                transitionWidthDm: 10,
                maximumGradePermille: 200,
                maximumCutFillDm: 8,
                edgeVariationDm: 0,
                vegetationSuppressionPermille: 1000,
                traversalCostPermille: 1000,
                crossingPolicy: policy);
            return new WorldRoadIntent(
                "barrier:a-b:" + (int)policy,
                "a",
                "b",
                Seed,
                profile,
                "barrier regression",
                new[]
                {
                    new WorldRoadPlanPoint(0, 0),
                    new WorldRoadPlanPoint(160, 0),
                });
        }

        private static int CountSuppressed(byte suppression31, int population)
        {
            int count = 0;
            for (int ordinal = 0; ordinal < population; ordinal++)
            {
                if (WorldRoadVegetationSuppression.ShouldSuppress(
                        suppression31, Seed, 1234, 5678, ordinal))
                    count++;
            }
            return count;
        }

        private static void AssertPhysicalInfluenceMatchesSemanticRoad(
            FeatureCatalogue roads,
            WorldRoadNetworkRoute route,
            VoxelWorldGenSettings settings)
        {
            var corridors = new List<Primitive>();

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
                if (candidate.Shape == PrimitiveShape.TerrainCorridor
                    && unchecked((uint)candidate.D.y) == route.Road.Intent.Seed)
                    corridors.Add(candidate);
            }

            Assert.Greater(corridors.Count, 0,
                "A semantic Kentridge road must remain traceable to its physical corridor pieces.");

            int scale = settings.VoxelsPerDecimetre;
            Primitive reference = corridors[0];
            int midpointX = DivideRounded(reference.A.x + reference.B.x, scale * 2);
            int midpointZ = DivideRounded(reference.A.z + reference.B.z, scale * 2);
            var semantic = new WorldRoadInfluence(route.Road);

            bool matchedIntermediateCoverage = false;
            int radius = route.GradeRadiusDm;
            for (int dz = -radius; dz <= radius && !matchedIntermediateCoverage; dz++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int xdm = midpointX + dx;
                    int zdm = midpointZ + dz;
                    if (!semantic.TrySample(xdm, zdm, out WorldRoadInfluenceSample semanticSample))
                        continue;
                    if (semanticSample.Coverage31 == 0 || semanticSample.Coverage31 == 31)
                        continue;
                    Assert.IsTrue(TrySamplePhysicalUnion(
                            corridors,
                            xdm * scale,
                            zdm * scale,
                            out TerrainCorridorSample physicalSample),
                        "Every nonzero semantic shoulder sample must exist in the lowered corridor union.");

                    Assert.AreEqual(semanticSample.DistanceDm, physicalSample.DistanceDm);
                    Assert.AreEqual(semanticSample.TargetHeightDm * scale,
                        physicalSample.TargetHeightVoxels);
                    Assert.AreEqual(semanticSample.Coverage31, physicalSample.Coverage31,
                        "Terrain grading/material coverage must consume the same 0..31 semantic influence union.");
                    matchedIntermediateCoverage = true;
                    break;
                }
            }

            Assert.IsTrue(matchedIntermediateCoverage,
                "Regression needs an intermediate shoulder sample, not only full/zero coverage.");
        }

        private static bool TrySamplePhysicalUnion(
            List<Primitive> corridors,
            int worldX,
            int worldZ,
            out TerrainCorridorSample sample)
        {
            bool found = false;
            TerrainCorridorSample best = default;
            for (int i = 0; i < corridors.Count; i++)
            {
                Primitive corridor = corridors[i];
                if (!TerrainCorridorRasteriser.TrySample(
                        in corridor, worldX, worldZ, out TerrainCorridorSample candidate))
                    continue;
                if (!found
                    || candidate.Coverage31 > best.Coverage31
                    || candidate.Coverage31 == best.Coverage31
                       && candidate.DistanceDm < best.DistanceDm)
                {
                    best = candidate;
                    found = true;
                }
            }

            sample = best;
            return found;
        }

        private static int DivideRounded(int numerator, int denominator)
        {
            if (numerator >= 0) return (numerator + denominator / 2) / denominator;
            return -((-numerator + denominator / 2) / denominator);
        }

        private static int IntegerSqrt(long value)
        {
            if (value <= 0) return 0;
            long low = 1;
            long high = Math.Min(value, 3037000499L);
            while (low <= high)
            {
                long middle = low + ((high - low) >> 1);
                if (middle <= value / middle) low = middle + 1;
                else high = middle - 1;
            }
            return high > int.MaxValue ? int.MaxValue : (int)high;
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

        private sealed class ResolverFixtureTerrain : IWorldRoadTerrain
        {
            private readonly Func<int, int, int> _height;
            private readonly Func<int, int, WorldRoadTerrainFlags> _flags;

            public ResolverFixtureTerrain(
                Func<int, int, int> height,
                Func<int, int, WorldRoadTerrainFlags> flags)
            {
                _height = height;
                _flags = flags;
            }

            public int HeightAtDm(int xdm, int zdm) => _height(xdm, zdm);
            public WorldRoadTerrainFlags FlagsAtDm(int xdm, int zdm) => _flags(xdm, zdm);
        }

        private sealed class AlwaysSolidSurfaceQuery : IVoxelSurfaceQuery
        {
            public bool TryRead(int3 worldVoxel, out VoxelCell cell)
            {
                cell = SolidCell();
                return true;
            }

            public bool TryFindTopSolid(
                int x,
                int z,
                int minY,
                int maxY,
                out int y,
                out VoxelCell cell)
            {
                y = minY;
                cell = SolidCell();
                return true;
            }

            public bool TryFindTopSolidExcluding(
                int x,
                int z,
                int minY,
                int maxY,
                byte excludedMaterialA,
                byte excludedMaterialB,
                out int y,
                out VoxelCell cell)
            {
                y = minY;
                cell = SolidCell();
                return true;
            }

            private static VoxelCell SolidCell() => new VoxelCell { BaseMaterialId = 1 };
        }
    }
}

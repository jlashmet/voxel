using System;
using Game.WorldBuilder.Api;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Structures.Api;

namespace Game.WorldBuilder.Voxel.Tests.EditMode
{
    public sealed class MountainRoadIntegrationTests
    {
        [Test]
        public void CompositeTerrainUsesMountainInsideAndFallbackOutside()
        {
            MountainLandformSpec spec = CreateGentleMountain();
            var surface = new MountainLandformSurface(in spec);
            var terrain = new MountainLandformRoadTerrain(surface, new FlatTerrain(7));

            Assert.That(terrain.HeightAtDm(spec.OriginXdm, spec.OriginZdm),
                Is.EqualTo(surface.HeightAtDm(spec.OriginXdm, spec.OriginZdm)));
            Assert.That(terrain.HeightAtDm(spec.OriginXdm + 1000, spec.OriginZdm), Is.EqualTo(7));
        }

        [Test]
        public void GenericResolverAcceptsLegalMountainAscentAndRejectsOverGradeProfile()
        {
            MountainLandformSpec spec = CreateGentleMountain();
            var surface = new MountainLandformSurface(in spec);
            var terrain = new MountainLandformRoadTerrain(surface, new FlatTerrain(spec.OriginYdm));
            var controls = new[]
            {
                new WorldRoadPlanPoint(spec.OriginXdm - 650, spec.OriginZdm),
                new WorldRoadPlanPoint(spec.OriginXdm, spec.OriginZdm),
            };

            var legalProfile = new WorldRoadProfile(
                "mountain-test-legal", "road-surface", 18, 12,
                maximumGradePermille: 260,
                maximumCutFillDm: 24,
                edgeVariationDm: 0);
            var legalIntent = new WorldRoadIntent(
                "mountain-test-legal-route", "base", "summit", 19u,
                legalProfile, "independent mountain-road fixture", controls);
            ResolvedWorldRoad legal = WorldRoadResolver.Resolve(
                legalIntent, terrain, sampleSpacingDm: 20, searchMarginCells: 2);

            Assert.That(legal.IsResolved, Is.True, legal.FailureReason);
            Assert.That(legal.Points.Count, Is.GreaterThanOrEqualTo(2),
                "A resolved route needs endpoints; point density is an implementation detail of the generic resolver.");
            AssertGradeBound(legal);
            for (int i = 0; i < legal.Points.Count; i++)
            {
                ResolvedWorldRoadPoint point = legal.Points[i];
                int terrainHeight = terrain.HeightAtDm(point.Xdm, point.Zdm);
                Assert.That(Math.Abs(point.Ydm - terrainHeight),
                    Is.LessThanOrEqualTo(legalProfile.MaximumCutFillDm),
                    $"point {i} exceeds the configured cut/fill contract");
            }

            var rejectedProfile = new WorldRoadProfile(
                "mountain-test-steep", "road-surface", 18, 12,
                maximumGradePermille: 55,
                maximumCutFillDm: 0,
                edgeVariationDm: 0);
            var rejectedIntent = new WorldRoadIntent(
                "mountain-test-steep-route", "base", "summit", 19u,
                rejectedProfile, "independent over-grade mountain-road fixture", controls);
            ResolvedWorldRoad rejected = WorldRoadResolver.Resolve(
                rejectedIntent, terrain, sampleSpacingDm: 20, searchMarginCells: 0);

            Assert.That(rejected.IsResolved, Is.False,
                "over-grade route must not be accepted by the generic resolver");
            Assert.That(rejected.Status,
                Is.EqualTo(WorldRoadResolutionStatus.Blocked)
                    .Or.EqualTo(WorldRoadResolutionStatus.GradeExceeded)
                    .Or.EqualTo(WorldRoadResolutionStatus.CutFillExceeded),
                "An over-constrained ascent may be rejected during corridor search or later grade/cut-fill validation.");
        }

        [Test]
        public void GameFacingLoweringUsesSharedTerrainCorridorPrimitive()
        {
            MountainLandformSpec spec = CreateGentleMountain();
            var surface = new MountainLandformSurface(in spec);
            var terrain = new MountainLandformRoadTerrain(surface, new FlatTerrain(spec.OriginYdm));
            var profile = new WorldRoadProfile(
                "mountain-test-lowering", "road-surface", 18, 12,
                maximumGradePermille: 260,
                maximumCutFillDm: 24,
                edgeVariationDm: 0);
            var intent = new WorldRoadIntent(
                "mountain-test-lowering-route", "base", "summit", 23u,
                profile, "independent lowering fixture",
                new[]
                {
                    new WorldRoadPlanPoint(spec.OriginXdm - 650, spec.OriginZdm),
                    new WorldRoadPlanPoint(spec.OriginXdm, spec.OriginZdm),
                });
            ResolvedWorldRoad road = WorldRoadResolver.Resolve(
                intent, terrain, sampleSpacingDm: 20, searchMarginCells: 2);
            Assert.That(road.IsResolved, Is.True, road.FailureReason);

            var network = new WorldRoadNetwork(new[]
            {
                new WorldRoadNetworkRoute(
                    road,
                    WorldRoadSemanticClass.Pedestrian,
                    shoulderWidthDm: 3,
                    clearanceWidthDm: 5),
            });
            FeatureCatalogue catalogue = WorldBuilderRoadVoxelCatalogue.Build(
                network, roadSurfaceMaterial: 13, allocator: Allocator.Temp);
            try
            {
                int corridorCount = 0;
                for (int definitionIndex = 0; definitionIndex < catalogue.Definitions.Length; definitionIndex++)
                {
                    FeatureDefinition definition = catalogue.Definitions[definitionIndex];
                    int end = definition.ProgramOffset + definition.ProgramLength;
                    for (int pc = definition.ProgramOffset; pc < end;)
                    {
                        ShapeOp op = (ShapeOp)catalogue.Program[pc];
                        if (op == ShapeOp.EmitTerrainCorridor) corridorCount++;
                        Assert.That(op, Is.Not.EqualTo(ShapeOp.EmitRamp),
                            "mountain-road lowering must not reintroduce custom ramp geometry");
                        int length = ShapeOps.InstructionLength(op);
                        Assert.That(length, Is.GreaterThan(0));
                        pc += length;
                        if (op == ShapeOp.End) break;
                    }
                }
                Assert.That(corridorCount, Is.GreaterThan(0));
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        private static MountainLandformSpec CreateGentleMountain() =>
            new MountainLandformSpec(
                originXdm: 100,
                originYdm: 20,
                originZdm: -60,
                radiusXdm: 700,
                radiusZdm: 700,
                heightDm: 100,
                summitRadiusDm: 120,
                macroShape: MountainMacroShape.Massif,
                summitCharacter: MountainSummitCharacter.Broad,
                seed: 0xBEEFu,
                ridgeCount: 0,
                ridgeStrengthPermille: 0,
                asymmetryXPermille: 0,
                asymmetryZPermille: 0,
                roughnessAmplitudeDm: 0,
                roughnessScaleDm: 80,
                erosionStrengthPermille: 0);

        private static void AssertGradeBound(ResolvedWorldRoad road)
        {
            for (int i = 1; i < road.Points.Count; i++)
            {
                ResolvedWorldRoadPoint a = road.Points[i - 1];
                ResolvedWorldRoadPoint b = road.Points[i];
                long dx = (long)b.Xdm - a.Xdm;
                long dz = (long)b.Zdm - a.Zdm;
                int horizontal = Math.Max(1, (int)Math.Sqrt(dx * dx + dz * dz));
                int rise = Math.Abs(b.Ydm - a.Ydm);
                Assert.That((long)rise * 1000L,
                    Is.LessThanOrEqualTo((long)horizontal * road.Intent.Profile.MaximumGradePermille),
                    $"segment {i - 1} exceeds resolver grade contract");
            }
        }

        private sealed class FlatTerrain : IWorldRoadTerrain
        {
            private readonly int _height;
            public FlatTerrain(int height) => _height = height;
            public int HeightAtDm(int xdm, int zdm) => _height;
            public WorldRoadTerrainFlags FlagsAtDm(int xdm, int zdm) => WorldRoadTerrainFlags.None;
        }
    }
}

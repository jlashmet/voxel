using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class WorldRoadPresentationRegressionTests
    {
        private const uint Seed = 0x524F4144u;

        [Test]
        public void PresentationPathRoundsGenericTrailCornerWithoutChangingResolvedAuthority()
        {
            ResolvedWorldRoad road = Resolve(
                "trail-curve",
                WorldRoadProfiles.Trail,
                new WorldRoadPlanPoint(0, 0),
                new WorldRoadPlanPoint(80, 0),
                new WorldRoadPlanPoint(80, 80));
            var authority = new List<ResolvedWorldRoadPoint>(road.Points);

            IReadOnlyList<ResolvedWorldRoadPoint> presentation = WorldRoadPresentationPath.Build(road);

            Assert.That(presentation.Count, Is.GreaterThan(road.Points.Count));
            Assert.AreEqual(road.Points[0], presentation[0]);
            Assert.AreEqual(road.Points[road.Points.Count - 1], presentation[presentation.Count - 1]);
            CollectionAssert.AreEqual(authority, road.Points,
                "Presentation refinement must never rewrite the authoritative resolved route.");
            Assert.IsFalse(Contains(presentation, road.Points[1]),
                "An ordinary non-junction corner should be rounded instead of retaining its hard elbow.");
        }

        [Test]
        public void PresentationPathPreservesExactResolvedTopologyJunction()
        {
            ResolvedWorldRoad road = Resolve(
                "junction-curve",
                WorldRoadProfiles.DirtRoad,
                new WorldRoadPlanPoint(0, 0),
                new WorldRoadPlanPoint(80, 0),
                new WorldRoadPlanPoint(80, 80));
            var junctions = new[] { new WorldRoadJunction(80, 0, 3) };

            IReadOnlyList<ResolvedWorldRoadPoint> presentation = WorldRoadPresentationPath.Build(road, junctions);

            Assert.IsTrue(Contains(presentation, road.Points[1]),
                "Exact network junction vertices must remain on the presentation centreline.");
        }

        [Test]
        public void NetworkInfluenceUsesResolvedTopologyWhenRefiningPresentation()
        {
            WorldRoadNetworkRoute turning = Route(Resolve(
                "network-turn",
                WorldRoadProfiles.DirtRoad,
                new WorldRoadPlanPoint(0, 0),
                new WorldRoadPlanPoint(80, 0),
                new WorldRoadPlanPoint(80, 80)));
            WorldRoadNetworkRoute joining = Route(Resolve(
                "network-join",
                WorldRoadProfiles.Trail,
                new WorldRoadPlanPoint(80, 0),
                new WorldRoadPlanPoint(160, 0)));
            var network = new WorldRoadNetwork(new[] { turning, joining });

            Assert.AreEqual(1, network.Junctions.Count);
            Assert.IsTrue(network.TrySample(80, 0, out WorldRoadNetworkSample sample));
            Assert.AreEqual(31, sample.Influence.Coverage31,
                "Network sampling must keep the exact shared junction on the physical influence centreline.");
            Assert.AreEqual(0, sample.Influence.DistanceDm,
                "Topology-aware presentation must not round an actual shared junction away from its authoritative vertex.");

            var routeInfluence = new WorldRoadInfluence(turning.Road, network.Junctions);
            Assert.IsTrue(routeInfluence.TrySample(80, 0, out WorldRoadInfluenceSample routeSample));
            Assert.AreEqual(sample.Influence.Coverage31, routeSample.Coverage31);
            Assert.AreEqual(sample.Influence.TargetHeightDm, routeSample.TargetHeightDm,
                "Aggregate and reusable route influence must share the same topology-aware presentation field.");
        }

        [Test]
        public void GenericTerrainCorridorFormsBoundedCrownShoulderAndDeterministicWear()
        {
            Primitive corridor = Corridor(
                new int3(0, 20, 0),
                new int3(100, 20, 0),
                coreRadius: 12,
                outerRadius: 24,
                seed: Seed);

            Assert.IsTrue(TerrainCorridorRasteriser.TrySample(
                in corridor, 50, 0, out TerrainCorridorSample centre));
            Assert.AreEqual(31, centre.Coverage31);
            Assert.AreEqual(21, centre.TargetHeightVoxels,
                "Wide generic corridors should carry a shallow bounded crown above their resolved centre grade.");

            Assert.IsTrue(TerrainCorridorRasteriser.TrySample(
                in corridor, 50, 12, out TerrainCorridorSample coreEdge));
            Assert.AreEqual(20, coreEdge.TargetHeightVoxels,
                "The crown must recover to the resolved grade at the carriageway edge.");

            Assert.IsTrue(TerrainCorridorRasteriser.TrySample(
                in corridor, 50, 18, out TerrainCorridorSample shoulder));
            Assert.That(shoulder.Coverage31, Is.InRange(1, 30));
            Assert.That(shoulder.TargetHeightVoxels, Is.LessThanOrEqualTo(20),
                "The transition shoulder must fall away rather than continuing a flat painted ribbon.");

            Assert.IsTrue(TerrainCorridorRasteriser.TrySample(
                in corridor, 50, 0, out TerrainCorridorSample repeat));
            Assert.AreEqual(centre.SurfaceDetail31, repeat.SurfaceDetail31,
                "Surface wear must be stable for a fixed seed/world position.");

            byte first = 0;
            bool foundFirst = false;
            bool foundVariation = false;
            for (int x = 40; x <= 60 && !foundVariation; x++)
            for (int z = 0; z <= 10; z++)
            {
                Assert.IsTrue(TerrainCorridorRasteriser.TrySample(
                    in corridor, x, z, out TerrainCorridorSample sample));
                if (sample.Coverage31 != 31) continue;
                if (!foundFirst) { first = sample.SurfaceDetail31; foundFirst = true; }
                else if (sample.SurfaceDetail31 != first) { foundVariation = true; break; }
            }
            Assert.IsTrue(foundVariation,
                "The shared persisted surface-detail channel should provide restrained deterministic breakup inside the carriageway.");
        }

        [Test]
        public void CurvedPresentationInfluenceMatchesGenericPhysicalCorridor()
        {
            var profile = new WorldRoadProfile(
                "generic-curved-road",
                "road-surface",
                carriagewayWidthDm: 24,
                transitionWidthDm: 14,
                maximumGradePermille: 220,
                maximumCutFillDm: 20,
                edgeVariationDm: 0);
            ResolvedWorldRoad road = Resolve(
                "generic-curve",
                profile,
                new WorldRoadPlanPoint(0, 0),
                new WorldRoadPlanPoint(80, 0),
                new WorldRoadPlanPoint(80, 80));
            IReadOnlyList<ResolvedWorldRoadPoint> path = WorldRoadPresentationPath.Build(road);
            ResolvedWorldRoadPoint a = path[1];
            ResolvedWorldRoadPoint b = path[2];
            int x = DivideRounded(a.Xdm + b.Xdm, 2);
            int z = DivideRounded(a.Zdm + b.Zdm, 2);
            Primitive physical = Corridor(
                new int3(a.Xdm, a.Ydm, a.Zdm),
                new int3(b.Xdm, b.Ydm, b.Zdm),
                profile.CoreRadiusDm,
                profile.CoreRadiusDm + profile.TransitionWidthDm + profile.EdgeVariationDm,
                road.Intent.Seed);

            var semantic = new WorldRoadInfluence(road);
            Assert.IsTrue(semantic.TrySample(x, z, out WorldRoadInfluenceSample semanticSample));
            Assert.IsTrue(TerrainCorridorRasteriser.TrySample(
                in physical, x, z, out TerrainCorridorSample physicalSample));
            Assert.AreEqual(semanticSample.Coverage31, physicalSample.Coverage31);
            Assert.AreEqual(semanticSample.TargetHeightDm, physicalSample.TargetHeightVoxels);
        }

        [Test]
        public void NetworkJunctionsRequireExactSharedResolvedVerticesNotNearbySegments()
        {
            WorldRoadProfile profile = WorldRoadProfiles.Trail;
            WorldRoadNetworkRoute first = Route(Resolve(
                "join-a", profile,
                new WorldRoadPlanPoint(0, 0),
                new WorldRoadPlanPoint(80, 0)));
            WorldRoadNetworkRoute second = Route(Resolve(
                "join-b", profile,
                new WorldRoadPlanPoint(80, 0),
                new WorldRoadPlanPoint(80, 80)));
            WorldRoadNetworkRoute nearby = Route(Resolve(
                "nearby-not-joined", profile,
                new WorldRoadPlanPoint(81, 0),
                new WorldRoadPlanPoint(81, 80)));

            var network = new WorldRoadNetwork(new[] { first, second, nearby });

            Assert.AreEqual(1, network.Junctions.Count,
                "Only exact shared resolved topology may create a junction; influence overlap or proximity is insufficient.");
            Assert.AreEqual(80, network.Junctions[0].Xdm);
            Assert.AreEqual(0, network.Junctions[0].Zdm);
            Assert.AreEqual(WorldRoadJunctionKind.Join, network.Junctions[0].Kind);
        }

        private static Primitive Corridor(int3 a, int3 b, int coreRadius, int outerRadius, uint seed)
        {
            return new Primitive
            {
                Shape = PrimitiveShape.TerrainCorridor,
                Mode = PrimitiveMode.TerrainCorridor,
                Material = 13,
                A = a,
                B = b,
                InnerRadius = coreRadius,
                Radius = outerRadius,
                C = new int3(20, 4, 24),
                D = new int3(0, unchecked((int)seed), 1),
            };
        }

        private static WorldRoadNetworkRoute Route(ResolvedWorldRoad road)
            => new WorldRoadNetworkRoute(
                road,
                WorldRoadSemanticClass.Pedestrian,
                shoulderWidthDm: 4,
                clearanceWidthDm: 8);

        private static ResolvedWorldRoad Resolve(
            string id,
            WorldRoadProfile profile,
            params WorldRoadPlanPoint[] controls)
        {
            var intent = new WorldRoadIntent(
                id,
                id + ":from",
                id + ":to",
                Seed,
                profile,
                "road presentation regression fixture",
                controls);
            ResolvedWorldRoad road = WorldRoadResolver.Resolve(
                intent,
                new FlatTerrain(),
                sampleSpacingDm: 20,
                searchMarginCells: 0);
            Assert.AreEqual(WorldRoadResolutionStatus.Resolved, road.Status, road.FailureReason);
            return road;
        }

        private static bool Contains(
            IReadOnlyList<ResolvedWorldRoadPoint> points,
            ResolvedWorldRoadPoint expected)
        {
            for (int i = 0; i < points.Count; i++)
                if (points[i].Equals(expected)) return true;
            return false;
        }

        private static int DivideRounded(int numerator, int denominator)
            => numerator >= 0
                ? (numerator + denominator / 2) / denominator
                : -((-numerator + denominator / 2) / denominator);

        private sealed class FlatTerrain : IWorldRoadTerrain
        {
            public int HeightAtDm(int xdm, int zdm) => 20;
            public WorldRoadTerrainFlags FlagsAtDm(int xdm, int zdm) => WorldRoadTerrainFlags.None;
        }
    }
}
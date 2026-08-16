using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleAccessRouteValidatorTests
    {
        [Test]
        public void GeneratedResolvedRoutesStayInsideTheirAssignedWards()
        {
            for (uint seed = 1; seed <= 256; seed++)
            {
                CastlePlan dimensions = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                if (topology.KeepPlacement == CastleKeepPlacement.HighestGround)
                    topology.KeepPlacement = CastleKeepPlacement.Central;

                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);
                CastleAccessRoute route = CastleAccessRoute.Create(in dimensions, spatial);

                Assert.IsTrue(
                    CastleAccessRouteValidator.TryValidate(
                        in route,
                        spatial.OuterWardVertices,
                        spatial.InnerWardVertices,
                        out CastleAccessRouteIssue issue),
                    $"seed {seed}: invalid access route: {issue}");
            }
        }

        [Test]
        public void ConcaveOuterWardCannotLetRouteLeaveAndReenter()
        {
            CreateRectangularPlan(false, out CastlePlan dimensions, out CastleSpatialPlan spatial);
            CastleAccessRoute route = CastleAccessRoute.Create(in dimensions, spatial);
            int2 gate = route.Waypoint(0);
            int2 destination = route.Waypoint(1);
            Assert.Greater(destination.y - gate.y, 48);

            int hx = dimensions.BaileyHalfX;
            int hz = dimensions.BaileyHalfZ;
            int notchStart = gate.y + 12;
            int notchEnd = math.min(destination.y - 12, gate.y + 48);
            int2[] concaveOuter =
            {
                new int2(-hx, -hz),
                new int2( hx, -hz),
                new int2( hx, notchStart),
                new int2(  -8, notchStart),
                new int2(  -8, notchEnd),
                new int2( hx, notchEnd),
                new int2( hx,  hz),
                new int2(-hx,  hz),
            };

            Assert.IsFalse(CastleAccessRouteValidator.TryValidate(
                in route, concaveOuter, spatial.InnerWardVertices,
                out CastleAccessRouteIssue issue));
            Assert.AreEqual(CastleAccessRouteIssue.OuterRouteLeavesWard, issue);
        }

        [Test]
        public void NestedRouteCannotEnterInnerWardBeforeItsGate()
        {
            CreateRectangularPlan(true, out CastlePlan dimensions, out CastleSpatialPlan spatial);
            CastleAccessRoute route = CastleAccessRoute.Create(in dimensions, spatial);
            int2[] earlyInner = (int2[])spatial.InnerWardVertices.Clone();

            // Rectangular edge zero is the primary/inner approach edge. Move only that inner wall
            // toward the outer gate while leaving the planned inner-gate waypoint unchanged.
            earlyInner[0].y -= 24;
            earlyInner[1].y -= 24;

            Assert.IsFalse(CastleAccessRouteValidator.TryValidate(
                in route, spatial.OuterWardVertices, earlyInner,
                out CastleAccessRouteIssue issue));
            Assert.AreEqual(CastleAccessRouteIssue.InnerWardEnteredBeforeGate, issue);
        }

        [Test]
        public void NestedRouteCannotLeaveAndReenterInnerWardAfterGate()
        {
            CreateRectangularPlan(true, out CastlePlan dimensions, out CastleSpatialPlan spatial);
            CastleAccessRoute route = CastleAccessRoute.Create(in dimensions, spatial);
            int2 innerGate = route.Waypoint(1);
            int2 keepEntrance = route.Waypoint(2);
            Assert.Greater(keepEntrance.y - innerGate.y, 48);

            int minX = spatial.InnerWardVertices[0].x;
            int maxX = spatial.InnerWardVertices[1].x;
            int maxZ = spatial.InnerWardVertices[2].y;
            int notchStart = innerGate.y + 12;
            int notchEnd = math.min(keepEntrance.y - 12, innerGate.y + 48);
            int2[] concaveInner =
            {
                new int2(minX, innerGate.y),
                new int2(maxX, innerGate.y),
                new int2(maxX, notchStart),
                new int2(   -8, notchStart),
                new int2(   -8, notchEnd),
                new int2(maxX, notchEnd),
                new int2(maxX, maxZ),
                new int2(minX, maxZ),
            };

            Assert.IsFalse(CastleAccessRouteValidator.TryValidate(
                in route, spatial.OuterWardVertices, concaveInner,
                out CastleAccessRouteIssue issue));
            Assert.AreEqual(CastleAccessRouteIssue.InnerRouteLeavesWard, issue);
        }

        private static void CreateRectangularPlan(
            bool nested,
            out CastlePlan dimensions,
            out CastleSpatialPlan spatial)
        {
            for (uint seed = 1; seed <= 1024; seed++)
            {
                dimensions = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                topology.Perimeter = CastlePerimeterKind.Rectangular;
                topology.Wards = nested
                    ? CastleWardPattern.InnerAndOuterWards
                    : CastleWardPattern.SingleWard;
                topology.KeepPlacement = CastleKeepPlacement.Central;
                topology.DesiredTowerCount = 4;
                topology.HasPosternGate = false;
                spatial = CastleSpatialPlanner.Create(in dimensions, in topology);
                if (spatial.PrimaryGate.EdgeIndex == 0)
                    return;
            }

            dimensions = default;
            spatial = null;
            Assert.Fail("Could not find a deterministic rectangular seed with primary gate edge zero.");
        }
    }
}

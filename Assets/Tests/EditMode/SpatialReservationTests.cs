using System;
using System.Collections.Generic;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class SpatialReservationTests
    {
        private static readonly ReservationBoundsDm Window =
            new ReservationBoundsDm(-1000, -500, -1000, 3000, 1000, 3000);

        [Test]
        public void StableIdentityAndIndependentResolutionIgnoreInsertionOrder()
        {
            SpatialReservation low = SpatialReservation.Box(
                "candidate-low", ReservationCategory.Landmark,
                ReservationSemantics.HardOccupancy,
                new ReservationBoundsDm(0, 0, 0, 100, 100, 100),
                precedence: 10);
            SpatialReservation high = SpatialReservation.Box(
                "candidate-high", ReservationCategory.Landmark,
                ReservationSemantics.HardOccupancy,
                new ReservationBoundsDm(50, 0, 50, 150, 100, 150),
                precedence: 20);

            SpatialReservation[] forward = IndependentReservationResolver.Resolve(
                new[] { low, high }, Window, ReservationConsumerKind.Landmark);
            SpatialReservation[] reversed = IndependentReservationResolver.Resolve(
                new[] { high, low }, Window, ReservationConsumerKind.Landmark);

            Assert.That(forward, Has.Length.EqualTo(1));
            Assert.That(reversed, Has.Length.EqualTo(1));
            Assert.That(forward[0].Id, Is.EqualTo(high.Id));
            Assert.That(reversed[0].Id, Is.EqualTo(high.Id));
            Assert.That(
                ReservationId.FromStableText("candidate-high", ReservationCategory.Landmark),
                Is.EqualTo(high.Id));
        }

        [Test]
        public void EqualPrecedenceUsesStableIdentityTieBreak()
        {
            SpatialReservation a = SpatialReservation.Box(
                "stable-a", ReservationCategory.Landmark,
                ReservationSemantics.HardOccupancy,
                new ReservationBoundsDm(0, 0, 0, 100, 100, 100),
                precedence: 5);
            SpatialReservation b = SpatialReservation.Box(
                "stable-b", ReservationCategory.Landmark,
                ReservationSemantics.HardOccupancy,
                new ReservationBoundsDm(20, 0, 20, 120, 100, 120),
                precedence: 5);
            SpatialReservation expected = a.Id.CompareTo(b.Id) < 0 ? a : b;

            SpatialReservation[] first = IndependentReservationResolver.Resolve(
                new[] { a, b }, Window, ReservationConsumerKind.Landmark);
            SpatialReservation[] second = IndependentReservationResolver.Resolve(
                new[] { b, a }, Window, ReservationConsumerKind.Landmark);

            Assert.That(first, Has.Length.EqualTo(1));
            Assert.That(second, Has.Length.EqualTo(1));
            Assert.That(first[0].Id, Is.EqualTo(expected.Id));
            Assert.That(second[0].Id, Is.EqualTo(expected.Id));
        }

        [Test]
        public void HardClearanceAndSoftReservationsHaveDistinctOutcomes()
        {
            var claims = new[]
            {
                SpatialReservation.Box(
                    "hard", ReservationCategory.Building, ReservationSemantics.HardOccupancy,
                    new ReservationBoundsDm(0, 0, 0, 100, 100, 100)),
                SpatialReservation.Box(
                    "clear", ReservationCategory.Building, ReservationSemantics.Clearance,
                    new ReservationBoundsDm(150, 0, 0, 250, 100, 100)),
                SpatialReservation.Box(
                    "soft", ReservationCategory.Geographic, ReservationSemantics.SoftYield,
                    new ReservationBoundsDm(300, 0, 0, 400, 100, 100)),
            };
            SpatialReservationSnapshot snapshot = SpatialReservationSnapshot.Create(claims, Window);

            ReservationQueryResult hard = snapshot.Query(Probe("p-hard", 25, 25),
                ReservationConsumerKind.SettlementBuilding);
            ReservationQueryResult clearance = snapshot.Query(Probe("p-clear", 175, 25),
                ReservationConsumerKind.SettlementBuilding);
            ReservationQueryResult vegetationClearance = snapshot.Query(Probe("p-veg", 175, 25),
                ReservationConsumerKind.Vegetation);
            ReservationQueryResult soft = snapshot.Query(Probe("p-soft", 325, 25),
                ReservationConsumerKind.Vegetation);

            Assert.That(hard.Decision, Is.EqualTo(ReservationDecision.Rejected));
            Assert.That(hard.Reason, Is.EqualTo(ReservationReasonCode.HardOccupancyConflict));
            Assert.That(clearance.Decision, Is.EqualTo(ReservationDecision.Rejected));
            Assert.That(clearance.Reason, Is.EqualTo(ReservationReasonCode.ClearanceConflict));
            Assert.That(vegetationClearance.Decision, Is.EqualTo(ReservationDecision.Yield));
            Assert.That(soft.Decision, Is.EqualTo(ReservationDecision.Yield));
            StringAssert.Contains("owner=hard", hard.Describe());
            StringAssert.Contains("provenance=", hard.Describe());
        }

        [Test]
        public void SettlementRoadHandoffAllowsRoadButRejectsBuilding()
        {
            SpatialReservation envelope = WorldBuilderReservationFactory.SettlementEnvelope(
                "settlement:kentridge",
                new ReservationBoundsDm(0, 0, 0, 400, 100, 400));
            SpatialReservation arrival = WorldBuilderReservationFactory.PublicAccessCorridor(
                "settlement:kentridge:gate",
                new Int2(-50, 200), new Int2(100, 200),
                0, 60, 20);
            SpatialReservationSnapshot snapshot = SpatialReservationSnapshot.Create(
                new[] { envelope, arrival }, Window);
            SpatialReservation enteringRoad = WorldBuilderReservationFactory.RoadCorridor(
                "road:arrival", new Int2(-100, 200), new Int2(120, 200), 0, 40, 12);
            SpatialReservation blocker = SpatialReservation.Box(
                "building:blocker", ReservationCategory.Building,
                ReservationSemantics.HardOccupancy,
                new ReservationBoundsDm(20, 0, 180, 80, 80, 240));

            ReservationQueryResult road = snapshot.Query(
                enteringRoad, ReservationConsumerKind.Road,
                ReservationCategory.SettlementEnvelope | ReservationCategory.PublicAccess);
            ReservationQueryResult building = snapshot.Query(
                blocker, ReservationConsumerKind.SettlementBuilding,
                ReservationCategory.SettlementEnvelope | ReservationCategory.PublicAccess);

            Assert.That(road.IsAccepted, Is.True, road.Describe());
            Assert.That(road.Reason, Is.EqualTo(ReservationReasonCode.CompatibleHandoff));
            Assert.That(building.Decision, Is.EqualTo(ReservationDecision.Rejected));
        }

        [Test]
        public void True3DSeparationAllowsTunnelBelowBuildingButRejectsRealCollision()
        {
            SpatialReservation building = WorldBuilderReservationFactory.BuildingFootprint(
                "surface-building", new Int2(0, 0), new Int3(120, 100, 120));
            SpatialReservationSnapshot snapshot = SpatialReservationSnapshot.Create(
                new[] { building }, Window);

            SpatialReservation tunnelBelow = SpatialReservation.Corridor(
                "tunnel-below", ReservationCategory.Underground,
                ReservationSemantics.HardOccupancy,
                new Int2(-20, 60), new Int2(140, 60),
                -80, -40, 10);
            SpatialReservation tunnelThrough = SpatialReservation.Corridor(
                "tunnel-through", ReservationCategory.Underground,
                ReservationSemantics.HardOccupancy,
                new Int2(-20, 60), new Int2(140, 60),
                20, 40, 10);

            Assert.That(snapshot.Query(tunnelBelow, ReservationConsumerKind.Underground).IsAccepted, Is.True);
            ReservationQueryResult collision = snapshot.Query(tunnelThrough, ReservationConsumerKind.Underground);
            Assert.That(collision.Decision, Is.EqualTo(ReservationDecision.Rejected));
            Assert.That(collision.Reason, Is.EqualTo(ReservationReasonCode.HardOccupancyConflict));
        }

        [Test]
        public void ConnectorCompatibilityIsExplicitAndDoesNotRelaxUnrelatedUndergroundContent()
        {
            SpatialReservation shaftHandoff = SpatialReservation.Box(
                "surface:shaft", ReservationCategory.PublicAccess,
                ReservationSemantics.ProtectedCorridor | ReservationSemantics.CompatibleHandoff,
                new ReservationBoundsDm(0, -80, 0, 40, 80, 40),
                compatibleConsumers: ReservationConsumerKind.Connector,
                provenance: "surface-shaft");
            SpatialReservationSnapshot snapshot = SpatialReservationSnapshot.Create(
                new[] { shaftHandoff }, Window);
            SpatialReservation connector = WorldBuilderReservationFactory.UndergroundVolume(
                "connector", new ReservationBoundsDm(10, -60, 10, 30, 30, 30));

            Assert.That(snapshot.Query(connector, ReservationConsumerKind.Connector).IsAccepted, Is.True);
            ReservationQueryResult unrelated = snapshot.Query(connector, ReservationConsumerKind.Underground);
            Assert.That(unrelated.Decision, Is.EqualTo(ReservationDecision.Rejected));
            Assert.That(unrelated.Reason, Is.EqualTo(ReservationReasonCode.ProtectedCorridorConflict));
        }

        [Test]
        public void KentridgeProductionPlannerPublishesSharedClaimsDeterministically()
        {
            const uint seed = 0x4B454E54u;
            SettlementPlan firstPlan = KentridgeTownPlanner.Build(seed);
            SettlementPlan secondPlan = KentridgeTownPlanner.Build(seed);
            SpatialReservationSnapshot first = KentridgeTownPlanner.BuildReservationSnapshot(seed);
            SpatialReservationSnapshot second = KentridgeTownPlanner.BuildReservationSnapshot(seed);

            Assert.That(firstPlan.Plots.Count, Is.EqualTo(secondPlan.Plots.Count));
            Assert.That(first.Reservations.Count, Is.EqualTo(second.Reservations.Count));
            Assert.That(first.Reservations.Count, Is.GreaterThan(40));
            bool sawBuilding = false, sawClearance = false, sawApproach = false, sawRoad = false, sawPlaza = false;
            for (int i = 0; i < first.Reservations.Count; i++)
            {
                SpatialReservation a = first.Reservations[i];
                SpatialReservation b = second.Reservations[i];
                Assert.That(a.Id, Is.EqualTo(b.Id));
                Assert.That(a.Bounds, Is.EqualTo(b.Bounds));
                if (a.Category == ReservationCategory.Building
                    && (a.Semantics & ReservationSemantics.HardOccupancy) != 0) sawBuilding = true;
                if (a.Category == ReservationCategory.Building
                    && (a.Semantics & ReservationSemantics.Clearance) != 0) sawClearance = true;
                if (a.Category == ReservationCategory.PublicAccess) sawApproach = true;
                if (a.Category == ReservationCategory.Road) sawRoad = true;
                if (a.Category == ReservationCategory.Plaza) sawPlaza = true;
            }
            Assert.That(sawBuilding && sawClearance && sawApproach && sawRoad && sawPlaza, Is.True);
        }

        [Test]
        public void KentridgeBuildingClaimsDoNotViolateOtherBuildingClearances()
        {
            SpatialReservationSnapshot snapshot = KentridgeTownPlanner.BuildReservationSnapshot(0x4B454E54u);
            var claims = new List<SpatialReservation>();
            for (int i = 0; i < snapshot.Reservations.Count; i++)
                claims.Add(snapshot.Reservations[i]);

            for (int i = 0; i < claims.Count; i++)
            {
                SpatialReservation candidate = claims[i];
                if (candidate.Category != ReservationCategory.Building
                    || (candidate.Semantics & ReservationSemantics.HardOccupancy) == 0)
                    continue;

                var others = new List<SpatialReservation>();
                for (int j = 0; j < claims.Count; j++)
                    if (!string.Equals(claims[j].OwnerId, candidate.OwnerId, StringComparison.Ordinal))
                        others.Add(claims[j]);
                ReservationQueryResult result = SpatialReservationSnapshot.Create(others, Window).Query(
                    candidate,
                    ReservationConsumerKind.SettlementBuilding,
                    ReservationCategory.Building | ReservationCategory.Plaza);
                Assert.That(result.IsAccepted, Is.True,
                    candidate.OwnerId + " conflicts after production migration: " + result.Describe());
            }
        }

        [Test]
        public void BoundedWindowLimitsBroadPhaseWorkForLargeAndDistantClaims()
        {
            var claims = new List<SpatialReservation>();
            claims.Add(WorldBuilderReservationFactory.SettlementEnvelope(
                "macro-envelope",
                new ReservationBoundsDm(-100000, -100, -100000, 100000, 500, 100000)));
            for (int i = 0; i < 2000; i++)
            {
                int x = 10000 + i * 200;
                claims.Add(SpatialReservation.Box(
                    "distant:" + i,
                    ReservationCategory.Landmark,
                    ReservationSemantics.HardOccupancy,
                    new ReservationBoundsDm(x, 0, x, x + 20, 20, x + 20)));
            }

            var localWindow = new ReservationBoundsDm(0, -100, 0, 1024, 300, 1024);
            SpatialReservationSnapshot snapshot = SpatialReservationSnapshot.Create(claims, localWindow, 128);
            SpatialReservation query = Probe("bounded-query", 500, 500);
            ReservationQueryResult result = snapshot.Query(query, ReservationConsumerKind.Landmark);

            Assert.That(snapshot.Reservations.Count, Is.EqualTo(1),
                "Distant world claims must not be retained in a bounded planning snapshot.");
            Assert.That(result.Metrics.BucketsVisited, Is.LessThanOrEqualTo(4));
            Assert.That(result.Metrics.BroadPhaseCandidates, Is.LessThanOrEqualTo(1));
            Assert.That(result.Metrics.NarrowPhaseTests, Is.LessThanOrEqualTo(1));
            Assert.That(result.Metrics.ElapsedStopwatchTicks, Is.GreaterThanOrEqualTo(0));
        }

        private static SpatialReservation Probe(string id, int x, int z) =>
            SpatialReservation.Box(
                id,
                ReservationCategory.Landmark,
                ReservationSemantics.HardOccupancy,
                new ReservationBoundsDm(x, 0, z, x + 10, 20, z + 10));
    }
}

using MountingForce.WorldGen;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class SpatialReservationReusabilityTests
    {
        private static readonly ReservationBoundsDm Window =
            new ReservationBoundsDm(-500, -500, -500, 1000, 1000, 1000);

        [Test]
        public void ClearanceYieldPolicyAndVerticalSeparationAreConsumerConfigured()
        {
            SpatialReservation clearance = SpatialReservation.Box(
                "fixture-clearance",
                ReservationCategory.Geographic,
                ReservationSemantics.Clearance,
                new ReservationBoundsDm(0, 0, 0, 100, 100, 100),
                yieldingConsumers: ReservationConsumerKind.Connector);
            SpatialReservationSnapshot clearanceSnapshot = SpatialReservationSnapshot.Create(
                new[] { clearance }, Window);
            SpatialReservation overlap = SpatialReservation.Box(
                "fixture-overlap",
                ReservationCategory.Landmark,
                ReservationSemantics.HardOccupancy,
                new ReservationBoundsDm(25, 25, 25, 75, 75, 75));

            ReservationQueryResult configuredYield = clearanceSnapshot.Query(
                overlap,
                ReservationConsumerKind.Connector);
            ReservationQueryResult unrelatedReject = clearanceSnapshot.Query(
                overlap,
                ReservationConsumerKind.Landmark);

            Assert.That(configuredYield.Decision, Is.EqualTo(ReservationDecision.Yield));
            Assert.That(configuredYield.Reason, Is.EqualTo(ReservationReasonCode.ClearanceConflict));
            Assert.That(unrelatedReject.Decision, Is.EqualTo(ReservationDecision.Rejected));
            Assert.That(unrelatedReject.Reason, Is.EqualTo(ReservationReasonCode.ClearanceConflict));
            StringAssert.Contains("yieldingConsumers=Connector", unrelatedReject.Describe());

            SpatialReservation surface = SpatialReservation.Box(
                "fixture-surface",
                ReservationCategory.PublicAccess,
                ReservationSemantics.HardOccupancy,
                new ReservationBoundsDm(200, 0, 200, 300, 60, 300));
            SpatialReservationSnapshot verticalSnapshot = SpatialReservationSnapshot.Create(
                new[] { surface }, Window);
            SpatialReservation below = SpatialReservation.Box(
                "fixture-below",
                ReservationCategory.Underground,
                ReservationSemantics.HardOccupancy,
                new ReservationBoundsDm(220, -100, 220, 280, -20, 280));
            SpatialReservation collision = SpatialReservation.Box(
                "fixture-collision",
                ReservationCategory.Underground,
                ReservationSemantics.HardOccupancy,
                new ReservationBoundsDm(220, 20, 220, 280, 40, 280));

            Assert.That(
                verticalSnapshot.Query(below, ReservationConsumerKind.Underground).IsAccepted,
                Is.True,
                "Independent consumers must be able to reuse the generic 3D snapshot without place policy.");
            ReservationQueryResult collisionResult = verticalSnapshot.Query(
                collision,
                ReservationConsumerKind.Underground);
            Assert.That(collisionResult.Decision, Is.EqualTo(ReservationDecision.Rejected));
            Assert.That(collisionResult.Reason, Is.EqualTo(ReservationReasonCode.HardOccupancyConflict));
        }
    }
}

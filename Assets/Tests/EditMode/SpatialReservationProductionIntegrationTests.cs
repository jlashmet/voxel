using MountingForce.WorldGen;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class SpatialReservationProductionIntegrationTests
    {
        private static readonly ReservationBoundsDm Window =
            new ReservationBoundsDm(-500, -100, -500, 2500, 500, 2500);

        [Test]
        public void HalfOpenReservationBoundsAllowExactFaceTouching()
        {
            SpatialReservation existing = SpatialReservation.Box(
                "half-open:existing",
                ReservationCategory.Building,
                ReservationSemantics.HardOccupancy,
                new ReservationBoundsDm(0, 0, 0, 100, 100, 100));
            SpatialReservation touching = SpatialReservation.Box(
                "half-open:touching",
                ReservationCategory.StructuralChild,
                ReservationSemantics.Clearance,
                new ReservationBoundsDm(100, 0, 0, 160, 100, 100));
            SpatialReservationSnapshot snapshot = SpatialReservationSnapshot.Create(
                new[] { existing }, Window);

            ReservationQueryResult result = snapshot.Query(
                touching,
                ReservationConsumerKind.StructuralChild,
                ReservationCategory.Building);

            Assert.That(result.IsAccepted, Is.True, result.Describe());
            Assert.That(result.Reason, Is.EqualTo(ReservationReasonCode.NoIntersection));
        }

        [Test]
        public void KentridgeProductionStructureQueryAllowsHostButRejectsExternalOwner()
        {
            var geometry = new StructureSiteGeometry(
                new Int2(0, 0),
                new Int2(100, 100),
                new Int2(50, 10),
                publicEntranceHeightDm: 8,
                publicEntranceFacing: FrontageDirection.South);
            SpatialReservation host = WorldBuilderReservationFactory.BuildingFootprint(
                "kentridge-site:3",
                new Int2(0, 0),
                new Int3(100, 80, 100));
            SpatialReservationSnapshot hostOnly = SpatialReservationSnapshot.Create(
                new[] { host }, Window);

            ReservationQueryResult accepted = KentridgeStructureReservationValidation.Query(
                hostOnly,
                roleId: 3,
                geometry,
                maxYDm: 80);
            Assert.That(accepted.IsAccepted, Is.True, accepted.Describe());

            SpatialReservation blocker = WorldBuilderReservationFactory.BuildingFootprint(
                "external-structure",
                new Int2(60, 20),
                new Int3(80, 80, 80));
            SpatialReservationSnapshot blocked = SpatialReservationSnapshot.Create(
                new[] { host, blocker }, Window);
            ReservationQueryResult rejected = KentridgeStructureReservationValidation.Query(
                blocked,
                roleId: 3,
                geometry,
                maxYDm: 80);

            Assert.That(rejected.Decision, Is.EqualTo(ReservationDecision.Rejected));
            Assert.That(rejected.Reason, Is.EqualTo(ReservationReasonCode.HardOccupancyConflict));
            StringAssert.Contains("owner=external-structure", rejected.Describe());
        }

        [Test]
        public void CanonicalKentridgeStructureSitesPassSharedReservationValidation()
        {
            const uint seed = 0x4B454E54u;
            SettlementPlan plan = KentridgeTownPlanner.Build(seed);
            SpatialReservationSnapshot source = KentridgeTownPlanner.BuildReservationSnapshot(seed);

            for (int i = 0; i < plan.Plots.Count; i++)
            {
                BuildingPlot plot = plan.Plots[i];
                if (plot.Archetype == StructureArchetype.Well) continue;
                StructureIntent intent = KentridgeDefinition.StructureIntent(plot);
                StructureForm form = ArchitectureCompiler.Resolve(intent, plan.Theme, seed);
                Assert.That(
                    StructureSiteGeometryResolver.TryResolve(
                        intent, plan.Theme, form, out StructureSiteGeometry geometry),
                    Is.True,
                    "Missing site geometry for Kentridge role " + plot.RoleId);

                ReservationQueryResult result = KentridgeStructureReservationValidation.Query(
                    source,
                    plot.RoleId,
                    geometry,
                    intent.EnvelopeDm.Y);
                Assert.That(
                    result.IsAccepted,
                    Is.True,
                    "Kentridge role " + plot.RoleId + " failed production reservation validation: " +
                    result.Describe());
            }
        }
    }
}

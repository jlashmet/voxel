using Game.WorldBuilder.Api;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Architecture;
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
                3,
                geometry,
                80);
            Assert.That(accepted.IsAccepted, Is.True, accepted.Describe());

            SpatialReservation blocker = WorldBuilderReservationFactory.BuildingFootprint(
                "external-structure",
                new Int2(60, 20),
                new Int3(80, 80, 80));
            SpatialReservationSnapshot blocked = SpatialReservationSnapshot.Create(
                new[] { host, blocker }, Window);
            ReservationQueryResult rejected = KentridgeStructureReservationValidation.Query(
                blocked,
                3,
                geometry,
                80);

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

        [Test]
        public void MacroRoadHandoffKeepsLoweredCorridorInBothRegionBuckets()
        {
            const uint seed = 0x524F4144u;
            int regionEdge = VoxelGrid.RegionVoxelEdge;
            var settlement = new TopDownWorldNodeSpec(
                "fixture-settlement",
                "Fixture Settlement",
                TopDownWorldNodeKind.Settlement,
                envelopeHalfExtentDm: 8,
                source: "spatial reservation production seam fixture");
            var region = new TopDownWorldNodeSpec(
                "fixture-region",
                "Fixture Region",
                TopDownWorldNodeKind.Region,
                envelopeHalfExtentDm: 8,
                source: "spatial reservation production seam fixture");
            var profile = new WorldRoadProfile(
                "fixture-macro-road",
                "road-surface",
                carriagewayWidthDm: 12,
                transitionWidthDm: 4,
                maximumGradePermille: 1000,
                maximumCutFillDm: 256);
            var route = new TopDownWorldRouteSpec(
                settlement.Id,
                region.Id,
                new TopDownWorldGridPoint(1, 0),
                TopDownWorldEvidenceKind.VerifiedTransition,
                "verified fixture transition",
                "fixture route crosses a production region boundary",
                corridorWidthDm: 12,
                roadProfile: profile);
            var layout = new TopDownWorldLayout(
                settlement.Id,
                seed,
                new[]
                {
                    new TopDownWorldNodePlacement(settlement, new TopDownWorldGridPoint(0, 0)),
                    new TopDownWorldNodePlacement(region, new TopDownWorldGridPoint(1, 0)),
                },
                new[] { route });
            var rootCentreDm = new Int2(regionEdge - 12, 0);
            const int cellSizeDm = 24;
            var materials = new VoxelMaterialMap(1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);
            var settings = new VoxelWorldGenSettings(1, materials);

            TopDownWorldVoxelPlan plan = TopDownWorldVoxelCatalogue.Plan(
                layout, rootCentreDm, cellSizeDm);
            WorldRoadNetwork network = TopDownWorldRoadNetwork.Build(
                layout, rootCentreDm, cellSizeDm, settings);

            Assert.That(network.Routes.Count, Is.EqualTo(1));
            Assert.DoesNotThrow(() => TopDownWorldReservationAdapter.ValidateRoadHandoffs(plan, network));

            SpatialReservationSnapshot reservations = TopDownWorldReservationAdapter.Build(plan, network);
            bool foundArrivalHandoff = false;
            for (int i = 0; i < reservations.Reservations.Count; i++)
            {
                SpatialReservation reservation = reservations.Reservations[i];
                if ((reservation.Category & ReservationCategory.PublicAccess) == 0) continue;
                foundArrivalHandoff = true;
                Assert.That(
                    (reservation.Semantics & ReservationSemantics.CompatibleHandoff) != 0,
                    Is.True,
                    "Settlement arrival must remain an explicit compatible handoff.");
            }
            Assert.That(foundArrivalHandoff, Is.True, "Production macro adapter did not publish the settlement arrival handoff.");

            FeatureCatalogue roadCatalogue = WorldRoadNetworkVoxelCatalogue.Build(
                network, settings, Allocator.Temp);
            try
            {
                bool foundSeamPlacement = false;
                for (int i = 0; i < roadCatalogue.DefinitionCount; i++)
                {
                    int3 origin = roadCatalogue.ExplicitPlacements[i].Position;
                    int3 footprint = roadCatalogue.Definitions[i].Footprint;
                    if (origin.x >= regionEdge || origin.x + footprint.x <= regionEdge) continue;

                    int regionY = FloorDiv(origin.y, regionEdge);
                    int regionZ = FloorDiv(origin.z, regionEdge);
                    int3 leftMin = new int3(0, regionY * regionEdge, regionZ * regionEdge);
                    int3 rightMin = new int3(regionEdge, regionY * regionEdge, regionZ * regionEdge);
                    int3 extent = new int3(regionEdge);

                    Assert.That(
                        FeatureGeneration.FootprintIntersects(
                            origin, footprint, leftMin, leftMin + extent),
                        Is.True,
                        "Production region scan must retain the road piece on the left side of the seam.");
                    Assert.That(
                        FeatureGeneration.FootprintIntersects(
                            origin, footprint, rightMin, rightMin + extent),
                        Is.True,
                        "Production region scan must retain the same road piece on the right side of the seam.");
                    foundSeamPlacement = true;
                    break;
                }

                Assert.That(
                    foundSeamPlacement,
                    Is.True,
                    "The resolved macro road did not lower to a bounded corridor placement spanning the production region seam.");
            }
            finally
            {
                roadCatalogue.Dispose();
            }
        }

        private static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            int remainder = value % divisor;
            return remainder != 0 && value < 0 ? quotient - 1 : quotient;
        }
    }
}

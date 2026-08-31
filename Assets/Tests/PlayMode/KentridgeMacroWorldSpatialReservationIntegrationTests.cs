using System;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using VoxelEngine.Structures.Api;
using LegacyKentridgeDefinition = MountingForce.WorldGen.Content.Kentridge.KentridgeDefinition;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class KentridgeMacroWorldSpatialReservationIntegrationTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void PhysicalMacroPlanPublishesDeterministicSharedSpatialClaims()
        {
            TopDownWorldLayout layout = MountingForceTopDownWorldDefinition.Build(Seed);
            TopDownWorldPhysicalPlan physical = TopDownWorldPhysicalVoxelCatalogue.Plan(
                layout,
                KentridgeTopDownWorldPhysicalIntent.Build(),
                LegacyKentridgeDefinition.TownCentreDm,
                MountingForceTopDownWorldDefinition.CellSizeDm,
                Settings());

            SpatialReservationSnapshot first = TopDownWorldPhysicalReservationAdapter.Validate(physical);
            SpatialReservationSnapshot replay = TopDownWorldPhysicalReservationAdapter.Validate(physical);

            int buildings = 0;
            int roads = 0;
            int arrivals = 0;
            int settlements = 0;
            for (int i = 0; i < first.Reservations.Count; i++)
            {
                SpatialReservation claim = first.Reservations[i];
                if ((claim.Category & ReservationCategory.Building) != 0) buildings++;
                if ((claim.Category & ReservationCategory.Road) != 0) roads++;
                if ((claim.Category & ReservationCategory.PublicAccess) != 0) arrivals++;
                if ((claim.Category & ReservationCategory.SettlementEnvelope) != 0) settlements++;
            }

            Assert.That(buildings, Is.EqualTo(physical.BuildingCount * 2),
                "Each generic building must publish footprint plus clearance through the shared reservation substrate.");
            Assert.That(roads, Is.GreaterThan(physical.Routes.Count),
                "Resolved hard routes must publish segment-level shared road claims.");
            Assert.That(arrivals, Is.GreaterThan(0),
                "Settlement route endpoints must publish protected public-access handoffs.");
            Assert.That(settlements, Is.EqualTo(physical.Settlements.Count));
            Assert.That(replay.Reservations.Count, Is.EqualTo(first.Reservations.Count));
            for (int i = 0; i < first.Reservations.Count; i++)
            {
                Assert.That(replay.Reservations[i].Id, Is.EqualTo(first.Reservations[i].Id));
                Assert.That(replay.Reservations[i].Bounds, Is.EqualTo(first.Reservations[i].Bounds));
            }
        }

        [Test]
        public void KentridgeResolvedRoadsRespectGenericSettlementBuildingClearance()
        {
            TopDownWorldLayout layout = MountingForceTopDownWorldDefinition.Build(Seed);
            TopDownWorldPhysicalPlan physical = TopDownWorldPhysicalVoxelCatalogue.Plan(
                layout,
                KentridgeTopDownWorldPhysicalIntent.Build(),
                LegacyKentridgeDefinition.TownCentreDm,
                MountingForceTopDownWorldDefinition.CellSizeDm,
                Settings());

            Assert.DoesNotThrow(
                () => TopDownWorldPhysicalReservationAdapter.Validate(physical),
                "The real Kentridge route solution must satisfy generic building footprint and clearance reservations before catalogue publication.");
            Assert.That(
                physical.TryGetRoute(
                    MountingForceTopDownWorldDefinition.SouthFightingArea,
                    MountingForceTopDownWorldDefinition.OrcVillage,
                    out TopDownWorldPhysicalRoutePlan orcRoute),
                Is.True);
            Assert.That(
                physical.TryGetSettlement(
                    MountingForceTopDownWorldDefinition.OrcVillage,
                    out TopDownWorldSettlementPlan orcSettlement),
                Is.True);
            Int2 last = orcRoute.Tiles[orcRoute.Tiles.Count - 1];
            Assert.That(last.X, Is.EqualTo(orcSettlement.CentreDm.X));
            Assert.That(last.Y, Is.EqualTo(orcSettlement.CentreDm.Y));
        }

        [Test]
        public void SharedSpatialReservationsRejectIndependentRoadThroughBuildingFixture()
        {
            var alpha = new TopDownWorldNodeSpec(
                "alpha", "Alpha", TopDownWorldNodeKind.Settlement, 600, "independent fixture");
            var beta = new TopDownWorldNodeSpec(
                "beta", "Beta", TopDownWorldNodeKind.Settlement, 600, "independent fixture");
            var route = new TopDownWorldRouteSpec(
                "alpha",
                "beta",
                new TopDownWorldGridPoint(1, 0),
                TopDownWorldEvidenceKind.VerifiedTransition,
                "independent verified route",
                "independent resolved placement",
                36);

            var alphaCentre = new Int2(0, 0);
            var betaCentre = new Int2(400, 0);
            var building = new TopDownWorldBuildingBlockoutPlan(alphaCentre, 40, 40, 80);
            var physical = new TopDownWorldPhysicalPlan(
                new[]
                {
                    new TopDownWorldVoxelNodePlan(alpha, alphaCentre),
                    new TopDownWorldVoxelNodePlan(beta, betaCentre)
                },
                Array.Empty<TopDownWorldRegionPlan>(),
                new[]
                {
                    new TopDownWorldSettlementPlan(
                        alpha,
                        alphaCentre,
                        TopDownWorldSettlementRealizationKind.GenericBlockout,
                        new[] { building }),
                    new TopDownWorldSettlementPlan(
                        beta,
                        betaCentre,
                        TopDownWorldSettlementRealizationKind.GenericBlockout,
                        Array.Empty<TopDownWorldBuildingBlockoutPlan>())
                },
                new[]
                {
                    new TopDownWorldPhysicalRoutePlan(
                        route,
                        new[] { alphaCentre, new Int2(200, 0), betaCentre },
                        geographyConstrained: false,
                        solveSteps: 3)
                });

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => TopDownWorldPhysicalReservationAdapter.Validate(physical));
            StringAssert.Contains("spatial reservation conflict", error.Message.ToLowerInvariant());
        }

        private static VoxelWorldGenSettings Settings()
        {
            var materials = new VoxelMaterialMap(
                foundationStone: 1,
                masonry: 2,
                darkMasonry: 3,
                timber: 4,
                glass: 5,
                warmWindow: 6,
                roofTile: 7,
                slate: 8,
                cloth: 9,
                moss: 10,
                water: 11,
                roadSurface: 12);
            return new VoxelWorldGenSettings(1, materials);
        }
    }
}

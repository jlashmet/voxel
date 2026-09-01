using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class TopDownWorldConstraintRelaxationPolicyTests
    {
        [Test]
        public void GenericRouteConstraintIsStrictUnlessConsumerExplicitlyOptsIn()
        {
            var strict = new TopDownWorldRouteRegionConstraintSpec(
                "from",
                "to",
                "ridge",
                TopDownWorldRouteRegionSolutionKind.GoAround);
            var relaxed = new TopDownWorldRouteRegionConstraintSpec(
                "from",
                "to",
                "ridge",
                TopDownWorldRouteRegionSolutionKind.GoAround,
                relaxationMode: TopDownWorldConstraintRelaxationMode.EndpointEscape);

            Assert.That(strict.RelaxationMode, Is.EqualTo(TopDownWorldConstraintRelaxationMode.Strict));
            Assert.That(relaxed.RelaxationMode, Is.EqualTo(TopDownWorldConstraintRelaxationMode.EndpointEscape));
        }

        [Test]
        public void KentridgeOnlyRelaxesTheAuthoredOrcVillageRidgeShoulder()
        {
            TopDownWorldPhysicalIntentSpec intent = KentridgeTopDownWorldPhysicalIntent.Build();
            var relaxedCount = 0;

            for (var i = 0; i < intent.RouteConstraints.Count; i++)
            {
                TopDownWorldRouteRegionConstraintSpec constraint = intent.RouteConstraints[i];
                if (constraint.RelaxationMode != TopDownWorldConstraintRelaxationMode.EndpointEscape)
                    continue;

                relaxedCount++;
                Assert.That(constraint.FromId, Is.EqualTo(KentridgeTopDownWorldLayout.SouthFightingArea));
                Assert.That(constraint.ToId, Is.EqualTo(KentridgeTopDownWorldLayout.OrcVillage));
                Assert.That(constraint.RegionId, Is.EqualTo(KentridgeTopDownWorldPhysicalIntent.SouthernRidge));
                Assert.That(constraint.SolutionKind, Is.EqualTo(TopDownWorldRouteRegionSolutionKind.GoAround));
            }

            Assert.That(relaxedCount, Is.EqualTo(1),
                "Endpoint relaxation is scene policy and must stay narrowly authored rather than becoming a global planner fallback.");
        }

        [Test]
        public void GoAroundRejectsGenericSettlementApproachWhenRouteGateFallsInsideBlocker()
        {
            const string fromId = "fixture-route";
            const string settlementId = "fixture-settlement";
            const string blockerId = "fixture-ridge";
            var from = new TopDownWorldNodeSpec(fromId, "Fixture Route", TopDownWorldNodeKind.Route);
            var settlement = new TopDownWorldNodeSpec(
                settlementId,
                "Fixture Settlement",
                TopDownWorldNodeKind.Settlement);
            var route = new TopDownWorldRouteSpec(
                fromId,
                settlementId,
                new TopDownWorldGridPoint(5, 0),
                "independent endpoint-gate regression");
            var layout = new TopDownWorldLayout(
                fromId,
                1u,
                new[]
                {
                    new TopDownWorldNodePlacement(from, new TopDownWorldGridPoint(-5, 0)),
                    new TopDownWorldNodePlacement(settlement, new TopDownWorldGridPoint(0, 0))
                },
                new[] { route });
            var intent = new TopDownWorldPhysicalIntentSpec(
                new[]
                {
                    new TopDownWorldRegionSpec(
                        blockerId,
                        "Fixture Ridge",
                        TopDownWorldRegionKind.MountainRidge,
                        TopDownWorldRegionRelationKind.AnchoredAt,
                        settlementId,
                        string.Empty,
                        halfExtentXDm: 100,
                        halfExtentZDm: 100,
                        elevationDeltaDm: 0,
                        offsetXDm: -250)
                },
                new[]
                {
                    new TopDownWorldRouteRegionConstraintSpec(
                        fromId,
                        settlementId,
                        blockerId,
                        TopDownWorldRouteRegionSolutionKind.GoAround,
                        clearanceDm: 45,
                        relaxationMode: TopDownWorldConstraintRelaxationMode.EndpointEscape)
                },
                new[]
                {
                    new TopDownWorldSettlementPhysicalSpec(
                        settlementId,
                        TopDownWorldSettlementRealizationKind.GenericBlockout,
                        minimumBuildingCount: 4)
                });

            bool planned = TopDownWorldPhysicalPlanner.TryPlan(
                layout,
                intent,
                new Int2(0, 0),
                cellSizeDm: 100,
                voxelsPerDecimetre: 1,
                out _,
                out string error);

            Assert.That(planned, Is.False,
                "EndpointEscape may relax the solved route endpoint overlap, but it must not silently authorize the attached settlement approach to cross blocking geography.");
            StringAssert.Contains("leaves its authored geography solution", error);
        }

        [Test]
        public void KentridgeOrcGoAroundPlansWithSettlementArrivalOutsideSouthernRidge()
        {
            const uint seed = 0x4B454E54u;
            TopDownWorldLayout layout = MountingForceTopDownWorldDefinition.Build(seed);
            TopDownWorldPhysicalIntentSpec intent = KentridgeTopDownWorldPhysicalIntent.Build();

            bool planned = TopDownWorldPhysicalPlanner.TryPlan(
                layout,
                intent,
                new Int2(0, 0),
                MountingForceTopDownWorldDefinition.CellSizeDm,
                voxelsPerDecimetre: 1,
                out TopDownWorldPhysicalPlan physical,
                out string error);

            Assert.That(planned, Is.True,
                "Kentridge composition must keep the Orc settlement arrival gate on the settlement side of Southern Ridge so GoAround remains a real dry detour. " + error);
            Assert.That(
                physical.TryGetRegion(KentridgeTopDownWorldPhysicalIntent.SouthernRidge, out TopDownWorldRegionPlan ridge),
                Is.True);
            Assert.That(
                physical.TryGetRoute(
                    KentridgeTopDownWorldLayout.SouthFightingArea,
                    KentridgeTopDownWorldLayout.OrcVillage,
                    out TopDownWorldPhysicalRoutePlan orcRoute),
                Is.True);

            int margin = orcRoute.Route.CorridorWidthDm / 2;
            for (var i = 0; i < orcRoute.Tiles.Count; i++)
            {
                Assert.That(
                    ridge.Contains(orcRoute.Tiles[i], -margin),
                    Is.False,
                    "The final Orc road corridor must skirt Southern Ridge; EndpointEscape cannot become an implicit crossing after settlement approaches are attached.");
            }
        }
    }
}

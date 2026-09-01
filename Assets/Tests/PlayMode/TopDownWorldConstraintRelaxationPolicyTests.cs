using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
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
    }
}

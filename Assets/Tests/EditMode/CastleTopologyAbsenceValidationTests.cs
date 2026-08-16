using NUnit.Framework;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleTopologyAbsenceValidationTests
    {
        [Test]
        public void TopologyValidatorRejectsPosternRecipeWhenPosternIsAbsent()
        {
            CastleTopologyPlan plan = CastleLayoutPlanner.Create(29u);
            plan.HasPosternGate = false;
            plan.PosternDoor = CastleWallDoorPlanner.Postern();

            Assert.IsFalse(
                CastleTopologyPlanValidator.TryValidate(
                    in plan, out CastleTopologyPlanIssue issue));
            Assert.AreEqual(CastleTopologyPlanIssue.UnexpectedPosternDoorPlan, issue);
        }

        [Test]
        public void TopologyValidatorRejectsInnerWardRecipeForSingleWard()
        {
            CastleTopologyPlan plan = CastleLayoutPlanner.Create(31u);
            plan.Perimeter = CastlePerimeterKind.Rectangular;
            plan.Wards = CastleWardPattern.SingleWard;
            plan.InnerWardDoor = CastleWallDoorPlanner.InnerWard();

            Assert.IsFalse(
                CastleTopologyPlanValidator.TryValidate(
                    in plan, out CastleTopologyPlanIssue issue));
            Assert.AreEqual(CastleTopologyPlanIssue.UnexpectedInnerWardDoorPlan, issue);
        }
    }
}

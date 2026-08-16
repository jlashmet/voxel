using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleDungeonPreflightTests
    {
        [Test]
        public void AttachingDesignedDungeonChangesSpatialAdmissionCost()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 137u);
            var topology = new CastleTopologyPlan
            {
                Perimeter = CastlePerimeterKind.Rectangular,
                KeepPlacement = CastleKeepPlacement.Central,
                Wards = CastleWardPattern.SingleWard,
                DesiredTowerCount = 4,
                HasPosternGate = false,
            };

            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);
            CastleSpatialPlan buildingsOnly = CastleSpatialPlanCompletion.AttachCourtyardBuildings(
                in dimensions, spatial);
            CastleSpatialPlan completed = CastleSpatialPlanCompletion.AttachDungeon(
                in dimensions, buildingsOnly);

            Assert.IsNull(buildingsOnly.Dungeon);
            Assert.NotNull(completed.Dungeon);
            Assert.IsTrue(DungeonPlanValidator.TryValidate(
                completed.Dungeon, out DungeonPlanIssue issue), issue.ToString());

            long legacyFallback = CastleBuildPreflight.EstimateWrites(
                in dimensions, buildingsOnly);
            long planned = CastleBuildPreflight.EstimateWrites(
                in dimensions, completed);

            Assert.Greater(planned, legacyFallback,
                "Attaching this castle's designed dungeon should raise admission cost above the " +
                "historical flat underground fallback, proving preflight consumes the room graph.");
        }
    }
}

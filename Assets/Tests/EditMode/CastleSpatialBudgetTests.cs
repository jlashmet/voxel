using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleSpatialBudgetTests
    {
        [Test]
        public void GeneratedSpatialPlansFitDefaultRuntimeAdmissionBudget()
        {
            for (uint seed = 1; seed <= 256; seed++)
            {
                CastlePlan plan = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);

                CastleBuildPreflightResult result = CastleBuildPreflight.Evaluate(
                    in plan, spatial, VoxelBrush.DefaultWriteBudget);

                Assert.IsTrue(result.IsValid,
                    $"seed {seed}: {result.Issue} / {result.PlanIssue} / " +
                    $"{result.SpatialPlanIssue}, estimate {result.EstimatedWrites:N0}");
                Assert.LessOrEqual(result.EstimatedWrites, VoxelBrush.DefaultWriteBudget,
                    $"seed {seed}: generated topology exceeds runtime admission budget");
            }
        }
    }
}

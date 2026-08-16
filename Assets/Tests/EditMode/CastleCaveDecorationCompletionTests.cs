using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleCaveDecorationCompletionTests
    {
        [Test]
        public void CompletedCaveExitCarriesValidatedDecorationPlan()
        {
            bool foundCaveExit = false;

            for (uint seed = 1; seed <= 256; seed++)
            {
                CastlePlan plan = CastlePlanner.Create(new int3(256, 220, 376), seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
                if (spatial.KeepRequiresTerrainResolution)
                    spatial = CastleSpatialPlanner.ResolveHighestGroundKeep(in plan, spatial, int2.zero);

                CastleSpatialPlan completed = CastleSpatialPlanCompletion.CompleteResolved(
                    in plan, spatial);
                if (!completed.Dungeon.HasCaveExit)
                {
                    Assert.IsNull(completed.Cave);
                    Assert.IsNull(completed.CaveDecoration);
                    continue;
                }

                foundCaveExit = true;
                Assert.NotNull(completed.Cave);
                Assert.NotNull(completed.CaveDecoration);
                Assert.IsTrue(
                    CastleCaveDecorationPlanValidator.TryValidate(
                        completed.Cave,
                        completed.CaveDecoration,
                        out CastleCaveDecorationPlanIssue issue),
                    $"seed {seed}: {issue}");
            }

            Assert.IsTrue(foundCaveExit,
                "Expected the dungeon seed stream to produce at least one cave exit.");
        }

        [Test]
        public void RuntimeReadyPreflightRejectsMissingDecorationForCaveExit()
        {
            bool foundCaveExit = false;

            for (uint seed = 1; seed <= 256 && !foundCaveExit; seed++)
            {
                CastlePlan plan = CastlePlanner.Create(new int3(256, 220, 376), seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
                if (spatial.KeepRequiresTerrainResolution)
                    spatial = CastleSpatialPlanner.ResolveHighestGroundKeep(in plan, spatial, int2.zero);

                CastleSpatialPlan completed = CastleSpatialPlanCompletion.CompleteResolved(
                    in plan, spatial);
                if (!completed.Dungeon.HasCaveExit) continue;
                foundCaveExit = true;

                // Reattaching the same cave deliberately invalidates downstream decoration while
                // preserving the rest of the completed semantic plan.
                CastleSpatialPlan withoutDecoration = CastleSpatialPlanCompletion.AttachCave(
                    in plan, completed);
                Assert.NotNull(withoutDecoration.Cave);
                Assert.IsNull(withoutDecoration.CaveDecoration);

                CastleBuildPreflightResult result = CastleBuildPreflight.EvaluateRuntimeReady(
                    in plan, withoutDecoration, long.MaxValue);
                Assert.AreEqual(CastleBuildPreflightIssue.IncompleteSpatialPlan, result.Issue);
                Assert.AreEqual(
                    CastleSpatialBuildReadinessIssue.MissingCaveDecorationPlan,
                    result.ReadinessIssue);
            }

            Assert.IsTrue(foundCaveExit,
                "Expected the dungeon seed stream to produce at least one cave exit.");
        }
    }
}

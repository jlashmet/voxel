using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleLandscapePlannerTests
    {
        [Test]
        public void PlannerIsDeterministicAndProducesValidDecorations()
        {
            for (uint seed = 1; seed <= 128; seed++)
            {
                CastlePlan plan = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                topology.KeepPlacement = CastleKeepPlacement.Central;
                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
                CastleApproachFrame approach = CastleApproachFrame.FromGate(in spatial.PrimaryGate);

                CastleLandscapePlan first = CastleLandscapePlanner.Create(
                    in plan, spatial.OuterWardVertices, in approach);
                CastleLandscapePlan second = CastleLandscapePlanner.Create(
                    in plan, spatial.OuterWardVertices, in approach);

                Assert.IsTrue(CastleLandscapePlanValidator.TryValidate(
                    first, out CastleLandscapePlanIssue issue), $"seed {seed}: {issue}");
                Assert.AreEqual(first.Decorations.Length, second.Decorations.Length);
                for (int i = 0; i < first.Decorations.Length; i++)
                {
                    Assert.AreEqual(first.Decorations[i].Id, second.Decorations[i].Id);
                    Assert.AreEqual(first.Decorations[i].Kind, second.Decorations[i].Kind);
                    Assert.AreEqual(first.Decorations[i].Centre, second.Decorations[i].Centre);
                    Assert.AreEqual(first.Decorations[i].Radius, second.Decorations[i].Radius);
                    Assert.AreEqual(first.Decorations[i].Height, second.Decorations[i].Height);
                    Assert.AreEqual(first.Decorations[i].Size, second.Decorations[i].Size);
                }
            }
        }

        [Test]
        public void CompletionAttachesRuntimeReadyLandscape()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 509u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(509u);
            topology.KeepPlacement = CastleKeepPlacement.Central;
            CastleSpatialPlan raw = CastleSpatialPlanner.Create(in plan, in topology);

            CastleSpatialPlan completed = CastleSpatialPlanCompletion.CompleteResolved(
                in plan, raw);

            Assert.NotNull(completed.Landscape);
            Assert.IsTrue(CastleLandscapePlanValidator.TryValidate(
                completed.Landscape, out CastleLandscapePlanIssue issue), issue.ToString());
        }
    }
}

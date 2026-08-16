using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleLandscapeBuildBoundsTests
    {
        [Test]
        public void CastleBoundsIncludeActualPlannedLandscapeEnvelope()
        {
            CastlePlan plan = CastlePlanner.Create(new int3(256, 220, 376), 151u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(151u);
            topology.KeepPlacement = CastleKeepPlacement.Central;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
            spatial = CastleGatehousePlanCompletion.Attach(in plan, spatial);
            spatial = CastleSpatialPlanCompletion.CompleteResolved(in plan, spatial);

            CastleLandscapeBuildBounds landscapeBounds =
                CastleLandscapeBuildBoundsResolver.Resolve(in plan, spatial.Landscape);
            CastleBuildBounds castleBounds = CastleBuildBoundsResolver.Resolve(in plan, spatial);

            Assert.IsTrue(castleBounds.Contains(landscapeBounds.Min));
            Assert.IsTrue(castleBounds.Contains(landscapeBounds.MaxExclusive - 1));
        }

        [Test]
        public void CastleBoundsFollowLandscapePlanBeyondGenericSafetyHalo()
        {
            CastlePlan plan = CastlePlanner.Create(new int3(256, 220, 376), 157u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(157u);
            topology.KeepPlacement = CastleKeepPlacement.Central;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
            spatial = CastleGatehousePlanCompletion.Attach(in plan, spatial);
            spatial = CastleSpatialPlanCompletion.CompleteResolved(in plan, spatial);

            CastleLandscapeDecorationSpec[] decorations = spatial.Landscape.Decorations;
            Assert.Greater(decorations.Length, 0);
            CastleLandscapeDecorationSpec moved = decorations[0];
            moved.Centre = new int2(plan.PlateauRadius + plan.CliffDrop + 1200, 900);
            decorations[0] = moved;

            Assert.IsTrue(
                CastleLandscapePlanValidator.TryValidate(
                    spatial.Landscape, out CastleLandscapePlanIssue issue),
                issue.ToString());

            CastleLandscapeBuildBounds landscapeBounds =
                CastleLandscapeBuildBoundsResolver.Resolve(in plan, spatial.Landscape);
            CastleBuildBounds castleBounds = CastleBuildBoundsResolver.Resolve(in plan, spatial);

            Assert.IsTrue(castleBounds.Contains(landscapeBounds.Min),
                "Castle dependency bounds ignored the moved planned landscape minimum.");
            Assert.IsTrue(castleBounds.Contains(landscapeBounds.MaxExclusive - 1),
                "Castle dependency bounds ignored the moved planned landscape maximum.");
        }
    }
}

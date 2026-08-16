using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleLandscapePlanValidatorTests
    {
        [Test]
        public void ValidatorRejectsDecorationIdDrift()
        {
            CastleLandscapePlan landscape = CreatePlan(17u);
            CastleLandscapeDecorationSpec changed = landscape.Decorations[0];
            changed.Id = 99;
            landscape.Decorations[0] = changed;

            Assert.IsFalse(
                CastleLandscapePlanValidator.TryValidate(
                    landscape, out CastleLandscapePlanIssue issue));
            Assert.AreEqual(CastleLandscapePlanIssue.DecorationIdMismatch, issue);
        }

        [Test]
        public void ValidatorRejectsInvalidConeDimensions()
        {
            CastleLandscapePlan landscape = CreatePlan(23u);
            int index = FindCone(landscape);
            CastleLandscapeDecorationSpec changed = landscape.Decorations[index];
            changed.Radius = 0;
            landscape.Decorations[index] = changed;

            Assert.IsFalse(
                CastleLandscapePlanValidator.TryValidate(
                    landscape, out CastleLandscapePlanIssue issue));
            Assert.AreEqual(CastleLandscapePlanIssue.InvalidConeDimensions, issue);
        }

        [Test]
        public void ValidatorRejectsInvalidRubbleDimensions()
        {
            CastleLandscapePlan landscape = CreatePlan(29u);
            int index = FindRubble(landscape);
            CastleLandscapeDecorationSpec changed = landscape.Decorations[index];
            changed.Size.y = 0;
            landscape.Decorations[index] = changed;

            Assert.IsFalse(
                CastleLandscapePlanValidator.TryValidate(
                    landscape, out CastleLandscapePlanIssue issue));
            Assert.AreEqual(CastleLandscapePlanIssue.InvalidRubbleDimensions, issue);
        }

        private static CastleLandscapePlan CreatePlan(uint seed)
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, seed);
            int2[] perimeter =
            {
                new int2(-plan.BaileyHalfX, -plan.BaileyHalfZ),
                new int2( plan.BaileyHalfX, -plan.BaileyHalfZ),
                new int2( plan.BaileyHalfX,  plan.BaileyHalfZ),
                new int2(-plan.BaileyHalfX,  plan.BaileyHalfZ),
            };
            var gate = new CastleGatePlacementSpec
            {
                EdgeIndex = 0,
                Centre = new int2(0, -plan.BaileyHalfZ),
                Outward = new float2(0f, -1f),
            };
            CastleApproachFrame approach = CastleApproachFrame.FromGate(in gate);
            return CastleLandscapePlanner.Create(in plan, perimeter, in approach);
        }

        private static int FindCone(CastleLandscapePlan landscape)
        {
            for (int i = 0; i < landscape.Decorations.Length; i++)
            {
                CastleLandscapeDecorationKind kind = landscape.Decorations[i].Kind;
                if (kind == CastleLandscapeDecorationKind.PerimeterMossShrub ||
                    kind == CastleLandscapeDecorationKind.PerimeterGrassShrub ||
                    kind == CastleLandscapeDecorationKind.ApproachDarkStoneRock ||
                    kind == CastleLandscapeDecorationKind.ApproachStoneRock ||
                    kind == CastleLandscapeDecorationKind.ApproachMossScrub)
                    return i;
            }

            Assert.Fail("Landscape planner produced no cone decoration.");
            return -1;
        }

        private static int FindRubble(CastleLandscapePlan landscape)
        {
            for (int i = 0; i < landscape.Decorations.Length; i++)
            {
                CastleLandscapeDecorationKind kind = landscape.Decorations[i].Kind;
                if (kind == CastleLandscapeDecorationKind.PerimeterStoneRubble ||
                    kind == CastleLandscapeDecorationKind.PerimeterDarkStoneRubble)
                    return i;
            }

            Assert.Fail("Landscape planner produced no rubble decoration.");
            return -1;
        }
    }
}

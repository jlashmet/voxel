using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleLandscapePlanningTests
    {
        [Test]
        public void PlannerIsDeterministicAndValidAcrossSeeds()
        {
            for (uint seed = 1; seed <= 128; seed++)
            {
                CastlePlan plan = CastlePlanner.Create(int3.zero, seed);
                int2[] perimeter = Rectangle(in plan);
                CastleGatePlacementSpec gate = FrontGate(in plan);
                CastleApproachFrame approach = CastleApproachFrame.FromGate(in gate);

                CastleLandscapePlan first = CastleLandscapePlanner.Create(
                    in plan, perimeter, in approach);
                CastleLandscapePlan second = CastleLandscapePlanner.Create(
                    in plan, perimeter, in approach);

                Assert.IsTrue(
                    CastleLandscapePlanValidator.TryValidate(
                        first, out CastleLandscapePlanIssue issue),
                    $"seed {seed}: {issue}");
                Assert.AreEqual(first.Decorations.Length, second.Decorations.Length,
                    $"seed {seed}: deterministic decoration count drifted");

                for (int i = 0; i < first.Decorations.Length; i++)
                {
                    CastleLandscapeDecorationSpec a = first.Decorations[i];
                    CastleLandscapeDecorationSpec b = second.Decorations[i];
                    Assert.AreEqual(a.Id, b.Id, $"seed {seed}, decoration {i}: id");
                    Assert.AreEqual(a.Kind, b.Kind, $"seed {seed}, decoration {i}: kind");
                    Assert.AreEqual(a.Centre, b.Centre, $"seed {seed}, decoration {i}: centre");
                    Assert.AreEqual(a.Radius, b.Radius, $"seed {seed}, decoration {i}: radius");
                    Assert.AreEqual(a.Height, b.Height, $"seed {seed}, decoration {i}: height");
                    Assert.AreEqual(a.Size, b.Size, $"seed {seed}, decoration {i}: size");
                }
            }
        }

        [Test]
        public void PlannerPreservesApproachDecorationsInGateFrame()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 41u);
            int2[] perimeter = Rectangle(in plan);
            var gate = new CastleGatePlacementSpec
            {
                EdgeIndex = 1,
                Centre = new int2(plan.BaileyHalfX, 0),
                Outward = new float2(1f, 0f),
            };
            CastleApproachFrame approach = CastleApproachFrame.FromGate(in gate);

            CastleLandscapePlan landscape = CastleLandscapePlanner.Create(
                in plan, perimeter, in approach);

            bool foundApproachDecoration = false;
            for (int i = 0; i < landscape.Decorations.Length; i++)
            {
                CastleLandscapeDecorationSpec decoration = landscape.Decorations[i];
                if (decoration.Kind != CastleLandscapeDecorationKind.ApproachDarkStoneRock &&
                    decoration.Kind != CastleLandscapeDecorationKind.ApproachStoneRock &&
                    decoration.Kind != CastleLandscapeDecorationKind.ApproachMossScrub)
                    continue;

                foundApproachDecoration = true;
                float2 relative = new float2(
                    decoration.Centre.x - gate.Centre.x,
                    decoration.Centre.y - gate.Centre.y);
                Assert.Greater(math.dot(relative, approach.Outward), 0f,
                    $"approach decoration {decoration.Id} was placed behind the rotated gate");
            }

            Assert.IsTrue(foundApproachDecoration);
        }

        private static int2[] Rectangle(in CastlePlan plan) =>
            new[]
            {
                new int2(-plan.BaileyHalfX, -plan.BaileyHalfZ),
                new int2( plan.BaileyHalfX, -plan.BaileyHalfZ),
                new int2( plan.BaileyHalfX,  plan.BaileyHalfZ),
                new int2(-plan.BaileyHalfX,  plan.BaileyHalfZ),
            };

        private static CastleGatePlacementSpec FrontGate(in CastlePlan plan) =>
            new CastleGatePlacementSpec
            {
                EdgeIndex = 0,
                Centre = new int2(0, -plan.BaileyHalfZ),
                Outward = new float2(0f, -1f),
            };
    }
}

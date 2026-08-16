using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CaveBuildBoundsTests
    {
        [Test]
        public void BoundsContainEveryPlannedChamberAndPassageEnvelope()
        {
            var constraints = new CavePlanningConstraints
            {
                Entrance = new int3(120, 64, -80),
                EntranceToMainOffset = new int3(0, 21, 0),
                MainRadii = new int3(34, 22, 42),
                SecondaryChamberCount = 4,
                SecondaryMinRadii = new int3(16, 11, 18),
                SecondaryMaxRadii = new int3(27, 19, 31),
                MinimumHorizontalSpread = 44,
                MaximumHorizontalSpread = 92,
                VerticalSpread = 13,
                PassageWidth = 16,
                PassageHeight = 20,
            };
            CavePlan plan = CavePlanner.Create(73u, in constraints);
            CaveBuildBounds bounds = CaveBuildBoundsResolver.Resolve(plan);

            for (int i = 0; i < plan.Chambers.Length; i++)
            {
                CaveChamberPlan chamber = plan.Chambers[i];
                Assert.IsTrue(bounds.Contains(chamber.Centre), $"chamber {i} centre escaped bounds");
                Assert.IsTrue(bounds.Contains(chamber.Centre - chamber.Radii),
                    $"chamber {i} minimum escaped bounds");
                Assert.IsTrue(bounds.Contains(chamber.Centre + chamber.Radii),
                    $"chamber {i} maximum escaped bounds");
            }

            for (int i = 0; i < plan.Passages.Length; i++)
            {
                CavePassagePlan passage = plan.Passages[i];
                int3 from = plan.Chambers[passage.FromChamberId].Centre;
                int3 to = plan.Chambers[passage.ToChamberId].Centre;
                int3 midpoint = new int3(
                    (from.x + to.x) / 2,
                    (from.y + to.y) / 2,
                    (from.z + to.z) / 2);
                int radius = math.max(1, passage.Width / 2);
                int halfHeight = math.max(1, passage.Height / 2);

                Assert.IsTrue(bounds.Contains(midpoint + new int3(radius, halfHeight, radius)),
                    $"passage {i} positive carve envelope escaped bounds");
                Assert.IsTrue(bounds.Contains(midpoint - new int3(radius, halfHeight, radius)),
                    $"passage {i} negative carve envelope escaped bounds");
            }
        }
    }
}

using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CaveRealizerTests
    {
        [Test]
        public void PlannedCaveCarvesConnectedVoidUsingOnlyBulkColumns()
        {
            var table = new RegionTable(8, Allocator.Persistent);
            var pool = new BrickPool(16384, Allocator.Persistent);

            try
            {
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);

                // Start from authored solid rock so empty samples prove that the cave actually
                // carved storage rather than merely reading the default empty world.
                var rock = new VoxelBrush(reads, mutations, writeBudget: 2_000_000);
                rock.FillBulk(new int3(8, 20, 8), new int3(220, 120, 220), Mat.Stone);

                CavePlanningConstraints constraints = new CavePlanningConstraints
                {
                    Entrance = new int3(110, 72, 110),
                    EntranceToMainOffset = new int3(0, 8, 0),
                    MainRadii = new int3(24, 18, 28),
                    SecondaryChamberCount = 2,
                    SecondaryMinRadii = new int3(16, 12, 18),
                    SecondaryMaxRadii = new int3(22, 16, 24),
                    MinimumHorizontalSpread = 48,
                    MaximumHorizontalSpread = 68,
                    VerticalSpread = 8,
                    PassageWidth = 14,
                    PassageHeight = 18,
                };
                CavePlan plan = CavePlanner.Create(41u, in constraints);

                var carve = new VoxelBrush(reads, mutations, writeBudget: 1);
                CaveRealizer.Build(ref carve, plan);

                for (int i = 0; i < plan.Chambers.Length; i++)
                {
                    int3 centre = plan.Chambers[i].Centre;
                    Assert.AreEqual(Mat.Empty, carve.Get(centre.x, centre.y, centre.z),
                        $"chamber {i} centre was not carved");
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
                    Assert.AreEqual(Mat.Empty, carve.Get(midpoint.x, midpoint.y, midpoint.z),
                        $"passage {i} midpoint was not carved");
                }

                Assert.AreEqual(0, carve.VoxelsWritten,
                    "Generic cave realization must remain on batched column writes.");
                Assert.Greater(carve.BulkVoxelsWritten, 0);
                Assert.IsFalse(carve.BudgetExceeded);
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        [Test]
        public void RotatedNonCircularChamberCarvesAndBoundsItsProjectedLongAxis()
        {
            var table = new RegionTable(8, Allocator.Persistent);
            var pool = new BrickPool(16384, Allocator.Persistent);

            try
            {
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                var rock = new VoxelBrush(reads, mutations, writeBudget: 2_000_000);
                rock.FillBulk(new int3(8, 20, 8), new int3(220, 120, 220), Mat.Stone);

                CavePlanningConstraints constraints = new CavePlanningConstraints
                {
                    Entrance = new int3(110, 72, 110),
                    EntranceToMainOffset = new int3(0, 8, 0),
                    MainRadii = new int3(24, 18, 28),
                    SecondaryChamberCount = 1,
                    SecondaryMinRadii = new int3(12, 8, 14),
                    SecondaryMaxRadii = new int3(18, 12, 20),
                    MinimumHorizontalSpread = 48,
                    MaximumHorizontalSpread = 52,
                    VerticalSpread = 0,
                    PassageWidth = 14,
                    PassageHeight = 18,
                };
                CavePlan plan = CavePlanner.Create(73u, in constraints);
                CaveChamberPlan chamber = plan.Chambers[1];
                chamber.Radii = new int3(8, 6, 24);
                chamber.RotationRadians = math.PI * 0.5f;
                plan.Chambers[1] = chamber;

                Assert.IsTrue(CavePlanValidator.TryValidate(plan, out CavePlanIssue issue),
                    issue.ToString());

                int3 longAxisSample = chamber.Centre + new int3(20, 0, 0);
                CaveBuildBounds bounds = CaveBuildBoundsResolver.Resolve(plan);
                Assert.IsTrue(bounds.Contains(longAxisSample),
                    "Dependency bounds clipped the rotated chamber's projected long axis.");

                var carve = new VoxelBrush(reads, mutations, writeBudget: 1);
                CaveRealizer.Build(ref carve, plan);

                Assert.AreEqual(Mat.Empty,
                    carve.Get(longAxisSample.x, longAxisSample.y, longAxisSample.z),
                    "Runtime clipped the rotated chamber at the unrotated X radius.");
                Assert.AreEqual(0, carve.VoxelsWritten);
                Assert.Greater(carve.BulkVoxelsWritten, 0);
                Assert.IsFalse(carve.BudgetExceeded);
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }
    }
}

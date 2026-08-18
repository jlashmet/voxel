using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Structures.Api;
using Game.Structures.Runtime;   // CastleBuilder became CastlePlanner in the game layer
using Game.Structures.Api;
using Mat = Game.Materials.Api.GameMaterialIds;   // engine-side Mat constants were removed

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleBuilderTests
    {
        [Test]
        public void PlanHeightMatchesItsFloorStack()
        {
            for (uint seed = 1; seed <= 64; seed++)
            {
                var plan = CastlePlanner.Plan(int3.zero, seed);
                Assert.AreEqual(plan.Floors * plan.FloorHeight, plan.KeepHeight,
                    $"Seed {seed} produced a keep shell that disagrees with its floors.");
            }
        }

        [Test]
        public void PlanIsApproximatelyDoubleTheFormerFootprintWithoutExceedingPreflightBudget()
        {
            for (uint seed = 1; seed <= 64; seed++)
            {
                var plan = CastlePlanner.Plan(int3.zero, seed);

                // The former minimum bailey was 300 x 300 voxels. A 440 x 440 minimum is 2.15
                // times that area while retaining the same 10 cm voxel/player scale.
                Assert.GreaterOrEqual(plan.BaileyHalfX * 2, 440, $"seed {seed}: bailey width");
                Assert.GreaterOrEqual(plan.BaileyHalfZ * 2, 440, $"seed {seed}: bailey depth");
                Assert.GreaterOrEqual(plan.KeepHalfX * 2, 184, $"seed {seed}: keep width");
                Assert.GreaterOrEqual(plan.Floors, 5, $"seed {seed}: floor count");
                Assert.LessOrEqual(CastlePlanner.EstimateWrites(in plan),
                    VoxelBrush.DefaultWriteBudget,
                    $"seed {seed} would be rejected before construction");
            }
        }

        [Test]
        public void SpiralStairUsesWalkableRisesAndMaintainsPlayerHeadroom()
        {
            var table = new RegionTable(4, Allocator.Persistent);
            var pool = new BrickPool(256, Allocator.Persistent);

            try
            {
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                var brush = new VoxelBrush(reads, mutations, writeBudget: 500_000);
                const int cx = 32, cz = 32, baseY = 2, radius = 12, height = 38;
                brush.SpiralStair(cx, baseY, cz, radius, height, Mat.Stone);

                const int rise = 2;
                const int run = 3;
                int innerRadius = math.max(2, radius - 10);
                float walkingRadius = (innerRadius + radius) * 0.5f;
                float anglePerStep = run / walkingRadius;
                int steps = height / rise;

                for (int step = 0; step < steps; step++)
                {
                    float angle = (step + 0.4f) * anglePerStep;
                    int x = cx + (int)math.round(math.cos(angle) * walkingRadius);
                    int z = cz + (int)math.round(math.sin(angle) * walkingRadius);
                    int y = baseY + step * rise;

                    Assert.AreEqual(Mat.Stone, brush.Get(x, y, z), $"missing tread {step}");
                    Assert.AreEqual(Mat.Stone, brush.Get(x, y + 1, z), $"thin tread {step}");

                    // CharacterMotor is 18 voxels tall. Sample the centre of the walking line;
                    // the full-volume play-mode test below verifies the actor-sized route.
                    for (int h = 2; h < 18; h++)
                        Assert.AreEqual(Mat.Empty, brush.Get(x, y + h, z),
                            $"tread {step} has no headroom at +{h}");
                }

                Assert.IsFalse(brush.BudgetExceeded);
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        [Test]
        public void BulkColumnPreservesNeighboursAndCollapsesUniformBricks()
        {
            var table = new RegionTable(4, Allocator.Persistent);
            var pool = new BrickPool(32, Allocator.Persistent);

            try
            {
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                var brush = new VoxelBrush(reads, mutations, writeBudget: 1);

                for (int z = 0; z < VoxelDimensions.BrickEdge; z++)
                for (int x = 0; x < VoxelDimensions.BrickEdge; x++)
                    brush.FillColumnBulk(x, 0, VoxelDimensions.BrickEdge, z, Mat.Stone);

                Assert.AreEqual(Mat.Stone, brush.Get(3, 4, 5));
                Assert.AreEqual(Mat.Empty, brush.Get(8, 4, 5),
                    "A column batch must not overwrite the neighbouring brick.");
                Assert.AreEqual(0, pool.AllocatedCount,
                    "A completely filled brick must collapse back to a uniform reference.");
                Assert.IsFalse(brush.BudgetExceeded,
                    "Batched column writes must not consume the slow per-voxel budget.");
                Assert.AreEqual(512, brush.BulkVoxelsWritten);
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }
    }
}

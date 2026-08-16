using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleBuilderTests
    {
        [Test]
        public void PlannerHeightMatchesItsFloorStack()
        {
            for (uint seed = 1; seed <= 64; seed++)
            {
                var plan = CastlePlanner.Create(int3.zero, seed);
                Assert.AreEqual(plan.Floors * plan.FloorHeight, plan.KeepHeight,
                    $"Seed {seed} produced a keep shell that disagrees with its floors.");
            }
        }

        [Test]
        public void PlannerIsApproximatelyDoubleTheFormerFootprintWithoutExceedingPreflightBudget()
        {
            for (uint seed = 1; seed <= 64; seed++)
            {
                var plan = CastlePlanner.Create(int3.zero, seed);

                // The former minimum bailey was 300 x 300 voxels. A 440 x 440 minimum is 2.15
                // times that area while retaining the same 10 cm voxel/player scale.
                Assert.GreaterOrEqual(plan.BaileyHalfX * 2, 440, $"seed {seed}: bailey width");
                Assert.GreaterOrEqual(plan.BaileyHalfZ * 2, 440, $"seed {seed}: bailey depth");
                Assert.GreaterOrEqual(plan.KeepHalfX * 2, 184, $"seed {seed}: keep width");
                Assert.GreaterOrEqual(plan.Floors, 5, $"seed {seed}: floor count");
                Assert.LessOrEqual(CastleBuildPreflight.EstimateWrites(in plan),
                    VoxelBrush.DefaultWriteBudget,
                    $"seed {seed} would be rejected before construction");
            }
        }

        [Test]
        public void PlannerProducesStructurallyValidPlans()
        {
            for (uint seed = 1; seed <= 256; seed++)
            {
                var plan = CastlePlanner.Create(new int3((int)seed * 3, 64, -(int)seed * 2), seed);
                Assert.IsTrue(CastlePlanValidator.TryValidate(in plan, out CastlePlanIssue issue),
                    $"Seed {seed} produced invalid castle plan: {issue}");
                Assert.AreEqual(CastlePlanIssue.None, issue);
            }
        }

        [Test]
        public void CompatibilityFacadePlanDelegatesToPlanner()
        {
            for (uint seed = 1; seed <= 256; seed++)
            {
                var centre = new int3((int)seed * 3, 64, -(int)seed * 2);
                var planned = CastlePlanner.Create(centre, seed);
                var compatibility = CastleBuilder.Plan(centre, seed);
                AssertPlansEqual(in compatibility, in planned, seed);
            }
        }

        [Test]
        public void CompatibilityFacadeEstimateDelegatesToPreflight()
        {
            for (uint seed = 1; seed <= 256; seed++)
            {
                var plan = CastlePlanner.Create(int3.zero, seed);
                Assert.AreEqual(
                    CastleBuilder.EstimateWrites(in plan),
                    CastleBuildPreflight.EstimateWrites(in plan),
                    $"Seed {seed} changed the compatibility estimate.");
            }
        }

        [Test]
        public void CompatibilityFacadeDefaultBuildHandleRemainsNoOp()
        {
            var build = default(CastleBuilder.IncrementalBuild);

            Assert.IsFalse(build.IsCreated);
            Assert.IsFalse(build.IsComplete);
            Assert.AreEqual(0, build.StageNumber);
            Assert.AreEqual(0L, build.TotalVoxelsWritten);
            Assert.IsTrue(CastleBuilder.StepBuild(ref build));
        }

        [Test]
        public void PreflightRejectsStructurallyInvalidPlan()
        {
            var plan = CastlePlanner.Create(int3.zero, 19u);
            plan.KeepHeight++;

            CastleBuildPreflightResult result = CastleBuildPreflight.Evaluate(
                in plan, VoxelBrush.DefaultWriteBudget);

            Assert.AreEqual(CastleBuildPreflightIssue.InvalidPlan, result.Issue);
            Assert.AreEqual(CastlePlanIssue.KeepFloorStackMismatch, result.PlanIssue);
            Assert.IsFalse(result.IsValid);
        }

        [Test]
        public void PreflightRejectsPlanAboveWriteBudget()
        {
            var plan = CastlePlanner.Create(int3.zero, 23u);
            long estimate = CastleBuildPreflight.EstimateWrites(in plan);

            CastleBuildPreflightResult result = CastleBuildPreflight.Evaluate(
                in plan, estimate - 1);

            Assert.AreEqual(CastleBuildPreflightIssue.WriteBudgetExceeded, result.Issue);
            Assert.AreEqual(estimate, result.EstimatedWrites);
            Assert.IsFalse(result.IsValid);
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

        private static void AssertPlansEqual(in CastlePlan expected, in CastlePlan actual, uint seed)
        {
            Assert.AreEqual(expected.Centre, actual.Centre, $"seed {seed}: centre");
            Assert.AreEqual(expected.Seed, actual.Seed, $"seed {seed}: seed");
            Assert.AreEqual(expected.PlateauRadius, actual.PlateauRadius, $"seed {seed}: plateau radius");
            Assert.AreEqual(expected.PlateauHeight, actual.PlateauHeight, $"seed {seed}: plateau height");
            Assert.AreEqual(expected.CliffDrop, actual.CliffDrop, $"seed {seed}: cliff drop");
            Assert.AreEqual(expected.BaileyHalfX, actual.BaileyHalfX, $"seed {seed}: bailey X");
            Assert.AreEqual(expected.BaileyHalfZ, actual.BaileyHalfZ, $"seed {seed}: bailey Z");
            Assert.AreEqual(expected.WallHeight, actual.WallHeight, $"seed {seed}: wall height");
            Assert.AreEqual(expected.WallThickness, actual.WallThickness, $"seed {seed}: wall thickness");
            Assert.AreEqual(expected.TowerRadius, actual.TowerRadius, $"seed {seed}: tower radius");
            Assert.AreEqual(expected.TowerHeight, actual.TowerHeight, $"seed {seed}: tower height");
            Assert.AreEqual(expected.GateTowerRadius, actual.GateTowerRadius, $"seed {seed}: gate tower radius");
            Assert.AreEqual(expected.GateTowerHeight, actual.GateTowerHeight, $"seed {seed}: gate tower height");
            Assert.AreEqual(expected.KeepHalfX, actual.KeepHalfX, $"seed {seed}: keep X");
            Assert.AreEqual(expected.KeepHalfZ, actual.KeepHalfZ, $"seed {seed}: keep Z");
            Assert.AreEqual(expected.KeepHeight, actual.KeepHeight, $"seed {seed}: keep height");
            Assert.AreEqual(expected.FloorHeight, actual.FloorHeight, $"seed {seed}: floor height");
            Assert.AreEqual(expected.Floors, actual.Floors, $"seed {seed}: floors");
        }
    }
}

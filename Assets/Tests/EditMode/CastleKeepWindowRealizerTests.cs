using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepWindowRealizerTests
    {
        [Test]
        public void PlannedWindowsMatchLegacyWindowSubstageOnProjectedKeep()
        {
            CastlePlan plan = CastlePlanner.Create(new int3(256, 64, 256), 37u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(37u);
            topology.KeepPlacement = CastleKeepPlacement.Central;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
            spatial = CastleSpatialPlanCompletion.CompleteResolved(in plan, spatial);
            CastleSpatialProjection projection = CastleSpatialProjection.Create(in plan, spatial);
            CastleKeepWindowSpec[] windows = spatial.KeepWindows;

            var legacyTable = new RegionTable(8, Allocator.Persistent);
            var legacyPool = new BrickPool(4096, Allocator.Persistent);
            var plannedTable = new RegionTable(8, Allocator.Persistent);
            var plannedPool = new BrickPool(4096, Allocator.Persistent);

            try
            {
                var legacyReads = new RegionReadSource(in legacyTable, in legacyPool);
                var legacyMutations = new RegionMutationStore(in legacyTable, in legacyPool);
                var legacyBrush = new VoxelBrush(legacyReads, legacyMutations, writeBudget: 2_000_000);
                var plannedReads = new RegionReadSource(in plannedTable, in plannedPool);
                var plannedMutations = new RegionMutationStore(in plannedTable, in plannedPool);
                var plannedBrush = new VoxelBrush(plannedReads, plannedMutations, writeBudget: 2_000_000);

                SeedWindowVolumes(ref legacyBrush, in plan, projection.KeepCentreWorld, windows);
                SeedWindowVolumes(ref plannedBrush, in plan, projection.KeepCentreWorld, windows);

                int legacyStage = 4;
                Assert.IsTrue(CastleKeepRealizer.TryStep(
                    ref legacyBrush,
                    in projection.KeepPlan,
                    spatial.KeepFloors,
                    ref legacyStage));
                Assert.AreEqual(5, legacyStage);

                CastleKeepWindowRealizer.Build(
                    ref plannedBrush,
                    in plan,
                    projection.KeepCentreWorld,
                    windows);

                int baseY = plan.Centre.y + plan.PlateauHeight;
                for (int i = 0; i < windows.Length; i++)
                {
                    CastleKeepWindowSpec window = windows[i];
                    int originX = projection.KeepCentreWorld.x + window.LocalOrigin.x;
                    int originY = baseY + window.BaseYOffset;
                    int originZ = projection.KeepCentreWorld.y + window.LocalOrigin.y;

                    for (int d = 0; d < window.Depth; d++)
                    for (int h = 0; h < window.Height; h++)
                    for (int w = 0; w < window.Width; w++)
                    {
                        byte expected = legacyBrush.Get(originX + w, originY + h, originZ + d);
                        byte actual = plannedBrush.Get(originX + w, originY + h, originZ + d);
                        Assert.AreEqual(
                            expected,
                            actual,
                            $"window {i} voxel ({w},{h},{d}) drifted");
                    }
                }

                Assert.IsFalse(legacyBrush.BudgetExceeded);
                Assert.IsFalse(plannedBrush.BudgetExceeded);
            }
            finally
            {
                legacyTable.Dispose();
                legacyPool.Dispose();
                plannedTable.Dispose();
                plannedPool.Dispose();
            }
        }

        private static void SeedWindowVolumes(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int2 worldKeepCentre,
            CastleKeepWindowSpec[] windows)
        {
            int baseY = plan.Centre.y + plan.PlateauHeight;
            for (int i = 0; i < windows.Length; i++)
            {
                CastleKeepWindowSpec window = windows[i];
                int originX = worldKeepCentre.x + window.LocalOrigin.x;
                int originY = baseY + window.BaseYOffset;
                int originZ = worldKeepCentre.y + window.LocalOrigin.y;

                for (int d = 0; d < window.Depth; d++)
                for (int w = 0; w < window.Width; w++)
                {
                    brush.FillColumnBulk(
                        originX + w,
                        originY,
                        originY + window.Height,
                        originZ + d,
                        Mat.Stone);
                }
            }
        }
    }
}

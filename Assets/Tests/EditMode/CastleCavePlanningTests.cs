using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleCavePlanningTests
    {
        [Test]
        public void CavePlanningMatchesOptionalDungeonThresholdAndIsDeterministic()
        {
            bool observedCave = false;
            bool observedNoCave = false;

            for (uint seed = 1; seed <= 32; seed++)
            {
                CastlePlan castle = CastlePlanner.Create(new int3(512, 220, 512), seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                topology.Perimeter = CastlePerimeterKind.Rectangular;
                topology.Wards = CastleWardPattern.SingleWard;
                topology.KeepPlacement = CastleKeepPlacement.Central;
                topology.DesiredTowerCount = 4;
                topology.HasPosternGate = false;

                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in castle, in topology);
                CastleSpatialPlan completed = CastleSpatialPlanCompletion.CompleteResolved(
                    in castle, spatial);
                Assert.NotNull(completed.Dungeon, $"seed {seed}: dungeon was not completed");

                if (!completed.Dungeon.HasCaveExit)
                {
                    observedNoCave = true;
                    Assert.IsNull(completed.Cave,
                        $"seed {seed}: castle attached a cave without a dungeon cave threshold");
                    Assert.IsTrue(
                        CastleCaveBuildReadiness.TryValidate(
                            completed, out CastleCaveBuildReadinessIssue noCaveIssue),
                        $"seed {seed}: no-cave plan was not runtime-ready: {noCaveIssue}");
                    continue;
                }

                observedCave = true;
                Assert.NotNull(completed.Cave,
                    $"seed {seed}: completed castle did not attach its natural cave plan");

                CavePlan first = completed.Cave;
                CavePlan second = CastleCavePlanning.Create(in castle, completed.Dungeon);
                DungeonRoomPlan threshold = completed.Dungeon.Rooms[
                    completed.Dungeon.CaveThresholdRoomId];
                int3 expectedEntrance = new int3(
                    threshold.Centre.x,
                    threshold.Centre.y - threshold.Size.y / 2,
                    threshold.Centre.z);

                Assert.AreEqual(
                    CastleSeedPartition.Derive(castle.Seed, CastleSeedDomain.Cave),
                    first.Seed,
                    $"seed {seed}: castle cave did not use the dedicated Cave seed domain");
                Assert.AreEqual(expectedEntrance, first.Entrance,
                    $"seed {seed}: cave entrance drifted from dungeon threshold");
                Assert.IsTrue(CavePlanValidator.TryValidate(first, out CavePlanIssue issue),
                    $"seed {seed}: {issue}");
                Assert.IsTrue(
                    CastleCaveBuildReadiness.TryValidate(
                        completed, out CastleCaveBuildReadinessIssue readinessIssue),
                    $"seed {seed}: cave plan was not runtime-ready: {readinessIssue}");
                Assert.AreEqual(first.Seed, second.Seed);
                Assert.AreEqual(first.Chambers.Length, second.Chambers.Length);
                Assert.AreEqual(first.Passages.Length, second.Passages.Length);
                for (int i = 0; i < first.Chambers.Length; i++)
                {
                    Assert.AreEqual(first.Chambers[i].Centre, second.Chambers[i].Centre,
                        $"seed {seed}, chamber {i}: non-deterministic centre");
                    Assert.AreEqual(first.Chambers[i].Radii, second.Chambers[i].Radii,
                        $"seed {seed}, chamber {i}: non-deterministic radii");
                }
            }

            Assert.IsTrue(observedCave, "Expected at least one seed with a natural cave exit.");
            Assert.IsTrue(observedNoCave, "Expected at least one seed without a natural cave exit.");
        }
    }
}

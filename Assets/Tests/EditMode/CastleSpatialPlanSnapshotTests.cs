using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleSpatialPlanSnapshotTests
    {
        [Test]
        public void RuntimeSnapshotIsDetachedFromCallerOwnedPlanArrays()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 137u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(137u);
            topology.KeepPlacement = CastleKeepPlacement.Central;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);
            spatial = CastleSpatialPlanCompletion.CompleteResolved(in dimensions, spatial);

            CastleSpatialPlan snapshot = CastleSpatialPlanSnapshot.CloneRuntimeReady(
                in dimensions, spatial);

            Assert.AreNotSame(spatial.OuterWardVertices, snapshot.OuterWardVertices);
            Assert.AreNotSame(spatial.Towers, snapshot.Towers);
            Assert.AreNotSame(spatial.KeepFloors, snapshot.KeepFloors);
            Assert.AreNotSame(spatial.KeepWindows, snapshot.KeepWindows);
            Assert.AreNotSame(spatial.Dungeon.Rooms, snapshot.Dungeon.Rooms);
            Assert.AreNotSame(spatial.Landscape.Decorations, snapshot.Landscape.Decorations);

            int2 snapshotOuter = snapshot.OuterWardVertices[0];
            CastleTowerPlacementSpec snapshotTower = snapshot.Towers[0];
            CastleKeepFloorPlan snapshotFloor = snapshot.KeepFloors[0];
            CastleKeepWindowSpec snapshotWindow = snapshot.KeepWindows[0];
            int3 snapshotDungeonRoom = snapshot.Dungeon.Rooms[0].Centre;
            int2 snapshotLandscape = snapshot.Landscape.Decorations[0].Centre;

            spatial.OuterWardVertices[0] += new int2(999, 999);
            CastleTowerPlacementSpec changedTower = spatial.Towers[0];
            changedTower.HeightVariation += 999;
            spatial.Towers[0] = changedTower;
            spatial.KeepFloors[0] = default;
            spatial.KeepWindows[0] = default;
            DungeonRoomPlan changedRoom = spatial.Dungeon.Rooms[0];
            changedRoom.Centre += new int3(999, 0, 999);
            spatial.Dungeon.Rooms[0] = changedRoom;
            CastleLandscapeDecorationSpec changedLandscape = spatial.Landscape.Decorations[0];
            changedLandscape.Centre += new int2(999, 999);
            spatial.Landscape.Decorations[0] = changedLandscape;

            Assert.AreEqual(snapshotOuter, snapshot.OuterWardVertices[0]);
            Assert.AreEqual(snapshotTower.HeightVariation, snapshot.Towers[0].HeightVariation);
            Assert.AreEqual(snapshotFloor.FloorIndex, snapshot.KeepFloors[0].FloorIndex);
            Assert.AreEqual(snapshotFloor.Purpose, snapshot.KeepFloors[0].Purpose);
            Assert.AreEqual(snapshotWindow.FloorIndex, snapshot.KeepWindows[0].FloorIndex);
            Assert.AreEqual(snapshotDungeonRoom, snapshot.Dungeon.Rooms[0].Centre);
            Assert.AreEqual(snapshotLandscape, snapshot.Landscape.Decorations[0].Centre);

            Assert.IsTrue(
                CastleSpatialPlanValidator.TryValidate(
                    in dimensions, snapshot, out CastleSpatialPlanIssue spatialIssue),
                spatialIssue.ToString());
            Assert.IsTrue(
                CastleSpatialBuildReadiness.TryValidate(
                    in dimensions, snapshot, out CastleSpatialBuildReadinessIssue readinessIssue),
                readinessIssue.ToString());
        }

        [Test]
        public void RuntimeSnapshotDetachesOptionalCaveAndInnerTowerArrays()
        {
            for (uint seed = 1; seed <= 256; seed++)
            {
                CastlePlan dimensions = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                topology.KeepPlacement = CastleKeepPlacement.Central;
                topology.Wards = CastleWardPattern.InnerAndOuterWards;
                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);
                spatial = CastleSpatialPlanCompletion.CompleteResolved(in dimensions, spatial);
                if (spatial.Cave == null)
                    continue;

                CastleSpatialPlan snapshot = CastleSpatialPlanSnapshot.CloneRuntimeReady(
                    in dimensions, spatial);

                Assert.AreNotSame(spatial.InnerTowers, snapshot.InnerTowers);
                Assert.AreNotSame(spatial.Cave.Chambers, snapshot.Cave.Chambers);
                Assert.AreNotSame(spatial.Cave.Passages, snapshot.Cave.Passages);

                CastleTowerPlacementSpec innerBefore = snapshot.InnerTowers[0];
                CaveChamberPlan chamberBefore = snapshot.Cave.Chambers[0];

                CastleTowerPlacementSpec changedTower = spatial.InnerTowers[0];
                changedTower.HasRoof = !changedTower.HasRoof;
                spatial.InnerTowers[0] = changedTower;
                CaveChamberPlan changedChamber = spatial.Cave.Chambers[0];
                changedChamber.Centre += new int3(500, 500, 500);
                spatial.Cave.Chambers[0] = changedChamber;

                Assert.AreEqual(innerBefore.HasRoof, snapshot.InnerTowers[0].HasRoof);
                Assert.AreEqual(chamberBefore.Centre, snapshot.Cave.Chambers[0].Centre);
                return;
            }

            Assert.Fail("Expected at least one seed in 1..256 to produce a planned natural cave.");
        }
    }
}

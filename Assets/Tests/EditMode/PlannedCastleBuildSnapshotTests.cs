using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Composition;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class PlannedCastleBuildSnapshotTests
    {
        [Test]
        public void PublicSpatialCopyCannotMutateFrozenBundle()
        {
            var centre = new int3(256, 220, 376);
            PlannedCastleBuild build = StructuresComposition.PlanCastleBuild(
                centre,
                419u,
                0x71A5u);

            CastleSpatialPlan first = build.Spatial;
            Assert.NotNull(first);
            Assert.NotNull(first.Dungeon);
            Assert.Greater(first.OuterWardVertices.Length, 0);
            Assert.Greater(first.Dungeon.Rooms.Length, 0);
            Assert.Greater(first.KeepFloors.Length, 0);
            Assert.NotNull(first.KeepFloors[0].Accents);

            int2 originalVertex = first.OuterWardVertices[0];
            DungeonRoomPlan originalRoom = first.Dungeon.Rooms[0];
            CastleRoomAccentPlan firstAccents = first.KeepFloors[0].Accents;
            CastleSpatialProjection projectionBefore = build.Projection;

            first.OuterWardVertices[0] = originalVertex + new int2(777, -333);
            DungeonRoomPlan mutatedRoom = first.Dungeon.Rooms[0];
            mutatedRoom.Centre += new int3(91, 17, -53);
            first.Dungeon.Rooms[0] = mutatedRoom;

            CastleSpatialPlan second = build.Spatial;
            CastleSpatialProjection projectionAfter = build.Projection;

            Assert.AreNotSame(first, second,
                "Each public Spatial read must detach caller-owned mutable arrays from the bundle.");
            Assert.AreEqual(originalVertex, second.OuterWardVertices[0],
                "Perimeter mutation escaped back into the frozen planned castle bundle.");
            Assert.AreEqual(originalRoom.Centre, second.Dungeon.Rooms[0].Centre,
                "Nested dungeon mutation escaped back into the frozen planned castle bundle.");
            Assert.AreNotSame(firstAccents, second.KeepFloors[0].Accents,
                "Detached keep-floor snapshots must not retain planner-owned room-accent identity.");
            CollectionAssert.AreEqual(firstAccents.Snapshot(), second.KeepFloors[0].Accents.Snapshot(),
                "Deep-cloning room accents must preserve their semantic content.");
            Assert.AreEqual(projectionBefore.KeepCentreWorld, projectionAfter.KeepCentreWorld);
            Assert.AreEqual(
                projectionBefore.PrimaryGateGeometry.Origin,
                projectionAfter.PrimaryGateGeometry.Origin,
                "Presentation/interaction projection changed after mutating a public Spatial copy.");
        }

        [Test]
        public void DefaultBundlePreservesNullSpatialCompatibility()
        {
            PlannedCastleBuild build = default;
            Assert.IsNull(build.Spatial);
        }
    }
}

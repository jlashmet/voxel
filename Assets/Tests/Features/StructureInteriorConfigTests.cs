using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.Features
{
    public sealed class StructureInteriorConfigTests
    {
        [Test]
        public void ConnectedRoomGraphWithClearPassagesIsNavigable()
        {
            var config = new InteriorLayoutConfig();
            config.Volumes.Add(Room(new int3(0, 0, 0)));
            config.Volumes.Add(Room(new int3(8, 0, 0)));
            config.Volumes.Add(Room(new int3(16, 0, 0)));
            config.Connections.Add(Doorway(0, 1));
            config.Connections.Add(Doorway(1, 2));

            Assert.IsTrue(config.IsWellFormed);
            Assert.IsTrue(config.HasConnectedInteriorGraph());
            Assert.IsTrue(config.IsNavigable(minimumWidth: 3, minimumHeight: 4));
        }

        [Test]
        public void ExteriorOpeningDoesNotHideDisconnectedRoom()
        {
            var config = new InteriorLayoutConfig();
            config.Volumes.Add(Room(new int3(0, 0, 0)));
            config.Volumes.Add(Room(new int3(8, 0, 0)));
            config.Connections.Add(new ConnectiveOpeningConfig
            {
                Kind = InteriorConnectionKind.Doorway,
                FromVolumeIndex = 0,
                ToVolumeIndex = -1,
                Min = new int3(0, 0, 0),
                Size = new int3(3, 4, 1),
            });

            Assert.IsTrue(config.IsWellFormed);
            Assert.IsFalse(config.HasConnectedInteriorGraph());
            Assert.IsFalse(config.IsNavigable(3, 4));
        }

        [Test]
        public void PassageBelowRequiredClearanceIsNotNavigable()
        {
            var config = new InteriorLayoutConfig();
            config.Volumes.Add(Room(new int3(0, 0, 0)));
            config.Volumes.Add(Room(new int3(8, 0, 0)));
            ConnectiveOpeningConfig narrow = Doorway(0, 1);
            narrow.Size = new int3(2, 3, 1);
            config.Connections.Add(narrow);

            Assert.IsTrue(config.IsWellFormed);
            Assert.IsTrue(config.HasConnectedInteriorGraph());
            Assert.IsFalse(config.IsNavigable(minimumWidth: 3, minimumHeight: 4));
        }

        private static InteriorVolumeConfig Room(int3 min) => new InteriorVolumeConfig
        {
            Min = min,
            Size = new int3(8, 6, 8),
            FloorMaterialRole = StructureMaterialRole.Floor,
            CeilingMaterialRole = StructureMaterialRole.PrimaryWall,
        };

        private static ConnectiveOpeningConfig Doorway(int from, int to) =>
            new ConnectiveOpeningConfig
            {
                Kind = InteriorConnectionKind.Doorway,
                FromVolumeIndex = from,
                ToVolumeIndex = to,
                Min = new int3(0, 0, 0),
                Size = new int3(3, 4, 1),
                FrameThickness = 1,
                FrameMaterialRole = StructureMaterialRole.Trim,
            };
    }
}

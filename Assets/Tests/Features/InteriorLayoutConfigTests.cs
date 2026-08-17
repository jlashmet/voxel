using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.Features
{
    public sealed class InteriorLayoutConfigTests
    {
        [Test]
        public void InteriorLayoutSupportsNavigableRoomCarvesAndRoomToRoomConnections()
        {
            var layout = new InteriorLayoutConfig();
            layout.Volumes.Add(new InteriorVolumeConfig
            {
                Kind = StructureInteriorVolumeKind.Room,
                Min = new int3(2, 1, 2),
                Size = new int3(12, 8, 10),
                WallThickness = 1,
                FloorThickness = 1,
                CeilingThickness = 1,
                WallMaterialRole = StructureMaterialRole.PrimaryWall,
                FloorMaterialRole = StructureMaterialRole.Floor,
                CeilingMaterialRole = StructureMaterialRole.Trim,
            });
            layout.Volumes.Add(new InteriorVolumeConfig
            {
                Kind = StructureInteriorVolumeKind.Room,
                Min = new int3(14, 1, 2),
                Size = new int3(10, 8, 10),
                WallThickness = 1,
                FloorThickness = 1,
                CeilingThickness = 1,
                WallMaterialRole = StructureMaterialRole.PrimaryWall,
                FloorMaterialRole = StructureMaterialRole.Floor,
                CeilingMaterialRole = StructureMaterialRole.Trim,
            });
            layout.Connections.Add(new ConnectiveOpeningConfig
            {
                Kind = InteriorConnectionKind.Doorway,
                FromVolumeIndex = 0,
                ToVolumeIndex = 1,
                Min = new int3(13, 1, 5),
                Size = new int3(2, 6, 4),
                FrameThickness = 1,
                FrameMaterialRole = StructureMaterialRole.Trim,
            });

            Assert.IsTrue(layout.IsWellFormed);
            Assert.IsTrue(layout.HasConnectedInteriorGraph());
            Assert.IsTrue(layout.IsNavigable(3, 6));
            Assert.AreEqual(2, layout.Volumes.Length);
            Assert.AreEqual(1, layout.Connections.Length);
        }

        [Test]
        public void InteriorLayoutAllowsExplicitExteriorConnection()
        {
            var layout = new InteriorLayoutConfig();
            layout.Volumes.Add(new InteriorVolumeConfig
            {
                Kind = StructureInteriorVolumeKind.Hall,
                Min = int3.zero,
                Size = new int3(16, 8, 16),
            });
            layout.Connections.Add(new ConnectiveOpeningConfig
            {
                Kind = InteriorConnectionKind.Passage,
                FromVolumeIndex = 0,
                ToVolumeIndex = -1,
                Min = new int3(6, 0, 0),
                Size = new int3(4, 6, 2),
            });

            Assert.IsTrue(layout.IsWellFormed);
            Assert.IsTrue(layout.Connections[0].IsExterior);
            Assert.AreEqual(-1, layout.Connections[0].ToVolumeIndex);
        }

        [Test]
        public void NavigableInteriorRejectsDisconnectedRooms()
        {
            var layout = new InteriorLayoutConfig();
            layout.Volumes.Add(new InteriorVolumeConfig
            {
                Kind = StructureInteriorVolumeKind.Room,
                Min = int3.zero,
                Size = new int3(10, 8, 10),
            });
            layout.Volumes.Add(new InteriorVolumeConfig
            {
                Kind = StructureInteriorVolumeKind.Room,
                Min = new int3(12, 0, 0),
                Size = new int3(10, 8, 10),
            });

            Assert.IsTrue(layout.IsWellFormed);
            Assert.IsFalse(layout.HasConnectedInteriorGraph());
            Assert.IsFalse(layout.IsNavigable(3, 6));
        }

        [Test]
        public void InteriorLayoutRejectsInvalidRoomReferencesAndVolumes()
        {
            var invalidReference = new InteriorLayoutConfig();
            invalidReference.Volumes.Add(new InteriorVolumeConfig
            {
                Min = int3.zero,
                Size = new int3(12, 8, 12),
            });
            invalidReference.Connections.Add(new ConnectiveOpeningConfig
            {
                Kind = InteriorConnectionKind.Arch,
                FromVolumeIndex = 0,
                ToVolumeIndex = 1,
                Min = new int3(10, 0, 4),
                Size = new int3(2, 6, 4),
            });
            Assert.IsFalse(invalidReference.IsWellFormed);

            var invalidVolume = new InteriorLayoutConfig();
            invalidVolume.Volumes.Add(new InteriorVolumeConfig
            {
                Min = int3.zero,
                Size = new int3(12, 0, 12),
            });
            Assert.IsFalse(invalidVolume.IsWellFormed);
        }
    }
}

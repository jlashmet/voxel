using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.Features
{
    public sealed class InteriorLayoutConfigTests
    {
        [Test]
        public void InteriorLayoutSupportsRoomCarvesAndRoomToRoomConnections()
        {
            var layout = new InteriorLayoutConfig();
            layout.Volumes.Add(new InteriorVolumeConfig
            {
                Min = new int3(2, 1, 2),
                Size = new int3(12, 8, 10),
                FloorMaterialRole = StructureMaterialRole.Floor,
                CeilingMaterialRole = StructureMaterialRole.Trim,
            });
            layout.Volumes.Add(new InteriorVolumeConfig
            {
                Min = new int3(14, 1, 2),
                Size = new int3(10, 8, 10),
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
            Assert.AreEqual(2, layout.Volumes.Length);
            Assert.AreEqual(1, layout.Connections.Length);
            Assert.AreEqual(InteriorConnectionKind.Doorway, layout.Connections[0].Kind);
        }

        [Test]
        public void InteriorLayoutAllowsExplicitExteriorConnection()
        {
            var layout = new InteriorLayoutConfig();
            layout.Volumes.Add(new InteriorVolumeConfig
            {
                Min = int3.zero,
                Size = new int3(16, 8, 16),
            });
            layout.Connections.Add(new ConnectiveOpeningConfig
            {
                Kind = InteriorConnectionKind.OpenPassage,
                FromVolumeIndex = 0,
                ToVolumeIndex = -1,
                Min = new int3(6, 0, 0),
                Size = new int3(4, 6, 2),
            });

            Assert.IsTrue(layout.IsWellFormed);
            Assert.AreEqual(-1, layout.Connections[0].ToVolumeIndex);
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

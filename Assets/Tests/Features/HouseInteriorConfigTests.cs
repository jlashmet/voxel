using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.Features
{
    public sealed class HouseInteriorConfigTests
    {
        [Test]
        public void HouseConfigAcceptsMultiRoomInteriorWithNavigableDoorway()
        {
            HouseConfig house = HousePresets.CottageCompatibility(1, 2);
            var interior = new InteriorLayoutConfig();

            interior.Volumes.Add(new InteriorVolumeConfig
            {
                Kind = StructureInteriorVolumeKind.Room,
                Min = new int3(4, 8, 4),
                Size = new int3(28, 20, 56),
                WallThickness = 1,
                FloorThickness = 1,
                CeilingThickness = 1,
                WallMaterialRole = StructureMaterialRole.PrimaryWall,
                FloorMaterialRole = StructureMaterialRole.Floor,
                CeilingMaterialRole = StructureMaterialRole.Trim,
            });
            interior.Volumes.Add(new InteriorVolumeConfig
            {
                Kind = StructureInteriorVolumeKind.Room,
                Min = new int3(32, 8, 4),
                Size = new int3(28, 20, 56),
                WallThickness = 1,
                FloorThickness = 1,
                CeilingThickness = 1,
                WallMaterialRole = StructureMaterialRole.PrimaryWall,
                FloorMaterialRole = StructureMaterialRole.Floor,
                CeilingMaterialRole = StructureMaterialRole.Trim,
            });
            interior.Connections.Add(new ConnectiveOpeningConfig
            {
                Kind = InteriorConnectionKind.Doorway,
                FromVolumeIndex = 0,
                ToVolumeIndex = 1,
                Min = new int3(30, 8, 28),
                Size = new int3(4, 12, 6),
                FrameThickness = 1,
                FrameMaterialRole = StructureMaterialRole.Trim,
            });

            house.Interior = interior;

            Assert.AreEqual(2, house.Interior.Volumes.Length);
            Assert.AreEqual(1, house.Interior.Connections.Length);
            Assert.AreEqual(InteriorConnectionKind.Doorway, house.Interior.Connections[0].Kind);
            Assert.IsTrue(house.Interior.IsWellFormed);
            Assert.IsTrue(house.Interior.HasConnectedInteriorGraph());
            Assert.IsTrue(house.Interior.IsNavigable(minimumWidth: 3, minimumHeight: 8));
        }
    }
}

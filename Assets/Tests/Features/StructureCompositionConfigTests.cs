using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.Features
{
    public sealed class StructureCompositionConfigTests
    {
        [Test]
        public void InteriorLayoutUsesBoundedRoomsAndSharedOpenings()
        {
            var layout = new InteriorLayoutConfig();
            layout.Rooms.Add(new RoomVolumeConfig
            {
                LocalMin = new int3(0, 0, 0),
                Size = new int3(16, 10, 16),
                WallThickness = 2,
                FloorThickness = 1,
                CeilingThickness = 1,
                WallMaterialRole = StructureMaterialRole.PrimaryWall,
                FloorMaterialRole = StructureMaterialRole.Floor,
                CeilingMaterialRole = StructureMaterialRole.Floor,
            });
            layout.Rooms.Add(new RoomVolumeConfig
            {
                LocalMin = new int3(16, 0, 0),
                Size = new int3(16, 10, 16),
                WallThickness = 2,
                FloorThickness = 1,
                CeilingThickness = 1,
                WallMaterialRole = StructureMaterialRole.PrimaryWall,
                FloorMaterialRole = StructureMaterialRole.Floor,
                CeilingMaterialRole = StructureMaterialRole.Floor,
            });
            layout.Connections.Add(new ConnectiveOpeningConfig
            {
                FromRoomIndex = 0,
                ToRoomIndex = 1,
                Facing = Facing.East,
                LocalOffset = 8,
                Opening = new OpeningConfig
                {
                    Kind = StructureOpeningKind.Door,
                    Width = 4,
                    Height = 7,
                    FrameThickness = 1,
                    FrameMaterialRole = StructureMaterialRole.Trim,
                    FillMaterialRole = StructureMaterialRole.Opening,
                },
            });

            Assert.AreEqual(2, layout.Rooms.Length);
            Assert.AreEqual(1, layout.Connections.Length);
            Assert.AreEqual(StructureOpeningKind.Door, layout.Connections[0].Opening.Kind);
        }

        [Test]
        public void CourtyardComposesSharedWallConfiguration()
        {
            var courtyard = new CourtyardConfig
            {
                OffsetX = 8,
                OffsetZ = 8,
                Width = 32,
                Depth = 24,
                FloorEnabled = true,
                FloorThickness = 1,
                PerimeterWallEnabled = true,
                PerimeterWall = new WallRunConfig
                {
                    Thickness = 2,
                    Height = 6,
                    RepetitionSpacing = 8,
                    PrimaryMaterialRole = StructureMaterialRole.PrimaryWall,
                    TrimMaterialRole = StructureMaterialRole.Trim,
                },
                FloorMaterialRole = StructureMaterialRole.Floor,
            };

            Assert.IsTrue(courtyard.PerimeterWallEnabled);
            Assert.AreEqual(32, courtyard.Width);
            Assert.AreEqual(2, courtyard.PerimeterWall.Thickness);
        }

        [Test]
        public void AttachmentKindsResolveToStableExternalNames()
        {
            Assert.AreEqual(new FixedString32Bytes("MainEntrance"),
                StructureAttachmentNames.Resolve(StructureAttachmentKind.MainEntrance));
            Assert.AreEqual(new FixedString32Bytes("RearEntrance"),
                StructureAttachmentNames.Resolve(StructureAttachmentKind.RearEntrance));
            Assert.AreEqual(new FixedString32Bytes("Road"),
                StructureAttachmentNames.Resolve(StructureAttachmentKind.Road));
            Assert.AreEqual(new FixedString32Bytes("Basement"),
                StructureAttachmentNames.Resolve(StructureAttachmentKind.Basement));
            Assert.AreEqual(new FixedString32Bytes("Crypt"),
                StructureAttachmentNames.Resolve(StructureAttachmentKind.Crypt));
            Assert.AreEqual(new FixedString32Bytes("Cave"),
                StructureAttachmentNames.Resolve(StructureAttachmentKind.Cave));
            Assert.AreEqual(new FixedString32Bytes("Extension"),
                StructureAttachmentNames.Resolve(StructureAttachmentKind.Extension));
        }
    }
}

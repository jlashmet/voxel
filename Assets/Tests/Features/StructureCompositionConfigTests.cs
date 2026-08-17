using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.Features
{
    public sealed class StructureCompositionConfigTests
    {
        [Test]
        public void OpenSpaceSupportsCourtyardSurfaceAndIndependentEdgeTreatments()
        {
            var courtyard = new OpenSpaceConfig
            {
                Area = new StructureFootprintRect(new int2(8, 8), new int2(32, 24)),
                SurfaceMode = OpenSpaceSurfaceMode.Paved,
                SurfaceThickness = 1,
                SurfaceMaterialRole = StructureMaterialRole.Floor,
                North = new OpenSpaceEdgeConfig
                {
                    Kind = OpenSpaceEdgeKind.Wall,
                    Height = 8,
                    Thickness = 2,
                    EntranceWidth = 4,
                    PrimaryMaterialRole = StructureMaterialRole.PrimaryWall,
                },
                East = new OpenSpaceEdgeConfig
                {
                    Kind = OpenSpaceEdgeKind.Colonnade,
                    Height = 10,
                    Thickness = 2,
                    RepetitionSpacing = 6,
                    PrimaryMaterialRole = StructureMaterialRole.Column,
                },
                South = new OpenSpaceEdgeConfig { Kind = OpenSpaceEdgeKind.BuildingFace },
                West = new OpenSpaceEdgeConfig { Kind = OpenSpaceEdgeKind.Open },
            };

            Assert.IsTrue(courtyard.IsWellFormed);
            Assert.AreEqual(new int2(32, 24), courtyard.Area.Size);
            Assert.AreEqual(OpenSpaceEdgeKind.Colonnade, courtyard.East.Kind);
            Assert.AreEqual(6, courtyard.East.RepetitionSpacing);
        }

        [Test]
        public void OpenSpaceRejectsInvalidSurfaceOrEdgeDimensions()
        {
            var invalidSurface = new OpenSpaceConfig
            {
                Area = new StructureFootprintRect(int2.zero, new int2(16, 16)),
                SurfaceMode = OpenSpaceSurfaceMode.Paved,
                SurfaceThickness = 0,
                North = new OpenSpaceEdgeConfig { Kind = OpenSpaceEdgeKind.Open },
                East = new OpenSpaceEdgeConfig { Kind = OpenSpaceEdgeKind.Open },
                South = new OpenSpaceEdgeConfig { Kind = OpenSpaceEdgeKind.Open },
                West = new OpenSpaceEdgeConfig { Kind = OpenSpaceEdgeKind.Open },
            };
            Assert.IsFalse(invalidSurface.IsWellFormed);

            var invalidWall = new OpenSpaceConfig
            {
                Area = new StructureFootprintRect(int2.zero, new int2(16, 16)),
                SurfaceMode = OpenSpaceSurfaceMode.None,
                North = new OpenSpaceEdgeConfig
                {
                    Kind = OpenSpaceEdgeKind.Wall,
                    Height = 8,
                    Thickness = 0,
                },
                East = new OpenSpaceEdgeConfig { Kind = OpenSpaceEdgeKind.Open },
                South = new OpenSpaceEdgeConfig { Kind = OpenSpaceEdgeKind.Open },
                West = new OpenSpaceEdgeConfig { Kind = OpenSpaceEdgeKind.Open },
            };
            Assert.IsFalse(invalidWall.IsWellFormed);
        }

        [TestCase(StructureAttachmentKind.MainEntrance, "MainEntrance")]
        [TestCase(StructureAttachmentKind.RearEntrance, "RearEntrance")]
        [TestCase(StructureAttachmentKind.Road, "Road")]
        [TestCase(StructureAttachmentKind.Basement, "Basement")]
        [TestCase(StructureAttachmentKind.Crypt, "Crypt")]
        [TestCase(StructureAttachmentKind.Cave, "Cave")]
        [TestCase(StructureAttachmentKind.Extension, "Extension")]
        public void AttachmentKindsResolveToStableExternalNames(
            StructureAttachmentKind kind,
            string expectedName)
        {
            FixedString32Bytes name = StructureAttachmentNames.Resolve(kind);
            Assert.AreEqual(expectedName, name.ToString());
        }

        [Test]
        public void AttachmentConfigCarriesSemanticKindWithoutStructureInternals()
        {
            var attachment = new AttachmentAnchorConfig
            {
                Kind = StructureAttachmentKind.Cave,
                LocalPosition = new int3(12, -4, 0),
                Facing = Facing.North,
                SnapToGround = false,
            };

            Assert.IsTrue(attachment.IsWellFormed);
            Assert.AreEqual(StructureAttachmentKind.Cave, attachment.Kind);
            Assert.AreEqual(new int3(12, -4, 0), attachment.LocalPosition);
            Assert.AreEqual("Cave", StructureAttachmentNames.Resolve(attachment.Kind).ToString());
        }
    }
}

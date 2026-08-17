using NUnit.Framework;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.Features
{
    public sealed class StructureWallConfigTests
    {
        [Test]
        public void CornerBehaviorDerivesThicknessBasedInsets()
        {
            var wall = new StructureWallRunConfig
            {
                Length = 40,
                Height = 20,
                Thickness = 4,
                PrimaryMaterial = StructureMaterialRole.PrimaryWall,
                CornerBehavior = StructureWallCornerBehavior.TrimBoth,
            };

            Assert.AreEqual(4, wall.StartInset);
            Assert.AreEqual(4, wall.EndInset);
            Assert.AreEqual(32, wall.UsableLength);
            Assert.IsTrue(wall.IsWellFormed);
        }

        [Test]
        public void MaterialBandsMustFitAndCannotOverlap()
        {
            var wall = new StructureWallRunConfig
            {
                Length = 64,
                Height = 24,
                Thickness = 4,
                PrimaryMaterial = StructureMaterialRole.PrimaryWall,
            };
            wall.MaterialBands.Add(new StructureWallMaterialBand(
                0, 6, StructureMaterialRole.Foundation));
            wall.MaterialBands.Add(new StructureWallMaterialBand(
                18, 6, StructureMaterialRole.Trim));

            Assert.IsTrue(wall.IsWellFormed);

            wall.MaterialBands.Add(new StructureWallMaterialBand(
                4, 8, StructureMaterialRole.SecondaryWall));
            Assert.IsFalse(wall.IsWellFormed, "overlapping material bands make precedence ambiguous");
        }

        [Test]
        public void RepetitionSpacingIsOptionalButOffsetMustBelongToPattern()
        {
            var wall = new StructureWallRunConfig
            {
                Length = 80,
                Height = 24,
                Thickness = 4,
                PrimaryMaterial = StructureMaterialRole.PrimaryWall,
                RepetitionSpacing = 16,
                RepetitionOffset = 4,
            };

            Assert.IsTrue(wall.IsWellFormed);

            wall.RepetitionOffset = 16;
            Assert.IsFalse(wall.IsWellFormed);

            wall.RepetitionSpacing = 0;
            wall.RepetitionOffset = 1;
            Assert.IsFalse(wall.IsWellFormed);
        }

        [Test]
        public void CornerTrimmingCannotConsumeEntireRun()
        {
            var wall = new StructureWallRunConfig
            {
                Length = 8,
                Height = 12,
                Thickness = 4,
                PrimaryMaterial = StructureMaterialRole.PrimaryWall,
                CornerBehavior = StructureWallCornerBehavior.TrimBoth,
            };

            Assert.AreEqual(0, wall.UsableLength);
            Assert.IsFalse(wall.IsWellFormed);
        }
    }
}

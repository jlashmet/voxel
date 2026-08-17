using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.Features
{
    public sealed class StructureComponentConfigTests
    {
        [Test]
        public void FootprintConfigSupportsBoundedComposedRectangles()
        {
            var config = new StructureFootprintConfig
            {
                Primary = new StructureFootprintRect(
                    new int2(0, 0),
                    new int2(48, 32)),
                BasePlane = BasePlaneRule.MeanGround,
                FoundationStyle = StructureFoundationStyle.TerrainFill,
                FoundationDepth = 4,
                FoundationMaterial = StructureMaterialRole.Foundation,
            };

            config.AdditionalRects.Add(new StructureFootprintRect(
                new int2(12, 32),
                new int2(24, 16)));

            Assert.AreEqual(2, config.PartCount);
            Assert.IsTrue(config.IsComposed);
            Assert.AreEqual(new int2(48, 32), config.Primary.Size);
            Assert.AreEqual(new int2(24, 16), config.AdditionalRects[0].Size);
            Assert.AreEqual(StructureMaterialRole.Foundation, config.FoundationMaterial);
            Assert.IsTrue(config.IsWellFormed);
        }

        [Test]
        public void WallAndOpeningConfigsKeepGeometryAndMaterialSemanticsSeparate()
        {
            var wall = new WallRunConfig
            {
                Thickness = 4,
                Height = 24,
                BaseOffset = 0,
                MaterialBandHeight = 6,
                RepetitionSpacing = 12,
                CornerMode = WallCornerMode.Continuous,
                PrimaryMaterialRole = StructureMaterialRole.PrimaryWall,
                SecondaryMaterialRole = StructureMaterialRole.SecondaryWall,
                TrimMaterialRole = StructureMaterialRole.Trim,
            };

            var opening = new OpeningConfig
            {
                Kind = StructureOpeningKind.Window,
                Width = 6,
                Height = 8,
                BottomOffset = 7,
                Spacing = 12,
                StartMargin = 5,
                EndMargin = 5,
                FrameThickness = 1,
                LintelThickness = 1,
                WidthVariation = 1,
                HeightVariation = 2,
                FrameMaterialRole = StructureMaterialRole.Trim,
                FillMaterialRole = StructureMaterialRole.Glass,
            };

            Assert.AreEqual(4, wall.Thickness);
            Assert.AreEqual(12, wall.RepetitionSpacing);
            Assert.AreEqual(StructureOpeningKind.Window, opening.Kind);
            Assert.AreEqual(StructureMaterialRole.Glass, opening.FillMaterialRole);
        }

        [Test]
        public void FloorAndRoofConfigsRemainIntegerOnly()
        {
            var floors = new FloorLevelConfig
            {
                FloorCount = 3,
                LevelHeight = 18,
                SlabThickness = 2,
                MinimumLevelHeightDelta = -1,
                MaximumLevelHeightDelta = 2,
                SlabMaterialRole = StructureMaterialRole.Floor,
            };

            var roof = new RoofConfig
            {
                Style = RoofStyle.Gable,
                RidgeAxis = RoofAxis.X,
                PitchRise = 1,
                PitchRun = 2,
                EaveOverhang = 2,
                Thickness = 2,
                ParapetHeight = 0,
                MaterialRole = StructureMaterialRole.Roof,
                TrimMaterialRole = StructureMaterialRole.Trim,
            };

            Assert.AreEqual(3, floors.FloorCount);
            Assert.AreEqual(18, floors.LevelHeight);
            Assert.AreEqual(RoofStyle.Gable, roof.Style);
            Assert.AreEqual(1, roof.PitchRise);
            Assert.AreEqual(2, roof.PitchRun);
        }
    }
}

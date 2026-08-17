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

        [Test]
        public void StairRampConfigCoversStraightRampAndLandingSemantics()
        {
            var stairs = new StairRampConfig
            {
                Style = StairRampStyle.LandingTurn,
                Axis = RoofAxis.Z,
                Width = 5,
                StepCount = 12,
                Rise = 1,
                Run = 2,
                LandingLength = 6,
                MaterialRole = StructureMaterialRole.Floor,
            };

            Assert.AreEqual(StairRampStyle.LandingTurn, stairs.Style);
            Assert.AreEqual(RoofAxis.Z, stairs.Axis);
            Assert.AreEqual(12, stairs.StepCount);
            Assert.AreEqual(6, stairs.LandingLength);
            Assert.AreEqual(StructureMaterialRole.Floor, stairs.MaterialRole);
        }

        [Test]
        public void TowerConfigCapturesShapePlacementTopAndOpeningPolicy()
        {
            var tower = new TowerConfig
            {
                Shape = TowerShape.Round,
                PlacementMode = TowerPlacementMode.Corners,
                TopStyle = TowerTopStyle.Roof,
                Radius = 8,
                Height = 40,
                Count = 4,
                Spacing = 32,
                OpeningsEnabled = true,
                Opening = new OpeningConfig
                {
                    Kind = StructureOpeningKind.Window,
                    Width = 3,
                    Height = 6,
                    Spacing = 10,
                },
                Roof = new RoofConfig
                {
                    Style = RoofStyle.Hip,
                    PitchRise = 1,
                    PitchRun = 2,
                },
                WallMaterialRole = StructureMaterialRole.PrimaryWall,
                TrimMaterialRole = StructureMaterialRole.Trim,
            };

            Assert.AreEqual(TowerShape.Round, tower.Shape);
            Assert.AreEqual(TowerPlacementMode.Corners, tower.PlacementMode);
            Assert.AreEqual(TowerTopStyle.Roof, tower.TopStyle);
            Assert.AreEqual(8, tower.Radius);
            Assert.AreEqual(4, tower.Count);
            Assert.IsTrue(tower.OpeningsEnabled);
            Assert.AreEqual(StructureOpeningKind.Window, tower.Opening.Kind);
            Assert.AreEqual(RoofStyle.Hip, tower.Roof.Style);
        }

        [Test]
        public void ColumnConfigSupportsRepeatedColonnades()
        {
            var columns = new ColumnConfig
            {
                Shape = ColumnShape.Round,
                Radius = 2,
                Height = 18,
                BaseHeight = 2,
                CapitalHeight = 2,
                Count = 10,
                Spacing = 7,
                ShaftMaterialRole = StructureMaterialRole.Column,
                TrimMaterialRole = StructureMaterialRole.Trim,
            };

            Assert.AreEqual(ColumnShape.Round, columns.Shape);
            Assert.AreEqual(10, columns.Count);
            Assert.AreEqual(7, columns.Spacing);
            Assert.AreEqual(StructureMaterialRole.Column, columns.ShaftMaterialRole);
        }

        [Test]
        public void ButtressConfigKeepsFlyingApproximationBoundedAndExplicit()
        {
            var buttress = new ButtressConfig
            {
                Width = 3,
                Depth = 5,
                Height = 22,
                Count = 6,
                Spacing = 12,
                Taper = 2,
                FlyingEnabled = true,
                FlyingSpan = 8,
                FlyingRise = 4,
                FlyingConnectionHeight = 14,
                MaterialRole = StructureMaterialRole.SecondaryWall,
            };

            Assert.IsTrue(buttress.FlyingEnabled);
            Assert.AreEqual(8, buttress.FlyingSpan);
            Assert.AreEqual(4, buttress.FlyingRise);
            Assert.AreEqual(14, buttress.FlyingConnectionHeight);
        }

        [Test]
        public void BattlementConfigSeparatesParapetAndCrenellationCadence()
        {
            var battlement = new BattlementConfig
            {
                ParapetThickness = 2,
                ParapetHeight = 4,
                MerlonWidth = 3,
                MerlonHeight = 3,
                GapWidth = 2,
                CornerMerlonWidth = 4,
                MaterialRole = StructureMaterialRole.PrimaryWall,
            };

            Assert.AreEqual(2, battlement.ParapetThickness);
            Assert.AreEqual(3, battlement.MerlonWidth);
            Assert.AreEqual(2, battlement.GapWidth);
            Assert.AreEqual(4, battlement.CornerMerlonWidth);
        }

        [Test]
        public void VerticalAccentConfigSharesGeometryWithoutArchetypeOwnership()
        {
            var accent = new VerticalAccentConfig
            {
                Style = VerticalAccentStyle.Spire,
                Width = 7,
                Depth = 7,
                Height = 28,
                Taper = 6,
                Count = 2,
                Spacing = 24,
                MaterialRole = StructureMaterialRole.Accent,
                TrimMaterialRole = StructureMaterialRole.Trim,
            };

            Assert.AreEqual(VerticalAccentStyle.Spire, accent.Style);
            Assert.AreEqual(28, accent.Height);
            Assert.AreEqual(6, accent.Taper);
            Assert.AreEqual(2, accent.Count);
            Assert.AreEqual(StructureMaterialRole.Accent, accent.MaterialRole);
        }
    }
}

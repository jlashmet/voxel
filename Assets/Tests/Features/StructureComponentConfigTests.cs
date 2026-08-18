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
                Primary = new StructureFootprintRect(new int2(0, 0), new int2(48, 32)),
                BasePlane = BasePlaneRule.MeanGround,
                FoundationStyle = StructureFoundationStyle.TerrainFill,
                FoundationDepth = 4,
                FoundationMaterial = StructureMaterialRole.Foundation,
            };

            config.AdditionalRects.Add(new StructureFootprintRect(
                new int2(12, 32), new int2(24, 16)));

            Assert.AreEqual(2, config.PartCount);
            Assert.IsTrue(config.IsComposed);
            Assert.AreEqual(new int2(48, 32), config.Primary.Size);
            Assert.AreEqual(new int2(24, 16), config.AdditionalRects[0].Size);
            Assert.AreEqual(StructureMaterialRole.Foundation, config.FoundationMaterial);
            Assert.IsTrue(config.IsWellFormed);
        }

        [Test]
        public void WallConfigSupportsBandsCornersAndRepetition()
        {
            var wall = new StructureWallRunConfig
            {
                Length = 48,
                Thickness = 4,
                Height = 24,
                PrimaryMaterial = StructureMaterialRole.PrimaryWall,
                CornerBehavior = StructureWallCornerBehavior.TrimBoth,
                RepetitionSpacing = 12,
                RepetitionOffset = 2,
            };
            wall.MaterialBands.Add(new StructureWallMaterialBand(
                0, 4, StructureMaterialRole.SecondaryWall));
            wall.MaterialBands.Add(new StructureWallMaterialBand(
                20, 4, StructureMaterialRole.Trim));

            Assert.IsTrue(wall.IsWellFormed);
            Assert.AreEqual(4, wall.StartInset);
            Assert.AreEqual(4, wall.EndInset);
            Assert.AreEqual(40, wall.UsableLength);
            Assert.AreEqual(2, wall.MaterialBands.Length);
            Assert.AreEqual(12, wall.RepetitionSpacing);
        }

        [Test]
        public void OpeningConfigKeepsGeometryAndMaterialSemanticsSeparate()
        {
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

            Assert.AreEqual(StructureOpeningKind.Window, opening.Kind);
            Assert.AreEqual(12, opening.Spacing);
            Assert.AreEqual(1, opening.FrameThickness);
            Assert.AreEqual(StructureMaterialRole.Glass, opening.FillMaterialRole);
        }

        [Test]
        public void OpeningConfigRejectsImpossibleRepetitionSpacing()
        {
            var opening = new OpeningConfig
            {
                Kind = StructureOpeningKind.Window,
                Width = 6,
                Height = 8,
                Spacing = 7,
                WidthVariation = 2,
                HeightVariation = 1,
            };

            Assert.IsFalse(opening.IsWellFormed,
                "spacing smaller than the maximum deterministic opening width can overlap");

            opening.Spacing = 8;
            Assert.IsTrue(opening.IsWellFormed);
            Assert.AreEqual(3, opening.MaxCountForSpan(24));
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
            Assert.AreEqual(2, floors.SlabThickness);
            Assert.AreEqual(RoofStyle.Gable, roof.Style);
            Assert.AreEqual(1, roof.PitchRise);
            Assert.AreEqual(2, roof.PitchRun);
        }

        [Test]
        public void RoofConfigRejectsUnsupportedStyleCombinations()
        {
            var flatWithPitch = new RoofConfig
            {
                Style = RoofStyle.Flat,
                PitchRise = 1,
                PitchRun = 2,
                Thickness = 2,
            };
            Assert.IsFalse(flatWithPitch.IsWellFormed,
                "flat roofs must not carry an ignored pitch");

            var pitchedWithoutRun = new RoofConfig
            {
                Style = RoofStyle.Gable,
                PitchRise = 1,
                PitchRun = 0,
                Thickness = 2,
            };
            Assert.IsFalse(pitchedWithoutRun.IsWellFormed,
                "pitched roofs require a positive integer rise/run pair");

            var pitchedWithParapet = new RoofConfig
            {
                Style = RoofStyle.Hip,
                PitchRise = 1,
                PitchRun = 2,
                Thickness = 2,
                ParapetHeight = 3,
            };
            Assert.IsFalse(pitchedWithParapet.IsWellFormed,
                "the shared roof component does not combine pitched roofs and flat-roof parapets");

            var flat = new RoofConfig
            {
                Style = RoofStyle.Flat,
                PitchRise = 0,
                PitchRun = 0,
                Thickness = 2,
                ParapetHeight = 3,
            };
            Assert.IsTrue(flat.IsWellFormed);
        }

        [Test]
        public void StairAndLandingConfigBoundsMultiFlightCirculation()
        {
            var stairs = new StairConfig
            {
                Width = 5,
                StepRise = 1,
                StepRun = 2,
                StepCount = 12,
                StepsPerFlight = 6,
                Layout = StructureStairLayout.HalfTurn,
                Landing = new LandingConfig
                {
                    Width = 5,
                    Length = 6,
                    Thickness = 1,
                    MaterialRole = StructureMaterialRole.Floor,
                },
                MaterialRole = StructureMaterialRole.Floor,
            };

            Assert.IsTrue(stairs.IsWellFormed);
            Assert.IsTrue(stairs.RequiresIntermediateLanding);
            Assert.AreEqual(12, stairs.TotalRise);
            Assert.AreEqual(24, stairs.TotalRun);
            Assert.AreEqual(StructureStairLayout.HalfTurn, stairs.Layout);
        }

        [Test]
        public void RampConfigRequiresLandingWhenFlightRunIsBounded()
        {
            var ramp = new RampConfig
            {
                Width = 6,
                Rise = 6,
                Run = 30,
                Thickness = 2,
                MaxRunPerFlight = 15,
                Landing = new LandingConfig
                {
                    Width = 6,
                    Length = 6,
                    Thickness = 2,
                    MaterialRole = StructureMaterialRole.Floor,
                },
                MaterialRole = StructureMaterialRole.Floor,
            };

            Assert.IsTrue(ramp.IsWellFormed);
            Assert.IsTrue(ramp.RequiresIntermediateLanding);
        }

        [Test]
        public void TowerConfigCapturesShapePlacementTopAndOpeningPolicy()
        {
            var tower = new TowerConfig
            {
                Shape = StructureTowerShape.Round,
                Placement = StructureTowerPlacement.Corners,
                TopStyle = StructureTowerTopStyle.Roof,
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

            Assert.IsTrue(tower.IsWellFormed);
            Assert.AreEqual(StructureTowerShape.Round, tower.Shape);
            Assert.AreEqual(StructureTowerPlacement.Corners, tower.Placement);
            Assert.AreEqual(StructureTowerTopStyle.Roof, tower.TopStyle);
            Assert.AreEqual(8, tower.Radius);
            Assert.AreEqual(4, tower.Count);
            Assert.IsTrue(tower.OpeningsEnabled);
        }

        [Test]
        public void ColumnConfigSupportsRepeatedColonnades()
        {
            var columns = new ColumnConfig
            {
                Shape = StructureColumnShape.Round,
                Radius = 2,
                Height = 18,
                BaseHeight = 2,
                CapitalHeight = 2,
                Count = 10,
                Spacing = 7,
                ShaftMaterialRole = StructureMaterialRole.Column,
                TrimMaterialRole = StructureMaterialRole.Trim,
            };

            Assert.IsTrue(columns.IsWellFormed);
            Assert.AreEqual(StructureColumnShape.Round, columns.Shape);
            Assert.AreEqual(10, columns.Count);
            Assert.AreEqual(7, columns.Spacing);
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

            Assert.IsTrue(buttress.IsWellFormed);
            Assert.IsTrue(buttress.FlyingEnabled);
            Assert.AreEqual(8, buttress.FlyingSpan);
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

            Assert.IsTrue(battlement.IsWellFormed);
            Assert.AreEqual(3, battlement.MerlonWidth);
            Assert.AreEqual(2, battlement.GapWidth);
            Assert.AreEqual(4, battlement.CornerMerlonWidth);
        }

        [Test]
        public void VerticalAccentConfigSharesGeometryWithoutArchetypeOwnership()
        {
            var accent = new VerticalAccentConfig
            {
                Style = StructureVerticalAccentStyle.Spire,
                Width = 7,
                Depth = 7,
                Height = 28,
                Taper = 6,
                Count = 2,
                Spacing = 24,
                MaterialRole = StructureMaterialRole.Accent,
                TrimMaterialRole = StructureMaterialRole.Trim,
            };

            Assert.IsTrue(accent.IsWellFormed);
            Assert.AreEqual(StructureVerticalAccentStyle.Spire, accent.Style);
            Assert.AreEqual(28, accent.Height);
            Assert.AreEqual(2, accent.Count);
        }
    }
}

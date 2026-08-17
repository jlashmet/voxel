using NUnit.Framework;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.Features
{
    public sealed class StructureAdvancedComponentConfigTests
    {
        [Test]
        public void CirculationAndTowerConfigsExposeBoundedIntegerControls()
        {
            var stairs = new StairRampConfig
            {
                Style = StairRampStyle.LandingTurn,
                Axis = RoofAxis.Z,
                Width = 6,
                StepCount = 12,
                Rise = 1,
                Run = 2,
                LandingLength = 5,
                MaterialRole = StructureMaterialRole.Floor,
            };

            var tower = new TowerConfig
            {
                Shape = TowerShape.Round,
                PlacementMode = TowerPlacementMode.Corners,
                TopStyle = TowerTopStyle.Parapet,
                Radius = 8,
                Height = 30,
                Count = 4,
                Spacing = 24,
                OpeningsEnabled = true,
                Opening = new OpeningConfig
                {
                    Kind = StructureOpeningKind.Window,
                    Width = 3,
                    Height = 5,
                    Spacing = 8,
                    FrameMaterialRole = StructureMaterialRole.Trim,
                    FillMaterialRole = StructureMaterialRole.Glass,
                },
                WallMaterialRole = StructureMaterialRole.PrimaryWall,
                TrimMaterialRole = StructureMaterialRole.Trim,
            };

            Assert.AreEqual(StairRampStyle.LandingTurn, stairs.Style);
            Assert.AreEqual(5, stairs.LandingLength);
            Assert.AreEqual(TowerShape.Round, tower.Shape);
            Assert.AreEqual(4, tower.Count);
            Assert.IsTrue(tower.OpeningsEnabled);
        }

        [Test]
        public void ColumnButtressAndBattlementConfigsStayArchetypeNeutral()
        {
            var column = new ColumnConfig
            {
                Shape = ColumnShape.Round,
                Radius = 2,
                Height = 18,
                BaseHeight = 2,
                CapitalHeight = 2,
                Count = 8,
                Spacing = 6,
                ShaftMaterialRole = StructureMaterialRole.Column,
                TrimMaterialRole = StructureMaterialRole.Trim,
            };

            var buttress = new ButtressConfig
            {
                Width = 4,
                Depth = 6,
                Height = 22,
                Count = 6,
                Spacing = 10,
                Taper = 2,
                FlyingEnabled = true,
                FlyingSpan = 8,
                FlyingRise = 3,
                FlyingConnectionHeight = 16,
                MaterialRole = StructureMaterialRole.PrimaryWall,
            };

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

            Assert.AreEqual(8, column.Count);
            Assert.IsTrue(buttress.FlyingEnabled);
            Assert.AreEqual(8, buttress.FlyingSpan);
            Assert.AreEqual(3, battlement.MerlonWidth);
        }

        [Test]
        public void VerticalAccentConfigSeparatesGeometryFromArchetypeSemantics()
        {
            var chimney = new VerticalAccentConfig
            {
                Style = VerticalAccentStyle.Chimney,
                Width = 4,
                Depth = 4,
                Height = 12,
                Taper = 0,
                Count = 2,
                Spacing = 18,
                MaterialRole = StructureMaterialRole.Accent,
                TrimMaterialRole = StructureMaterialRole.Trim,
            };

            var spire = chimney;
            spire.Style = VerticalAccentStyle.Spire;
            spire.Height = 30;
            spire.Taper = 4;

            Assert.AreEqual(VerticalAccentStyle.Chimney, chimney.Style);
            Assert.AreEqual(VerticalAccentStyle.Spire, spire.Style);
            Assert.AreEqual(StructureMaterialRole.Accent, spire.MaterialRole);
        }
    }
}

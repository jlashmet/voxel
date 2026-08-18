using NUnit.Framework;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.Features
{
    public sealed class StructureVerticalFeatureConfigTests
    {
        [Test]
        public void TowerConfigComposesSharedRoofAndOpeningContracts()
        {
            var tower = new TowerConfig
            {
                Shape = StructureTowerShape.Round,
                Placement = StructureTowerPlacement.Corners,
                TopStyle = StructureTowerTopStyle.Roof,
                Radius = 8,
                Height = 32,
                Count = 4,
                Spacing = 24,
                OpeningsEnabled = true,
                Opening = new OpeningConfig
                {
                    Kind = StructureOpeningKind.Window,
                    Width = 3,
                    Height = 5,
                    Spacing = 8,
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
            Assert.AreEqual(4, tower.Count);
            Assert.AreEqual(StructureOpeningKind.Window, tower.Opening.Kind);
            Assert.AreEqual(RoofStyle.Hip, tower.Roof.Style);
        }

        [Test]
        public void ColumnConfigRepresentsSingleColumnsAndColonnades()
        {
            var columns = new ColumnConfig
            {
                Shape = StructureColumnShape.Round,
                Radius = 2,
                Height = 18,
                BaseHeight = 2,
                CapitalHeight = 2,
                Count = 8,
                Spacing = 7,
                ShaftMaterialRole = StructureMaterialRole.Column,
                TrimMaterialRole = StructureMaterialRole.Trim,
            };

            Assert.IsTrue(columns.IsWellFormed);
            Assert.Greater(columns.Count, 1);
            Assert.AreEqual(7, columns.Spacing);
        }

        [Test]
        public void ButtressConfigProvidesBoundedFlyingApproximationHook()
        {
            var buttress = new ButtressConfig
            {
                Width = 3,
                Depth = 5,
                Height = 24,
                Count = 6,
                Spacing = 12,
                Taper = 1,
                FlyingEnabled = true,
                FlyingSpan = 8,
                FlyingRise = 4,
                FlyingConnectionHeight = 18,
                MaterialRole = StructureMaterialRole.PrimaryWall,
            };

            Assert.IsTrue(buttress.IsWellFormed);
            Assert.IsTrue(buttress.FlyingEnabled);
            Assert.AreEqual(8, buttress.FlyingSpan);
        }

        [Test]
        public void BattlementConfigSeparatesParapetMerlonAndGapCadence()
        {
            var battlement = new BattlementConfig
            {
                ParapetThickness = 2,
                ParapetHeight = 2,
                MerlonWidth = 3,
                MerlonHeight = 3,
                GapWidth = 2,
                CornerMerlonWidth = 4,
                MaterialRole = StructureMaterialRole.PrimaryWall,
            };

            Assert.IsTrue(battlement.IsWellFormed);
            Assert.AreNotEqual(battlement.MerlonWidth, battlement.GapWidth);
        }

        [Test]
        public void VerticalAccentConfigKeepsArchetypeBehaviorOutOfSharedGeometry()
        {
            var chimney = new VerticalAccentConfig
            {
                Style = StructureVerticalAccentStyle.Chimney,
                Width = 3,
                Depth = 3,
                Height = 10,
                Taper = 0,
                Count = 2,
                Spacing = 14,
                MaterialRole = StructureMaterialRole.SecondaryWall,
                TrimMaterialRole = StructureMaterialRole.Trim,
            };

            Assert.IsTrue(chimney.IsWellFormed);
            Assert.AreEqual(StructureVerticalAccentStyle.Chimney, chimney.Style);
            Assert.AreEqual(2, chimney.Count);
        }
    }
}

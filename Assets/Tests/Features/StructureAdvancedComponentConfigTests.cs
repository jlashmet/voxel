using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.Features
{
    public sealed class StructureAdvancedComponentConfigTests
    {
        [Test]
        public void CirculationAndTowerConfigsExposeBoundedIntegerControls()
        {
            var circulation = new VerticalCirculationConfig
            {
                Style = VerticalCirculationStyle.Stairs,
                Width = 6,
                TotalRise = 12,
                StepRise = 1,
                StepRun = 2,
                LandingLength = 5,
                LandingEverySteps = 6,
                MaterialRole = StructureMaterialRole.Floor,
                TrimMaterialRole = StructureMaterialRole.Trim,
            };

            var tower = new TowerConfig
            {
                Shape = TowerShape.Round,
                PlacementMode = TowerPlacementMode.Corners,
                TopStyle = TowerTopStyle.Parapet,
                Radius = 8,
                Height = 30,
                Count = 4,
                PlacementSpacing = 24,
                WallThickness = 3,
                TaperPerLevel = 1,
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

            Assert.AreEqual(VerticalCirculationStyle.Stairs, circulation.Style);
            Assert.AreEqual(5, circulation.LandingLength);
            Assert.AreEqual(6, circulation.LandingEverySteps);
            Assert.AreEqual(TowerShape.Round, tower.Shape);
            Assert.AreEqual(4, tower.Count);
            Assert.AreEqual(24, tower.PlacementSpacing);
            Assert.AreEqual(StructureOpeningKind.Window, tower.Opening.Kind);
        }

        [Test]
        public void ColumnButtressAndBattlementConfigsStayArchetypeNeutral()
        {
            var column = new ColumnConfig
            {
                Width = 4,
                Depth = 4,
                Height = 18,
                BaseHeight = 2,
                CapitalHeight = 2,
                Taper = 1,
                ShaftMaterialRole = StructureMaterialRole.Column,
                TrimMaterialRole = StructureMaterialRole.Trim,
            };

            var colonnade = new ColonnadeConfig
            {
                Column = column,
                Count = 8,
                Spacing = 6,
                StartMargin = 3,
                EndMargin = 3,
                ConnectWithLintel = true,
                LintelHeight = 2,
                LintelMaterialRole = StructureMaterialRole.Trim,
            };

            var buttress = new ButtressConfig
            {
                Style = ButtressStyle.FlyingApproximation,
                Width = 4,
                Depth = 6,
                Height = 22,
                Spacing = 10,
                StartMargin = 4,
                EndMargin = 4,
                LowerAttachmentHeight = 8,
                UpperAttachmentHeight = 16,
                FlyingClearance = 6,
                MaterialRole = StructureMaterialRole.PrimaryWall,
                TrimMaterialRole = StructureMaterialRole.Trim,
            };

            var battlement = new BattlementConfig
            {
                Style = BattlementStyle.Crenellated,
                Height = 4,
                Thickness = 2,
                MerlonWidth = 3,
                CrenelWidth = 2,
                StartMargin = 1,
                EndMargin = 1,
                MaterialRole = StructureMaterialRole.PrimaryWall,
                TrimMaterialRole = StructureMaterialRole.Trim,
            };

            Assert.AreEqual(8, colonnade.Count);
            Assert.IsTrue(colonnade.ConnectWithLintel);
            Assert.AreEqual(ButtressStyle.FlyingApproximation, buttress.Style);
            Assert.AreEqual(6, buttress.FlyingClearance);
            Assert.AreEqual(BattlementStyle.Crenellated, battlement.Style);
            Assert.AreEqual(3, battlement.MerlonWidth);
        }

        [Test]
        public void VerticalAccentConfigSeparatesGeometryFromArchetypeSemantics()
        {
            var chimney = new VerticalAccentConfig
            {
                Kind = VerticalAccentKind.Chimney,
                Width = 4,
                Depth = 4,
                Height = 12,
                Taper = 0,
                CapHeight = 2,
                Hollow = true,
                MaterialRole = StructureMaterialRole.Accent,
                CapMaterialRole = StructureMaterialRole.Trim,
            };

            var spire = chimney;
            spire.Kind = VerticalAccentKind.Spire;
            spire.Height = 30;
            spire.Taper = 4;
            spire.Hollow = false;

            Assert.AreEqual(VerticalAccentKind.Chimney, chimney.Kind);
            Assert.AreEqual(VerticalAccentKind.Spire, spire.Kind);
            Assert.AreEqual(StructureMaterialRole.Accent, spire.MaterialRole);
        }

        [Test]
        public void InteriorCourtyardAndAttachmentConfigsRemainExplicitAndBounded()
        {
            var interior = new InteriorLayoutConfig();
            interior.Volumes.Add(new InteriorVolumeConfig
            {
                Offset = new int3(2, 1, 2),
                Size = new int3(12, 8, 10),
                FloorThickness = 1,
                CeilingThickness = 1,
                FloorMaterialRole = StructureMaterialRole.Floor,
                WallMaterialRole = StructureMaterialRole.PrimaryWall,
            });
            interior.Volumes.Add(new InteriorVolumeConfig
            {
                Offset = new int3(14, 1, 2),
                Size = new int3(10, 8, 10),
                FloorThickness = 1,
                CeilingThickness = 1,
                FloorMaterialRole = StructureMaterialRole.Floor,
                WallMaterialRole = StructureMaterialRole.PrimaryWall,
            });
            interior.Connections.Add(new InteriorConnectionConfig
            {
                FromVolumeIndex = 0,
                ToVolumeIndex = 1,
                Facing = Facing.East,
                HorizontalOffset = 4,
                BottomOffset = 0,
                Width = 3,
                Height = 5,
                FrameMaterialRole = StructureMaterialRole.Trim,
            });

            var courtyard = new CourtyardConfig
            {
                OffsetX = 8,
                OffsetZ = 8,
                Width = 20,
                Depth = 16,
                PerimeterClearance = 3,
                OpenToSky = true,
                SurfaceEnabled = true,
                SurfaceMaterialRole = StructureMaterialRole.Floor,
            };

            var attachment = new StructureAttachmentConfig
            {
                Kind = StructureAttachmentKind.Cave,
                LocalPosition = new int3(12, -4, 8),
                Facing = Facing.Down,
            };

            Assert.AreEqual(2, interior.Volumes.Length);
            Assert.AreEqual(1, interior.Connections.Length);
            Assert.IsTrue(courtyard.OpenToSky);
            Assert.AreEqual(StructureAttachmentKind.Cave, attachment.Kind);
            Assert.AreEqual(
                new FixedString32Bytes("Cave"),
                StructureAttachmentSemantics.Name(attachment.Kind));
        }
    }
}

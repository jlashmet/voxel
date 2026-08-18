using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.Features
{
    public sealed class HouseDetailConfigTests
    {
        [Test]
        public void CottagePresetExposesCoreDimensionsFoundationAndPalette()
        {
            HouseConfig house = HousePresets.CottageCompatibility(11, 22);

            Assert.AreEqual(64, house.Width);
            Assert.AreEqual(64, house.Depth);
            Assert.AreEqual(1, house.FloorCount);
            Assert.AreEqual(32, house.FloorHeight);
            Assert.AreEqual(4, house.WallThickness);
            Assert.AreEqual(StructureFoundationStyle.Slab, house.FoundationStyle);
            Assert.AreEqual(8, house.FoundationDepth);
            Assert.AreEqual(11, house.Palette.Resolve(StructureMaterialRole.Foundation));
            Assert.AreEqual(11, house.Palette.Resolve(StructureMaterialRole.PrimaryWall));
            Assert.AreEqual(22, house.Palette.Resolve(StructureMaterialRole.Roof));
        }

        [Test]
        public void RoofAndDormerHooksExposePitchAxisEavesMaterialsAndExtensionPoint()
        {
            HouseConfig house = HousePresets.CottageCompatibility(1, 2);
            house.Roof.Style = RoofStyle.Hip;
            house.Roof.RidgeAxis = RoofAxis.X;
            house.Roof.PitchRise = 2;
            house.Roof.PitchRun = 3;
            house.Roof.EaveOverhang = 4;
            house.Roof.MaterialRole = StructureMaterialRole.Roof;
            house.Dormers = new HouseDormerConfig
            {
                Count = 2,
                Facade = HouseRoofFacade.Front,
                Width = 8,
                Height = 7,
                Depth = 6,
                Spacing = 10,
                EdgeMargin = 5,
                Style = RoofStyle.Gable,
                RoofMaterialRole = StructureMaterialRole.Roof,
                WallMaterialRole = StructureMaterialRole.SecondaryWall,
            };

            Assert.AreEqual(RoofStyle.Hip, house.Roof.Style);
            Assert.AreEqual(RoofAxis.X, house.Roof.RidgeAxis);
            Assert.AreEqual(2, house.Roof.PitchRise);
            Assert.AreEqual(3, house.Roof.PitchRun);
            Assert.AreEqual(4, house.Roof.EaveOverhang);
            Assert.IsTrue(house.Dormers.Enabled);
            Assert.IsTrue(house.Dormers.IsWellFormed);
        }

        [Test]
        public void DoorAndWindowLayoutsExposeIndependentFacadeRules()
        {
            var frontDoor = new HouseDoorLayoutConfig
            {
                Facade = HouseFacade.Front,
                Placement = HouseFacadePlacementMode.Centered,
                Count = 1,
                Opening = new OpeningConfig
                {
                    Kind = StructureOpeningKind.Door,
                    Width = 10,
                    Height = 20,
                    FrameThickness = 1,
                    LintelThickness = 2,
                },
                StepsEnabled = true,
                StepDepth = 2,
                StepHeight = 1,
                StepMaterialRole = StructureMaterialRole.Foundation,
            };

            var rearDoors = new HouseDoorLayoutConfig
            {
                Facade = HouseFacade.Rear,
                Placement = HouseFacadePlacementMode.EvenlySpaced,
                Count = 2,
                Opening = new OpeningConfig
                {
                    Kind = StructureOpeningKind.Door,
                    Width = 8,
                    Height = 18,
                },
            };

            var sideDoor = new HouseDoorLayoutConfig
            {
                Facade = HouseFacade.Right,
                Placement = HouseFacadePlacementMode.Centered,
                Count = 1,
                Opening = new OpeningConfig
                {
                    Kind = StructureOpeningKind.Door,
                    Width = 7,
                    Height = 18,
                    FrameThickness = 1,
                },
            };

            var sideWindows = new HouseWindowLayoutConfig
            {
                Facade = HouseFacade.Left,
                Placement = HouseFacadePlacementMode.EvenlySpaced,
                Count = 4,
                Opening = new OpeningConfig
                {
                    Kind = StructureOpeningKind.Window,
                    Width = 6,
                    Height = 8,
                    BottomOffset = 9,
                    Spacing = 12,
                    FrameThickness = 1,
                    LintelThickness = 1,
                    WidthVariation = 1,
                    HeightVariation = 2,
                },
                ShuttersEnabled = true,
                ShutterThickness = 1,
                ShutterMaterialRole = StructureMaterialRole.Trim,
            };

            Assert.IsTrue(frontDoor.IsWellFormed);
            Assert.AreEqual(1, frontDoor.Opening.FrameThickness);
            Assert.AreEqual(2, frontDoor.Opening.LintelThickness);
            Assert.IsTrue(frontDoor.StepsEnabled);
            Assert.AreEqual(2, frontDoor.StepDepth);
            Assert.AreEqual(1, frontDoor.StepHeight);
            Assert.AreEqual(2, rearDoors.Count);
            Assert.IsTrue(rearDoors.IsWellFormed);
            Assert.AreEqual(HouseFacade.Right, sideDoor.Facade);
            Assert.IsTrue(sideDoor.IsWellFormed);
            Assert.IsTrue(sideWindows.IsWellFormed);
            Assert.AreEqual(4, sideWindows.Count);
            Assert.AreEqual(9, sideWindows.SillHeight);
            Assert.AreEqual(17, sideWindows.HeadHeight);
            Assert.AreEqual(1, sideWindows.Opening.WidthVariation);
            Assert.AreEqual(2, sideWindows.Opening.HeightVariation);
        }

        [Test]
        public void InvalidFacadeDetailsAreRejectedRatherThanSilentlyAccepted()
        {
            var door = new HouseDoorLayoutConfig
            {
                Facade = HouseFacade.Front,
                Placement = HouseFacadePlacementMode.ExplicitOffsets,
                Count = 1,
                Opening = new OpeningConfig
                {
                    Kind = StructureOpeningKind.Door,
                    Width = 8,
                    Height = 18,
                },
            };
            door.ExplicitOffsets.Add(-1);
            Assert.IsFalse(door.IsWellFormed);

            var windows = new HouseWindowLayoutConfig
            {
                Facade = HouseFacade.Rear,
                Placement = HouseFacadePlacementMode.Centered,
                Count = 1,
                Opening = new OpeningConfig
                {
                    Kind = StructureOpeningKind.Window,
                    Width = 6,
                    Height = 8,
                },
                ShuttersEnabled = true,
                ShutterThickness = 0,
            };
            Assert.IsFalse(windows.IsWellFormed);
        }

        [Test]
        public void ChimneyExteriorAndInteriorHooksComposeWithoutFixingOneHouseLayout()
        {
            var interior = new InteriorLayoutConfig();
            interior.Volumes.Add(new InteriorVolumeConfig
            {
                Min = new int3(4, 8, 4),
                Size = new int3(24, 20, 24),
                FloorMaterialRole = StructureMaterialRole.Floor,
                CeilingMaterialRole = StructureMaterialRole.Trim,
            });

            var chimney = new HouseChimneyConfig
            {
                Enabled = true,
                LocalPosition = new int2(12, 14),
                Geometry = new VerticalAccentConfig
                {
                    Style = StructureVerticalAccentStyle.Chimney,
                    Width = 4,
                    Depth = 4,
                    Height = 18,
                    Taper = 0,
                    Count = 1,
                    Spacing = 0,
                    MaterialRole = StructureMaterialRole.Accent,
                    TrimMaterialRole = StructureMaterialRole.Trim,
                },
                FireplaceInteriorVolumeIndex = 0,
            };

            var porch = new HouseExteriorFeatureConfig
            {
                Enabled = true,
                Kind = HouseExteriorFeatureKind.Porch,
                Facade = HouseFacade.Front,
                HorizontalOffset = 0,
                BottomOffset = 0,
                Width = 24,
                Depth = 8,
                Thickness = 2,
                MaterialRole = StructureMaterialRole.Floor,
            };

            var awning = new HouseExteriorFeatureConfig
            {
                Enabled = true,
                Kind = HouseExteriorFeatureKind.Awning,
                Facade = HouseFacade.Rear,
                HorizontalOffset = 0,
                BottomOffset = 16,
                Width = 18,
                Depth = 5,
                Thickness = 1,
                CoverRoof = new RoofConfig
                {
                    Style = RoofStyle.Shed,
                    RidgeAxis = RoofAxis.X,
                    PitchRise = 1,
                    PitchRun = 3,
                    Thickness = 1,
                },
                MaterialRole = StructureMaterialRole.Roof,
            };

            var balcony = new HouseExteriorFeatureConfig
            {
                Enabled = true,
                Kind = HouseExteriorFeatureKind.Balcony,
                Facade = HouseFacade.Right,
                HorizontalOffset = 6,
                BottomOffset = 20,
                Width = 16,
                Depth = 5,
                Thickness = 2,
                MaterialRole = StructureMaterialRole.Floor,
            };

            Assert.IsTrue(chimney.IsWellFormed);
            Assert.IsTrue(chimney.HasFireplaceHook);
            Assert.IsTrue(porch.IsWellFormed);
            Assert.IsTrue(awning.IsWellFormed);
            Assert.IsTrue(balcony.IsWellFormed);
            Assert.IsTrue(interior.IsWellFormed);
            Assert.AreEqual(1, interior.Volumes.Length);
        }
    }
}

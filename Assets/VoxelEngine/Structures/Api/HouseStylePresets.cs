using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Coherent house style presets layered on the same <see cref="HouseConfig"/> contract used by
    /// the compatibility cottage. Presets choose values only; callers may override any field before
    /// compiling so no style requires a separate builder.
    /// </summary>
    public static class HouseStylePresets
    {
        public static HouseConfig CompactCabin(byte wallMaterial, byte roofMaterial)
        {
            HouseConfig config = HousePresets.CottageCompatibility(wallMaterial, roofMaterial);

            config.Footprint.Primary = new StructureFootprintRect(
                new int2(0, 0), new int2(48, 40));
            config.Footprint.FoundationDepth = 4;
            config.Walls.Length = 48;
            config.Walls.Height = 24;
            config.Walls.Thickness = 3;
            config.Floors.FloorCount = 1;
            config.Floors.LevelHeight = 24;
            config.Floors.SlabThickness = 4;
            config.MainDoor.Width = 8;
            config.MainDoor.Height = 16;
            config.FrontDoors.Opening = config.MainDoor;
            config.Roof.PitchRise = 2;
            config.Roof.PitchRun = 3;
            config.Roof.EaveOverhang = 2;

            config.FrontWindows = new HouseWindowLayoutConfig
            {
                Facade = HouseFacade.Front,
                Placement = HouseFacadePlacementMode.EvenlySpaced,
                Count = 2,
                Opening = new OpeningConfig
                {
                    Kind = StructureOpeningKind.Window,
                    Width = 5,
                    Height = 6,
                    BottomOffset = 8,
                    Spacing = 12,
                    FrameThickness = 1,
                    LintelThickness = 1,
                    FrameMaterialRole = StructureMaterialRole.Trim,
                    FillMaterialRole = StructureMaterialRole.Opening,
                },
            };

            return config;
        }

        public static HouseConfig Farmhouse(byte wallMaterial, byte roofMaterial)
        {
            HouseConfig config = HousePresets.CottageCompatibility(wallMaterial, roofMaterial);

            config.Footprint.Primary = new StructureFootprintRect(
                new int2(0, 0), new int2(96, 72));
            config.Footprint.FoundationDepth = 6;
            config.Walls.Length = 96;
            config.Walls.Height = 48;
            config.Walls.Thickness = 4;
            config.Floors.FloorCount = 2;
            config.Floors.LevelHeight = 24;
            config.Floors.SlabThickness = 4;
            config.MainDoor.Width = 14;
            config.MainDoor.Height = 22;
            config.FrontDoors.Opening = config.MainDoor;
            config.Roof.PitchRise = 1;
            config.Roof.PitchRun = 3;
            config.Roof.EaveOverhang = 4;

            config.FrontWindows = new HouseWindowLayoutConfig
            {
                Facade = HouseFacade.Front,
                Placement = HouseFacadePlacementMode.EvenlySpaced,
                Count = 4,
                Opening = new OpeningConfig
                {
                    Kind = StructureOpeningKind.Window,
                    Width = 7,
                    Height = 9,
                    BottomOffset = 9,
                    Spacing = 16,
                    FrameThickness = 1,
                    LintelThickness = 1,
                    FrameMaterialRole = StructureMaterialRole.Trim,
                    FillMaterialRole = StructureMaterialRole.Opening,
                },
                ShuttersEnabled = true,
                ShutterThickness = 1,
                ShutterMaterialRole = StructureMaterialRole.Trim,
            };

            config.RearDoors = new HouseDoorLayoutConfig
            {
                Facade = HouseFacade.Rear,
                Placement = HouseFacadePlacementMode.Centered,
                Count = 1,
                Opening = new OpeningConfig
                {
                    Kind = StructureOpeningKind.Door,
                    Width = 10,
                    Height = 20,
                    FrameThickness = 1,
                    LintelThickness = 1,
                    FrameMaterialRole = StructureMaterialRole.Trim,
                    FillMaterialRole = StructureMaterialRole.Opening,
                },
                StepsEnabled = true,
                StepDepth = 3,
                StepHeight = 1,
                StepMaterialRole = StructureMaterialRole.Foundation,
            };

            return config;
        }
    }
}

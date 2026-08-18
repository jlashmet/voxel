using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Pure-data house preset factories that deliberately reuse <see cref="HouseConfig"/> and the
    /// shared component contracts. They start from the compatibility cottage so material-role and
    /// facade defaults remain coherent, then override only the authored values that define each
    /// variant.
    /// </summary>
    public static class HousePresetVariants
    {
        /// <summary>A broad two-storey farmhouse with porch, regular facade windows and chimney.</summary>
        public static HouseConfig Farmhouse(byte masonryMaterial, byte timberMaterial)
        {
            HouseConfig config = HousePresets.CottageCompatibility(masonryMaterial, timberMaterial);

            StructureFootprintConfig footprint = config.Footprint;
            footprint.Primary = new StructureFootprintRect(new int2(0, 0), new int2(80, 56));
            footprint.FoundationDepth = 6;
            config.Footprint = footprint;

            StructureWallRunConfig walls = config.Walls;
            walls.Length = 80;
            walls.Height = 44;
            config.Walls = walls;

            FloorLevelConfig floors = config.Floors;
            floors.FloorCount = 2;
            floors.LevelHeight = 22;
            floors.SlabThickness = 2;
            config.Floors = floors;

            RoofConfig roof = config.Roof;
            roof.Style = RoofStyle.Gable;
            roof.RidgeAxis = RoofAxis.X;
            roof.PitchRise = 2;
            roof.PitchRun = 3;
            roof.EaveOverhang = 3;
            config.Roof = roof;

            config.FrontDoors = new HouseDoorLayoutConfig
            {
                Facade = HouseFacade.Front,
                Placement = HouseFacadePlacementMode.Centered,
                Count = 1,
                Opening = config.MainDoor,
                StepsEnabled = true,
                StepDepth = 2,
                StepHeight = 1,
                StepMaterialRole = StructureMaterialRole.Foundation,
            };

            OpeningConfig farmhouseWindow = new OpeningConfig
            {
                Kind = StructureOpeningKind.Window,
                Width = 7,
                Height = 9,
                BottomOffset = 8,
                Spacing = 12,
                StartMargin = 8,
                EndMargin = 8,
                FrameThickness = 1,
                LintelThickness = 1,
                WidthVariation = 1,
                HeightVariation = 1,
                FillMaterialRole = StructureMaterialRole.Opening,
            };
            config.FrontWindows = WindowRow(HouseFacade.Front, 4, farmhouseWindow);
            config.RearWindows = WindowRow(HouseFacade.Rear, 4, farmhouseWindow);
            config.LeftWindows = WindowRow(HouseFacade.Left, 3, farmhouseWindow);
            config.RightWindows = WindowRow(HouseFacade.Right, 3, farmhouseWindow);

            config.Chimney = new HouseChimneyConfig
            {
                Enabled = true,
                LocalPosition = new int2(18, 12),
                Geometry = new VerticalAccentConfig
                {
                    Style = StructureVerticalAccentStyle.Chimney,
                    Width = 4,
                    Depth = 4,
                    Height = 18,
                    Count = 1,
                    MaterialRole = StructureMaterialRole.Accent,
                    TrimMaterialRole = StructureMaterialRole.Trim,
                },
                FireplaceInteriorVolumeIndex = -1,
            };

            config.ExteriorFeatures.Add(new HouseExteriorFeatureConfig
            {
                Enabled = true,
                Kind = HouseExteriorFeatureKind.Porch,
                Facade = HouseFacade.Front,
                Width = 32,
                Depth = 8,
                Thickness = 2,
                MaterialRole = StructureMaterialRole.Floor,
            });

            return config;
        }

        /// <summary>A narrow three-storey townhouse with a hip roof and dense street-facing windows.</summary>
        public static HouseConfig Townhouse(byte masonryMaterial, byte timberMaterial)
        {
            HouseConfig config = HousePresets.CottageCompatibility(masonryMaterial, timberMaterial);

            StructureFootprintConfig footprint = config.Footprint;
            footprint.Primary = new StructureFootprintRect(new int2(0, 0), new int2(40, 64));
            footprint.FoundationDepth = 4;
            config.Footprint = footprint;

            StructureWallRunConfig walls = config.Walls;
            walls.Length = 64;
            walls.Height = 54;
            walls.Thickness = 3;
            config.Walls = walls;

            FloorLevelConfig floors = config.Floors;
            floors.FloorCount = 3;
            floors.LevelHeight = 18;
            floors.SlabThickness = 2;
            config.Floors = floors;

            RoofConfig roof = config.Roof;
            roof.Style = RoofStyle.Hip;
            roof.RidgeAxis = RoofAxis.Z;
            roof.PitchRise = 1;
            roof.PitchRun = 3;
            roof.EaveOverhang = 1;
            config.Roof = roof;

            OpeningConfig streetWindow = new OpeningConfig
            {
                Kind = StructureOpeningKind.Window,
                Width = 6,
                Height = 10,
                BottomOffset = 5,
                Spacing = 8,
                StartMargin = 4,
                EndMargin = 4,
                FrameThickness = 1,
                LintelThickness = 1,
                WidthVariation = 0,
                HeightVariation = 1,
                FillMaterialRole = StructureMaterialRole.Opening,
            };
            config.FrontWindows = WindowRow(HouseFacade.Front, 3, streetWindow);
            config.RearWindows = WindowRow(HouseFacade.Rear, 2, streetWindow);

            config.RearDoors = new HouseDoorLayoutConfig
            {
                Facade = HouseFacade.Rear,
                Placement = HouseFacadePlacementMode.Centered,
                Count = 1,
                Opening = new OpeningConfig
                {
                    Kind = StructureOpeningKind.Door,
                    Width = 8,
                    Height = 18,
                    FrameThickness = 1,
                    LintelThickness = 1,
                    FillMaterialRole = StructureMaterialRole.Opening,
                },
            };

            return config;
        }

        private static HouseWindowLayoutConfig WindowRow(
            HouseFacade facade,
            int count,
            OpeningConfig opening)
        {
            return new HouseWindowLayoutConfig
            {
                Facade = facade,
                Placement = HouseFacadePlacementMode.EvenlySpaced,
                Count = count,
                Opening = opening,
                ShuttersEnabled = false,
                ShutterThickness = 0,
                ShutterMaterialRole = StructureMaterialRole.Trim,
            };
        }
    }
}

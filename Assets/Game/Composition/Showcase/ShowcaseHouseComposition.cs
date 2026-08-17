using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Showcase-owned house composition. This deliberately consumes the same public house config and
    /// compiler as ordinary authored content; it is not a showcase-only geometry builder.
    /// </summary>
    public static class ShowcaseHouseComposition
    {
        /// <summary>
        /// Creates a detailed farmhouse configuration that exercises facade, roof, vertical-accent,
        /// exterior-feature, interior, and semantic-palette hooks in one application composition.
        /// </summary>
        public static HouseConfig DetailedFarmhouse(in ShowcaseMaterialSet materials)
        {
            HouseConfig config = HousePresetLibrary.Farmhouse(
                materials.WorldgenFoundation,
                materials.WorldgenMasonry,
                materials.WorldgenRoofTile);

            config.Palette.SecondaryWall = materials.WorldgenTimber;
            config.Palette.Trim = materials.WorldgenTimber;
            config.Palette.Accent = materials.WorldgenDarkMasonry;
            config.Palette.Glass = materials.WorldgenGlass;

            config.FrontWindows = new HouseWindowLayoutConfig
            {
                Facade = HouseFacade.Front,
                Placement = HouseFacadePlacementMode.EvenlySpaced,
                Count = 3,
                Opening = new OpeningConfig
                {
                    Kind = StructureOpeningKind.Window,
                    Width = 8,
                    Height = 10,
                    BottomOffset = 12,
                    Spacing = 20,
                    StartMargin = 10,
                    EndMargin = 10,
                    FrameThickness = 1,
                    LintelThickness = 1,
                    WidthVariation = 1,
                    HeightVariation = 1,
                    FrameMaterialRole = StructureMaterialRole.Trim,
                    FillMaterialRole = StructureMaterialRole.Glass,
                },
            };

            config.Dormers = new HouseDormerConfig
            {
                Count = 2,
                Facade = HouseRoofFacade.Front,
                Width = 10,
                Height = 8,
                Depth = 8,
                Spacing = 18,
                EdgeMargin = 12,
                Style = RoofStyle.Gable,
                RoofMaterialRole = StructureMaterialRole.Roof,
                WallMaterialRole = StructureMaterialRole.SecondaryWall,
            };

            config.Chimney = new HouseChimneyConfig
            {
                Enabled = true,
                LocalPosition = new int2(68, 46),
                Geometry = new VerticalAccentConfig
                {
                    Style = StructureVerticalAccentStyle.Chimney,
                    Width = 5,
                    Depth = 5,
                    Height = 24,
                    Count = 1,
                    MaterialRole = StructureMaterialRole.Accent,
                    TrimMaterialRole = StructureMaterialRole.Trim,
                },
                FireplaceInteriorVolumeIndex = 0,
            };

            config.ExteriorFeatures.Add(new HouseExteriorFeatureConfig
            {
                Enabled = true,
                Kind = HouseExteriorFeatureKind.Porch,
                Facade = HouseFacade.Front,
                HorizontalOffset = 0,
                BottomOffset = 0,
                Width = 34,
                Depth = 9,
                Thickness = 2,
                MaterialRole = StructureMaterialRole.Floor,
            });

            config.Interior.Volumes.Add(new InteriorVolumeConfig
            {
                Kind = StructureInteriorVolumeKind.Room,
                Min = new int3(5, config.FoundationDepth, 5),
                Size = new int3(78, 34, 62),
                WallThickness = 0,
                FloorThickness = 1,
                CeilingThickness = 1,
                WallMaterialRole = StructureMaterialRole.PrimaryWall,
                FloorMaterialRole = StructureMaterialRole.Floor,
                CeilingMaterialRole = StructureMaterialRole.Trim,
            });

            return config;
        }

        /// <summary>
        /// Compiles the detailed showcase shell through the same deterministic house compiler used by
        /// the compatibility fixture. Detail hooks remain config data until their shared emitters are
        /// enabled; no duplicate showcase geometry path is introduced.
        /// </summary>
        public static int[] BuildDetailedFarmhouseProgram(
            in ShowcaseMaterialSet materials,
            int mainDoorAnchorIndex,
            int hearthAnchorIndex)
        {
            HouseConfig config = DetailedFarmhouse(in materials);
            return HouseProgramCompiler.BuildCompatibilityProgram(
                in config,
                mainDoorAnchorIndex,
                hearthAnchorIndex);
        }
    }
}

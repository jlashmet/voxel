using Unity.Collections;

namespace VoxelEngine.Structures.Api
{
    /// <summary>Which facade owns a house roof detail such as a dormer.</summary>
    public enum HouseRoofFacade : byte
    {
        Front = 0,
        Rear = 1,
        Left = 2,
        Right = 3,
    }

    /// <summary>
    /// Optional bounded dormer hook for house roofs. This remains authoring data: a house compiler
    /// may realize it with existing box/prism/opening operations without adding a roof-specific
    /// engine opcode. Count zero disables dormers while preserving the rest of the roof config.
    /// </summary>
    public struct HouseDormerConfig
    {
        public int Count;
        public HouseRoofFacade Facade;
        public int Width;
        public int Height;
        public int Depth;
        public int Spacing;
        public int EdgeMargin;
        public RoofStyle Style;
        public StructureMaterialRole RoofMaterialRole;
        public StructureMaterialRole WallMaterialRole;

        public bool Enabled => Count > 0;

        public bool IsWellFormed => Count == 0 ||
            (Count > 0 && Width > 0 && Height > 0 && Depth > 0 &&
             Spacing >= 0 && EdgeMargin >= 0 && Style != RoofStyle.Flat);
    }

    /// <summary>
    /// Archetype-level house configuration composed from the shared structure contracts.
    /// Compatibility fields remain explicit while detailed facade/interior hooks allow one house
    /// compiler to grow without introducing per-style builders.
    /// </summary>
    public struct HouseConfig
    {
        public StructureFootprintConfig Footprint;
        public StructureWallRunConfig Walls;
        public FloorLevelConfig Floors;

        /// <summary>Compatibility alias used by the original cottage compiler path.</summary>
        public OpeningConfig MainDoor;

        public HouseDoorLayoutConfig FrontDoors;
        public HouseDoorLayoutConfig RearDoors;
        public HouseDoorLayoutConfig LeftDoors;
        public HouseDoorLayoutConfig RightDoors;

        public HouseWindowLayoutConfig FrontWindows;
        public HouseWindowLayoutConfig RearWindows;
        public HouseWindowLayoutConfig LeftWindows;
        public HouseWindowLayoutConfig RightWindows;

        public RoofConfig Roof;
        public HouseDormerConfig Dormers;
        public HouseChimneyConfig Chimney;
        public FixedList512Bytes<HouseExteriorFeatureConfig> ExteriorFeatures;
        public InteriorLayoutConfig Interior;
        public StructureMaterialPalette Palette;

        /// <summary>Primary rectangular house width in definition-local X voxels.</summary>
        public int Width => Footprint.Primary.Size.x;

        /// <summary>Primary rectangular house depth in definition-local Z voxels.</summary>
        public int Depth => Footprint.Primary.Size.y;

        public int FloorCount => Floors.FloorCount;
        public int FloorHeight => Floors.LevelHeight;
        public int WallThickness => Walls.Thickness;
        public StructureFoundationStyle FoundationStyle => Footprint.FoundationStyle;
        public int FoundationDepth => Footprint.FoundationDepth;

        public RoofStyle RoofStyle => Roof.Style;
        public int RoofPitchRise => Roof.PitchRise;
        public int RoofPitchRun => Roof.PitchRun;
        public RoofAxis RoofRidgeAxis => Roof.RidgeAxis;
        public int RoofEaveOverhang => Roof.EaveOverhang;
        public byte RoofMaterial => Palette.Resolve(Roof.MaterialRole);
    }

    /// <summary>Compatibility defaults for the original hand-authored cottage shape program.</summary>
    public static class HousePresets
    {
        public static HouseConfig CottageCompatibility(byte stoneMaterial, byte woodMaterial)
        {
            var footprint = new StructureFootprintConfig
            {
                Primary = new StructureFootprintRect(
                    new Unity.Mathematics.int2(0, 0),
                    new Unity.Mathematics.int2(64, 64)),
                BasePlane = BasePlaneRule.LowestGround,
                FoundationStyle = StructureFoundationStyle.Slab,
                FoundationDepth = 8,
                FoundationMaterial = StructureMaterialRole.Foundation,
            };

            var walls = new StructureWallRunConfig
            {
                Length = 64,
                Height = 32,
                Thickness = 4,
                PrimaryMaterial = StructureMaterialRole.PrimaryWall,
                CornerBehavior = StructureWallCornerBehavior.Overlap,
                RepetitionSpacing = 0,
                RepetitionOffset = 0,
            };

            var floors = new FloorLevelConfig
            {
                FloorCount = 1,
                LevelHeight = 32,
                SlabThickness = 8,
                MinimumLevelHeightDelta = 0,
                MaximumLevelHeightDelta = 0,
                SlabMaterialRole = StructureMaterialRole.Floor,
            };

            var door = new OpeningConfig
            {
                Kind = StructureOpeningKind.Door,
                Width = 12,
                Height = 20,
                BottomOffset = 0,
                Spacing = 0,
                StartMargin = 0,
                EndMargin = 0,
                FrameThickness = 0,
                LintelThickness = 0,
                WidthVariation = 0,
                HeightVariation = 0,
                FillMaterialRole = StructureMaterialRole.Opening,
            };

            var roof = new RoofConfig
            {
                Style = RoofStyle.Gable,
                RidgeAxis = RoofAxis.Z,
                PitchRise = 1,
                PitchRun = 2,
                EaveOverhang = 0,
                Thickness = 1,
                ParapetHeight = 0,
                MaterialRole = StructureMaterialRole.Roof,
                TrimMaterialRole = StructureMaterialRole.Trim,
            };

            return new HouseConfig
            {
                Footprint = footprint,
                Walls = walls,
                Floors = floors,
                MainDoor = door,
                FrontDoors = new HouseDoorLayoutConfig
                {
                    Facade = HouseFacade.Front,
                    Placement = HouseFacadePlacementMode.Centered,
                    Count = 1,
                    Opening = door,
                },
                RearDoors = new HouseDoorLayoutConfig
                {
                    Facade = HouseFacade.Rear,
                    Placement = HouseFacadePlacementMode.Centered,
                    Count = 0,
                },
                LeftDoors = new HouseDoorLayoutConfig
                {
                    Facade = HouseFacade.Left,
                    Placement = HouseFacadePlacementMode.Centered,
                    Count = 0,
                },
                RightDoors = new HouseDoorLayoutConfig
                {
                    Facade = HouseFacade.Right,
                    Placement = HouseFacadePlacementMode.Centered,
                    Count = 0,
                },
                FrontWindows = new HouseWindowLayoutConfig { Facade = HouseFacade.Front },
                RearWindows = new HouseWindowLayoutConfig { Facade = HouseFacade.Rear },
                LeftWindows = new HouseWindowLayoutConfig { Facade = HouseFacade.Left },
                RightWindows = new HouseWindowLayoutConfig { Facade = HouseFacade.Right },
                Roof = roof,
                Dormers = default,
                Chimney = new HouseChimneyConfig
                {
                    Enabled = false,
                    FireplaceInteriorVolumeIndex = -1,
                },
                Palette = new StructureMaterialPalette
                {
                    Foundation = stoneMaterial,
                    PrimaryWall = stoneMaterial,
                    SecondaryWall = stoneMaterial,
                    Trim = stoneMaterial,
                    Roof = woodMaterial,
                    Floor = stoneMaterial,
                    Opening = 0,
                },
            };
        }
    }
}

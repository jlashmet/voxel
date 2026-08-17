using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Coherent house presets built from the same shared <see cref="HouseConfig"/> vocabulary.
    /// Presets are pure config factories: callers can override any returned component before the
    /// ordinary house compiler consumes it.
    /// </summary>
    public static class HousePresetLibrary
    {
        /// <summary>A smaller single-storey cottage with a shallow foundation and compact door.</summary>
        public static HouseConfig CompactCottage(byte stoneMaterial, byte woodMaterial)
        {
            HouseConfig config = HousePresets.CottageCompatibility(stoneMaterial, woodMaterial);

            config.Footprint.Primary = new StructureFootprintRect(
                new int2(0, 0),
                new int2(48, 48));
            config.Footprint.FoundationDepth = 6;

            config.Walls.Length = 48;
            config.Walls.Height = 28;
            config.Walls.Thickness = 3;

            config.Floors.FloorCount = 1;
            config.Floors.LevelHeight = 28;
            config.Floors.SlabThickness = 6;

            config.MainDoor.Width = 10;
            config.MainDoor.Height = 18;
            config.FrontDoors.Opening = config.MainDoor;

            config.Roof.PitchRise = 1;
            config.Roof.PitchRun = 2;
            config.Roof.EaveOverhang = 1;

            return config;
        }

        /// <summary>
        /// A broader two-storey farmhouse preset with heavier walls, a deeper foundation, wider
        /// entrance, steeper roof, and independently selectable wall material.
        /// </summary>
        public static HouseConfig Farmhouse(
            byte foundationMaterial,
            byte wallMaterial,
            byte roofMaterial)
        {
            HouseConfig config = HousePresets.CottageCompatibility(
                foundationMaterial,
                roofMaterial);

            config.Footprint.Primary = new StructureFootprintRect(
                new int2(0, 0),
                new int2(88, 72));
            config.Footprint.FoundationDepth = 10;

            config.Walls.Length = 88;
            config.Walls.Height = 40;
            config.Walls.Thickness = 5;

            config.Floors.FloorCount = 2;
            config.Floors.LevelHeight = 20;
            config.Floors.SlabThickness = 2;

            config.MainDoor.Width = 14;
            config.MainDoor.Height = 22;
            config.FrontDoors.Opening = config.MainDoor;

            config.Roof.PitchRise = 2;
            config.Roof.PitchRun = 3;
            config.Roof.EaveOverhang = 2;

            config.Palette.Foundation = foundationMaterial;
            config.Palette.PrimaryWall = wallMaterial;
            config.Palette.SecondaryWall = wallMaterial;
            config.Palette.Roof = roofMaterial;
            config.Palette.Floor = foundationMaterial;

            return config;
        }
    }
}

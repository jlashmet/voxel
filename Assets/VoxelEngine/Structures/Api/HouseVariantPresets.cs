using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Materially different house presets that remain ordinary <see cref="HouseConfig"/> values.
    /// They intentionally reuse the shared house compiler instead of introducing style-specific
    /// builders, keeping presets as policy over the common authoring contracts.
    /// </summary>
    public static class HouseVariantPresets
    {
        public static HouseConfig TallTownhouse(byte masonryMaterial, byte roofMaterial)
        {
            HouseConfig config = HousePresets.CottageCompatibility(masonryMaterial, roofMaterial);

            config.Footprint.Primary = new StructureFootprintRect(int2.zero, new int2(48, 56));
            config.Walls.Length = 48;
            config.Walls.Height = 52;
            config.Floors.FloorCount = 2;
            config.Floors.LevelHeight = 26;
            config.Floors.SlabThickness = 6;

            config.MainDoor.Width = 10;
            config.MainDoor.Height = 20;
            HouseDoorLayoutConfig frontDoor = config.FrontDoors;
            frontDoor.Opening = config.MainDoor;
            config.FrontDoors = frontDoor;

            config.Roof.RidgeAxis = RoofAxis.Z;
            config.Roof.PitchRise = 1;
            config.Roof.PitchRun = 1;
            config.Roof.EaveOverhang = 2;

            return config;
        }

        public static HouseConfig WideFarmhouse(byte wallMaterial, byte roofMaterial)
        {
            HouseConfig config = HousePresets.CottageCompatibility(wallMaterial, roofMaterial);

            config.Footprint.Primary = new StructureFootprintRect(int2.zero, new int2(80, 64));
            config.Walls.Length = 80;
            config.Walls.Height = 30;
            config.Floors.FloorCount = 1;
            config.Floors.LevelHeight = 30;
            config.Floors.SlabThickness = 6;

            config.MainDoor.Width = 14;
            config.MainDoor.Height = 20;
            HouseDoorLayoutConfig frontDoor = config.FrontDoors;
            frontDoor.Opening = config.MainDoor;
            config.FrontDoors = frontDoor;

            config.Roof.RidgeAxis = RoofAxis.X;
            config.Roof.PitchRise = 1;
            config.Roof.PitchRun = 3;
            config.Roof.EaveOverhang = 3;

            return config;
        }
    }
}

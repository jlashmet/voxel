using Game.WorldBuilder.Voxel;
using Unity.Mathematics;
using VoxelEngine.Terrain.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>Showcase-owned parameters for the reusable WorldBuilder mountain landmark.</summary>
    public static class ShowcaseMountainDragonLayout
    {
        public const int OriginX = -1712;
        public const int OriginZ = -400;
        public const int FootprintEdge = 1200;
        public const int MountainRadius = 500;
        public const int MountainHeight = 280;
        public const int SummitRadius = 80;
        public const int PathWidth = 30;
        public const int PathRun = 360;
        public const int PathRise = 46;
        public const int SwitchbackCount = 6;
        public const int PlaceholderSize = 60;

        public static MountainLandmarkSpec CreateLandmark(uint seed)
        {
            int centre = FootprintEdge / 2;
            int pathMinX = centre - PathRun / 2;
            int firstRampZ = centre - MountainRadius - PathWidth - 10;
            int entryWorldX = OriginX + pathMinX;
            int entryWorldZ = OriginZ + firstRampZ;
            int baseY = TerrainQuery.HeightAt(entryWorldX, entryWorldZ, seed) + 1;

            return new MountainLandmarkSpec(
                new int3(OriginX, baseY, OriginZ),
                FootprintEdge,
                MountainRadius,
                MountainHeight,
                SummitRadius,
                PathWidth,
                PathRun,
                PathRise,
                SwitchbackCount,
                PlaceholderSize);
        }
    }
}

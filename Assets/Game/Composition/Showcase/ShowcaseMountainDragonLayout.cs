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
            MountainLandmarkSpec placementProbe = CreateLandmarkAtBaseY(0);
            MountainPathTierGeometry entry = placementProbe.PathTier(0);
            int entryWorldX = OriginX + entry.LowLandingMinX;
            int entryWorldZ = OriginZ + entry.LocalZ;
            int baseY = TerrainQuery.HeightAt(entryWorldX, entryWorldZ, seed) + 1;
            return CreateLandmarkAtBaseY(baseY);
        }

        /// <summary>
        /// Scene-owned player envelope translated into physical measurements rather than leaking
        /// VoxelShowcase motor constants into WorldBuilder. At the current 10 cm scale this derives
        /// the established 24-voxel headroom and 16-voxel clear walking lane, while the 50% grade
        /// ceiling preserves the normal-movement 2:1 run-to-rise contract.
        /// </summary>
        public static MountainLandmarkTraversalProfile CreateTraversalProfile() =>
            new MountainLandmarkTraversalProfile(
                voxelSizeMillimetres: 100,
                bodyHeightMillimetres: 1800,
                bodyRadiusMillimetres: 300,
                overheadMarginMillimetres: 600,
                lateralMarginMillimetres: 500,
                maximumGradePercent: 50);

        /// <summary>
        /// Showcase-specific visual policy for the shared mountain authoring API. Geometry semantics
        /// remain generic in WorldBuilder; this composition chooses the narrower crest and natural
        /// ridge/buttress support form required by the Mountain Dragon presentation.
        /// </summary>
        public static MountainLandmarkPresentationProfile CreatePresentationProfile() =>
            new MountainLandmarkPresentationProfile(
                crestRadiusPercent: 75,
                minimumPlaceholderCrestMargin: 12,
                supportForm: MountainLandmarkSupportForm.RidgeAndButtress);

        private static MountainLandmarkSpec CreateLandmarkAtBaseY(int baseY) =>
            new MountainLandmarkSpec(
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

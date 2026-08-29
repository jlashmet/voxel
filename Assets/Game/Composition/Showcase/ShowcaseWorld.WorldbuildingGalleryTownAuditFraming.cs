using Unity.Mathematics;
using Game.WorldBuilder.Api;
using VoxelEngine.Terrain.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Deterministic built-player framing for the six town-architecture audit subjects. The XZ
    /// contract is public so regression tests can prove evidence cameras stay outside authored
    /// structures while preserving the required wide, player-facade, and close-detail distances.
    /// </summary>
    public sealed partial class ShowcaseWorld
    {
        private const int GalleryTownAuditViewsPerDistrict = 3;

        public static int2 WorldbuildingGalleryTownAuditTargetXZ(int districtIndex, int viewIndex)
        {
            int district = NormalizeTownDistrictIndex(districtIndex);
            int view = NormalizeTownAuditView(viewIndex);
            int2 centre = s_GalleryTownDistrictCentres[district];
            if (view == 0) return centre;

            string styleId = s_GalleryTownStyleIds[district];
            if (styleId == WorldBuilderTownArchitectureIds.Rossdam)
            {
                int2 landmark = WorldbuildingGalleryTownLandmarkOriginXZ(district);
                // The gatehouse south face is z=-11 from the landmark. The player view targets the
                // gate centre while approaching around the west side of the commerce footprint. The
                // close view targets the left tower arrow-slit reveal without entering its buttress.
                return view == 1
                    ? landmark + new int2(0, -11)
                    : landmark + new int2(-21, -11);
            }

            int2 residence = WorldbuildingGalleryTownResidenceOriginXZ(district);
            if (styleId == WorldBuilderTownArchitectureIds.FairyVillage)
            {
                // The detailed treehouse room is centred 27 voxels above the trunk and its south
                // facade lies at z=-11. Keep both near views on the actual room facade.
                return view == 1
                    ? residence + new int2(0, -11)
                    : residence + new int2(-7, -11);
            }

            return view == 1
                ? residence + new int2(0, -17)
                : residence + new int2(-8, -17);
        }

        public static int2 WorldbuildingGalleryTownAuditSpawnXZ(int districtIndex, int viewIndex)
        {
            int district = NormalizeTownDistrictIndex(districtIndex);
            int view = NormalizeTownAuditView(viewIndex);
            int2 target = WorldbuildingGalleryTownAuditTargetXZ(district, view);
            if (view == 0) return target + new int2(0, -130);

            string styleId = s_GalleryTownStyleIds[district];
            if (styleId == WorldBuilderTownArchitectureIds.Rossdam)
            {
                int2 landmark = WorldbuildingGalleryTownLandmarkOriginXZ(district);
                // Commerce occupies roughly x[-24,+24], z[-64,-28] from the landmark including its
                // foundation. These positions keep the complete camera ray west/south of that volume
                // until it has cleared the north edge, then converge on the gate / slit at 2-5 m.
                return view == 1
                    ? landmark + new int2(-42, -37)
                    : landmark + new int2(-37, -23);
            }

            if (styleId == WorldBuilderTownArchitectureIds.FairyVillage)
            {
                int2 residence = WorldbuildingGalleryTownResidenceOriginXZ(district);
                return view == 1
                    ? residence + new int2(0, -48)
                    : residence + new int2(-7, -26);
            }

            return target + new int2(0, view == 1 ? -35 : -15);
        }

        public static int WorldbuildingGalleryTownAuditEyeHeightVoxels(int districtIndex, int viewIndex)
        {
            int district = NormalizeTownDistrictIndex(districtIndex);
            int view = NormalizeTownAuditView(viewIndex);
            if (view == 0) return 48;
            if (view == 2 && s_GalleryTownStyleIds[district] == WorldBuilderTownArchitectureIds.FairyVillage)
                return 34;
            return 18;
        }

        public static int WorldbuildingGalleryTownAuditLookHeightVoxels(int districtIndex, int viewIndex)
        {
            int district = NormalizeTownDistrictIndex(districtIndex);
            int view = NormalizeTownAuditView(viewIndex);
            if (view == 0) return 34;

            string styleId = s_GalleryTownStyleIds[district];
            if (styleId == WorldBuilderTownArchitectureIds.Rossdam)
                return view == 1 ? 24 : 19;
            if (styleId == WorldBuilderTownArchitectureIds.FairyVillage)
                return view == 1 ? 36 : 35;
            return view == 1 ? 13 : 10;
        }

        public float3 WorldbuildingGalleryTownAuditSpawnPosition(int districtIndex, int viewIndex)
        {
            int2 xz = WorldbuildingGalleryTownAuditSpawnXZ(districtIndex, viewIndex);
            int y = TerrainQuery.HeightAt(xz.x, xz.y, Seed) +
                    WorldbuildingGalleryTownAuditEyeHeightVoxels(districtIndex, viewIndex);
            return new float3(xz.x, y, xz.y) * VoxelSize;
        }

        public float3 WorldbuildingGalleryTownAuditLookTarget(int districtIndex, int viewIndex)
        {
            int2 xz = WorldbuildingGalleryTownAuditTargetXZ(districtIndex, viewIndex);
            int y = TerrainQuery.HeightAt(xz.x, xz.y, Seed) +
                    WorldbuildingGalleryTownAuditLookHeightVoxels(districtIndex, viewIndex);
            return new float3(xz.x, y, xz.y) * VoxelSize;
        }

        private static int NormalizeTownAuditView(int viewIndex)
        {
            int view = viewIndex % GalleryTownAuditViewsPerDistrict;
            return view < 0 ? view + GalleryTownAuditViewsPerDistrict : view;
        }
    }
}

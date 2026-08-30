using System;
using Game.WorldBuilder.Runtime;
using Game.WorldBuilder.Voxel;
using Unity.Mathematics;
using VoxelEngine.Composition;
using VoxelEngine.Structures.Api;
using VoxelEngine.Terrain.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Compatibility boundary for gallery bakes created before the registered town-architecture districts.
    /// A fresh bake pays no runtime authoring cost; an older bake repairs only this bounded catalogue.
    /// </summary>
    public sealed partial class ShowcaseWorld
    {
        private const int GalleryTownRepairWriteBudget = 22_000_000;

        public bool HasWorldbuildingGalleryTownArchitectureContent()
        {
            for (int i = 0; i < s_GalleryTownDistrictCentres.Length; i++)
            {
                int2 residence = WorldbuildingGalleryTownResidenceOriginXZ(i);
                if (!HasBuiltContentAbove(residence.x, residence.y)) return false;
            }
            return true;
        }

        public static int2 WorldbuildingGalleryTownResidenceOriginXZ(int districtIndex)
        {
            int i = NormalizeTownDistrictIndex(districtIndex);
            int seedShift = (int)(s_GalleryTownSeeds[i] % 5u) - 2;
            return s_GalleryTownDistrictCentres[i] + new int2(-47 + seedShift, -12);
        }

        public static int2 WorldbuildingGalleryTownLandmarkOriginXZ(int districtIndex)
        {
            int i = NormalizeTownDistrictIndex(districtIndex);
            int seedShift = (int)(s_GalleryTownSeeds[i] % 5u) - 2;
            return s_GalleryTownDistrictCentres[i] + new int2(40 - seedShift, 34);
        }

        public void EnsureWorldbuildingGalleryTownArchitectureBlocking()
        {
            EnsureGalleryProofTownRegistered();
            if (HasWorldbuildingGalleryTownArchitectureContent())
            {
                UnityEngine.Debug.Log($"TOWNARCH_BAKE parity=present repair=false districts={s_GalleryTownStyleIds.Length}");
                return;
            }

            var regions = new System.Collections.Generic.HashSet<int3>();
            for (int i = 0; i < s_GalleryTownDistrictCentres.Length; i++)
                AddGalleryRegionNeighbourhood(regions, s_GalleryTownDistrictCentres[i], 1);
            foreach (int3 region in regions) GenerateRegionBlocking(region);

            IStructureAuthoringSession authoring = StructuresComposition.CreateAuthoringSession(
                ReadStorage, MutationStorage, _palette, writeBudget: GalleryTownRepairWriteBudget);

            long writesStart = authoring.TotalVoxelsWritten;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < s_GalleryTownStyleIds.Length; i++)
            {
                var program = WorldBuilderTownArchitecture.Resolve(s_GalleryTownStyleIds[i], s_GalleryTownSeeds[i]);
                TownArchitectureVoxelPalette palette = GalleryTownPalette(i);
                WorldBuilderTownArchitectureVoxelAuthoring.Author(
                    authoring,
                    s_GalleryTownDistrictCentres[i],
                    (x, z) => TerrainQuery.HeightAt(x, z, Seed),
                    program,
                    in palette);
            }
            timer.Stop();

            long townWrites = authoring.TotalVoxelsWritten - writesStart;
            UnityEngine.Debug.Log(
                $"TOWNARCH_AUTHORING mode=stale-bake-repair districts={s_GalleryTownStyleIds.Length} " +
                $"writes={townWrites} elapsedMs={timer.Elapsed.TotalMilliseconds:0.###} " +
                $"budget={authoring.WriteBudget} budgetExceeded={authoring.BudgetExceeded}");

            if (authoring.BudgetExceeded)
                throw new InvalidOperationException(
                    $"Town architecture stale-bake repair exceeded its {authoring.WriteBudget:N0}-write budget.");
            if (!HasWorldbuildingGalleryTownArchitectureContent())
                throw new InvalidOperationException(
                    $"Town architecture stale-bake repair completed without all {s_GalleryTownStyleIds.Length} representative structures.");
        }
    }
}

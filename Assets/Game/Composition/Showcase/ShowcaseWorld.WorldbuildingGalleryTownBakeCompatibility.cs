using System;
using Game.Structures.Runtime;
using Game.WorldBuilder.Runtime;
using Game.WorldBuilder.Voxel;
using Unity.Mathematics;
using VoxelEngine.Composition;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Compatibility boundary for gallery bakes created before the six town-architecture districts.
    /// A fresh bake already contains the representative structures and pays no runtime authoring cost;
    /// an older bake repairs only the missing town districts through the same shared WorldBuilder path.
    /// </summary>
    public sealed partial class ShowcaseWorld
    {
        private const int GalleryTownRepairWriteBudget = 18_000_000;

        /// <summary>
        /// Returns true only when every canonical town district contains its representative residence.
        /// The probe runs before gallery vegetation/world objects are populated, so it measures authored
        /// voxel content rather than presentation clutter and naturally recognizes a future refreshed bake.
        /// </summary>
        public bool HasWorldbuildingGalleryTownArchitectureContent()
        {
            for (int i = 0; i < s_GalleryTownDistrictCentres.Length; i++)
            {
                int2 residence = WorldbuildingGalleryTownResidenceOriginXZ(i);
                if (!HasBuiltContentAbove(residence.x, residence.y))
                    return false;
            }

            return true;
        }

        /// <summary>Canonical representative residence origin shared by bake probing and evidence framing.</summary>
        public int2 WorldbuildingGalleryTownResidenceOriginXZ(int districtIndex)
        {
            int i = NormalizeTownDistrictIndex(districtIndex);
            int seedShift = (int)(s_GalleryTownSeeds[i] % 5u) - 2;
            return s_GalleryTownDistrictCentres[i] + new int2(-47 + seedShift, -12);
        }

        /// <summary>Canonical landmark origin for a town district, including Rossdam's fortified gatehouse.</summary>
        public int2 WorldbuildingGalleryTownLandmarkOriginXZ(int districtIndex)
        {
            int i = NormalizeTownDistrictIndex(districtIndex);
            int seedShift = (int)(s_GalleryTownSeeds[i] % 5u) - 2;
            return s_GalleryTownDistrictCentres[i] + new int2(40 - seedShift, 34);
        }

        /// <summary>
        /// Repairs a stale gallery bake without rerunning the castle, original exhibits, promenade,
        /// cave, guild houses, or other expensive gallery generation. A future bake containing all six
        /// town structures skips this method after the inexpensive storage probe.
        /// </summary>
        public void EnsureWorldbuildingGalleryTownArchitectureBlocking()
        {
            if (HasWorldbuildingGalleryTownArchitectureContent())
            {
                UnityEngine.Debug.Log("TOWNARCH_BAKE parity=present repair=false");
                return;
            }

            var regions = new System.Collections.Generic.HashSet<int3>();
            for (int i = 0; i < s_GalleryTownDistrictCentres.Length; i++)
                AddGalleryRegionNeighbourhood(regions, s_GalleryTownDistrictCentres[i], 1);

            foreach (int3 region in regions)
                GenerateRegionBlocking(region);

            IStructureAuthoringSession authoring = StructuresComposition.CreateAuthoringSession(
                ReadStorage,
                MutationStorage,
                _palette,
                writeBudget: GalleryTownRepairWriteBudget);

            long writesStart = authoring.TotalVoxelsWritten;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < s_GalleryTownStyleIds.Length; i++)
            {
                string styleId = s_GalleryTownStyleIds[i];
                var program = WorldBuilderTownArchitecture.Resolve(styleId, s_GalleryTownSeeds[i]);
                TownArchitectureVoxelPalette palette = GalleryTownPalette(styleId);
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
                    "Town architecture stale-bake repair completed without all six representative structures.");
        }
    }
}

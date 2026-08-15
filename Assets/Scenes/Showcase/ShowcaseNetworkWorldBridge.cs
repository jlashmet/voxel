using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Presentation bridge for authoritative mutations applied by networking. The shared
    /// deterministic applier marks authoritative regions dirty, while showcase rendering consumes
    /// the logical voxel-change feed. Keep that translation here instead of teaching Net about a
    /// particular renderer or demo harness.
    /// </summary>
    internal static class ShowcaseNetworkWorldBridge
    {
        private const VoxelChangeKind NetworkEditKinds =
            VoxelChangeKind.Occupancy |
            VoxelChangeKind.BaseMaterial |
            VoxelChangeKind.SurfaceStyle |
            VoxelChangeKind.Coating;

        /// <summary>
        /// Publishes resident regions that storage marked dirty after one or more authoritative
        /// events were drained. Network interest and showcase streaming are both centred on the
        /// player, so scanning the resident showcase disc is bounded and avoids a second mutation
        /// side channel inside networking.
        /// </summary>
        public static int PublishDirtyRegionsAround(ShowcaseWorld world, float3 playerMetres)
        {
            if (world == null) return 0;

            int3 centre = ShowcaseWorld.RegionAt((UnityEngine.Vector3)playerMetres);
            int radius = world.LoadRadiusRegions + 1;
            int published = 0;

            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                var regionCoord = new int3(centre.x + dx, 0, centre.z + dz);
                if (PublishRegion(world, regionCoord))
                    published++;
            }

            return published;
        }

        /// <summary>
        /// Publishes one authoritative replacement (repair or full-state recovery). Those paths
        /// can change storage without draining an alteration batch, so they notify this adapter
        /// explicitly through ClientNetworkRuntime's replacement events.
        /// </summary>
        public static bool PublishRegion(ShowcaseWorld world, int3 regionCoord)
        {
            if (world == null ||
                !world.IsGenerated(regionCoord) ||
                !world.IsRegionDirty(regionCoord))
                return false;

            world.PublishRegionChange(regionCoord, NetworkEditKinds);
            return true;
        }
    }
}

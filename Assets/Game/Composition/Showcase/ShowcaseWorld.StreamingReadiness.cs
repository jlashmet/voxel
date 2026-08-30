using Unity.Mathematics;

namespace VoxelEngine.Showcase
{
    public sealed partial class ShowcaseWorld
    {
        /// <summary>
        /// True when the generated-content publication for one presentation column is final.
        ///
        /// Terrain publication is intentionally not enough. <see cref="FinishRegion"/> commits
        /// terrain first, then queues authored feature realization; <see cref="CompleteFeatureBuild"/>
        /// publishes the later feature mutation/invalidation. Evidence that presents authored
        /// content may use this query before accepting renderer coverage without waiting for
        /// unrelated regions elsewhere in the streaming residency disc.
        ///
        /// The query is observational only. It checks the same bounded surface-layer span used by
        /// streaming for this X/Z column, plus the caller's explicit Y layer when that lies outside
        /// the terrain span. It does not generate, expand residency, scan world history, traverse
        /// voxels/meshes, or allocate.
        /// </summary>
        public bool IsPresentationColumnContentSettled(float3 presentationMetres)
        {
            int3 pointRegion = PositionToRegion(presentationMetres);
            SurfaceLayerSpan(pointRegion.x, pointRegion.z, out int minLayer, out int maxLayer);
            if (maxLayer - minLayer > MaxSurfaceLayersPerColumn)
                maxLayer = minLayer + MaxSurfaceLayersPerColumn;

            for (int ry = minLayer; ry <= maxLayer; ry++)
                if (!IsRegionContentSettled(new int3(pointRegion.x, ry, pointRegion.z)))
                    return false;

            if ((pointRegion.y < minLayer || pointRegion.y > maxLayer)
                && !IsRegionContentSettled(pointRegion))
                return false;

            return true;
        }

        /// <summary>
        /// Opening-preload compatibility query. Startup only needs its bounded terrain column to
        /// exist; renderer publication is checked separately by the opening gate. Authored remote
        /// evidence must use <see cref="IsPresentationColumnContentSettled"/> so terrain-only
        /// publication cannot race later feature realization.
        /// </summary>
        public bool IsCurrentDemandContentSettled(float3 cameraMetres)
        {
            int3 pointRegion = PositionToRegion(cameraMetres);
            SurfaceLayerSpan(pointRegion.x, pointRegion.z, out int minLayer, out int maxLayer);
            if (maxLayer - minLayer > MaxSurfaceLayersPerColumn)
                maxLayer = minLayer + MaxSurfaceLayersPerColumn;

            for (int ry = minLayer; ry <= maxLayer; ry++)
                if (!_generated.Contains(new int3(pointRegion.x, ry, pointRegion.z)))
                    return false;

            return true;
        }

        private bool IsRegionContentSettled(int3 regionCoord)
        {
            if (!_generated.Contains(regionCoord)) return false;

            // No catalogue means terrain is the final content publication for this region.
            if (!_catalogue.IsCreated) return true;

            if (_featureBuild != null && _featureBuild.RegionCoord.Equals(regionCoord))
                return false;
            if (_pendingFeatureRegions.Contains(regionCoord)) return false;
            if (_deferredFeatureRegions.Contains(regionCoord)) return false;

            return true;
        }
    }
}

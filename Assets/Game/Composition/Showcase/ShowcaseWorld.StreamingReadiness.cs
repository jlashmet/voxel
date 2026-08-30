using Unity.Mathematics;

namespace VoxelEngine.Showcase
{
    public sealed partial class ShowcaseWorld
    {
        /// <summary>
        /// True when every region currently demanded by the camera has completed both terrain
        /// generation and the separately queued feature-realization publication.
        ///
        /// A generated terrain region is not presentation-stable yet when its feature work is
        /// still queued or in flight: <see cref="FinishRegion"/> publishes terrain first and
        /// <see cref="CompleteFeatureBuild"/> publishes a second invalidation after authored
        /// structures are committed. Capture/loading gates that only watch renderer coverage can
        /// otherwise observe the terrain-only publication and declare the view ready one frame
        /// before buildings arrive.
        ///
        /// This intentionally walks the same bounded horizontal demand and surface-layer span as
        /// <see cref="RefreshPending"/>. It does not scan world history, expand residency, or start
        /// generation; it only asks whether the already-maintained current demand is final.
        /// </summary>
        public bool IsCurrentDemandContentSettled(float3 cameraMetres)
        {
            int3 centre = PositionToRegion(cameraMetres);
            int radiusSquared = LoadRadiusRegions * LoadRadiusRegions;

            for (int dx = -LoadRadiusRegions; dx <= LoadRadiusRegions; dx++)
            for (int dz = -LoadRadiusRegions; dz <= LoadRadiusRegions; dz++)
            {
                if (dx * dx + dz * dz > radiusSquared) continue;

                int rx = centre.x + dx;
                int rz = centre.z + dz;
                SurfaceLayerSpan(rx, rz, out int minLayer, out int maxLayer);
                if (maxLayer - minLayer > MaxSurfaceLayersPerColumn)
                    maxLayer = minLayer + MaxSurfaceLayersPerColumn;

                for (int ry = minLayer; ry <= maxLayer; ry++)
                    if (!IsDemandedRegionContentSettled(new int3(rx, ry, rz)))
                        return false;

                if ((centre.y < minLayer || centre.y > maxLayer)
                    && !IsDemandedRegionContentSettled(new int3(rx, centre.y, rz)))
                    return false;
            }

            return true;
        }

        private bool IsDemandedRegionContentSettled(int3 regionCoord)
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

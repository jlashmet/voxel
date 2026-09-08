using Unity.Mathematics;
using UnityEngine;

namespace VoxelEngine.Showcase
{
    public sealed partial class ShowcaseWorld
    {
        /// <summary>
        /// True when the generated content for one presentation column is final enough for
        /// player-visible evidence to wait on renderer publication.
        ///
        /// Terrain residency alone is not sufficient: terrain commits before authored feature
        /// realization, and the later feature build mutates the same regions. The current
        /// <see cref="SurfaceLayerSpan"/> already incorporates explicit authored feature
        /// footprints, so this query checks that bounded combined span plus the caller's explicit
        /// layer when it lies outside the surface span. It is observational only: it does not
        /// generate content, widen residency, advance feature work, or alter rendering budgets.
        /// </summary>
        public bool IsPresentationColumnContentSettled(Vector3 presentationMetres)
        {
            int3 pointRegion = RegionAt(presentationMetres);
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

        private bool IsRegionContentSettled(int3 regionCoord)
        {
            if (!_generated.Contains(regionCoord)) return false;

            // Without authored feature rules, committed terrain is final for this region.
            if (!_catalogue.IsCreated) return true;

            if (_featureBuild != null && _featureBuild.RegionCoord.Equals(regionCoord))
                return false;
            if (_pendingFeatureRegions.Contains(regionCoord)) return false;
            if (_deferredFeatureRegions.Contains(regionCoord)) return false;

            return true;
        }
    }
}

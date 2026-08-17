using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ShowcaseResidencyFootprintTests
    {
        [Test]
        public void RegionCornerOffsetStillLoadsPhysicalStep4CoreInsideNearCoverage()
        {
            float region = ShowcaseWorld.RegionMetres;
            float radius = 8f * region;

            // Region-index residency used to center its disc on region (0,0), even though the
            // camera can be almost a full region toward the +X/+Z corner. At that position the
            // step-4 box shell can legitimately own region (6,6): its nearest point is only
            // ~362 m from the camera, well inside the configured 409.6 m near coverage. The old
            // integer rule rejected it because 6^2 + 6^2 > 8^2, leaving exact metadata impossible
            // while the far-terrain hole could already include the same world-space area.
            var camera = new float3(region - 0.1f, 32f, region - 0.1f);

            Assert.Greater(6 * 6 + 6 * 6, 8 * 8,
                "Fixture must remain outside the obsolete region-index circle.");
            Assert.True(ShowcaseResidencyFootprint.ColumnIntersectsRadius(
                camera, regionX: 6, regionZ: 6, radiusMetres: radius),
                "A region physically intersecting the camera-centred near radius must be streamed "
              + "even when its integer coordinate lies outside the old region-centred disc.");
            Assert.False(ShowcaseResidencyFootprint.ColumnIntersectsRadius(
                camera, regionX: 7, regionZ: 7, radiusMetres: radius),
                "The camera-relative footprint should add only physically needed fringe columns, "
              + "not inflate residency to a square.");
        }
    }
}

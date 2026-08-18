using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ShowcaseResidencyFootprintTests
    {
        [Test]
        public void RegionCornerOffsetIdentifiesFringeThatMustCloseFarHole()
        {
            float region = ShowcaseWorld.RegionMetres;
            float radius = 8f * region;

            // The bounded full-detail wanted set is an integer region disc. When the camera sits
            // near the +X/+Z corner of its current region, (6,6) is outside that budget even though
            // its AABB is physically inside the nominal 409.6 m camera radius. The correct response
            // is not to load more full-detail regions (which can exhaust BrickPool); the published
            // near-coverage radius must contract so far terrain remains underneath this fringe.
            var camera = new float3(region - 0.1f, 32f, region - 0.1f);

            Assert.Greater(6 * 6 + 6 * 6, 8 * 8,
                "Fixture must remain outside the bounded radius-8 region disc.");
            Assert.True(ShowcaseResidencyFootprint.ColumnIntersectsRadius(
                camera, regionX: 6, regionZ: 6, radiusMetres: radius),
                "Fixture must remain physically inside the nominal camera-centred coverage radius.");
            Assert.False(ShowcaseResidencyFootprint.ColumnIntersectsRadius(
                camera, regionX: 7, regionZ: 7, radiusMetres: radius),
                "The camera-relative helper should identify only the true physical fringe, not a square.");
        }
    }
}

using NUnit.Framework;
using VoxelEngine.Storage.Api;
using VoxelEngine.Storage.Runtime;

namespace VoxelEngine.CI
{
    public sealed class MaterialPaletteRegistrationTests
    {
        [Test]
        public void SparseRegistration_DoesNotMakeGapSlotsRegisteredOrDestructible()
        {
            MaterialPalette palette = default;
            palette.Register(20, 220, DestructionClass.Crumble,
                             SurfaceStyles.MasonryJoint, 0u);

            Assert.That(palette.Count, Is.EqualTo(21));
            Assert.That(palette.IsRegistered(20), Is.True);
            Assert.That(palette.IsRegistered(16), Is.False,
                "Count tracks the highest occupied slot, not registration of every lower ID.");
            Assert.That(palette.GetHardness(16), Is.EqualTo(0));
            Assert.That(palette.GetDestructionClass(16), Is.EqualTo(DestructionClass.None));
            Assert.That(palette.IsDestructible(16), Is.False);
            Assert.That(palette.IsFlammable(16), Is.False);
        }

        [Test]
        public void Registration_RoundTripsSemanticFreePlacementProperties()
        {
            MaterialPalette palette = default;
            var definition = new MaterialDefinition(
                materialId: 7,
                hardness: 80,
                destructionClass: DestructionClass.Crumble,
                defaultSurfaceStyle: SurfaceStyles.Smooth,
                allowedCoatings: 1u << Coatings.Soot,
                flammable: false,
                placementSurfaceStyle: SurfaceStyles.Rounded,
                placementCoating: Coatings.Soot);

            palette.Register(in definition);

            Assert.That(palette.GetPlacementSurfaceStyle(7), Is.EqualTo(SurfaceStyles.Rounded));
            Assert.That(palette.GetPlacementCoating(7), Is.EqualTo(Coatings.Soot));
            Assert.That(palette.GetPlacementSurfaceStyle(6), Is.EqualTo(SurfaceStyles.MaterialDefault));
            Assert.That(palette.GetPlacementCoating(6), Is.EqualTo(Coatings.None));
        }
    }
}

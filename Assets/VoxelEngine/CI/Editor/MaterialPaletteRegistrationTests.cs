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
    }
}

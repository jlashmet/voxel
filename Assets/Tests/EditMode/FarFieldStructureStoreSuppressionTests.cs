using NUnit.Framework;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class FarFieldStructureStoreSuppressionTests
    {
        [Test]
        public void Suppression_IsConservativeAtFallbackColumnGranularity()
        {
            var store = new FarFieldStructureStore();
            int column = FarFieldStructureStore.VoxelsPerColumn;

            store.SuppressBuiltSilhouette(3, 5, 4, 6);

            Assert.That(store.IsBuiltSilhouetteSuppressedAt(0, 0), Is.True,
                "Any semantic footprint touching a coarse fallback column must suppress that whole fallback column.");
            Assert.That(store.IsBuiltSilhouetteSuppressedAt(column - 1, column - 1), Is.True);
            Assert.That(store.IsBuiltSilhouetteSuppressedAt(column, 0), Is.False,
                "Anonymous fallback in adjacent columns must remain available.");
            Assert.That(store.IsBuiltSilhouetteSuppressedAt(0, column), Is.False);
        }

        [Test]
        public void Suppression_CrossesNegativeRegionBoundaryDeterministically()
        {
            var store = new FarFieldStructureStore();

            store.SuppressBuiltSilhouette(-2, -2, 2, 2);

            Assert.That(store.IsBuiltSilhouetteSuppressedAt(-1, -1), Is.True);
            Assert.That(store.IsBuiltSilhouetteSuppressedAt(0, 0), Is.True);
            Assert.That(store.IsBuiltSilhouetteSuppressedAt(-FarFieldStructureStore.VoxelsPerColumn - 1, 0), Is.False);
            Assert.That(store.IsBuiltSilhouetteSuppressedAt(0, FarFieldStructureStore.VoxelsPerColumn), Is.False);
        }

        [Test]
        public void Suppression_IsIdempotentAndClearRemovesIt()
        {
            var store = new FarFieldStructureStore();

            store.SuppressBuiltSilhouette(10, 20, 30, 40);
            int firstVersion = store.Version;
            store.SuppressBuiltSilhouette(10, 20, 30, 40);

            Assert.That(store.Version, Is.EqualTo(firstVersion),
                "Re-registering the same semantic exclusion must not churn cached far meshes.");

            store.Clear();

            Assert.That(store.IsBuiltSilhouetteSuppressedAt(10, 20), Is.False);
            Assert.That(store.Version, Is.EqualTo(firstVersion + 1));
        }

        [Test]
        public void InvalidSuppressionBoundsFailClosed()
        {
            var store = new FarFieldStructureStore();

            Assert.That(() => store.SuppressBuiltSilhouette(10, 0, 10, 1),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => store.SuppressBuiltSilhouette(0, 10, 1, 10),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }
    }
}

using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class FeaturePresentationManifestTests
    {
        [Test]
        public void Query_ReturnsUnrelatedBakedShapesInStableSourceOrderWithoutResidencyProvider()
        {
            var manifest = new FeaturePresentationManifest(sectorSizeVoxels: 100);
            FeaturePresentationBake mountain = Bake(
                30, 1, FeatureKind.Landform,
                new int3(80, 10, -20), new int3(180, 140, 90), PrimitiveShape.Frustum);
            FeaturePresentationBake structure = Bake(
                10, 1, FeatureKind.Structure,
                new int3(-30, 20, -30), new int3(40, 90, 40), PrimitiveShape.Box);

            manifest.Upsert(mountain);
            manifest.Upsert(structure);

            var result = manifest.Query(new FeaturePresentationBounds(
                new int3(-100, 0, -100), new int3(250, 200, 150)));

            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result[0].SourceId, Is.EqualTo(10UL));
            Assert.That(result[1].SourceId, Is.EqualTo(30UL));
            Assert.That(result[0].GetPrimitive(0).Shape, Is.EqualTo(PrimitiveShape.Box));
            Assert.That(result[1].GetPrimitive(0).Shape, Is.EqualTo(PrimitiveShape.Frustum));
        }

        [Test]
        public void Query_CrossSectorBakeIsReturnedOnceAndHonorsVerticalBounds()
        {
            var manifest = new FeaturePresentationManifest(sectorSizeVoxels: 100);
            manifest.Upsert(Bake(
                20, 1, FeatureKind.Infrastructure,
                new int3(90, 200, 90), new int3(210, 260, 210), PrimitiveShape.Ramp));

            var below = manifest.Query(new FeaturePresentationBounds(
                new int3(0, 0, 0), new int3(300, 100, 300)));
            var intersecting = manifest.Query(new FeaturePresentationBounds(
                new int3(0, 180, 0), new int3(300, 300, 300)));

            Assert.That(below, Is.Empty);
            Assert.That(intersecting.Count, Is.EqualTo(1));
            Assert.That(intersecting[0].SourceId, Is.EqualTo(20UL));
        }

        [Test]
        public void Upsert_ReplacesRevisionAndSectorMembershipByStableSourceIdentity()
        {
            var manifest = new FeaturePresentationManifest(sectorSizeVoxels: 100);
            manifest.Upsert(Bake(
                7, 1, FeatureKind.Structure,
                new int3(0, 0, 0), new int3(30, 30, 30), PrimitiveShape.Box));
            manifest.Upsert(Bake(
                7, 2, FeatureKind.Structure,
                new int3(300, 0, 300), new int3(340, 40, 340), PrimitiveShape.RoundedBox));

            Assert.That(manifest.Count, Is.EqualTo(1));
            Assert.That(manifest.Query(new FeaturePresentationBounds(
                new int3(-10, -10, -10), new int3(100, 100, 100))), Is.Empty);

            var moved = manifest.Query(new FeaturePresentationBounds(
                new int3(250, -10, 250), new int3(400, 100, 400)));
            Assert.That(moved.Count, Is.EqualTo(1));
            Assert.That(moved[0].Revision, Is.EqualTo(2UL));
            Assert.That(moved[0].GetPrimitive(0).Shape, Is.EqualTo(PrimitiveShape.RoundedBox));
        }

        [Test]
        public void Remove_DeletesBakeAndAllSectorMembership()
        {
            var manifest = new FeaturePresentationManifest(sectorSizeVoxels: 100);
            manifest.Upsert(Bake(
                9, 1, FeatureKind.Landform,
                new int3(90, 0, 90), new int3(210, 120, 210), PrimitiveShape.Ellipsoid));

            Assert.That(manifest.Remove(9), Is.True);
            Assert.That(manifest.Remove(9), Is.False);
            Assert.That(manifest.Count, Is.Zero);
            Assert.That(manifest.TryGet(9, out _), Is.False);
            Assert.That(manifest.Query(new FeaturePresentationBounds(
                new int3(0, -10, 0), new int3(300, 200, 300))), Is.Empty);
        }

        private static FeaturePresentationBake Bake(
            ulong sourceId,
            ulong revision,
            FeatureKind kind,
            int3 min,
            int3 max,
            PrimitiveShape shape)
        {
            var primitive = new Primitive
            {
                Shape = shape,
                Mode = PrimitiveMode.Fill,
                A = min,
                B = max,
                Material = 1,
            };
            return new FeaturePresentationBake(
                sourceId,
                revision,
                kind,
                min,
                0,
                min,
                max,
                new[] { primitive });
        }
    }
}

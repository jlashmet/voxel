using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Architecture;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class WorldVisibilityManifestTests
    {
        [Test]
        public void Query_CrossSectorStructureIsReturnedOnce()
        {
            var manifest = new WorldVisibilityManifest(sectorSizeDm: 100);
            manifest.Upsert(Descriptor(20, 90, 10, 130, 40));

            var results = manifest.Query(new WorldVisibilityBoundsDm(0, 0, 200, 100));

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].StructureKey, Is.EqualTo(20UL));
        }

        [Test]
        public void Query_OrderIsStableIndependentOfInsertionAndSectorTraversal()
        {
            var first = new WorldVisibilityManifest(100);
            first.Upsert(Descriptor(30, 150, -40, 180, 20));
            first.Upsert(Descriptor(10, -40, -40, 20, 20));
            first.Upsert(Descriptor(20, 80, -20, 130, 30));

            var second = new WorldVisibilityManifest(100);
            second.Upsert(Descriptor(20, 80, -20, 130, 30));
            second.Upsert(Descriptor(30, 150, -40, 180, 20));
            second.Upsert(Descriptor(10, -40, -40, 20, 20));

            var bounds = new WorldVisibilityBoundsDm(-100, -100, 250, 100);
            var a = first.Query(bounds);
            var b = second.Query(bounds);

            Assert.That(a.Count, Is.EqualTo(3));
            Assert.That(b.Count, Is.EqualTo(3));
            for (int i = 0; i < a.Count; i++)
            {
                Assert.That(a[i].StructureKey, Is.EqualTo(b[i].StructureKey));
                if (i > 0) Assert.That(a[i - 1].StructureKey, Is.LessThan(a[i].StructureKey));
            }
        }

        [Test]
        public void Upsert_ReplacesOldSectorMembershipWithoutDuplicatingStructure()
        {
            var manifest = new WorldVisibilityManifest(100);
            manifest.Upsert(Descriptor(7, 0, 0, 40, 40, revision: 1));
            manifest.Upsert(Descriptor(7, 300, 300, 340, 340, revision: 2));

            Assert.That(manifest.Count, Is.EqualTo(1));
            Assert.That(manifest.Query(new WorldVisibilityBoundsDm(-10, -10, 100, 100)), Is.Empty);
            var moved = manifest.Query(new WorldVisibilityBoundsDm(250, 250, 400, 400));
            Assert.That(moved.Count, Is.EqualTo(1));
            Assert.That(moved[0].Revision, Is.EqualTo(2UL));
        }

        [Test]
        public void Remove_DeletesDescriptorAndAllSectorMembership()
        {
            var manifest = new WorldVisibilityManifest(100);
            manifest.Upsert(Descriptor(9, 90, 90, 210, 210));

            Assert.That(manifest.Remove(9), Is.True);
            Assert.That(manifest.Remove(9), Is.False);
            Assert.That(manifest.Count, Is.Zero);
            Assert.That(manifest.Query(new WorldVisibilityBoundsDm(0, 0, 300, 300)), Is.Empty);
        }

        [Test]
        public void Query_UsesPreplannedDescriptorsWithoutAnyResidencyProvider()
        {
            IWorldVisibilitySource source = new WorldVisibilityManifest(100);
            ((WorldVisibilityManifest)source).Upsert(Descriptor(42, 10000, -20000, 10100, -19900));

            var result = source.Query(new WorldVisibilityBoundsDm(9900, -20100, 10200, -19800));

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].StructureKey, Is.EqualTo(42UL));
        }

        private static StructureFarPresentation Descriptor(
            ulong key,
            int minX,
            int minY,
            int maxX,
            int maxY,
            ulong revision = 1) =>
            new StructureFarPresentation(
                key,
                settlementKey: 1,
                footprintMinDm: new Int2(minX, minY),
                footprintMaxDm: new Int2(maxX, maxY),
                heightDm: 80,
                facing: FrontageDirection.North,
                archetype: StructureArchetype.Townhouse,
                architectureKey: 2,
                materialFamilyKey: 3,
                visibilityClass: StructureVisibilityClass.OrdinaryStructure,
                revision: revision);
    }
}

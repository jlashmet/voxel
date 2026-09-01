using System.Collections.Generic;
using Game.WorldBuilder.Runtime;
using Game.WorldBuilder.Voxel;
using MountingForce.WorldGen;
using NUnit.Framework;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class NaturalScatterVisibilityIndexTests
    {
        [Test]
        public void GenerateOrdinaryBoulders_IsDeterministicByWorldSector()
        {
            IReadOnlyList<NaturalScatterRecord> first =
                NaturalScatterVisibilityIndex.GenerateOrdinaryBoulders(1234u, -2, 3, 1000, 24);
            IReadOnlyList<NaturalScatterRecord> second =
                NaturalScatterVisibilityIndex.GenerateOrdinaryBoulders(1234u, -2, 3, 1000, 24);

            Assert.That(first, Has.Count.EqualTo(24));
            Assert.That(second, Is.EqualTo(first));
            for (int i = 1; i < first.Count; i++)
                Assert.That(first[i].StableId, Is.GreaterThan(first[i - 1].StableId));
        }

        [Test]
        public void Query_UsesFixedSectorsAndStableOrderIncludingNegativeCoordinates()
        {
            var records = new List<NaturalScatterRecord>();
            records.AddRange(NaturalScatterVisibilityIndex.GenerateOrdinaryBoulders(55u, -1, 0, 100, 5));
            records.AddRange(NaturalScatterVisibilityIndex.GenerateOrdinaryBoulders(55u, 0, 0, 100, 5));
            records.AddRange(NaturalScatterVisibilityIndex.GenerateOrdinaryBoulders(55u, 1, 0, 100, 5));
            records.Reverse();

            IReadOnlyList<NaturalScatterRecord> visible =
                NaturalScatterVisibilityIndex.Query(records, 100, -1, 0, 0, 0);

            Assert.That(visible, Has.Count.EqualTo(10));
            for (int i = 0; i < visible.Count; i++)
            {
                int x = visible[i].PositionDm.X;
                Assert.That(x, Is.GreaterThanOrEqualTo(-100).And.LessThan(100));
                if (i > 0) Assert.That(visible[i].StableId, Is.GreaterThan(visible[i - 1].StableId));
            }
        }

        [Test]
        public void SignificantScatter_AutomaticallyPromotesOnceWhileOrdinaryPopulationStaysOutOfSparseIndex()
        {
            // 12 km reference horizon, one milliradian minimum projected significance.
            var policy = new NaturalScatterPromotionPolicy(
                referenceDistanceDm: 120000,
                minimumProjectedMicroradians: 1000);
            var baker = new FeaturePresentationBaker();
            var manifest = new FeaturePresentationManifest(sectorSizeVoxels: 512);

            IReadOnlyList<NaturalScatterRecord> ordinary =
                NaturalScatterVisibilityIndex.GenerateOrdinaryBoulders(923u, 4, -3, 1000, 128);
            for (int i = 0; i < ordinary.Count; i++)
            {
                NaturalScatterRecord record = ordinary[i];
                bool promoted = NaturalScatterPresentationPromotion.TryBake(
                    in record, in policy, material: 1, baker, out FeaturePresentationBake bake);
                if (promoted) manifest.Upsert(bake);
            }

            Assert.That(manifest.Count, Is.Zero,
                "Mass-population boulders must not create one sparse presentation record per member.");

            var giantRock = new NaturalScatterRecord(
                stableId: 0xC0FFEEUL,
                positionDm: new Int2(25000, -31000),
                radiusDm: 900,
                heightDm: 1400,
                kind: NaturalScatterKind.RockSpire,
                importance: NaturalScatterImportance.Ordinary,
                revision: 77UL);

            Assert.That(policy.ShouldPromote(in giantRock), Is.True,
                "Intrinsic projected significance should promote an exceptional-size member without a named-content registration.");
            Assert.That(NaturalScatterPresentationPromotion.TryBake(
                in giantRock, in policy, material: 1, baker, out FeaturePresentationBake first), Is.True);
            manifest.Upsert(first);

            Assert.That(first.SourceId, Is.EqualTo(giantRock.StableId));
            Assert.That(first.Kind, Is.EqualTo(FeatureKind.Landform));
            Assert.That(manifest.Count, Is.EqualTo(1));

            Assert.That(NaturalScatterPresentationPromotion.TryBake(
                in giantRock, in policy, material: 1, baker, out FeaturePresentationBake repeated), Is.True);
            manifest.Upsert(repeated);

            Assert.That(repeated.SourceId, Is.EqualTo(first.SourceId));
            Assert.That(repeated.Revision, Is.EqualTo(first.Revision));
            Assert.That(repeated.BoundsMin, Is.EqualTo(first.BoundsMin));
            Assert.That(repeated.BoundsMax, Is.EqualTo(first.BoundsMax));
            Assert.That(manifest.Count, Is.EqualTo(1),
                "Repeated derivation of the same authoritative scatter member must replace, not duplicate, its sparse presentation.");

            var namedOverride = new NaturalScatterRecord(
                stableId: 0x1234UL,
                positionDm: new Int2(-5000, 7000),
                radiusDm: 20,
                heightDm: 30,
                kind: NaturalScatterKind.NaturalArch,
                importance: NaturalScatterImportance.HorizonLandmark,
                revision: 9UL);
            Assert.That(policy.ShouldPromote(in namedOverride), Is.True,
                "Semantic importance remains an optional override for otherwise sub-threshold members.");
        }
    }
}

using System.Linq;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class WorldVisibilityClusterBuilderTests
    {
        [Test]
        public void Build_IsDeterministicIndependentOfInputOrder()
        {
            var a = Record(30UL, 7UL, 10, 10, 60, 60, 80, 3UL, StructureVisibilityClass.OrdinaryStructure);
            var b = Record(10UL, 7UL, 70, 20, 120, 70, 100, 3UL, StructureVisibilityClass.OrdinaryStructure);
            var c = Record(20UL, 7UL, 20, 80, 80, 130, 120, 9UL, StructureVisibilityClass.OrdinaryStructure);

            var first = WorldVisibilityClusterBuilder.Build(new[] { a, b, c }, 200);
            var second = WorldVisibilityClusterBuilder.Build(new[] { c, a, b }, 200);

            Assert.That(first, Has.Count.EqualTo(1));
            Assert.That(second, Has.Count.EqualTo(1));
            Assert.That(second[0].ClusterKey, Is.EqualTo(first[0].ClusterKey));
            Assert.That(second[0].Revision, Is.EqualTo(first[0].Revision));
            CollectionAssert.AreEqual(first[0].MemberStructureKeys, second[0].MemberStructureKeys);
            CollectionAssert.AreEqual(new ulong[] { 10UL, 20UL, 30UL }, first[0].MemberStructureKeys);
            Assert.That(first[0].DominantMaterialFamilyKey, Is.EqualTo(3UL));
            Assert.That(first[0].MaxHeightDm, Is.EqualTo(120));
            Assert.That(first[0].MemberCount, Is.EqualTo(3));
        }

        [Test]
        public void CrossingSectorBoundary_HasSingleCenterOwnedClusterMembership()
        {
            var crossing = Record(
                1UL,
                2UL,
                150,
                20,
                250,
                80,
                100,
                3UL,
                StructureVisibilityClass.OrdinaryStructure);

            var clusters = WorldVisibilityClusterBuilder.Build(new[] { crossing }, 200);

            Assert.That(clusters, Has.Count.EqualTo(1));
            Assert.That(clusters[0].SectorX, Is.EqualTo(1),
                "A boundary-crossing structure belongs to the deterministic sector containing its center, never both sectors.");
            Assert.That(clusters[0].MemberStructureKeys.Single(), Is.EqualTo(1UL));
        }

        [Test]
        public void LandmarksAndAnchors_RemainOutsideOrdinaryClusterMembership()
        {
            var ordinary = Record(1UL, 5UL, 0, 0, 50, 50, 80, 3UL, StructureVisibilityClass.OrdinaryStructure);
            var anchor = Record(2UL, 5UL, 60, 0, 110, 50, 100, 4UL, StructureVisibilityClass.SettlementAnchor);
            var landmark = Record(3UL, 5UL, 120, 0, 170, 50, 130, 4UL, StructureVisibilityClass.Landmark);

            var clusters = WorldVisibilityClusterBuilder.Build(new[] { landmark, ordinary, anchor }, 200);

            Assert.That(clusters, Has.Count.EqualTo(1));
            CollectionAssert.AreEqual(new ulong[] { 1UL }, clusters[0].MemberStructureKeys);
        }

        [Test]
        public void NegativeCoordinates_UseFloorSectorsDeterministically()
        {
            var record = Record(1UL, 1UL, -210, -10, -190, 10, 50, 1UL, StructureVisibilityClass.OrdinaryStructure);

            var clusters = WorldVisibilityClusterBuilder.Build(new[] { record }, 200);

            Assert.That(clusters[0].SectorX, Is.EqualTo(-1));
            Assert.That(clusters[0].SectorZ, Is.EqualTo(0));
        }

        private static StructureFarPresentation Record(
            ulong structureKey,
            ulong settlementKey,
            int minX,
            int minZ,
            int maxX,
            int maxZ,
            int height,
            ulong materialFamily,
            StructureVisibilityClass visibility)
        {
            return new StructureFarPresentation(
                structureKey,
                settlementKey,
                new Int2(minX, minZ),
                new Int2(maxX, maxZ),
                height,
                (FrontageDirection)0,
                (StructureArchetype)0,
                11UL,
                materialFamily,
                visibility,
                structureKey * 17UL);
        }
    }
}

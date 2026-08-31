using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ForestCanopyClusterBuilderTests
    {
        [Test]
        public void Build_IsStableAcrossInputOrderAndKeepsLandmarkIndependent()
        {
            TreeVisibilityEntry a = Entry(1u, 0, 0, new float3(2f, 0f, 2f));
            TreeVisibilityEntry b = Entry(2u, 0, 0, new float3(6f, 0f, 5f));
            TreeVisibilityEntry landmark = Entry(3u, 0, 0, new float3(8f, 0f, 7f));

            IReadOnlyList<ForestCanopyCluster> first = ForestCanopyClusterBuilder.Build(
                new[] { a, b, landmark },
                tree => tree.StableId == landmark.StableId);
            IReadOnlyList<ForestCanopyCluster> second = ForestCanopyClusterBuilder.Build(
                new[] { landmark, b, a },
                tree => tree.StableId == landmark.StableId);

            Assert.That(first, Has.Count.EqualTo(1));
            Assert.That(second, Has.Count.EqualTo(1));
            Assert.That(first[0].StableId, Is.EqualTo(second[0].StableId));
            Assert.That(first[0].Revision, Is.EqualTo(second[0].Revision));
            Assert.That(first[0].MemberIds, Is.EqualTo(second[0].MemberIds));
            Assert.That(first[0].MemberCount, Is.EqualTo(2));
            Assert.That(first[0].MemberIds.Contains(landmark.StableId), Is.False);
        }

        [Test]
        public void DamageChange_InvalidatesOnlyAffectedSectorCluster()
        {
            TreeVisibilityEntry a = Entry(11u, 0, 0, new float3(2f, 0f, 2f));
            TreeVisibilityEntry b = Entry(12u, 1, 0, new float3(12f, 0f, 2f));
            IReadOnlyList<ForestCanopyCluster> before = ForestCanopyClusterBuilder.Build(new[] { a, b });

            TreeVisibilityEntry damagedA = WithDamage(a, new TreeDamageState(0.35f, false));
            IReadOnlyList<ForestCanopyCluster> after = ForestCanopyClusterBuilder.Build(new[] { damagedA, b });

            Assert.That(after[0].StableId, Is.EqualTo(before[0].StableId));
            Assert.That(after[0].Revision, Is.Not.EqualTo(before[0].Revision));
            Assert.That(after[0].MeanFoliageHealth, Is.LessThan(before[0].MeanFoliageHealth));
            Assert.That(after[1].StableId, Is.EqualTo(before[1].StableId));
            Assert.That(after[1].Revision, Is.EqualTo(before[1].Revision));
        }

        [Test]
        public void SeveredTree_IsRemovedFromCanopyMembershipWithoutChangingOtherSector()
        {
            TreeVisibilityEntry a = Entry(21u, -1, 0, new float3(-2f, 0f, 2f));
            TreeVisibilityEntry b = Entry(22u, -1, 0, new float3(-6f, 0f, 4f));
            TreeVisibilityEntry c = Entry(23u, 1, 0, new float3(12f, 0f, 2f));
            IReadOnlyList<ForestCanopyCluster> before = ForestCanopyClusterBuilder.Build(new[] { a, b, c });

            IReadOnlyList<ForestCanopyCluster> after = ForestCanopyClusterBuilder.Build(
                new[] { WithDamage(a, new TreeDamageState(0f, true)), b, c });

            Assert.That(after, Has.Count.EqualTo(2));
            Assert.That(after[0].MemberCount, Is.EqualTo(1));
            Assert.That(after[0].MemberIds.Contains(b.StableId), Is.True);
            Assert.That(after[1].Revision, Is.EqualTo(before[1].Revision));
        }

        private static TreeVisibilityEntry Entry(uint seed, int sectorX, int sectorZ, float3 position)
        {
            var tree = new TreeInstance
            {
                Seed = seed,
                Species = TreeSpecies.Pine,
                PositionMetres = position,
                Scale = 1f,
            };
            return new TreeVisibilityEntry(
                VegetationVisibility.StableTreeId(tree),
                (int)seed,
                sectorX,
                sectorZ,
                tree,
                new TreeDamageState(1f, false));
        }

        private static TreeVisibilityEntry WithDamage(TreeVisibilityEntry entry, TreeDamageState damage) =>
            new TreeVisibilityEntry(
                entry.StableId,
                entry.SourceIndex,
                entry.SectorX,
                entry.SectorZ,
                entry.Instance,
                damage);
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Vegetation.Api;
using VoxelEngine.Vegetation.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class VegetationFarProxyInvalidationTests
    {
        private readonly List<TreeVisibilityEntry> _visible = new();

        [SetUp]
        public void SetUp()
        {
            TreeWorldRuntime.Clear();
            TreeWorldRuntime.Replace(new[]
            {
                new TreeInstance
                {
                    PositionMetres = new float3(32f, 0f, 48f),
                    Species = TreeSpecies.Oak,
                    Seed = 1234u,
                    Scale = 1f,
                },
                new TreeInstance
                {
                    PositionMetres = new float3(40f, 0f, 52f),
                    Species = TreeSpecies.Pine,
                    Seed = 5678u,
                    Scale = 1.1f,
                },
            });
        }

        [TearDown]
        public void TearDown()
        {
            TreeWorldRuntime.Clear();
        }

        [Test]
        public void ExistingTreeWorldDamageChangesOnlyAffectedPresentationRevision()
        {
            ITreeWorldReadSource source = TreeWorldReadRegistry.Current;
            Query(source);

            Assert.That(_visible.Count, Is.EqualTo(2));
            ulong firstStableId = _visible[0].StableId;
            ulong secondStableId = _visible[1].StableId;
            ulong firstRevision = _visible[0].PresentationRevision;
            ulong secondRevision = _visible[1].PresentationRevision;

            int damagedSourceIndex = _visible[0].SourceIndex;
            TreeWorldRuntime.SetDamage(damagedSourceIndex, 0.45f, false);
            Query(source);

            TreeVisibilityEntry firstAfter = Find(firstStableId);
            TreeVisibilityEntry secondAfter = Find(secondStableId);
            Assert.That(firstAfter.PresentationRevision, Is.Not.EqualTo(firstRevision));
            Assert.That(secondAfter.PresentationRevision, Is.EqualTo(secondRevision));
            Assert.That(firstAfter.Damage.FoliageHealth, Is.EqualTo(0.45f).Within(0.001f));
        }

        [Test]
        public void ExistingRemovedBranchInvalidatesIndividualAndOwningCanopyOnly()
        {
            ITreeWorldReadSource source = TreeWorldReadRegistry.Current;
            Query(source);
            IReadOnlyList<ForestCanopyCluster> before = ForestCanopyClusterBuilder.Build(_visible);

            Assert.That(before.Count, Is.EqualTo(1));
            ulong clusterRevision = before[0].Revision;
            ulong treeRevision = _visible[0].PresentationRevision;
            int sourceIndex = _visible[0].SourceIndex;

            Assert.That(TreeWorldRuntime.RemoveBranch(sourceIndex, 2), Is.True);
            Query(source);
            IReadOnlyList<ForestCanopyCluster> after = ForestCanopyClusterBuilder.Build(_visible);

            Assert.That(_visible[0].PresentationRevision, Is.Not.EqualTo(treeRevision));
            Assert.That(after.Count, Is.EqualTo(1));
            Assert.That(after[0].StableId, Is.EqualTo(before[0].StableId));
            Assert.That(after[0].Revision, Is.Not.EqualTo(clusterRevision));
        }

        [Test]
        public void ExistingSeverStateRemovesTreeFromCanopyWithoutChangingStableIdentity()
        {
            ITreeWorldReadSource source = TreeWorldReadRegistry.Current;
            Query(source);
            ulong severedStableId = _visible[0].StableId;
            int sourceIndex = _visible[0].SourceIndex;

            TreeWorldRuntime.SetDamage(sourceIndex, 0f, true);
            Query(source);

            TreeVisibilityEntry severed = Find(severedStableId);
            Assert.That(severed.Damage.Severed, Is.True);
            IReadOnlyList<ForestCanopyCluster> after = ForestCanopyClusterBuilder.Build(_visible);
            Assert.That(after.Count, Is.EqualTo(1));
            Assert.That(after[0].MemberCount, Is.EqualTo(1));
            Assert.That(Contains(after[0].MemberIds, severedStableId), Is.False);
        }

        private void Query(ITreeWorldReadSource source)
        {
            VegetationVisibility.QueryTrees(
                source,
                128f,
                new VisibilitySectorBounds(-1, -1, 1, 1),
                _visible);
        }

        private TreeVisibilityEntry Find(ulong stableId)
        {
            for (int i = 0; i < _visible.Count; i++)
            {
                if (_visible[i].StableId == stableId)
                    return _visible[i];
            }

            Assert.Fail($"Missing tree visibility entry {stableId}.");
            return default;
        }

        private static bool Contains(IReadOnlyList<ulong> ids, ulong stableId)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == stableId)
                    return true;
            }

            return false;
        }
    }
}

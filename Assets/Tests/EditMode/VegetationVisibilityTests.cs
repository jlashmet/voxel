using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class VegetationVisibilityTests
    {
        [Test]
        public void VegetationQuery_IsStableAcrossInputOrderAndHandlesNegativeSectors()
        {
            VegetationInstance a = Vegetation(11u, new float3(-1f, 0f, 1f));
            VegetationInstance b = Vegetation(22u, new float3(12f, 0f, 1f));
            VegetationInstance c = Vegetation(33u, new float3(-12f, 0f, -1f));
            var bounds = new VisibilitySectorBounds(-2, -1, 1, 1);
            var first = new List<VegetationVisibilityEntry>();
            var second = new List<VegetationVisibilityEntry>();

            VegetationVisibility.QueryVegetation(new[] { a, b, c }, 10f, bounds, first);
            VegetationVisibility.QueryVegetation(new[] { c, a, b }, 10f, bounds, second);

            Assert.That(first, Has.Count.EqualTo(3));
            Assert.That(second, Has.Count.EqualTo(3));
            for (int i = 0; i < first.Count; i++)
                Assert.That(second[i].StableId, Is.EqualTo(first[i].StableId));
            Assert.That(SectorFor(first, VegetationVisibility.StableVegetationId(a)), Is.EqualTo(new int2(-1, 0)));
            Assert.That(SectorFor(first, VegetationVisibility.StableVegetationId(c)), Is.EqualTo(new int2(-2, -1)));
        }

        [Test]
        public void TreeQuery_UsesExistingReadSourceWithoutMutatingWorldTruth()
        {
            TreeInstance firstTree = Tree(1u, new float3(5f, 0f, 5f));
            TreeInstance secondTree = Tree(2u, new float3(25f, 0f, 5f));
            var source = new FakeTreeSource(firstTree, secondTree);
            int versionBefore = source.Version;
            var output = new List<TreeVisibilityEntry>();

            VegetationVisibility.QueryTrees(
                source,
                10f,
                new VisibilitySectorBounds(0, 0, 0, 0),
                output);

            Assert.That(output, Has.Count.EqualTo(1));
            Assert.That(output[0].StableId, Is.EqualTo(VegetationVisibility.StableTreeId(firstTree)));
            Assert.That(output[0].SourceIndex, Is.EqualTo(0));
            Assert.That(source.Version, Is.EqualTo(versionBefore));
            Assert.That(source.SkeletonRequests, Is.EqualTo(0),
                "visibility membership must not generate tree skeletons or presentation geometry");
        }

        [Test]
        public void CameraSectorWindow_ChangesMembershipWithoutChangingStableIds()
        {
            VegetationInstance near = Vegetation(101u, new float3(5f, 0f, 5f));
            VegetationInstance far = Vegetation(202u, new float3(105f, 0f, 5f));
            var all = new[] { near, far };
            var output = new List<VegetationVisibilityEntry>();

            VisibilitySectorBounds nearWindow = VisibilitySectorBounds.Around(new float2(5f, 5f), 4f, 10f);
            VegetationVisibility.QueryVegetation(all, 10f, nearWindow, output);
            ulong nearId = output[0].StableId;
            Assert.That(output, Has.Count.EqualTo(1));

            VisibilitySectorBounds farWindow = VisibilitySectorBounds.Around(new float2(105f, 5f), 4f, 10f);
            VegetationVisibility.QueryVegetation(all, 10f, farWindow, output);
            Assert.That(output, Has.Count.EqualTo(1));
            Assert.That(output[0].StableId, Is.EqualTo(VegetationVisibility.StableVegetationId(far)));

            VegetationVisibility.QueryVegetation(all, 10f, nearWindow, output);
            Assert.That(output[0].StableId, Is.EqualTo(nearId));
        }

        private static VegetationInstance Vegetation(uint seed, float3 position) =>
            new VegetationInstance
            {
                Seed = seed,
                PositionMetres = position,
                SurfaceNormal = new float3(0f, 1f, 0f),
                Kind = VegetationKind.Bush,
                Scale = 1f,
            };

        private static TreeInstance Tree(uint seed, float3 position) =>
            new TreeInstance
            {
                Seed = seed,
                PositionMetres = position,
                Species = TreeSpecies.Oak,
                Scale = 1f,
            };

        private static int2 SectorFor(IReadOnlyList<VegetationVisibilityEntry> entries, ulong stableId)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].StableId == stableId) return new int2(entries[i].SectorX, entries[i].SectorZ);
            Assert.Fail("stable vegetation id not found");
            return default;
        }

        private sealed class FakeTreeSource : ITreeWorldReadSource
        {
            private readonly TreeInstance[] _instances;
            private readonly TreeDamageState[] _damage;

            public FakeTreeSource(params TreeInstance[] instances)
            {
                _instances = instances ?? Array.Empty<TreeInstance>();
                _damage = new TreeDamageState[_instances.Length];
                for (int i = 0; i < _damage.Length; i++) _damage[i] = new TreeDamageState(1f, false);
            }

            public IReadOnlyList<TreeInstance> Instances => _instances;
            public IReadOnlyList<TreeDamageState> Damage => _damage;
            public int Version => 7;
            public int DamageVersion => 3;
            public int SkeletonRequests { get; private set; }

            public event Action SnapshotChanged { add { } remove { } }
            public event Action<TreeBranchCutEvent> BranchCut { add { } remove { } }
            public event Action<TreeDamageChangedEvent> DamageChanged { add { } remove { } }
            public event Action<TreeSeveredEvent> TreeSevered { add { } remove { } }

            public IReadOnlyCollection<int> RemovedBranches(int treeIndex) => Array.Empty<int>();

            public TreeSkeletonSnapshot SkeletonFor(int treeIndex)
            {
                SkeletonRequests++;
                return null;
            }

            public TreeSkeletonSnapshot SkeletonFor(in TreeInstance instance)
            {
                SkeletonRequests++;
                return null;
            }
        }
    }
}

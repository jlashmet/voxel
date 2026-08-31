using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen.Architecture;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ShowcaseFarStructureSourceTests
    {
        [Test]
        public void Query_UsesSemanticSourceWithoutResidencyAndPreservesStableIdentity()
        {
            var record = new StructureFarPresentation(
                0x1234UL,
                0x55UL,
                new Int2(100, 200),
                new Int2(300, 500),
                120,
                (FrontageDirection)0,
                (StructureArchetype)0,
                0x77UL,
                0x8899UL,
                StructureVisibilityClass.Landmark,
                0xABCDUL);
            var source = new FakeVisibilitySource(record);
            float2 policyCamera = new float2(-1f, -1f);
            var adapter = new ShowcaseFarStructureSource(
                source,
                (_, camera) =>
                {
                    policyCamera = camera;
                    return FarStructureTier.Far;
                },
                xz => xz.x + xz.y);

            var cameraXZ = new float2(20f, 35f);
            IReadOnlyList<FarStructureInstance> instances = adapter.Query(cameraXZ, 30f);

            Assert.That(source.QueryCount, Is.EqualTo(1));
            Assert.That(policyCamera.x, Is.EqualTo(cameraXZ.x));
            Assert.That(policyCamera.y, Is.EqualTo(cameraXZ.y));
            Assert.That(instances, Has.Count.EqualTo(1));
            FarStructureInstance instance = instances[0];
            Assert.That(instance.StableId, Is.EqualTo(0x1234UL));
            Assert.That(instance.Tier, Is.EqualTo(FarStructureTier.Far));
            Assert.That(instance.Position.x, Is.EqualTo(20f).Within(0.001f));
            Assert.That(instance.Position.z, Is.EqualTo(35f).Within(0.001f));
            Assert.That(instance.Position.y, Is.EqualTo(55f).Within(0.001f));
            Assert.That(instance.Scale.x, Is.EqualTo(20f).Within(0.001f));
            Assert.That(instance.Scale.y, Is.EqualTo(12f).Within(0.001f));
            Assert.That(instance.Scale.z, Is.EqualTo(30f).Within(0.001f));
            Assert.That((instance.Flags & FarStructureVisualFlags.Landmark) != 0, Is.True);
        }

        [Test]
        public void Query_CulledPolicyOmitsRecord()
        {
            var record = Record(1UL, 1UL, 0, 0, StructureVisibilityClass.OrdinaryStructure);
            var adapter = new ShowcaseFarStructureSource(
                new FakeVisibilitySource(record),
                (_, __) => FarStructureTier.Culled,
                _ => 0f);

            Assert.That(adapter.Query(float2.zero, 20f), Is.Empty);
        }

        [Test]
        public void Query_RemovedLandmarkStaysAbsentWithoutChangingSemanticManifest()
        {
            StructureFarPresentation landmark = Record(
                77UL, 5UL, 0, 0, StructureVisibilityClass.Landmark);
            var source = new FakeVisibilitySource(landmark);
            var state = new StructureVisualStateStore();
            var adapter = new ShowcaseFarStructureSource(
                source,
                (_, __) => FarStructureTier.Horizon,
                _ => 0f,
                null,
                state);

            Assert.That(adapter.Query(float2.zero, 100f), Has.Count.EqualTo(1));
            Assert.That(state.Remove(landmark.StructureKey), Is.True);
            Assert.That(adapter.Query(float2.zero, 100f), Is.Empty,
                "coarse CPU state must suppress the far proxy after voxel regions can unload");
            Assert.That(source.TryGet(landmark.StructureKey, out _), Is.True,
                "visual removal must not mutate the semantic planning manifest");

            Assert.That(state.Restore(landmark.StructureKey), Is.True);
            Assert.That(adapter.Query(float2.zero, 100f), Has.Count.EqualTo(1));
        }

        [Test]
        public void Query_FarClusterBoundsDenseSettlementAndPreservesLandmarkWithoutDoubleMembers()
        {
            var records = new List<StructureFarPresentation>();
            for (int i = 0; i < 12; i++)
            {
                int x = (i % 4) * 120;
                int z = (i / 4) * 120;
                records.Add(Record(
                    (ulong)(100 + i),
                    settlementKey: 0xCAFEUL,
                    minX: x,
                    minZ: z,
                    visibility: StructureVisibilityClass.OrdinaryStructure));
            }
            records.Add(Record(
                999UL,
                settlementKey: 0xCAFEUL,
                minX: 180,
                minZ: 120,
                visibility: StructureVisibilityClass.Landmark));

            var source = new FakeVisibilitySource(records.ToArray());
            var adapter = new ShowcaseFarStructureSource(
                source,
                (_, __) => FarStructureTier.Mid,
                _ => 0f,
                new ShowcaseFarStructureSource.ClusterConfiguration(
                    sectorSizeDm: 1000,
                    selectTier: (_, __) => FarStructureTier.Far));

            IReadOnlyList<FarStructureInstance> instances = adapter.Query(new float2(20f, 15f), 100f);

            Assert.That(instances, Has.Count.EqualTo(2),
                "twelve ordinary buildings should collapse to one cluster while the landmark remains independent");
            Assert.That(ContainsStableId(instances, 999UL), Is.True);
            Assert.That(CountProxy(instances, "settlement-cluster"), Is.EqualTo(1));
            Assert.That(CountOrdinaryMemberIds(instances, 100UL, 111UL), Is.EqualTo(0),
                "active cluster representation must suppress its individual ordinary members");
        }

        [Test]
        public void Query_InactiveClusterReturnsMembersInsteadOfDoubleRenderingClusterAndMembers()
        {
            StructureFarPresentation first = Record(
                10UL, 7UL, 0, 0, StructureVisibilityClass.OrdinaryStructure);
            StructureFarPresentation second = Record(
                11UL, 7UL, 120, 0, StructureVisibilityClass.OrdinaryStructure);
            var adapter = new ShowcaseFarStructureSource(
                new FakeVisibilitySource(first, second),
                (_, __) => FarStructureTier.Mid,
                _ => 0f,
                new ShowcaseFarStructureSource.ClusterConfiguration(
                    sectorSizeDm: 1000,
                    selectTier: (_, __) => FarStructureTier.Culled));

            IReadOnlyList<FarStructureInstance> instances = adapter.Query(float2.zero, 50f);

            Assert.That(instances, Has.Count.EqualTo(2));
            Assert.That(CountProxy(instances, "settlement-cluster"), Is.EqualTo(0));
            Assert.That(ContainsStableId(instances, 10UL), Is.True);
            Assert.That(ContainsStableId(instances, 11UL), Is.True);
        }

        private static StructureFarPresentation Record(
            ulong key,
            ulong settlementKey,
            int minX,
            int minZ,
            StructureVisibilityClass visibility)
        {
            return new StructureFarPresentation(
                key,
                settlementKey,
                new Int2(minX, minZ),
                new Int2(minX + 100, minZ + 100),
                80,
                (FrontageDirection)0,
                (StructureArchetype)0,
                3UL,
                4UL,
                visibility,
                5UL);
        }

        private static bool ContainsStableId(IReadOnlyList<FarStructureInstance> instances, ulong stableId)
        {
            for (int i = 0; i < instances.Count; i++)
                if (instances[i].StableId == stableId) return true;
            return false;
        }

        private static int CountProxy(IReadOnlyList<FarStructureInstance> instances, string proxyKey)
        {
            int count = 0;
            for (int i = 0; i < instances.Count; i++)
                if (string.Equals(instances[i].ProxyKey, proxyKey, StringComparison.Ordinal)) count++;
            return count;
        }

        private static int CountOrdinaryMemberIds(
            IReadOnlyList<FarStructureInstance> instances,
            ulong first,
            ulong last)
        {
            int count = 0;
            for (int i = 0; i < instances.Count; i++)
                if (instances[i].StableId >= first && instances[i].StableId <= last) count++;
            return count;
        }

        private sealed class FakeVisibilitySource : IWorldVisibilitySource
        {
            private readonly StructureFarPresentation[] _records;

            public FakeVisibilitySource(params StructureFarPresentation[] records)
            {
                _records = records ?? Array.Empty<StructureFarPresentation>();
            }

            public int QueryCount { get; private set; }

            public bool TryGet(ulong structureKey, out StructureFarPresentation value)
            {
                for (int i = 0; i < _records.Length; i++)
                {
                    if (_records[i].StructureKey != structureKey) continue;
                    value = _records[i];
                    return true;
                }

                value = default;
                return false;
            }

            public IReadOnlyList<StructureFarPresentation> Query(WorldVisibilityBoundsDm bounds)
            {
                QueryCount++;
                var matches = new List<StructureFarPresentation>();
                for (int i = 0; i < _records.Length; i++)
                    if (bounds.Intersects(_records[i])) matches.Add(_records[i]);
                return matches;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class SettlementFarHlodTests
    {
        [Test]
        public void Source_SwitchesOrdinaryMembersToOneClusterWhileKeepingLandmarkIndependent()
        {
            StructureFarPresentation[] records =
            {
                Record(1UL, 7UL, 0, 0, 80, 80, StructureVisibilityClass.OrdinaryStructure),
                Record(2UL, 7UL, 90, 0, 170, 80, StructureVisibilityClass.OrdinaryStructure),
                Record(3UL, 7UL, 0, 90, 80, 170, StructureVisibilityClass.OrdinaryStructure),
                Record(99UL, 7UL, 90, 90, 170, 170, StructureVisibilityClass.Landmark)
            };
            var source = new FakeVisibilitySource(records);
            var camera = new float2(-20f, -20f);

            var mid = new ShowcaseFarStructureSource(
                source,
                (_, __) => FarStructureTier.Mid,
                _ => 0f,
                new ShowcaseFarStructureSource.ClusterConfiguration(
                    200,
                    (_, __) => FarStructureTier.Culled));
            IReadOnlyList<FarStructureInstance> midInstances = mid.Query(camera, 100f);

            Assert.That(midInstances.Count, Is.EqualTo(4));
            Assert.That(midInstances.Any(value => value.ProxyKey == "settlement-cluster"), Is.False);

            var far = new ShowcaseFarStructureSource(
                source,
                (_, __) => FarStructureTier.Far,
                _ => 0f,
                new ShowcaseFarStructureSource.ClusterConfiguration(
                    200,
                    (_, __) => FarStructureTier.Far));
            IReadOnlyList<FarStructureInstance> farInstances = far.Query(camera, 100f);

            Assert.That(farInstances.Count, Is.EqualTo(2),
                "Three ordinary structures should collapse to one cluster while the landmark remains independent.");
            FarStructureInstance cluster = farInstances.Single(value => value.ProxyKey == "settlement-cluster");
            Assert.That(cluster.Tier, Is.EqualTo(FarStructureTier.Far));
            Assert.That(farInstances.Any(value => value.StableId == 99UL), Is.True);
            Assert.That(farInstances.Any(value => value.StableId == 1UL || value.StableId == 2UL || value.StableId == 3UL), Is.False);
        }

        [Test]
        public void ClusterPolicy_UsesSeparateEnterExitThresholdsForMemberHandoff()
        {
            StructureFarPresentation[] members =
            {
                Record(1UL, 5UL, 0, 0, 100, 100, StructureVisibilityClass.OrdinaryStructure),
                Record(2UL, 5UL, 100, 0, 300, 100, StructureVisibilityClass.OrdinaryStructure)
            };
            WorldVisibilityClusterBuilder.Cluster cluster =
                WorldVisibilityClusterBuilder.Build(members, 400).Single();
            var policy = new FarWorldVisibilityPolicy(
                new FarWorldVisibilityPolicy.Thresholds(100f, 80f, 40f, 30f, 10f, 5f),
                new FarWorldVisibilityPolicy.DistanceCaps(300f, 900f, 1200f, 2000f),
                90f,
                1000);
            float centerX = (cluster.FootprintMinDm.X + cluster.FootprintMaxDm.X) * 0.05f;
            float centerZ = (cluster.FootprintMinDm.Y + cluster.FootprintMaxDm.Y) * 0.05f;

            Assert.That(policy.SelectCluster(cluster, new float2(centerX - 140f, centerZ)),
                Is.EqualTo(FarStructureTier.Culled));
            Assert.That(policy.SelectCluster(cluster, new float2(centerX - 200f, centerZ)),
                Is.EqualTo(FarStructureTier.Far));
            Assert.That(policy.SelectCluster(cluster, new float2(centerX - 170f, centerZ)),
                Is.EqualTo(FarStructureTier.Far),
                "An active cluster must remain active inside the hysteresis band while approaching.");
            Assert.That(policy.SelectCluster(cluster, new float2(centerX - 140f, centerZ)),
                Is.EqualTo(FarStructureTier.Culled));
            Assert.That(policy.SelectCluster(cluster, new float2(centerX - 170f, centerZ)),
                Is.EqualTo(FarStructureTier.Culled),
                "A culled cluster must not re-enter until projected size crosses the farther exit threshold.");
            Assert.That(policy.SelectCluster(cluster, new float2(centerX - 200f, centerZ)),
                Is.EqualTo(FarStructureTier.Far));
        }

        private static StructureFarPresentation Record(
            ulong key,
            ulong settlementKey,
            int minX,
            int minZ,
            int maxX,
            int maxZ,
            StructureVisibilityClass visibility)
        {
            return new StructureFarPresentation(
                key,
                settlementKey,
                new Int2(minX, minZ),
                new Int2(maxX, maxZ),
                100,
                (FrontageDirection)0,
                (StructureArchetype)0,
                11UL,
                22UL,
                visibility,
                key * 31UL);
        }

        private sealed class FakeVisibilitySource : IWorldVisibilitySource
        {
            private readonly StructureFarPresentation[] _records;

            public FakeVisibilitySource(StructureFarPresentation[] records)
            {
                _records = records;
            }

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

            public IReadOnlyList<StructureFarPresentation> Query(WorldVisibilityBoundsDm bounds) =>
                _records.Where(bounds.Intersects)
                    .OrderBy(value => value.StructureKey)
                    .ToArray();
        }
    }
}

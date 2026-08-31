using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.Vegetation;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ProceduralForestCanopyRendererTests
    {
        [Test]
        public void SetClusters_UsesOneInstancedProxyPerDeterministicCluster()
        {
            var trees = new List<TreeVisibilityEntry>
            {
                Entry(1UL, 0, new float3(5f, 0f, 5f)),
                Entry(2UL, 1, new float3(12f, 0f, 8f)),
                Entry(3UL, 2, new float3(70f, 0f, 5f)),
            };
            IReadOnlyList<ForestCanopyCluster> clusters = ForestCanopyClusterBuilder.Build(trees);
            var go = new GameObject("forest-canopy-renderer-test");
            try
            {
                var renderer = go.AddComponent<ProceduralForestCanopyRenderer>();
                renderer.SetClusters(clusters);

                Assert.That(clusters.Count, Is.EqualTo(2));
                Assert.That(renderer.InstanceCount, Is.EqualTo(2));
                Assert.That(renderer.EstimatedDrawCount, Is.EqualTo(1),
                    "many canopy clusters should share one instanced draw rather than GameObjects per tree");
                Assert.That(go.transform.childCount, Is.EqualTo(0),
                    "canopy HLOD must remain an instanced presentation path, not persistent cluster objects");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SetClusters_ExcludesSeveredTreesThroughExistingTreeStateProjection()
        {
            var trees = new List<TreeVisibilityEntry>
            {
                Entry(10UL, 0, new float3(5f, 0f, 5f), severed: true),
                Entry(11UL, 1, new float3(8f, 0f, 6f)),
            };
            IReadOnlyList<ForestCanopyCluster> clusters = ForestCanopyClusterBuilder.Build(trees);
            var go = new GameObject("forest-canopy-severed-test");
            try
            {
                var renderer = go.AddComponent<ProceduralForestCanopyRenderer>();
                renderer.SetClusters(clusters);

                Assert.That(clusters.Count, Is.EqualTo(1));
                Assert.That(clusters[0].MemberCount, Is.EqualTo(1));
                Assert.That(renderer.InstanceCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static TreeVisibilityEntry Entry(
            ulong id, int index, float3 position, bool severed = false)
        {
            var instance = new TreeInstance
            {
                PositionMetres = position,
                Species = TreeSpecies.Pine,
                Seed = (uint)(index + 1),
                Scale = 1f,
            };
            return new TreeVisibilityEntry(
                id,
                index,
                (int)math.floor(position.x / 64f),
                (int)math.floor(position.z / 64f),
                instance,
                new TreeDamageState(1f, severed));
        }
    }
}

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ArchReferenceGrowthTopologyCleanupPassTests
    {
        private const int IvyClusterCount = 16;
        private const int IvyLeavesPerCluster = 8;
        private const int IvyLeafVertexCount = 17;

        [UnityTest, Timeout(30000)]
        public IEnumerator FinalTopologyCleanupRemovesAllStemQuadsWithoutRegressingAaaMassAcrossRebuild()
        {
            var host = new GameObject("Arch topology cleanup regression");
            try
            {
                host.transform.SetPositionAndRotation(
                    new Vector3(-0.85728186f, 8.398123f, -9.309617f),
                    new Quaternion(0.09724782f, -0.01389580f, 0.00135791f, 0.9951624f));
                Camera camera = host.AddComponent<Camera>();
                ArchReferenceGrowthWorldSpace.EnsureInstalled(camera);
                ArchReferenceGrowth growth = host.AddComponent<ArchReferenceGrowth>();
                host.AddComponent<ArchReferenceGrowthDetailPass>();
                host.AddComponent<ArchReferenceGrowthLushPass>();
                ArchReferenceGrowthEnglishIvyPass english = host.AddComponent<ArchReferenceGrowthEnglishIvyPass>();
                for (int frame = 0; frame < 28 && !english.EnglishApplied; frame++) yield return null;
                Assert.That(english.EnglishApplied, Is.True);
                ArchReferenceGrowthMassBreakupPass mass = host.AddComponent<ArchReferenceGrowthMassBreakupPass>();
                for (int frame = 0; frame < 32 && !mass.CompositionApplied; frame++) yield return null;
                Assert.That(mass.CompositionApplied, Is.True);
                ArchReferenceGrowthReadabilityPass readability = host.AddComponent<ArchReferenceGrowthReadabilityPass>();
                for (int frame = 0; frame < 36 && !readability.ReadabilityApplied; frame++) yield return null;
                Assert.That(readability.ReadabilityApplied, Is.True);
                ArchReferenceGrowthArchitecturalPass architectural = host.AddComponent<ArchReferenceGrowthArchitecturalPass>();
                for (int frame = 0; frame < 40 && !architectural.ArchitecturalCompositionApplied; frame++) yield return null;
                Assert.That(architectural.ArchitecturalCompositionApplied, Is.True);
                ArchReferenceGrowthAaaPass aaa = host.AddComponent<ArchReferenceGrowthAaaPass>();
                for (int frame = 0; frame < 48 && !aaa.AaaCompositionApplied; frame++) yield return null;
                Assert.That(aaa.AaaCompositionApplied, Is.True);

                Mesh ivy = growth.HeroIvyMesh;
                int vertexBudget = growth.HeroVertexCount;
                Assert.That(ivy.vertexCount, Is.EqualTo(ArchReferenceGrowthTopologyCleanupPass.ExpectedIvyVertexCount),
                    "The regression must observe the exact production ivy topology, including its one omitted near-zero leaf stem.");
                Assert.That(ArchReferenceGrowthTopologyCleanupPass.TryBuildTopology(
                    ivy.vertexCount, out int[,] leafStarts, out int[] stemStarts), Is.True);
                Assert.That(stemStarts.Length, Is.EqualTo(ArchReferenceGrowthTopologyCleanupPass.ExpectedStemQuadCount));

                ArchReferenceGrowthTopologyCleanupPass cleanup = host.AddComponent<ArchReferenceGrowthTopologyCleanupPass>();
                for (int frame = 0; frame < 56 && !cleanup.TopologyCleanupApplied; frame++) yield return null;
                Assert.That(cleanup.TopologyCleanupApplied, Is.True);
                Assert.That(MaximumStemSpan(ivy, stemStarts), Is.LessThan(0.001f),
                    "Every authored deterministic stem quad must be degenerate; the player frame must contain no legacy diagonal vine.");

                Vector3[] clusters = MeasureClusterCentres(ivy, leafStarts);
                for (int cluster = 0; cluster < IvyClusterCount; cluster++)
                {
                    Vector2 expected = ArchReferenceGrowthAaaPass.Support(cluster);
                    Assert.That(Vector2.Distance(clusters[cluster], expected), Is.LessThan(0.10f));
                }
                Assert.That(clusters[14].y, Is.GreaterThan(7.90f));
                Assert.That(clusters[14].x - clusters[9].x, Is.GreaterThan(1.60f));
                Assert.That(clusters[15].x, Is.GreaterThan(1.50f));
                for (int cluster = 0; cluster <= 14; cluster++)
                    Assert.That(clusters[cluster].x, Is.LessThan(0.45f));

                Assert.That(AverageLeafRadius(ivy, leafStarts), Is.InRange(0.11f, 0.18f));
                Assert.That(growth.HeroLeafCount, Is.EqualTo(128));
                Assert.That(growth.HeroFlowerHeadCount, Is.EqualTo(30));
                Assert.That(growth.HeroDrawCallCount, Is.EqualTo(3));
                Assert.That(growth.HeroVertexCount, Is.EqualTo(vertexBudget).And.LessThanOrEqualTo(4096));

                Mesh firstIvy = ivy;
                Vector3 expectedCrown = clusters[13];
                growth.enabled = false;
                yield return null;
                growth.enabled = true;
                for (int frame = 0; frame < 76; frame++)
                {
                    if (growth.HeroIvyMesh != null && growth.HeroIvyMesh != firstIvy &&
                        english.EnglishApplied && mass.CompositionApplied && readability.ReadabilityApplied &&
                        architectural.ArchitecturalCompositionApplied && aaa.AaaCompositionApplied && cleanup.TopologyCleanupApplied)
                        break;
                    yield return null;
                }

                Mesh rebuiltIvy = growth.HeroIvyMesh;
                Assert.That(rebuiltIvy, Is.Not.Null.And.Not.SameAs(firstIvy));
                Assert.That(rebuiltIvy.vertexCount, Is.EqualTo(ArchReferenceGrowthTopologyCleanupPass.ExpectedIvyVertexCount));
                Assert.That(ArchReferenceGrowthTopologyCleanupPass.TryBuildTopology(
                    rebuiltIvy.vertexCount, out int[,] rebuiltLeaves, out int[] rebuiltStems), Is.True);
                Assert.That(rebuiltStems.Length, Is.EqualTo(ArchReferenceGrowthTopologyCleanupPass.ExpectedStemQuadCount));
                Assert.That(MaximumStemSpan(rebuiltIvy, rebuiltStems), Is.LessThan(0.001f));
                Vector3[] rebuiltClusters = MeasureClusterCentres(rebuiltIvy, rebuiltLeaves);
                Assert.That(Vector3.Distance(rebuiltClusters[13], expectedCrown), Is.LessThan(0.015f));
                Assert.That(growth.HeroLeafCount, Is.EqualTo(128));
                Assert.That(growth.HeroFlowerHeadCount, Is.EqualTo(30));
                Assert.That(growth.HeroDrawCallCount, Is.EqualTo(3));
                Assert.That(growth.HeroVertexCount, Is.EqualTo(vertexBudget));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static float MaximumStemSpan(Mesh mesh, int[] starts)
        {
            Vector3[] vertices = mesh.vertices;
            float max = 0f;
            for (int s = 0; s < starts.Length; s++)
            {
                int start = starts[s];
                for (int a = 0; a < 4; a++)
                for (int b = a + 1; b < 4; b++)
                    max = Mathf.Max(max, Vector3.Distance(vertices[start + a], vertices[start + b]));
            }
            return max;
        }

        private static Vector3[] MeasureClusterCentres(Mesh mesh, int[,] starts)
        {
            Vector3[] vertices = mesh.vertices;
            var result = new Vector3[IvyClusterCount];
            for (int cluster = 0; cluster < IvyClusterCount; cluster++)
            {
                Vector3 sum = Vector3.zero;
                for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++) sum += vertices[starts[cluster, leaf]];
                result[cluster] = sum / IvyLeavesPerCluster;
            }
            return result;
        }

        private static float AverageLeafRadius(Mesh mesh, int[,] starts)
        {
            Vector3[] vertices = mesh.vertices;
            float sum = 0f;
            int count = 0;
            for (int cluster = 0; cluster < IvyClusterCount; cluster++)
            for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
            {
                int start = starts[cluster, leaf];
                Vector3 centre = vertices[start];
                float radius = 0f;
                for (int i = 1; i < IvyLeafVertexCount; i++)
                    radius = Mathf.Max(radius, Vector2.Distance(centre, vertices[start + i]));
                sum += radius;
                count++;
            }
            return sum / count;
        }
    }
}

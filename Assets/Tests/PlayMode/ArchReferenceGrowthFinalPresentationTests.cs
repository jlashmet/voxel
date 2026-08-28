using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ArchReferenceGrowthFinalPresentationTests
    {
        private const int IvyClusterCount = 16;
        private const int IvyLeavesPerCluster = 8;
        private const int IvyLeafVertexCount = 17;
        private const int FlowerHeads = 30;
        private const int FlowerPetalsPerHead = 5;
        private const int FlowerPetalVertexCount = 7;
        private const int FlowerHeadVertexCount = FlowerPetalsPerHead * FlowerPetalVertexCount;

        [UnityTest, Timeout(30000)]
        public IEnumerator FinalPresentationMaintainsLayeredIvyAndIntegratedBouquetsAcrossRebuild()
        {
            var host = new GameObject("Arch final presentation regression");
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
                ArchReferenceGrowthTopologyCleanupPass finalizer = host.AddComponent<ArchReferenceGrowthTopologyCleanupPass>();
                for (int frame = 0; frame < 56 && !finalizer.TopologyCleanupApplied; frame++) yield return null;
                Assert.That(finalizer.TopologyCleanupApplied, Is.True);

                AssertPresentation(growth);

                Mesh firstIvy = growth.HeroIvyMesh;
                Mesh firstPetals = growth.HeroFlowerPetalMesh;
                growth.enabled = false;
                yield return null;
                growth.enabled = true;
                for (int frame = 0; frame < 76; frame++)
                {
                    if (growth.HeroIvyMesh != null && growth.HeroIvyMesh != firstIvy &&
                        growth.HeroFlowerPetalMesh != null && growth.HeroFlowerPetalMesh != firstPetals &&
                        english.EnglishApplied && mass.CompositionApplied && readability.ReadabilityApplied &&
                        architectural.ArchitecturalCompositionApplied && aaa.AaaCompositionApplied &&
                        finalizer.TopologyCleanupApplied)
                        break;
                    yield return null;
                }

                Assert.That(growth.HeroIvyMesh, Is.Not.Null.And.Not.SameAs(firstIvy));
                Assert.That(growth.HeroFlowerPetalMesh, Is.Not.Null.And.Not.SameAs(firstPetals));
                AssertPresentation(growth);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static void AssertPresentation(ArchReferenceGrowth growth)
        {
            Mesh ivy = growth.HeroIvyMesh;
            Mesh petals = growth.HeroFlowerPetalMesh;
            Assert.That(ivy, Is.Not.Null);
            Assert.That(petals, Is.Not.Null);
            Assert.That(ivy.vertexCount, Is.EqualTo(ArchReferenceGrowthTopologyCleanupPass.ExpectedIvyVertexCount));
            Assert.That(ArchReferenceGrowthTopologyCleanupPass.TryBuildTopology(
                ivy.vertexCount, out int[,] leafStarts, out int[] stemStarts), Is.True);
            Assert.That(stemStarts.Length, Is.EqualTo(ArchReferenceGrowthTopologyCleanupPass.ExpectedStemQuadCount));
            Assert.That(MaximumStemSpan(ivy, stemStarts), Is.LessThan(0.001f));
            Assert.That(MaximumTriangleEdge(ivy), Is.LessThan(0.30f),
                "A frame-spanning ivy sliver indicates stale/corrupt leaf ranges.");

            Vector3[] clusterCentres = MeasureClusterCentres(ivy, leafStarts);
            for (int cluster = 0; cluster < IvyClusterCount; cluster++)
            {
                Vector2 support = ArchReferenceGrowthAaaPass.Support(cluster);
                Assert.That(Vector2.Distance(clusterCentres[cluster], support), Is.LessThan(0.10f));
            }

            Assert.That(AverageLeafRadius(ivy, leafStarts), Is.GreaterThan(0.165f).And.LessThan(0.24f),
                "Leaves must remain broad enough to read as overlapping ivy rather than tiny repeated stamps.");
            Assert.That(AverageLeafCentreSpread(ivy, leafStarts), Is.GreaterThan(0.30f),
                "Each authored support must carry a broad layered foliage mass rather than a thin chain.");
            Assert.That(AverageFlowerHeadRadius(petals), Is.GreaterThan(0.19f).And.LessThan(0.27f),
                "Flower heads must remain large enough to integrate with the ivy mass.");
            Assert.That(MaximumFlowerHeadAnchorOffset(petals), Is.LessThan(0.22f),
                "Each five-head bouquet must stay compact around its masonry-supported anchor.");

            Assert.That(growth.HeroLeafCount, Is.EqualTo(128));
            Assert.That(growth.HeroFlowerHeadCount, Is.EqualTo(30));
            Assert.That(growth.HeroDrawCallCount, Is.EqualTo(3));
            Assert.That(growth.HeroVertexCount, Is.LessThanOrEqualTo(4096));
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

        private static float MaximumTriangleEdge(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            float max = 0f;
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]];
                Vector3 b = vertices[triangles[i + 1]];
                Vector3 c = vertices[triangles[i + 2]];
                max = Mathf.Max(max, Vector3.Distance(a, b));
                max = Mathf.Max(max, Vector3.Distance(b, c));
                max = Mathf.Max(max, Vector3.Distance(c, a));
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
                for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
                    sum += vertices[starts[cluster, leaf]];
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

        private static float AverageLeafCentreSpread(Mesh mesh, int[,] starts)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] clusterCentres = MeasureClusterCentres(mesh, starts);
            float sum = 0f;
            int count = 0;
            for (int cluster = 0; cluster < IvyClusterCount; cluster++)
            for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
            {
                sum += Vector2.Distance(clusterCentres[cluster], vertices[starts[cluster, leaf]]);
                count++;
            }
            return sum / count;
        }

        private static float AverageFlowerHeadRadius(Mesh petals)
        {
            Vector3[] vertices = petals.vertices;
            Assert.That(vertices.Length, Is.EqualTo(FlowerHeads * FlowerHeadVertexCount));
            float sum = 0f;
            for (int head = 0; head < FlowerHeads; head++)
            {
                Vector3 centre = MeasureFlowerHeadCentre(vertices, head);
                int start = head * FlowerHeadVertexCount;
                float radius = 0f;
                for (int i = 0; i < FlowerHeadVertexCount; i++)
                    radius = Mathf.Max(radius, Vector2.Distance(centre, vertices[start + i]));
                sum += radius;
            }
            return sum / FlowerHeads;
        }

        private static float MaximumFlowerHeadAnchorOffset(Mesh petals)
        {
            Vector3[] vertices = petals.vertices;
            float max = 0f;
            for (int head = 0; head < FlowerHeads; head++)
            {
                int bouquet = head / 5;
                Vector3 centre = MeasureFlowerHeadCentre(vertices, head);
                max = Mathf.Max(max, Vector2.Distance(centre, ArchReferenceGrowthAaaPass.BouquetAnchor(bouquet)));
            }
            return max;
        }

        private static Vector3 MeasureFlowerHeadCentre(Vector3[] vertices, int head)
        {
            Vector3 sum = Vector3.zero;
            int start = head * FlowerHeadVertexCount;
            for (int petal = 0; petal < FlowerPetalsPerHead; petal++)
                sum += vertices[start + petal * FlowerPetalVertexCount];
            return sum / FlowerPetalsPerHead;
        }
    }
}

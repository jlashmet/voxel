using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ArchReferenceGrowthSemanticMassPassTests
    {
        private const int ClusterCount = 16;
        private const int LeavesPerCluster = 8;
        private const int LeafVertexCount = 17;
        private const int HeadsPerBouquet = 5;
        private const int PetalsPerHead = 5;
        private const int PetalVertexCount = 7;
        private const int HeadVertexCount = PetalsPerHead * PetalVertexCount;

        [UnityTest, Timeout(30000)]
        public IEnumerator SemanticMassPassBuildsThreeMasonryMassesAcrossRebuild()
        {
            var host = new GameObject("Arch semantic mass regression");
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
                for (int i = 0; i < 28 && !english.EnglishApplied; i++) yield return null;
                ArchReferenceGrowthMassBreakupPass mass = host.AddComponent<ArchReferenceGrowthMassBreakupPass>();
                for (int i = 0; i < 32 && !mass.CompositionApplied; i++) yield return null;
                ArchReferenceGrowthReadabilityPass readability = host.AddComponent<ArchReferenceGrowthReadabilityPass>();
                for (int i = 0; i < 36 && !readability.ReadabilityApplied; i++) yield return null;
                ArchReferenceGrowthArchitecturalPass architectural = host.AddComponent<ArchReferenceGrowthArchitecturalPass>();
                for (int i = 0; i < 40 && !architectural.ArchitecturalCompositionApplied; i++) yield return null;
                ArchReferenceGrowthAaaPass aaa = host.AddComponent<ArchReferenceGrowthAaaPass>();
                for (int i = 0; i < 48 && !aaa.AaaCompositionApplied; i++) yield return null;
                ArchReferenceGrowthTopologyCleanupPass topology = host.AddComponent<ArchReferenceGrowthTopologyCleanupPass>();
                for (int i = 0; i < 56 && !topology.TopologyCleanupApplied; i++) yield return null;
                ArchReferenceGrowthOrganicFinishPass organic = host.AddComponent<ArchReferenceGrowthOrganicFinishPass>();
                for (int i = 0; i < 72 && !organic.OrganicFinishApplied; i++) yield return null;
                ArchReferenceGrowthReferenceFinishPass reference = host.AddComponent<ArchReferenceGrowthReferenceFinishPass>();
                for (int i = 0; i < 84 && !reference.ReferenceFinishApplied; i++) yield return null;
                ArchReferenceGrowthSemanticMassPass semantic = host.AddComponent<ArchReferenceGrowthSemanticMassPass>();
                for (int i = 0; i < 96 && !semantic.SemanticMassApplied; i++) yield return null;
                Assert.That(semantic.SemanticMassApplied, Is.True);

                AssertPresentation(growth);

                Mesh firstIvy = growth.HeroIvyMesh;
                Mesh firstFlowers = growth.HeroFlowerPetalMesh;
                growth.enabled = false;
                yield return null;
                growth.enabled = true;
                for (int i = 0; i < 140; i++)
                {
                    if (growth.HeroIvyMesh != null && growth.HeroIvyMesh != firstIvy &&
                        growth.HeroFlowerPetalMesh != null && growth.HeroFlowerPetalMesh != firstFlowers &&
                        semantic.SemanticMassApplied)
                        break;
                    yield return null;
                }

                Assert.That(growth.HeroIvyMesh, Is.Not.Null.And.Not.SameAs(firstIvy));
                Assert.That(growth.HeroFlowerPetalMesh, Is.Not.Null.And.Not.SameAs(firstFlowers));
                Assert.That(semantic.SemanticMassApplied, Is.True);
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
            Mesh flowers = growth.HeroFlowerPetalMesh;
            Assert.That(ivy, Is.Not.Null);
            Assert.That(flowers, Is.Not.Null);
            Assert.That(ArchReferenceGrowthTopologyCleanupPass.TryBuildTopology(
                ivy.vertexCount, out int[,] starts, out int[] stems), Is.True);
            Assert.That(stems.Length, Is.EqualTo(ArchReferenceGrowthTopologyCleanupPass.ExpectedStemQuadCount));
            Assert.That(MaxStemSpan(ivy, stems), Is.LessThan(0.001f));
            Assert.That(MaxTriangleEdge(ivy), Is.LessThan(0.30f));

            Vector3[] clusters = ClusterCentres(ivy, starts);
            AssertMass(clusters, 0, 0.55f, 0.75f);
            AssertMass(clusters, 1, 0.55f, 0.75f);
            AssertMass(clusters, 2, 1.15f, 0.25f);
            Assert.That(Vector2.Distance(clusters[4], clusters[5]), Is.GreaterThan(2.0f),
                "Lower-pier and upper-haunch growth must read as separate masses, not a uniform vine chain.");
            Assert.That(Vector2.Distance(clusters[9], clusters[10]), Is.GreaterThan(1.45f),
                "Haunch and crown growth need a deliberate compositional break.");
            Assert.That(Vector2.Distance(clusters[15], ArchReferenceGrowthAaaPass.Support(15)), Is.LessThan(0.035f),
                "The single right-side accent must remain sparse and masonry-supported.");
            for (int cluster = 0; cluster < 10; cluster++)
                Assert.That(clusters[cluster].x, Is.LessThan(-1.45f),
                    "Pier and haunch foliage must sit on the left stone face, not spill into the passage.");
            for (int cluster = 10; cluster < 15; cluster++)
                Assert.That(clusters[cluster].y, Is.GreaterThan(7.90f),
                    "Crown foliage must project outward onto the arch ring rather than float below it in the opening.");

            Assert.That(AverageLeafRadius(ivy, starts), Is.InRange(0.075f, 0.130f));

            Vector3[] bouquets = BouquetCentres(flowers);
            for (int bouquet = 0; bouquet < 6; bouquet++)
                Assert.That(Vector2.Distance(bouquets[bouquet], ArchReferenceGrowthSemanticMassPass.BouquetTarget(bouquet)),
                    Is.LessThan(0.035f));
            for (int bouquet = 0; bouquet < 4; bouquet++)
                Assert.That(bouquets[bouquet].x, Is.LessThan(-1.25f),
                    "Lower and haunch blossoms must remain embedded against masonry.");
            for (int bouquet = 4; bouquet < 6; bouquet++)
                Assert.That(bouquets[bouquet].y, Is.GreaterThan(7.90f),
                    "Crown blossoms must sit on the arch ring with the crown foliage.");
            for (int mass = 0; mass < 3; mass++)
            {
                float pair = Vector2.Distance(bouquets[mass * 2], bouquets[mass * 2 + 1]);
                Assert.That(pair, Is.InRange(0.25f, 0.70f),
                    "Each masonry mass must carry two distinct but integrated flower bouquets.");
            }

            Assert.That(growth.HeroLeafCount, Is.EqualTo(128));
            Assert.That(growth.HeroFlowerHeadCount, Is.EqualTo(30));
            Assert.That(growth.HeroDrawCallCount, Is.EqualTo(3));
            Assert.That(growth.HeroVertexCount, Is.LessThanOrEqualTo(4096));
        }

        private static void AssertMass(Vector3[] clusters, int mass, float minWidth, float minHeight)
        {
            int start = mass * 5;
            Vector2 sum = Vector2.zero;
            Vector2 expected = Vector2.zero;
            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;
            for (int i = 0; i < 5; i++)
            {
                Vector2 p = clusters[start + i];
                sum += p;
                expected += ArchReferenceGrowthSemanticMassPass.ClusterTarget(start + i);
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);
            }
            Vector2 centroid = sum / 5f;
            expected /= 5f;
            Assert.That(Vector2.Distance(centroid, expected), Is.LessThan(0.035f));
            Assert.That(maxX - minX, Is.GreaterThan(minWidth));
            Assert.That(maxY - minY, Is.GreaterThan(minHeight));
            for (int i = 0; i < 5; i++)
                Assert.That(Vector2.Distance(clusters[start + i], centroid), Is.LessThan(0.82f));
        }

        private static Vector3[] ClusterCentres(Mesh mesh, int[,] starts)
        {
            Vector3[] vertices = mesh.vertices;
            var result = new Vector3[ClusterCount];
            for (int cluster = 0; cluster < ClusterCount; cluster++)
            {
                Vector3 sum = Vector3.zero;
                for (int leaf = 0; leaf < LeavesPerCluster; leaf++) sum += vertices[starts[cluster, leaf]];
                result[cluster] = sum / LeavesPerCluster;
            }
            return result;
        }

        private static float AverageLeafRadius(Mesh mesh, int[,] starts)
        {
            Vector3[] vertices = mesh.vertices;
            float sum = 0f;
            int count = 0;
            for (int cluster = 0; cluster < ClusterCount; cluster++)
            for (int leaf = 0; leaf < LeavesPerCluster; leaf++)
            {
                int start = starts[cluster, leaf];
                float radius = 0f;
                for (int i = 1; i < LeafVertexCount; i++)
                    radius = Mathf.Max(radius, Vector2.Distance(vertices[start], vertices[start + i]));
                sum += radius;
                count++;
            }
            return sum / count;
        }

        private static Vector3[] BouquetCentres(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            var result = new Vector3[6];
            for (int bouquet = 0; bouquet < 6; bouquet++)
            {
                Vector3 sum = Vector3.zero;
                for (int local = 0; local < HeadsPerBouquet; local++)
                    sum += HeadCentre(vertices, bouquet * HeadsPerBouquet + local);
                result[bouquet] = sum / HeadsPerBouquet;
            }
            return result;
        }

        private static Vector3 HeadCentre(Vector3[] vertices, int head)
        {
            Vector3 sum = Vector3.zero;
            int start = head * HeadVertexCount;
            for (int petal = 0; petal < PetalsPerHead; petal++)
                sum += vertices[start + petal * PetalVertexCount];
            return sum / PetalsPerHead;
        }

        private static float MaxStemSpan(Mesh mesh, int[] starts)
        {
            Vector3[] vertices = mesh.vertices;
            float max = 0f;
            foreach (int start in starts)
                for (int a = 0; a < 4; a++)
                for (int b = a + 1; b < 4; b++)
                    max = Mathf.Max(max, Vector3.Distance(vertices[start + a], vertices[start + b]));
            return max;
        }

        private static float MaxTriangleEdge(Mesh mesh)
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
    }
}

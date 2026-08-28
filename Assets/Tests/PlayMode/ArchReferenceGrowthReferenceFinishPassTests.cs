using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ArchReferenceGrowthReferenceFinishPassTests
    {
        private const int Clusters = 16;
        private const int Leaves = 8;
        private const int LeafVertices = 17;
        private const int Heads = 30;
        private const int Petals = 5;
        private const int PetalVertices = 7;
        private const int HeadVertices = Petals * PetalVertices;

        [UnityTest, Timeout(30000)]
        public IEnumerator ReferenceFinishSeparatesLeavesAndBlossomsAcrossRebuild()
        {
            var host = new GameObject("Arch reference finish regression");
            try
            {
                host.transform.SetPositionAndRotation(new Vector3(-0.85728186f, 8.398123f, -9.309617f),
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
                Assert.That(reference.ReferenceFinishApplied, Is.True);
                AssertPresentation(growth);

                Mesh firstIvy = growth.HeroIvyMesh;
                Mesh firstFlowers = growth.HeroFlowerPetalMesh;
                growth.enabled = false;
                yield return null;
                growth.enabled = true;
                for (int i = 0; i < 104; i++)
                {
                    if (growth.HeroIvyMesh != null && growth.HeroIvyMesh != firstIvy &&
                        growth.HeroFlowerPetalMesh != null && growth.HeroFlowerPetalMesh != firstFlowers &&
                        reference.ReferenceFinishApplied) break;
                    yield return null;
                }
                Assert.That(growth.HeroIvyMesh, Is.Not.SameAs(firstIvy));
                Assert.That(growth.HeroFlowerPetalMesh, Is.Not.SameAs(firstFlowers));
                AssertPresentation(growth);
            }
            finally { Object.DestroyImmediate(host); }
        }

        private static void AssertPresentation(ArchReferenceGrowth growth)
        {
            Mesh ivy = growth.HeroIvyMesh;
            Mesh flowers = growth.HeroFlowerPetalMesh;
            Assert.That(ArchReferenceGrowthTopologyCleanupPass.TryBuildTopology(ivy.vertexCount, out int[,] starts, out int[] stems), Is.True);
            Assert.That(stems.Length, Is.EqualTo(ArchReferenceGrowthTopologyCleanupPass.ExpectedStemQuadCount));
            Assert.That(MaxStem(ivy, stems), Is.LessThan(0.001f));
            Assert.That(MaxTriangle(ivy), Is.LessThan(0.30f));
            Assert.That(AverageLeafRadius(ivy, starts), Is.InRange(0.075f, 0.130f));
            Assert.That(AverageSpread(ivy, starts), Is.InRange(0.16f, 0.29f));
            Assert.That(AverageNearestLeafDistance(ivy, starts), Is.InRange(0.075f, 0.22f),
                "Individual leaves must remain visually separable inside each bushy support mass.");
            Assert.That(LeafGreenStdDev(ivy, starts), Is.GreaterThan(0.045f),
                "The reference needs visible dark/light leaf variation instead of one flat green stamp.");
            Assert.That(AverageHeadRadius(flowers), Is.InRange(0.055f, 0.115f));
            Assert.That(MaxHeadAnchorOffset(flowers), Is.LessThan(0.27f));
            Assert.That(AverageNearestHeadDistance(flowers), Is.GreaterThan(0.075f),
                "Five blossoms must stay distinct instead of merging into one pale disk.");
            Color petal = AverageColor(flowers.colors);
            Assert.That(petal.r - petal.b, Is.GreaterThan(0.02f));
            Assert.That(growth.HeroLeafCount, Is.EqualTo(128));
            Assert.That(growth.HeroFlowerHeadCount, Is.EqualTo(30));
            Assert.That(growth.HeroDrawCallCount, Is.EqualTo(3));
            Assert.That(growth.HeroVertexCount, Is.LessThanOrEqualTo(4096));
        }

        private static float MaxStem(Mesh mesh, int[] starts)
        {
            Vector3[] v = mesh.vertices; float max = 0f;
            foreach (int start in starts)
                for (int a = 0; a < 4; a++) for (int b = a + 1; b < 4; b++) max = Mathf.Max(max, Vector3.Distance(v[start + a], v[start + b]));
            return max;
        }

        private static float MaxTriangle(Mesh mesh)
        {
            Vector3[] v = mesh.vertices; int[] t = mesh.triangles; float max = 0f;
            for (int i = 0; i + 2 < t.Length; i += 3)
            {
                max = Mathf.Max(max, Vector3.Distance(v[t[i]], v[t[i + 1]]));
                max = Mathf.Max(max, Vector3.Distance(v[t[i + 1]], v[t[i + 2]]));
                max = Mathf.Max(max, Vector3.Distance(v[t[i + 2]], v[t[i]]));
            }
            return max;
        }

        private static Vector3 ClusterCentre(Vector3[] v, int[,] starts, int cluster)
        {
            Vector3 sum = Vector3.zero;
            for (int leaf = 0; leaf < Leaves; leaf++) sum += v[starts[cluster, leaf]];
            return sum / Leaves;
        }

        private static float AverageLeafRadius(Mesh mesh, int[,] starts)
        {
            Vector3[] v = mesh.vertices; float sum = 0f; int count = 0;
            for (int c = 0; c < Clusters; c++) for (int leaf = 0; leaf < Leaves; leaf++)
            {
                int start = starts[c, leaf]; float radius = 0f;
                for (int i = 1; i < LeafVertices; i++) radius = Mathf.Max(radius, Vector2.Distance(v[start], v[start + i]));
                sum += radius; count++;
            }
            return sum / count;
        }

        private static float AverageSpread(Mesh mesh, int[,] starts)
        {
            Vector3[] v = mesh.vertices; float sum = 0f; int count = 0;
            for (int c = 0; c < Clusters; c++)
            {
                Vector3 centre = ClusterCentre(v, starts, c);
                for (int leaf = 0; leaf < Leaves; leaf++) { sum += Vector2.Distance(centre, v[starts[c, leaf]]); count++; }
            }
            return sum / count;
        }

        private static float AverageNearestLeafDistance(Mesh mesh, int[,] starts)
        {
            Vector3[] v = mesh.vertices; float sum = 0f; int count = 0;
            for (int c = 0; c < Clusters; c++) for (int leaf = 0; leaf < Leaves; leaf++)
            {
                float nearest = float.PositiveInfinity;
                Vector3 p = v[starts[c, leaf]];
                for (int other = 0; other < Leaves; other++) if (other != leaf)
                    nearest = Mathf.Min(nearest, Vector2.Distance(p, v[starts[c, other]]));
                sum += nearest; count++;
            }
            return sum / count;
        }

        private static float LeafGreenStdDev(Mesh mesh, int[,] starts)
        {
            Color[] colors = mesh.colors; float mean = 0f; int count = 0;
            for (int c = 0; c < Clusters; c++) for (int leaf = 0; leaf < Leaves; leaf++) { mean += colors[starts[c, leaf]].g; count++; }
            mean /= count; float variance = 0f;
            for (int c = 0; c < Clusters; c++) for (int leaf = 0; leaf < Leaves; leaf++) { float d = colors[starts[c, leaf]].g - mean; variance += d * d; }
            return Mathf.Sqrt(variance / count);
        }

        private static Vector3 HeadCentre(Vector3[] v, int head)
        {
            Vector3 sum = Vector3.zero; int start = head * HeadVertices;
            for (int p = 0; p < Petals; p++) sum += v[start + p * PetalVertices];
            return sum / Petals;
        }

        private static float AverageHeadRadius(Mesh mesh)
        {
            Vector3[] v = mesh.vertices; float sum = 0f;
            for (int h = 0; h < Heads; h++)
            {
                Vector3 centre = HeadCentre(v, h); int start = h * HeadVertices; float radius = 0f;
                for (int i = 0; i < HeadVertices; i++) radius = Mathf.Max(radius, Vector2.Distance(centre, v[start + i]));
                sum += radius;
            }
            return sum / Heads;
        }

        private static float MaxHeadAnchorOffset(Mesh mesh)
        {
            Vector3[] v = mesh.vertices; float max = 0f;
            for (int h = 0; h < Heads; h++) max = Mathf.Max(max, Vector2.Distance(HeadCentre(v, h), ArchReferenceGrowthAaaPass.BouquetAnchor(h / 5)));
            return max;
        }

        private static float AverageNearestHeadDistance(Mesh mesh)
        {
            Vector3[] v = mesh.vertices; float sum = 0f; int count = 0;
            for (int bouquet = 0; bouquet < 6; bouquet++) for (int local = 0; local < 5; local++)
            {
                int h = bouquet * 5 + local; Vector3 p = HeadCentre(v, h); float nearest = float.PositiveInfinity;
                for (int other = 0; other < 5; other++) if (other != local) nearest = Mathf.Min(nearest, Vector2.Distance(p, HeadCentre(v, bouquet * 5 + other)));
                sum += nearest; count++;
            }
            return sum / count;
        }

        private static Color AverageColor(Color[] colors)
        {
            Color sum = Color.clear; foreach (Color c in colors) sum += c; return sum / colors.Length;
        }
    }
}

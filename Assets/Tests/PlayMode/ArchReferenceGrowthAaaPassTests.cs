using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ArchReferenceGrowthAaaPassTests
    {
        private const int IvyClusterCount = 16;
        private const int IvyLeavesPerCluster = 8;
        private const int IvyLeafVertexCount = 17;
        private const int FlowerHeads = 30;
        private const int FlowerPetalsPerHead = 5;
        private const int FlowerPetalVertexCount = 7;
        private const int FlowerHeadVertexCount = FlowerPetalsPerHead * FlowerPetalVertexCount;
        private const int FlowerCentreVertexCount = 9;
        private static readonly Color StemColor = new(0.07f, 0.24f, 0.04f, 1f);

        [UnityTest, Timeout(30000)]
        public IEnumerator FinalAaaPassRemovesStemArtifactsAndBuildsContinuousReferenceMassAcrossRebuild()
        {
            var host = new GameObject("Arch AAA foliage regression");
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

                Mesh ivy = growth.HeroIvyMesh;
                Mesh petals = growth.HeroFlowerPetalMesh;
                Mesh centres = FindHeroMesh("Flower Centres");
                Assert.That(ivy, Is.Not.Null);
                Assert.That(petals, Is.Not.Null);
                Assert.That(centres, Is.Not.Null);
                int vertexBudget = growth.HeroVertexCount;

                ArchReferenceGrowthAaaPass finalPass = host.AddComponent<ArchReferenceGrowthAaaPass>();
                for (int frame = 0; frame < 48 && !finalPass.AaaCompositionApplied; frame++) yield return null;
                Assert.That(finalPass.AaaCompositionApplied, Is.True);
                Assert.That(growth.HeroIvyMesh, Is.SameAs(ivy));
                Assert.That(growth.HeroFlowerPetalMesh, Is.SameAs(petals));
                Assert.That(TryFindIvyLeafStarts(ivy, out int[,] starts, out int leaves), Is.True);
                Assert.That(leaves, Is.EqualTo(128));

                Vector3[] clusterCentres = MeasureClusterCentres(ivy, starts);
                for (int cluster = 0; cluster < IvyClusterCount; cluster++)
                {
                    Vector2 expected = ArchReferenceGrowthAaaPass.Support(cluster);
                    Assert.That(Vector2.Distance(clusterCentres[cluster], expected), Is.LessThan(0.10f),
                        $"Cluster {cluster} must stay centred on its final masonry support.");
                }

                Assert.That(clusterCentres[0].y, Is.InRange(0.70f, 0.88f));
                Assert.That(clusterCentres[4].y, Is.InRange(3.44f, 3.60f));
                Assert.That(clusterCentres[8].y, Is.InRange(6.16f, 6.34f));
                float crownMinX = float.PositiveInfinity;
                float crownMaxX = float.NegativeInfinity;
                float crownMaxY = float.NegativeInfinity;
                for (int cluster = 9; cluster <= 14; cluster++)
                {
                    crownMinX = Mathf.Min(crownMinX, clusterCentres[cluster].x);
                    crownMaxX = Mathf.Max(crownMaxX, clusterCentres[cluster].x);
                    crownMaxY = Mathf.Max(crownMaxY, clusterCentres[cluster].y);
                }
                Assert.That(crownMaxX - crownMinX, Is.GreaterThan(1.55f),
                    "The top foliage must sweep across the masonry crown instead of collapsing into one blob.");
                Assert.That(crownMaxY, Is.GreaterThan(8.0f));
                Assert.That(clusterCentres[15].x, Is.GreaterThan(1.50f));
                for (int cluster = 0; cluster <= 14; cluster++)
                    Assert.That(clusterCentres[cluster].x, Is.LessThan(0.45f),
                        "Only the deliberate sparse accent may remain on the right masonry.");

                Assert.That(AverageLeafRadius(ivy, starts), Is.InRange(0.11f, 0.18f));
                Assert.That(MaximumStemQuadSpan(ivy), Is.LessThan(0.002f),
                    "Every legacy stem quad must be degenerate so no long diagonal survives in the player frame.");
                Assert.That(growth.HeroDrawCallCount, Is.EqualTo(3));
                Assert.That(growth.HeroVertexCount, Is.EqualTo(vertexBudget).And.LessThanOrEqualTo(4096));

                Vector3[] heads = MeasureFlowerHeadCentres(petals);
                for (int bouquet = 0; bouquet < 6; bouquet++)
                {
                    Vector3 centroid = Average(heads, bouquet * 5, 5);
                    Assert.That(Vector2.Distance(centroid, ArchReferenceGrowthAaaPass.BouquetAnchor(bouquet)), Is.LessThan(0.10f),
                        $"Bouquet {bouquet} must remain integrated with the ivy mass.");
                }
                Assert.That(AverageFlowerHeadRadius(petals), Is.InRange(0.10f, 0.18f));
                Assert.That(AverageFlowerCentreRadius(centres), Is.LessThan(0.03f));
                Assert.That(MaxFlowerY(heads, 25, 5), Is.GreaterThan(7.95f));

                Mesh firstIvy = ivy;
                Vector3 expectedCrown = clusterCentres[13];
                Vector3 expectedBouquet = Average(heads, 25, 5);
                growth.enabled = false;
                yield return null;
                growth.enabled = true;
                for (int frame = 0; frame < 68; frame++)
                {
                    if (growth.HeroIvyMesh != null && growth.HeroIvyMesh != firstIvy &&
                        english.EnglishApplied && mass.CompositionApplied && readability.ReadabilityApplied &&
                        architectural.ArchitecturalCompositionApplied && finalPass.AaaCompositionApplied)
                        break;
                    yield return null;
                }

                Assert.That(growth.HeroIvyMesh, Is.Not.Null.And.Not.SameAs(firstIvy));
                Assert.That(finalPass.AaaCompositionApplied, Is.True);
                Assert.That(TryFindIvyLeafStarts(growth.HeroIvyMesh, out int[,] rebuiltStarts, out int rebuiltLeaves), Is.True);
                Assert.That(rebuiltLeaves, Is.EqualTo(128));
                Vector3[] rebuiltClusters = MeasureClusterCentres(growth.HeroIvyMesh, rebuiltStarts);
                Assert.That(Vector3.Distance(rebuiltClusters[13], expectedCrown), Is.LessThan(0.015f));
                Assert.That(MaximumStemQuadSpan(growth.HeroIvyMesh), Is.LessThan(0.002f));
                Vector3[] rebuiltHeads = MeasureFlowerHeadCentres(growth.HeroFlowerPetalMesh);
                Assert.That(Vector3.Distance(Average(rebuiltHeads, 25, 5), expectedBouquet), Is.LessThan(0.015f));
                Assert.That(growth.HeroDrawCallCount, Is.EqualTo(3));
                Assert.That(growth.HeroVertexCount, Is.EqualTo(vertexBudget));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static bool TryFindIvyLeafStarts(Mesh mesh, out int[,] starts, out int found)
        {
            starts = new int[IvyClusterCount, IvyLeavesPerCluster];
            found = 0;
            if (mesh == null) return false;
            Color[] colors = mesh.colors;
            int cursor = 0;
            int expected = IvyClusterCount * IvyLeavesPerCluster;
            while (cursor < mesh.vertexCount && found < expected)
            {
                while (cursor < mesh.vertexCount && IsStemColor(colors[cursor])) cursor++;
                if (cursor + IvyLeafVertexCount > mesh.vertexCount) break;
                bool leafRun = true;
                for (int i = 0; i < IvyLeafVertexCount; i++)
                    if (IsStemColor(colors[cursor + i])) { leafRun = false; break; }
                if (!leafRun) { cursor++; continue; }
                starts[found / IvyLeavesPerCluster, found % IvyLeavesPerCluster] = cursor;
                found++;
                cursor += IvyLeafVertexCount;
            }
            return found == expected;
        }

        private static bool IsStemColor(Color color)
        {
            const float tolerance = 0.006f;
            return Mathf.Abs(color.r - StemColor.r) < tolerance &&
                   Mathf.Abs(color.g - StemColor.g) < tolerance &&
                   Mathf.Abs(color.b - StemColor.b) < tolerance;
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

        private static float MaximumStemQuadSpan(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            Color[] colors = mesh.colors;
            float max = 0f;
            int cursor = 0;
            while (cursor < colors.Length)
            {
                if (!IsStemColor(colors[cursor])) { cursor++; continue; }
                int start = cursor;
                while (cursor < colors.Length && IsStemColor(colors[cursor])) cursor++;
                int length = cursor - start;
                for (int local = 0; local + 3 < length; local += 4)
                {
                    int q = start + local;
                    for (int a = 0; a < 4; a++)
                    for (int b = a + 1; b < 4; b++)
                        max = Mathf.Max(max, Vector3.Distance(vertices[q + a], vertices[q + b]));
                }
            }
            return max;
        }

        private static Vector3[] MeasureFlowerHeadCentres(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            var result = new Vector3[FlowerHeads];
            for (int head = 0; head < FlowerHeads; head++)
            {
                Vector3 sum = Vector3.zero;
                int start = head * FlowerHeadVertexCount;
                for (int petal = 0; petal < FlowerPetalsPerHead; petal++)
                    sum += vertices[start + petal * FlowerPetalVertexCount];
                result[head] = sum / FlowerPetalsPerHead;
            }
            return result;
        }

        private static float AverageFlowerHeadRadius(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] centres = MeasureFlowerHeadCentres(mesh);
            float sum = 0f;
            for (int head = 0; head < FlowerHeads; head++)
            {
                int start = head * FlowerHeadVertexCount;
                float radius = 0f;
                for (int i = 0; i < FlowerHeadVertexCount; i++)
                    radius = Mathf.Max(radius, Vector2.Distance(centres[head], vertices[start + i]));
                sum += radius;
            }
            return sum / FlowerHeads;
        }

        private static float AverageFlowerCentreRadius(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            float sum = 0f;
            for (int head = 0; head < FlowerHeads; head++)
            {
                int start = head * FlowerCentreVertexCount;
                Vector3 centre = vertices[start];
                float radius = 0f;
                for (int i = 1; i < FlowerCentreVertexCount; i++)
                    radius = Mathf.Max(radius, Vector2.Distance(centre, vertices[start + i]));
                sum += radius;
            }
            return sum / FlowerHeads;
        }

        private static Vector3 Average(Vector3[] values, int start, int count)
        {
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < count; i++) sum += values[start + i];
            return sum / count;
        }

        private static float MaxFlowerY(Vector3[] values, int start, int count)
        {
            float max = float.NegativeInfinity;
            for (int i = 0; i < count; i++) max = Mathf.Max(max, values[start + i].y);
            return max;
        }

        private static Mesh FindHeroMesh(string name)
        {
            foreach (Transform candidate in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (candidate == null || candidate.name != "Arch Reference Hero Growth") continue;
                if (!candidate.gameObject.activeInHierarchy || !candidate.gameObject.scene.IsValid()) continue;
                foreach (MeshFilter filter in candidate.GetComponentsInChildren<MeshFilter>(true))
                    if (filter != null && filter.gameObject.name == name) return filter.sharedMesh;
            }
            return null;
        }
    }
}

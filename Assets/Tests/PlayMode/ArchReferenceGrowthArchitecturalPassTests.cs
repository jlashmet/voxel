using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ArchReferenceGrowthArchitecturalPassTests
    {
        private const int IvyLeavesPerCluster = 8;
        private const int IvyLeafVertexCount = 17;
        private const int IvyClusterCount = 16;
        private const int FlowerHeads = 30;
        private const int FlowerPetalsPerHead = 5;
        private const int FlowerPetalVertexCount = 7;
        private const int FlowerCentreVertexCount = 9;
        private const int FlowerHeadVertexCount = FlowerPetalsPerHead * FlowerPetalVertexCount;
        private static readonly Color StemColor = new(0.07f, 0.24f, 0.04f, 1f);

        [UnityTest, Timeout(30000)]
        public IEnumerator FinalPassFollowsArchHaunchAndCrownWithSparseRightGrowthAcrossRebuild()
        {
            var host = new GameObject("Arch architectural composition regression");
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

                Mesh ivy = growth.HeroIvyMesh;
                Mesh petals = growth.HeroFlowerPetalMesh;
                Mesh centres = FindHeroMesh("Flower Centres");
                Assert.That(ivy, Is.Not.Null);
                Assert.That(petals, Is.Not.Null);
                Assert.That(centres, Is.Not.Null);
                int vertexBudget = growth.HeroVertexCount;

                ArchReferenceGrowthArchitecturalPass finalPass = host.AddComponent<ArchReferenceGrowthArchitecturalPass>();
                for (int frame = 0; frame < 40 && !finalPass.ArchitecturalCompositionApplied; frame++) yield return null;
                Assert.That(finalPass.ArchitecturalCompositionApplied, Is.True);
                Assert.That(growth.HeroIvyMesh, Is.SameAs(ivy));
                Assert.That(growth.HeroFlowerPetalMesh, Is.SameAs(petals));
                Assert.That(TryFindIvyLeafStarts(ivy, out int[,] starts, out int leaves), Is.True);
                Assert.That(leaves, Is.EqualTo(128));

                Vector3[] clusterCentres = MeasureClusterCentres(ivy, starts);
                for (int cluster = 0; cluster < IvyClusterCount; cluster++)
                {
                    Vector2 expected = ArchReferenceGrowthArchitecturalPass.ClusterSupport(cluster);
                    Assert.That(Vector2.Distance(clusterCentres[cluster], expected), Is.LessThan(0.13f),
                        $"Cluster {cluster} must remain centred on its semantic arch support.");
                }

                Assert.That(clusterCentres[0].y, Is.InRange(0.95f, 1.16f));
                Assert.That(clusterCentres[2].y, Is.InRange(2.28f, 2.48f));
                Assert.That(clusterCentres[3].y, Is.InRange(3.25f, 3.45f));
                Assert.That(clusterCentres[6].y, Is.InRange(5.46f, 5.66f));

                float crownMinY = float.PositiveInfinity;
                float crownMaxY = float.NegativeInfinity;
                float crownMinX = float.PositiveInfinity;
                float crownMaxX = float.NegativeInfinity;
                for (int cluster = 7; cluster <= 14; cluster++)
                {
                    crownMinY = Mathf.Min(crownMinY, clusterCentres[cluster].y);
                    crownMaxY = Mathf.Max(crownMaxY, clusterCentres[cluster].y);
                    crownMinX = Mathf.Min(crownMinX, clusterCentres[cluster].x);
                    crownMaxX = Mathf.Max(crownMaxX, clusterCentres[cluster].x);
                    float radial = Vector2.Distance(
                        clusterCentres[cluster],
                        new Vector2(0f, ArchReferenceGrowthArchitecturalPass.SpringlineY));
                    Assert.That(radial, Is.InRange(1.55f, 1.80f),
                        "Crown clusters must follow the masonry ring rather than a horizontal shelf.");
                }
                Assert.That(crownMinY, Is.GreaterThan(6.58f));
                Assert.That(crownMaxY, Is.GreaterThan(ArchReferenceGrowthArchitecturalPass.OpeningCrownY + 0.18f),
                    "Growth must visibly reach above the opening crown instead of stopping on the haunch.");
                Assert.That(crownMaxX - crownMinX, Is.GreaterThan(1.72f),
                    "Crown foliage must sweep across the arch instead of collapsing into one left blob.");
                Assert.That(crownMaxX, Is.GreaterThan(0.15f));

                Assert.That(clusterCentres[15].x, Is.GreaterThan(1.50f));
                Assert.That(clusterCentres[15].y, Is.InRange(4.40f, 4.70f));
                for (int cluster = 0; cluster <= 14; cluster++)
                    Assert.That(clusterCentres[cluster].x, Is.LessThan(0.42f),
                        "Only one cluster should remain on the right masonry.");

                Assert.That(AverageLeafRadius(ivy, starts), Is.InRange(0.14f, 0.19f));
                Assert.That(MaximumLocalVineSpan(ivy, starts), Is.LessThan(0.64f));
                Assert.That(growth.HeroDrawCallCount, Is.EqualTo(3));
                Assert.That(growth.HeroVertexCount, Is.EqualTo(vertexBudget).And.LessThanOrEqualTo(4096));

                Vector3[] headCentres = MeasureFlowerHeadCentres(petals);
                AssertBouquetCentroid(headCentres, 0, 6, ArchReferenceGrowthArchitecturalPass.BouquetAnchor(0));
                AssertBouquetCentroid(headCentres, 6, 8, ArchReferenceGrowthArchitecturalPass.BouquetAnchor(1));
                AssertBouquetCentroid(headCentres, 14, 8, ArchReferenceGrowthArchitecturalPass.BouquetAnchor(2));
                AssertBouquetCentroid(headCentres, 22, 8, ArchReferenceGrowthArchitecturalPass.BouquetAnchor(3));
                Assert.That(MaxFlowerY(headCentres, 22, 8), Is.GreaterThan(7.90f),
                    "A readable blossom group must reach the actual crown.");
                float headRadius = AverageFlowerHeadRadius(petals);
                Assert.That(headRadius, Is.InRange(0.15f, 0.22f));
                Assert.That(AverageFlowerCentreRadius(centres) / headRadius, Is.LessThan(0.13f));
                Assert.That(AverageNearestHeadDistance(petals) / headRadius, Is.LessThan(1.85f));

                Mesh firstIvy = ivy;
                Vector3 expectedCrown = clusterCentres[14];
                Vector2 expectedTopBouquet = ArchReferenceGrowthArchitecturalPass.BouquetAnchor(3);
                growth.enabled = false;
                yield return null;
                growth.enabled = true;
                for (int frame = 0; frame < 52; frame++)
                {
                    if (growth.HeroIvyMesh != null && growth.HeroIvyMesh != firstIvy &&
                        mass.CompositionApplied && readability.ReadabilityApplied && finalPass.ArchitecturalCompositionApplied)
                        break;
                    yield return null;
                }

                Assert.That(growth.HeroIvyMesh, Is.Not.Null.And.Not.SameAs(firstIvy));
                Assert.That(finalPass.ArchitecturalCompositionApplied, Is.True);
                Assert.That(TryFindIvyLeafStarts(growth.HeroIvyMesh, out int[,] rebuiltStarts, out int rebuiltLeaves), Is.True);
                Assert.That(rebuiltLeaves, Is.EqualTo(128));
                Vector3[] rebuiltClusters = MeasureClusterCentres(growth.HeroIvyMesh, rebuiltStarts);
                Assert.That(Vector3.Distance(rebuiltClusters[14], expectedCrown), Is.LessThan(0.02f));
                Vector3[] rebuiltHeads = MeasureFlowerHeadCentres(growth.HeroFlowerPetalMesh);
                Vector3 rebuiltTopBouquet = Average(rebuiltHeads, 22, 8);
                Assert.That(Vector2.Distance(rebuiltTopBouquet, expectedTopBouquet), Is.LessThan(0.12f));
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
            int vertexCount = mesh.vertexCount;
            if (colors == null || colors.Length != vertexCount) return false;
            int cursor = 0;
            int expected = IvyClusterCount * IvyLeavesPerCluster;
            while (cursor < vertexCount && found < expected)
            {
                while (cursor < vertexCount && IsStemColor(colors[cursor])) cursor++;
                if (cursor + IvyLeafVertexCount > vertexCount) break;
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

        private static float MaximumLocalVineSpan(Mesh mesh, int[,] starts)
        {
            Vector3[] vertices = mesh.vertices;
            float max = 0f;
            for (int cluster = 0; cluster < IvyClusterCount; cluster++)
            {
                int start = starts[cluster, 0] - 4;
                if (start < 0 || start + 4 > vertices.Length) continue;
                for (int a = 0; a < 4; a++)
                for (int b = a + 1; b < 4; b++)
                    max = Mathf.Max(max, Vector3.Distance(vertices[start + a], vertices[start + b]));
            }
            return max;
        }

        private static Vector3[] MeasureFlowerHeadCentres(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            var centres = new Vector3[FlowerHeads];
            for (int head = 0; head < FlowerHeads; head++)
            {
                int start = head * FlowerHeadVertexCount;
                Vector3 sum = Vector3.zero;
                for (int petal = 0; petal < FlowerPetalsPerHead; petal++)
                    sum += vertices[start + petal * FlowerPetalVertexCount];
                centres[head] = sum / FlowerPetalsPerHead;
            }
            return centres;
        }

        private static void AssertBouquetCentroid(Vector3[] heads, int start, int count, Vector2 expected)
        {
            Vector3 actual = Average(heads, start, count);
            Assert.That(Vector2.Distance(actual, expected), Is.LessThan(0.12f));
        }

        private static Vector3 Average(Vector3[] values, int start, int count)
        {
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < count; i++) sum += values[start + i];
            return sum / count;
        }

        private static float MaxFlowerY(Vector3[] heads, int start, int count)
        {
            float max = float.NegativeInfinity;
            for (int i = 0; i < count; i++) max = Mathf.Max(max, heads[start + i].y);
            return max;
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

        private static float AverageNearestHeadDistance(Mesh mesh)
        {
            Vector3[] centres = MeasureFlowerHeadCentres(mesh);
            float sum = 0f;
            for (int head = 0; head < FlowerHeads; head++)
            {
                int bouquet = Bouquet(head);
                float nearest = float.PositiveInfinity;
                for (int other = 0; other < FlowerHeads; other++)
                {
                    if (head == other || Bouquet(other) != bouquet) continue;
                    nearest = Mathf.Min(nearest, Vector2.Distance(centres[head], centres[other]));
                }
                sum += nearest;
            }
            return sum / FlowerHeads;
        }

        private static int Bouquet(int head)
        {
            if (head < 6) return 0;
            if (head < 14) return 1;
            if (head < 22) return 2;
            return 3;
        }

        private static Mesh FindHeroMesh(string name)
        {
            MeshFilter[] filters = Resources.FindObjectsOfTypeAll<MeshFilter>();
            foreach (MeshFilter filter in filters)
            {
                if (filter == null || filter.gameObject.name != name || !filter.gameObject.scene.IsValid() || !filter.gameObject.scene.isLoaded)
                    continue;
                Transform parent = filter.transform.parent;
                while (parent != null && parent.name != "Arch Reference Hero Growth") parent = parent.parent;
                if (parent != null) return filter.sharedMesh;
            }
            return null;
        }
    }
}

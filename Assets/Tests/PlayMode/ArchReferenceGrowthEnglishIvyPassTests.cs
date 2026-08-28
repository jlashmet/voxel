using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ArchReferenceGrowthEnglishIvyPassTests
    {
        private const int IvyLeavesPerCluster = 8;
        private const int IvyLeafVertexCount = 17;
        private const int IvyStemVertexCount = 4;
        private const int LeftIvyClusterCount = 12;
        private const int RightIvyClusterCount = 4;
        private const int FlowerClusterCount = 10;
        private const int FlowerHeadsPerCluster = 3;
        private const int FlowerPetalsPerHead = 5;
        private const int FlowerPetalVertexCount = 7;
        private static readonly Color StemColor = new(0.07f, 0.24f, 0.04f, 1f);

        [UnityTest, Timeout(30000)]
        public IEnumerator EnglishIvyPassBuildsStemFreeMassesAndRoundedBouquetsAcrossRebuild()
        {
            var host = new GameObject("Arch English ivy regression");
            try
            {
                host.transform.SetPositionAndRotation(
                    new Vector3(-0.85728186f, 8.398123f, -9.309617f),
                    new Quaternion(0.09724782f, -0.01389580f, 0.00135791f, 0.9951624f));
                Camera camera = host.AddComponent<Camera>();
                ArchReferenceGrowthWorldSpace.EnsureInstalled(camera);
                ArchReferenceGrowth growth = host.AddComponent<ArchReferenceGrowth>();
                ArchReferenceGrowthDetailPass detail = host.AddComponent<ArchReferenceGrowthDetailPass>();
                ArchReferenceGrowthLushPass lush = host.AddComponent<ArchReferenceGrowthLushPass>();

                for (int frame = 0; frame < 16 && !lush.LushApplied; frame++)
                    yield return null;
                Assert.That(detail.RefinementApplied, Is.True);
                Assert.That(lush.LushApplied, Is.True);

                Mesh ivy = growth.HeroIvyMesh;
                Mesh petals = growth.HeroFlowerPetalMesh;
                Assert.That(ivy, Is.Not.Null);
                Assert.That(petals, Is.Not.Null);
                int vertexBudget = growth.HeroVertexCount;
                float lushStemExtent = MaximumStemQuadExtent(ivy);
                float lushBouquetSpread = AverageFlowerClusterSpread(petals);
                Assert.That(lushStemExtent, Is.GreaterThan(0.02f),
                    "The discriminator requires visible authored stem geometry before the final pass.");
                Assert.That(lushBouquetSpread, Is.GreaterThan(0.15f),
                    "The discriminator requires the wide pre-final flower-head spacing seen in the rejected replay.");

                ArchReferenceGrowthEnglishIvyPass english = host.AddComponent<ArchReferenceGrowthEnglishIvyPass>();
                for (int frame = 0; frame < 24 && !english.EnglishApplied; frame++)
                    yield return null;

                Assert.That(english.EnglishApplied, Is.True);
                Assert.That(growth.HeroIvyMesh, Is.SameAs(ivy),
                    "Final refinement must mutate the existing combined ivy mesh.");
                Assert.That(growth.HeroFlowerPetalMesh, Is.SameAs(petals),
                    "Final refinement must mutate the existing combined petal mesh.");

                float stemExtent = MaximumStemQuadExtent(ivy);
                float bouquetSpread = AverageFlowerClusterSpread(petals);
                MeasureLeafMetrics(ivy, out float leftRadius, out float rightRadius, out float leafDepth);
                float flowerDepth = AverageFlowerHeadDepth(petals);

                Assert.That(stemExtent, Is.LessThan(0.001f),
                    "No visible path or leaf stem quad may survive to reconnect the masses into a diagonal garland.");
                Assert.That(bouquetSpread, Is.LessThan(lushBouquetSpread * 0.75f),
                    "The same three flower heads per cluster must become a compact readable bouquet.");
                Assert.That(leftRadius, Is.GreaterThan(0.18f).And.LessThan(0.30f),
                    "Left/crown leaves need enough screen mass to overlap without returning to giant cards.");
                Assert.That(rightRadius, Is.GreaterThan(0.09f).And.LessThan(0.17f),
                    "Right-side ivy must stay deliberately sparse and subordinate.");
                Assert.That(leftRadius, Is.GreaterThan(rightRadius * 1.35f),
                    "The final composition must preserve the reference's asymmetric foliage hierarchy.");
                Assert.That(leafDepth, Is.GreaterThan(0.018f),
                    "Overlapping ivy needs measurable bowl depth for lighting separation at the saved pose.");
                Assert.That(flowerDepth, Is.GreaterThan(0.015f),
                    "Rounded blossoms must not remain coplanar five-point cards.");
                Assert.That(growth.HeroDrawCallCount, Is.EqualTo(3));
                Assert.That(growth.HeroVertexCount, Is.EqualTo(vertexBudget));
                Assert.That(growth.HeroVertexCount, Is.LessThanOrEqualTo(4096));

                float finalBouquetSpread = bouquetSpread;
                float finalLeftRadius = leftRadius;
                float finalRightRadius = rightRadius;
                Mesh firstIvy = ivy;
                growth.enabled = false;
                yield return null;
                growth.enabled = true;

                for (int frame = 0; frame < 28; frame++)
                {
                    Mesh rebuilt = growth.HeroIvyMesh;
                    if (rebuilt != null && rebuilt != firstIvy && english.EnglishApplied &&
                        MaximumStemQuadExtent(rebuilt) < 0.001f)
                        break;
                    yield return null;
                }

                Assert.That(growth.HeroIvyMesh, Is.Not.Null);
                Assert.That(growth.HeroIvyMesh, Is.Not.SameAs(firstIvy));
                Assert.That(english.EnglishApplied, Is.True,
                    "The final one-shot refinement must reapply through the production rebuild lifecycle.");
                Assert.That(MaximumStemQuadExtent(growth.HeroIvyMesh), Is.LessThan(0.001f));
                Assert.That(AverageFlowerClusterSpread(growth.HeroFlowerPetalMesh),
                    Is.EqualTo(finalBouquetSpread).Within(0.01f));
                MeasureLeafMetrics(growth.HeroIvyMesh,
                    out float rebuiltLeftRadius, out float rebuiltRightRadius, out float rebuiltDepth);
                Assert.That(rebuiltLeftRadius, Is.EqualTo(finalLeftRadius).Within(0.01f));
                Assert.That(rebuiltRightRadius, Is.EqualTo(finalRightRadius).Within(0.01f));
                Assert.That(rebuiltDepth, Is.GreaterThan(0.018f));
                Assert.That(AverageFlowerHeadDepth(growth.HeroFlowerPetalMesh), Is.GreaterThan(0.015f));
                Assert.That(growth.HeroDrawCallCount, Is.EqualTo(3));
                Assert.That(growth.HeroVertexCount, Is.EqualTo(vertexBudget));

                growth.enabled = false;
                yield return null;
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static float MaximumStemQuadExtent(Mesh mesh)
        {
            if (mesh == null) return float.PositiveInfinity;
            Vector3[] vertices = mesh.vertices;
            Color[] colors = mesh.colors;
            float maximum = 0f;
            for (int i = 0; i + 3 < vertices.Length; i++)
            {
                if (!IsStemColor(colors[i]) || !IsStemColor(colors[i + 1]) ||
                    !IsStemColor(colors[i + 2]) || !IsStemColor(colors[i + 3]))
                    continue;

                Vector3 origin = vertices[i];
                for (int vertex = 1; vertex < 4; vertex++)
                    maximum = Mathf.Max(maximum, (vertices[i + vertex] - origin).magnitude);
                i += 3;
            }
            return maximum;
        }

        private static bool IsStemColor(Color color)
        {
            const float tolerance = 0.006f;
            return Mathf.Abs(color.r - StemColor.r) < tolerance &&
                   Mathf.Abs(color.g - StemColor.g) < tolerance &&
                   Mathf.Abs(color.b - StemColor.b) < tolerance;
        }

        private static float AverageFlowerClusterSpread(Mesh mesh)
        {
            if (mesh == null) return 0f;
            Vector3[] vertices = mesh.vertices;
            int headVertexCount = FlowerPetalsPerHead * FlowerPetalVertexCount;
            float sum = 0f;
            int clusters = 0;
            for (int cluster = 0; cluster < FlowerClusterCount; cluster++)
            {
                var heads = new Vector3[FlowerHeadsPerCluster];
                Vector3 centre = Vector3.zero;
                for (int localHead = 0; localHead < FlowerHeadsPerCluster; localHead++)
                {
                    int head = cluster * FlowerHeadsPerCluster + localHead;
                    Vector3 headCentre = MeasureFlowerHeadCentre(vertices, head * headVertexCount);
                    heads[localHead] = headCentre;
                    centre += headCentre;
                }
                centre /= FlowerHeadsPerCluster;
                for (int localHead = 0; localHead < FlowerHeadsPerCluster; localHead++)
                    sum += (heads[localHead] - centre).magnitude;
                clusters++;
            }
            return clusters == 0 ? 0f : sum / (clusters * FlowerHeadsPerCluster);
        }

        private static Vector3 MeasureFlowerHeadCentre(Vector3[] vertices, int headStart)
        {
            Vector3 centre = Vector3.zero;
            for (int petal = 0; petal < FlowerPetalsPerHead; petal++)
                centre += vertices[headStart + petal * FlowerPetalVertexCount];
            return centre / FlowerPetalsPerHead;
        }

        private static float AverageFlowerHeadDepth(Mesh mesh)
        {
            if (mesh == null) return 0f;
            Vector3[] vertices = mesh.vertices;
            int headVertexCount = FlowerPetalsPerHead * FlowerPetalVertexCount;
            int headCount = FlowerClusterCount * FlowerHeadsPerCluster;
            float sum = 0f;
            for (int head = 0; head < headCount; head++)
            {
                int start = head * headVertexCount;
                float min = float.PositiveInfinity;
                float max = float.NegativeInfinity;
                for (int i = 0; i < headVertexCount; i++)
                {
                    min = Mathf.Min(min, vertices[start + i].z);
                    max = Mathf.Max(max, vertices[start + i].z);
                }
                sum += max - min;
            }
            return headCount == 0 ? 0f : sum / headCount;
        }

        private static void MeasureLeafMetrics(
            Mesh mesh, out float leftRadius, out float rightRadius, out float averageDepth)
        {
            leftRadius = 0f;
            rightRadius = 0f;
            averageDepth = 0f;
            if (mesh == null) return;

            Vector3[] vertices = mesh.vertices;
            int cursor = 0;
            int leftCount = 0;
            int rightCount = 0;
            float leftRadiusSum = 0f;
            float rightRadiusSum = 0f;
            float depthSum = 0f;
            cursor = MeasureLeafPath(vertices, cursor, LeftIvyClusterCount,
                ref leftCount, ref leftRadiusSum, ref depthSum);
            MeasureLeafPath(vertices, cursor, RightIvyClusterCount,
                ref rightCount, ref rightRadiusSum, ref depthSum);

            leftRadius = leftCount == 0 ? 0f : leftRadiusSum / leftCount;
            rightRadius = rightCount == 0 ? 0f : rightRadiusSum / rightCount;
            int total = leftCount + rightCount;
            averageDepth = total == 0 ? 0f : depthSum / total;
        }

        private static int MeasureLeafPath(
            Vector3[] vertices,
            int cursor,
            int clusterCount,
            ref int count,
            ref float radiusSum,
            ref float depthSum)
        {
            for (int cluster = 0; cluster < clusterCount; cluster++)
            {
                if (cluster > 0) cursor += IvyStemVertexCount;
                for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
                {
                    if (cursor + IvyLeafVertexCount > vertices.Length) return vertices.Length;
                    Vector3 centre = vertices[cursor];
                    float radius = 0f;
                    float minZ = centre.z;
                    float maxZ = centre.z;
                    for (int i = 1; i < IvyLeafVertexCount; i++)
                    {
                        Vector3 delta = vertices[cursor + i] - centre;
                        radius = Mathf.Max(radius, new Vector2(delta.x, delta.y).magnitude);
                        minZ = Mathf.Min(minZ, vertices[cursor + i].z);
                        maxZ = Mathf.Max(maxZ, vertices[cursor + i].z);
                    }
                    radiusSum += radius;
                    depthSum += maxZ - minZ;
                    count++;
                    cursor += IvyLeafVertexCount;
                    if ((leaf & 1) == 0) cursor += IvyStemVertexCount;
                }
            }
            return cursor;
        }
    }
}

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

        [UnityTest, Timeout(30000)]
        public IEnumerator EnglishIvyPassRemovesGarlandConnectorsAndBuildsBouquetsAcrossRebuild()
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
                float lushConnectorExtent = MaximumPathConnectorExtent(ivy);
                float lushBouquetSpread = AverageFlowerClusterSpread(petals);
                Assert.That(lushConnectorExtent, Is.GreaterThan(0.02f),
                    "The discriminator requires the known visible inter-cluster connector before the English-ivy pass.");

                ArchReferenceGrowthEnglishIvyPass english = host.AddComponent<ArchReferenceGrowthEnglishIvyPass>();
                for (int frame = 0; frame < 20 && !english.EnglishApplied; frame++)
                    yield return null;

                Assert.That(english.EnglishApplied, Is.True);
                Assert.That(growth.HeroIvyMesh, Is.SameAs(ivy),
                    "English-ivy refinement must mutate the existing combined ivy mesh.");
                Assert.That(growth.HeroFlowerPetalMesh, Is.SameAs(petals),
                    "Bouquet refinement must mutate the existing combined petal mesh.");

                float connectorExtent = MaximumPathConnectorExtent(ivy);
                float bouquetSpread = AverageFlowerClusterSpread(petals);
                float leafRadius = AverageLeafRadius(ivy);
                float leafDepth = AverageLeafDepth(ivy);
                Assert.That(connectorExtent, Is.LessThan(0.001f),
                    "Inter-cluster connector quads must be degenerate so the hero growth cannot read as one diagonal garland.");
                Assert.That(bouquetSpread, Is.LessThan(lushBouquetSpread * 0.80f),
                    "The three existing flower heads per cluster must become a materially tighter bouquet.");
                Assert.That(leafRadius, Is.GreaterThan(0.11f).And.LessThan(0.24f),
                    "Broad English-ivy leaves must remain readable without returning to oversized blob cards.");
                Assert.That(leafDepth, Is.GreaterThan(0.008f),
                    "Leaves need shallow bowl depth so lighting can separate overlapping layers at the saved pose.");
                Assert.That(growth.HeroDrawCallCount, Is.EqualTo(3));
                Assert.That(growth.HeroVertexCount, Is.EqualTo(vertexBudget));
                Assert.That(growth.HeroVertexCount, Is.LessThanOrEqualTo(4096));

                float finalBouquetSpread = bouquetSpread;
                float finalLeafRadius = leafRadius;
                Mesh firstIvy = ivy;
                growth.enabled = false;
                yield return null;
                growth.enabled = true;

                for (int frame = 0; frame < 24; frame++)
                {
                    Mesh rebuilt = growth.HeroIvyMesh;
                    if (rebuilt != null && rebuilt != firstIvy && english.EnglishApplied &&
                        MaximumPathConnectorExtent(rebuilt) < 0.001f)
                        break;
                    yield return null;
                }

                Assert.That(growth.HeroIvyMesh, Is.Not.Null);
                Assert.That(growth.HeroIvyMesh, Is.Not.SameAs(firstIvy));
                Assert.That(english.EnglishApplied, Is.True,
                    "The final one-shot refinement must reapply through the production growth rebuild lifecycle.");
                Assert.That(MaximumPathConnectorExtent(growth.HeroIvyMesh), Is.LessThan(0.001f));
                Assert.That(AverageFlowerClusterSpread(growth.HeroFlowerPetalMesh),
                    Is.EqualTo(finalBouquetSpread).Within(0.01f));
                Assert.That(AverageLeafRadius(growth.HeroIvyMesh),
                    Is.EqualTo(finalLeafRadius).Within(0.01f));
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

        private static float MaximumPathConnectorExtent(Mesh mesh)
        {
            if (mesh == null) return float.PositiveInfinity;
            Vector3[] vertices = mesh.vertices;
            int cursor = 0;
            float maximum = 0f;
            cursor = MeasureConnectorPath(vertices, cursor, LeftIvyClusterCount, ref maximum);
            MeasureConnectorPath(vertices, cursor, RightIvyClusterCount, ref maximum);
            return maximum;
        }

        private static int MeasureConnectorPath(Vector3[] vertices, int cursor, int clusterCount, ref float maximum)
        {
            for (int cluster = 0; cluster < clusterCount; cluster++)
            {
                if (cluster > 0)
                {
                    if (cursor + IvyStemVertexCount > vertices.Length) return vertices.Length;
                    Vector3 origin = vertices[cursor];
                    for (int i = 1; i < IvyStemVertexCount; i++)
                        maximum = Mathf.Max(maximum, (vertices[cursor + i] - origin).magnitude);
                    cursor += IvyStemVertexCount;
                }

                for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
                {
                    cursor += IvyLeafVertexCount;
                    if ((leaf & 1) == 0) cursor += IvyStemVertexCount;
                    if (cursor > vertices.Length) return vertices.Length;
                }
            }
            return cursor;
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
                    int headStart = head * headVertexCount;
                    Vector3 headCentre = Vector3.zero;
                    for (int petal = 0; petal < FlowerPetalsPerHead; petal++)
                        headCentre += vertices[headStart + petal * FlowerPetalVertexCount];
                    headCentre /= FlowerPetalsPerHead;
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

        private static float AverageLeafRadius(Mesh mesh)
        {
            MeasureLeaves(mesh, out int count, out float radiusSum, out _);
            return count == 0 ? 0f : radiusSum / count;
        }

        private static float AverageLeafDepth(Mesh mesh)
        {
            MeasureLeaves(mesh, out int count, out _, out float depthSum);
            return count == 0 ? 0f : depthSum / count;
        }

        private static void MeasureLeaves(Mesh mesh, out int count, out float radiusSum, out float depthSum)
        {
            count = 0;
            radiusSum = 0f;
            depthSum = 0f;
            if (mesh == null) return;
            Vector3[] vertices = mesh.vertices;
            int cursor = 0;
            cursor = MeasureLeafPath(vertices, cursor, LeftIvyClusterCount, ref count, ref radiusSum, ref depthSum);
            MeasureLeafPath(vertices, cursor, RightIvyClusterCount, ref count, ref radiusSum, ref depthSum);
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

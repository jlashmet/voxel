using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ArchReferenceGrowthLushPassTests
    {
        private const int IvyLeavesPerCluster = 8;
        private const int IvyLeafVertexCount = 17;
        private const int IvyStemVertexCount = 4;
        private const int LeftIvyClusterCount = 12;
        private const int RightIvyClusterCount = 4;
        private const int FlowerPetalsPerHead = 5;
        private const int FlowerPetalVertexCount = 7;

        [UnityTest, Timeout(30000)]
        public IEnumerator LushPassBuildsLayeredCanopiesAndReadableFlowerClustersAcrossRebuild()
        {
            var host = new GameObject("Arch lush reference regression");
            try
            {
                host.transform.SetPositionAndRotation(
                    new Vector3(-0.85728186f, 8.398123f, -9.309617f),
                    new Quaternion(0.09724782f, -0.01389580f, 0.00135791f, 0.9951624f));
                Camera camera = host.AddComponent<Camera>();
                ArchReferenceGrowthWorldSpace.EnsureInstalled(camera);
                ArchReferenceGrowth growth = host.AddComponent<ArchReferenceGrowth>();
                ArchReferenceGrowthDetailPass detail = host.AddComponent<ArchReferenceGrowthDetailPass>();

                for (int frame = 0; frame < 12 && !detail.RefinementApplied; frame++)
                    yield return null;
                Assert.That(detail.RefinementApplied, Is.True,
                    "The production depth/detail pass must finish before final composition is measured.");

                Mesh ivy = growth.HeroIvyMesh;
                Mesh petals = growth.HeroFlowerPetalMesh;
                Assert.That(ivy, Is.Not.Null);
                Assert.That(petals, Is.Not.Null);
                int originalVertexBudget = growth.HeroVertexCount;
                float detailedClusterSpread = AverageClusterSpread(ivy);
                float detailedFlowerRadius = AverageFlowerHeadRadius(petals);
                Assert.That(CountVisiblePetals(petals), Is.EqualTo(90),
                    "The discriminator requires the known three-bract detail state before composition.");

                ArchReferenceGrowthLushPass lush = host.AddComponent<ArchReferenceGrowthLushPass>();
                for (int frame = 0; frame < 12 && !lush.LushApplied; frame++)
                    yield return null;

                Assert.That(lush.LushApplied, Is.True);
                Assert.That(growth.HeroIvyMesh, Is.SameAs(ivy),
                    "Final composition must mutate the existing combined ivy mesh, not add a renderer.");
                Assert.That(growth.HeroFlowerPetalMesh, Is.SameAs(petals));

                float composedSpread = AverageClusterSpread(ivy);
                float composedLeafRadius = AverageLeafRadius(ivy);
                float maxLeafRadius = MaximumLeafRadius(ivy);
                float composedFlowerRadius = AverageFlowerHeadRadius(petals);
                Assert.That(composedSpread, Is.GreaterThan(detailedClusterSpread * 1.18f),
                    "Reference composition must redistribute leaf centres into wider layered canopies rather than merely scaling cards in place.");
                Assert.That(composedLeafRadius, Is.GreaterThan(0.13f).And.LessThan(0.24f),
                    "Hero leaves must remain individually readable at the saved close-up pose without becoming oversized blobs.");
                Assert.That(maxLeafRadius, Is.LessThan(0.31f),
                    "No single ivy leaf may dominate the canopy silhouette.");
                Assert.That(composedFlowerRadius, Is.GreaterThan(detailedFlowerRadius * 1.30f),
                    "Flower heads must become materially more readable than the rejected tiny three-bract state.");
                Assert.That(CountVisiblePetals(petals), Is.EqualTo(150),
                    "All five petals on all 30 flower heads must remain readable after final composition.");
                Assert.That(growth.HeroDrawCallCount, Is.EqualTo(3));
                Assert.That(growth.HeroVertexCount, Is.EqualTo(originalVertexBudget),
                    "Final composition must not spend additional geometry or draw budget.");
                Assert.That(growth.HeroVertexCount, Is.LessThanOrEqualTo(4096));

                float finalSpread = composedSpread;
                float finalLeafRadius = composedLeafRadius;
                float finalFlowerRadius = composedFlowerRadius;
                Mesh firstIvy = ivy;
                growth.enabled = false;
                yield return null;
                growth.enabled = true;

                for (int frame = 0; frame < 16; frame++)
                {
                    if (growth.HeroIvyMesh != null && growth.HeroIvyMesh != firstIvy &&
                        detail.RefinementApplied && lush.LushApplied &&
                        CountVisiblePetals(growth.HeroFlowerPetalMesh) == 150)
                        break;
                    yield return null;
                }

                Assert.That(growth.HeroIvyMesh, Is.Not.Null);
                Assert.That(growth.HeroIvyMesh, Is.Not.SameAs(firstIvy),
                    "Growth re-enable must construct a fresh production mesh.");
                Assert.That(lush.LushApplied, Is.True,
                    "The same lifecycle path must recompose rebuilt meshes.");
                Assert.That(CountVisiblePetals(growth.HeroFlowerPetalMesh), Is.EqualTo(150));
                Assert.That(AverageClusterSpread(growth.HeroIvyMesh), Is.EqualTo(finalSpread).Within(0.01f));
                Assert.That(AverageLeafRadius(growth.HeroIvyMesh), Is.EqualTo(finalLeafRadius).Within(0.01f));
                Assert.That(AverageFlowerHeadRadius(growth.HeroFlowerPetalMesh), Is.EqualTo(finalFlowerRadius).Within(0.01f),
                    "The one-shot reference composition must be deterministic across production rebuilds.");
                Assert.That(growth.HeroDrawCallCount, Is.EqualTo(3));
                Assert.That(growth.HeroVertexCount, Is.EqualTo(originalVertexBudget));

                growth.enabled = false;
                yield return null;
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static float AverageLeafRadius(Mesh mesh)
        {
            MeasureLeaves(mesh, out int leaves, out float radiusSum, out _, out _);
            return leaves == 0 ? 0f : radiusSum / leaves;
        }

        private static float MaximumLeafRadius(Mesh mesh)
        {
            MeasureLeaves(mesh, out _, out _, out float maxRadius, out _);
            return maxRadius;
        }

        private static float AverageClusterSpread(Mesh mesh)
        {
            MeasureLeaves(mesh, out _, out _, out _, out float spread);
            return spread;
        }

        private static void MeasureLeaves(
            Mesh mesh,
            out int leaves,
            out float radiusSum,
            out float maxRadius,
            out float averageClusterSpread)
        {
            Vector3[] vertices = mesh.vertices;
            int cursor = 0;
            leaves = 0;
            radiusSum = 0f;
            maxRadius = 0f;
            float spreadSum = 0f;
            int clusters = 0;
            cursor = MeasurePath(vertices, cursor, LeftIvyClusterCount,
                ref leaves, ref radiusSum, ref maxRadius, ref spreadSum, ref clusters);
            MeasurePath(vertices, cursor, RightIvyClusterCount,
                ref leaves, ref radiusSum, ref maxRadius, ref spreadSum, ref clusters);
            averageClusterSpread = clusters == 0 ? 0f : spreadSum / clusters;
        }

        private static int MeasurePath(
            Vector3[] vertices,
            int cursor,
            int clusterCount,
            ref int leaves,
            ref float radiusSum,
            ref float maxLeafRadius,
            ref float spreadSum,
            ref int clusters)
        {
            for (int cluster = 0; cluster < clusterCount; cluster++)
            {
                if (cluster > 0)
                    cursor += IvyStemVertexCount;

                var centres = new Vector3[IvyLeavesPerCluster];
                Vector3 clusterCentre = Vector3.zero;
                for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
                {
                    if (cursor + IvyLeafVertexCount > vertices.Length)
                        return vertices.Length;

                    Vector3 centre = vertices[cursor];
                    centres[leaf] = centre;
                    clusterCentre += centre;
                    float radius = 0f;
                    for (int vertex = 1; vertex < IvyLeafVertexCount; vertex++)
                    {
                        Vector2 d = new(
                            vertices[cursor + vertex].x - centre.x,
                            vertices[cursor + vertex].y - centre.y);
                        radius = Mathf.Max(radius, d.magnitude);
                    }
                    radiusSum += radius;
                    maxLeafRadius = Mathf.Max(maxLeafRadius, radius);
                    leaves++;
                    cursor += IvyLeafVertexCount;
                    if ((leaf & 1) == 0)
                        cursor += IvyStemVertexCount;
                }

                clusterCentre /= IvyLeavesPerCluster;
                float clusterSpread = 0f;
                for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
                {
                    Vector2 d = new(
                        centres[leaf].x - clusterCentre.x,
                        centres[leaf].y - clusterCentre.y);
                    clusterSpread += d.magnitude;
                }
                spreadSum += clusterSpread / IvyLeavesPerCluster;
                clusters++;
            }
            return cursor;
        }

        private static float AverageFlowerHeadRadius(Mesh mesh)
        {
            if (mesh == null) return 0f;
            Vector3[] vertices = mesh.vertices;
            int headVertexCount = FlowerPetalsPerHead * FlowerPetalVertexCount;
            int headCount = vertices.Length / headVertexCount;
            if (headCount == 0) return 0f;

            float radiusSum = 0f;
            for (int head = 0; head < headCount; head++)
            {
                int headStart = head * headVertexCount;
                Vector3 headCentre = Vector3.zero;
                for (int petal = 0; petal < FlowerPetalsPerHead; petal++)
                    headCentre += vertices[headStart + petal * FlowerPetalVertexCount];
                headCentre /= FlowerPetalsPerHead;

                float radius = 0f;
                for (int vertex = 0; vertex < headVertexCount; vertex++)
                {
                    Vector2 d = new(
                        vertices[headStart + vertex].x - headCentre.x,
                        vertices[headStart + vertex].y - headCentre.y);
                    radius = Mathf.Max(radius, d.magnitude);
                }
                radiusSum += radius;
            }
            return radiusSum / headCount;
        }

        private static int CountVisiblePetals(Mesh mesh)
        {
            if (mesh == null) return 0;
            Vector3[] vertices = mesh.vertices;
            int visible = 0;
            for (int start = 0; start + FlowerPetalVertexCount <= vertices.Length;
                 start += FlowerPetalVertexCount)
            {
                Vector3 centre = vertices[start];
                float maxSqr = 0f;
                for (int vertex = 1; vertex < FlowerPetalVertexCount; vertex++)
                    maxSqr = Mathf.Max(maxSqr, (vertices[start + vertex] - centre).sqrMagnitude);
                if (maxSqr > 0.000001f)
                    visible++;
            }
            return visible;
        }
    }
}

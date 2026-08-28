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
        private const int FlowerPetalVertexCount = 7;

        [UnityTest, Timeout(30000)]
        public IEnumerator LushPassRestoresBroadLeavesAndFivePetalHeadsAcrossRebuild()
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
                    "The production depth/detail pass must finish before lush restoration is measured.");

                Mesh ivy = growth.HeroIvyMesh;
                Mesh petals = growth.HeroFlowerPetalMesh;
                Assert.That(ivy, Is.Not.Null);
                Assert.That(petals, Is.Not.Null);
                int originalVertexBudget = growth.HeroVertexCount;
                float detailedLeafRadius = AverageLeafRadius(ivy);
                Assert.That(CountVisiblePetals(petals), Is.EqualTo(90),
                    "The discriminator requires the known three-bract detail state before restoration.");

                ArchReferenceGrowthLushPass lush = host.AddComponent<ArchReferenceGrowthLushPass>();
                for (int frame = 0; frame < 12 && !lush.LushApplied; frame++)
                    yield return null;

                Assert.That(lush.LushApplied, Is.True);
                Assert.That(growth.HeroIvyMesh, Is.SameAs(ivy),
                    "Lush restoration must mutate the existing combined ivy mesh, not add a renderer.");
                Assert.That(growth.HeroFlowerPetalMesh, Is.SameAs(petals));
                Assert.That(AverageLeafRadius(ivy), Is.GreaterThan(detailedLeafRadius * 1.40f),
                    "Reference restoration must produce materially broader overlapping leaves than the rejected thin replay.");
                Assert.That(CountVisiblePetals(petals), Is.EqualTo(150),
                    "All five petals on all 30 flower heads must remain readable after lush restoration.");
                Assert.That(growth.HeroDrawCallCount, Is.EqualTo(3));
                Assert.That(growth.HeroVertexCount, Is.EqualTo(originalVertexBudget),
                    "The lush pass must not spend additional geometry or draw budget.");
                Assert.That(growth.HeroVertexCount, Is.LessThanOrEqualTo(4096));

                float lushLeafRadius = AverageLeafRadius(ivy);
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
                    "The same lifecycle path must restore lush geometry on rebuilt meshes.");
                Assert.That(CountVisiblePetals(growth.HeroFlowerPetalMesh), Is.EqualTo(150));
                Assert.That(AverageLeafRadius(growth.HeroIvyMesh), Is.EqualTo(lushLeafRadius).Within(0.01f),
                    "The one-shot restoration must be deterministic across production rebuilds.");
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
            Vector3[] vertices = mesh.vertices;
            int cursor = 0;
            int leaves = 0;
            float radiusSum = 0f;
            cursor = MeasurePath(vertices, cursor, LeftIvyClusterCount, ref leaves, ref radiusSum);
            MeasurePath(vertices, cursor, RightIvyClusterCount, ref leaves, ref radiusSum);
            return leaves == 0 ? 0f : radiusSum / leaves;
        }

        private static int MeasurePath(
            Vector3[] vertices,
            int cursor,
            int clusterCount,
            ref int leaves,
            ref float radiusSum)
        {
            for (int cluster = 0; cluster < clusterCount; cluster++)
            {
                if (cluster > 0)
                    cursor += IvyStemVertexCount;
                for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
                {
                    if (cursor + IvyLeafVertexCount > vertices.Length)
                        return vertices.Length;
                    Vector3 centre = vertices[cursor];
                    float maxRadius = 0f;
                    for (int vertex = 1; vertex < IvyLeafVertexCount; vertex++)
                    {
                        Vector2 d = new(
                            vertices[cursor + vertex].x - centre.x,
                            vertices[cursor + vertex].y - centre.y);
                        maxRadius = Mathf.Max(maxRadius, d.magnitude);
                    }
                    radiusSum += maxRadius;
                    leaves++;
                    cursor += IvyLeafVertexCount;
                    if ((leaf & 1) == 0)
                        cursor += IvyStemVertexCount;
                }
            }
            return cursor;
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

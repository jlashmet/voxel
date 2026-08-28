using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ArchReferenceGrowthDetailPassTests
    {
        [UnityTest, Timeout(30000)]
        public IEnumerator CloseUpRefinementAddsLeafDepthAndIrregularBlossomsAcrossRebuild()
        {
            var host = new GameObject("Arch reference detail regression");
            try
            {
                host.transform.SetPositionAndRotation(
                    new Vector3(-0.85728186f, 8.398123f, -9.309617f),
                    new Quaternion(0.09724782f, -0.01389580f, 0.00135791f, 0.9951624f));
                Camera camera = host.AddComponent<Camera>();
                ArchReferenceGrowthWorldSpace.EnsureInstalled(camera);
                ArchReferenceGrowthDetailPass detail = host.AddComponent<ArchReferenceGrowthDetailPass>();
                ArchReferenceGrowth growth = host.AddComponent<ArchReferenceGrowth>();

                yield return null;
                yield return null;
                yield return null;

                Assert.That(detail.RefinementApplied, Is.True,
                    "The close-up pass must refine the production mesh after world-space anchoring settles.");
                Assert.That(growth.HeroDrawCallCount, Is.EqualTo(3));
                Assert.That(growth.HeroVertexCount, Is.LessThanOrEqualTo(4096));
                Assert.That(growth.HeroIvyMesh.bounds.size.z, Is.GreaterThan(0.05f),
                    "Ivy needs visible front/back layering instead of one flat cutout depth band.");
                Assert.That(CountNonDegenerateTriangles(growth.HeroFlowerPetalMesh), Is.EqualTo(540),
                    "Thirty flower heads should expose three irregular bracts each, not five-way radial daisies.");

                Mesh firstIvy = growth.HeroIvyMesh;
                growth.enabled = false;
                yield return null;
                growth.enabled = true;
                yield return null;
                yield return null;
                yield return null;

                Assert.That(growth.HeroIvyMesh, Is.Not.SameAs(firstIvy),
                    "Growth rebuild should replace the authored mesh instance.");
                Assert.That(detail.RefinementApplied, Is.True,
                    "The event-driven detail pass must automatically refine each rebuilt hero mesh.");
                Assert.That(growth.HeroIvyMesh.bounds.size.z, Is.GreaterThan(0.05f));
                Assert.That(CountNonDegenerateTriangles(growth.HeroFlowerPetalMesh), Is.EqualTo(540));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static int CountNonDegenerateTriangles(Mesh mesh)
        {
            Assert.That(mesh, Is.Not.Null);
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            int count = 0;
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]];
                Vector3 b = vertices[triangles[i + 1]];
                Vector3 c = vertices[triangles[i + 2]];
                if (Vector3.Cross(b - a, c - a).sqrMagnitude > 0.00000001f)
                    count++;
            }
            return count;
        }
    }
}

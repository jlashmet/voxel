using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ArchReferenceGrowthFinalPresentationPassTests
    {
        private const int ClusterCount = 16;
        private const int LeavesPerCluster = 8;
        private const int LeafVertexCount = 17;
        private const int FlowerHeads = 30;
        private const int HeadsPerBouquet = 5;
        private const int PetalsPerHead = 5;
        private const int PetalVertexCount = 7;
        private const int HeadVertexCount = PetalsPerHead * PetalVertexCount;

        [UnityTest, Timeout(30000)]
        public IEnumerator FinalPresentationKeepsMasonryAttachmentAndBuildsLayeredBotanicalReadAcrossRebuild()
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
                ArchReferenceGrowthFinalPresentationPass finalPass = host.AddComponent<ArchReferenceGrowthFinalPresentationPass>();
                for (int i = 0; i < 120 && !finalPass.FinalPresentationApplied; i++) yield return null;

                Assert.That(finalPass.FinalPresentationApplied, Is.True);
                AssertPresentation(growth);

                Mesh firstIvy = growth.HeroIvyMesh;
                Mesh firstFlowers = growth.HeroFlowerPetalMesh;
                growth.enabled = false;
                yield return null;
                growth.enabled = true;
                for (int i = 0; i < 180; i++)
                {
                    if (growth.HeroIvyMesh != null && growth.HeroIvyMesh != firstIvy &&
                        growth.HeroFlowerPetalMesh != null && growth.HeroFlowerPetalMesh != firstFlowers &&
                        finalPass.FinalPresentationApplied)
                        break;
                    yield return null;
                }

                Assert.That(growth.HeroIvyMesh, Is.Not.Null.And.Not.SameAs(firstIvy));
                Assert.That(growth.HeroFlowerPetalMesh, Is.Not.Null.And.Not.SameAs(firstFlowers));
                Assert.That(finalPass.FinalPresentationApplied, Is.True);
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
            Assert.That(MaxTriangleEdge(ivy), Is.LessThan(0.42f));

            Vector3[] clusters = ClusterCentres(ivy, starts);
            for (int cluster = 0; cluster < 15; cluster++)
                Assert.That(Vector2.Distance(clusters[cluster], ArchReferenceGrowthSemanticMassPass.ClusterTarget(cluster)),
                    Is.LessThan(0.04f), "Final coverage must not move an authored masonry anchor.");
            Assert.That(Vector2.Distance(clusters[15], ArchReferenceGrowthAaaPass.Support(15)), Is.LessThan(0.04f));

            Assert.That(AverageLeafRadius(ivy, starts), Is.InRange(0.10f, 0.18f));
            Assert.That(AverageLeafDepth(ivy, starts), Is.GreaterThan(0.015f),
                "Leaves must retain dimensional relief instead of collapsing to flat paper cards.");

            Vector3[] bouquets = BouquetCentres(flowers);
            for (int mass = 0; mass < 3; mass++)
            {
                float pair = Vector2.Distance(bouquets[mass * 2], bouquets[mass * 2 + 1]);
                Assert.That(pair, Is.InRange(0.25f, 0.70f),
                    "Each masonry mass must keep two distinct but integrated blossom bouquets.");
            }
            Assert.That(bouquets[4].y, Is.GreaterThan(7.90f));
            Assert.That(bouquets[5].y, Is.GreaterThan(7.90f));

            Transform root = FindHeroRoot();
            Assert.That(root, Is.Not.Null);
            Material ivyMaterial = FindMaterial(root, "Lobed Ivy");
            Material petalMaterial = FindMaterial(root, "Flower Petals");
            Assert.That(ivyMaterial, Is.Not.Null);
            Assert.That(petalMaterial, Is.Not.Null);
            Color ivyBase = ivyMaterial.GetColor("_BaseColor");
            Color petalBase = petalMaterial.GetColor("_BaseColor");
            Assert.That(ivyBase.g - ivyBase.r, Is.GreaterThan(0.15f));
            Assert.That(ivyBase.g - ivyBase.b, Is.GreaterThan(0.25f));
            Assert.That(ivyBase.r, Is.LessThan(0.65f), "Ivy material must not return to a near-white wash.");
            Assert.That(petalBase.r - petalBase.g, Is.GreaterThan(0.15f));

            Assert.That(growth.HeroLeafCount, Is.EqualTo(128));
            Assert.That(growth.HeroFlowerHeadCount, Is.EqualTo(30));
            Assert.That(growth.HeroDrawCallCount, Is.EqualTo(3));
            Assert.That(growth.HeroVertexCount, Is.LessThanOrEqualTo(4096));
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

        private static float AverageLeafDepth(Mesh mesh, int[,] starts)
        {
            Vector3[] vertices = mesh.vertices;
            float sum = 0f;
            int count = 0;
            for (int cluster = 0; cluster < ClusterCount; cluster++)
            for (int leaf = 0; leaf < LeavesPerCluster; leaf++)
            {
                int start = starts[cluster, leaf];
                float min = vertices[start].z;
                float max = min;
                for (int i = 1; i < LeafVertexCount; i++)
                {
                    min = Mathf.Min(min, vertices[start + i].z);
                    max = Mathf.Max(max, vertices[start + i].z);
                }
                sum += max - min;
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
            for (int petal = 0; petal < PetalsPerHead; petal++) sum += vertices[start + petal * PetalVertexCount];
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

        private static Material FindMaterial(Transform root, string childName)
        {
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                if (renderer != null && renderer.gameObject.name == childName) return renderer.sharedMaterial;
            return null;
        }

        private static Transform FindHeroRoot()
        {
            foreach (Transform candidate in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (candidate == null || candidate.name != "Arch Reference Hero Growth") continue;
                GameObject value = candidate.gameObject;
                if (!value.activeInHierarchy || !value.scene.IsValid() || !value.scene.isLoaded) continue;
                return candidate;
            }
            return null;
        }
    }
}

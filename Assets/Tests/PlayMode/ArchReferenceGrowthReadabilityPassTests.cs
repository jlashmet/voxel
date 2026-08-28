using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ArchReferenceGrowthReadabilityPassTests
    {
        private const int IvyLeavesPerCluster = 8;
        private const int IvyLeafVertexCount = 17;
        private const int LeftIvyClusterCount = 12;
        private const int TotalIvyClusterCount = 16;
        private const int FlowerHeads = 30;
        private const int FlowerPetalsPerHead = 5;
        private const int FlowerPetalVertexCount = 7;
        private const int FlowerCentreVertexCount = 9;
        private const int HeadVertexCount = FlowerPetalsPerHead * FlowerPetalVertexCount;
        private static readonly Color StemColor = new(0.07f, 0.24f, 0.04f, 1f);

        [UnityTest, Timeout(30000)]
        public IEnumerator FinalReadabilityPassSeparatesLeafLayersAndBuildsOverlappingBouquetsAcrossRebuild()
        {
            var host = new GameObject("Arch readability regression");
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

                Mesh ivy = growth.HeroIvyMesh;
                Mesh petals = growth.HeroFlowerPetalMesh;
                Mesh centres = FindHeroMesh("Flower Centres");
                Assert.That(ivy, Is.Not.Null);
                Assert.That(petals, Is.Not.Null);
                Assert.That(centres, Is.Not.Null);
                Assert.That(TryFindIvyLeafStarts(ivy, out _, out int beforeLeaves), Is.True);
                Assert.That(beforeLeaves, Is.EqualTo(128));
                int vertexBudget = growth.HeroVertexCount;
                float beforeLeafRadius = AverageLeftLeafRadius(ivy);
                float beforeFlowerRadius = AverageFlowerHeadRadius(petals);

                ArchReferenceGrowthReadabilityPass readability = host.AddComponent<ArchReferenceGrowthReadabilityPass>();
                for (int frame = 0; frame < 36 && !readability.ReadabilityApplied; frame++) yield return null;
                Assert.That(readability.ReadabilityApplied, Is.True);
                Assert.That(growth.HeroIvyMesh, Is.SameAs(ivy));
                Assert.That(growth.HeroFlowerPetalMesh, Is.SameAs(petals));
                Assert.That(TryFindIvyLeafStarts(ivy, out _, out int afterLeaves), Is.True);
                Assert.That(afterLeaves, Is.EqualTo(128));

                float leafRadius = AverageLeftLeafRadius(ivy);
                Assert.That(leafRadius, Is.LessThan(beforeLeafRadius * 0.96f).And.GreaterThan(beforeLeafRadius * 0.74f),
                    "Individual leaves must separate instead of merging back into a solid cutout mass.");
                Assert.That(LeftLeafValueRange(ivy), Is.GreaterThan(0.14f),
                    "Existing leaf cards need enough value separation for overlapping layers to remain readable.");
                Assert.That(LeftLeafDepthRange(ivy), Is.GreaterThan(0.045f),
                    "Leaf centres need real front/back separation rather than one flat silhouette plane.");
                Assert.That(ReadableLocalVineCount(ivy), Is.GreaterThanOrEqualTo(10),
                    "Most left clusters must reuse their local stem quad as a short vine cue.");
                Assert.That(MaximumLocalVineSpan(ivy), Is.LessThan(0.56f),
                    "No local vine may recreate the rejected long diagonal garland.");

                float flowerRadius = AverageFlowerHeadRadius(petals);
                Assert.That(flowerRadius, Is.GreaterThan(beforeFlowerRadius * 1.15f).And.LessThan(beforeFlowerRadius * 1.60f),
                    "Bouquet heads must become larger layered blooms without turning into oversized star icons.");
                Assert.That(AverageFlowerCentreRadius(centres) / flowerRadius, Is.LessThan(0.16f),
                    "Flower centres must read as small pollen cores rather than dominant orange dots.");
                Assert.That(FlowerValueRange(petals), Is.GreaterThan(0.10f),
                    "Bouquets need visible blossom value variation instead of one repeated pink stamp.");
                Assert.That(AverageNearestHeadDistance(petals) / flowerRadius, Is.LessThan(1.75f),
                    "Enlarged heads must overlap into bouquets rather than remain isolated icons.");
                Assert.That(growth.HeroDrawCallCount, Is.EqualTo(3));
                Assert.That(growth.HeroVertexCount, Is.EqualTo(vertexBudget).And.LessThanOrEqualTo(4096));

                Mesh firstIvy = ivy;
                float expectedLeafRadius = leafRadius;
                float expectedFlowerRadius = flowerRadius;
                float expectedValueRange = LeftLeafValueRange(ivy);
                growth.enabled = false;
                yield return null;
                growth.enabled = true;
                for (int frame = 0; frame < 48; frame++)
                {
                    if (growth.HeroIvyMesh != null && growth.HeroIvyMesh != firstIvy &&
                        mass.CompositionApplied && readability.ReadabilityApplied) break;
                    yield return null;
                }

                Assert.That(growth.HeroIvyMesh, Is.Not.Null.And.Not.SameAs(firstIvy));
                Assert.That(mass.CompositionApplied, Is.True);
                Assert.That(readability.ReadabilityApplied, Is.True);
                Assert.That(TryFindIvyLeafStarts(growth.HeroIvyMesh, out _, out int rebuiltLeaves), Is.True);
                Assert.That(rebuiltLeaves, Is.EqualTo(128));
                Assert.That(AverageLeftLeafRadius(growth.HeroIvyMesh), Is.EqualTo(expectedLeafRadius).Within(0.01f));
                Assert.That(AverageFlowerHeadRadius(growth.HeroFlowerPetalMesh), Is.EqualTo(expectedFlowerRadius).Within(0.01f));
                Assert.That(LeftLeafValueRange(growth.HeroIvyMesh), Is.EqualTo(expectedValueRange).Within(0.01f));
                Assert.That(ReadableLocalVineCount(growth.HeroIvyMesh), Is.GreaterThanOrEqualTo(10));
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
            starts = new int[TotalIvyClusterCount, IvyLeavesPerCluster];
            found = 0;
            if (mesh == null) return false;
            Color[] colors = mesh.colors;
            int vertexCount = mesh.vertexCount;
            if (colors == null || colors.Length != vertexCount) return false;
            int cursor = 0;
            int expected = TotalIvyClusterCount * IvyLeavesPerCluster;
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

        private static float AverageLeftLeafRadius(Mesh mesh)
        {
            if (!TryFindIvyLeafStarts(mesh, out int[,] starts, out _)) return float.PositiveInfinity;
            Vector3[] vertices = mesh.vertices;
            float sum = 0f;
            int count = 0;
            for (int cluster = 0; cluster < LeftIvyClusterCount; cluster++)
            for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
            {
                int start = starts[cluster, leaf];
                Vector3 centre = vertices[start];
                float radius = 0f;
                for (int vertex = 1; vertex < IvyLeafVertexCount; vertex++)
                    radius = Mathf.Max(radius, Vector2.Distance(centre, vertices[start + vertex]));
                sum += radius;
                count++;
            }
            return sum / Mathf.Max(1, count);
        }

        private static float LeftLeafValueRange(Mesh mesh)
        {
            if (!TryFindIvyLeafStarts(mesh, out int[,] starts, out _)) return 0f;
            Color[] colors = mesh.colors;
            float minimum = float.PositiveInfinity;
            float maximum = float.NegativeInfinity;
            for (int cluster = 0; cluster < LeftIvyClusterCount; cluster++)
            for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
            {
                float value = Luminance(colors[starts[cluster, leaf]]);
                minimum = Mathf.Min(minimum, value);
                maximum = Mathf.Max(maximum, value);
            }
            return maximum - minimum;
        }

        private static float LeftLeafDepthRange(Mesh mesh)
        {
            if (!TryFindIvyLeafStarts(mesh, out int[,] starts, out _)) return 0f;
            Vector3[] vertices = mesh.vertices;
            float minimum = float.PositiveInfinity;
            float maximum = float.NegativeInfinity;
            for (int cluster = 0; cluster < LeftIvyClusterCount; cluster++)
            for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
            {
                float z = vertices[starts[cluster, leaf]].z;
                minimum = Mathf.Min(minimum, z);
                maximum = Mathf.Max(maximum, z);
            }
            return maximum - minimum;
        }

        private static int ReadableLocalVineCount(Mesh mesh)
        {
            if (!TryFindIvyLeafStarts(mesh, out int[,] starts, out _)) return 0;
            Vector3[] vertices = mesh.vertices;
            int count = 0;
            for (int cluster = 0; cluster < LeftIvyClusterCount; cluster++)
                if (StemSpan(vertices, starts[cluster, 0] - 4) > 0.28f) count++;
            return count;
        }

        private static float MaximumLocalVineSpan(Mesh mesh)
        {
            if (!TryFindIvyLeafStarts(mesh, out int[,] starts, out _)) return float.PositiveInfinity;
            Vector3[] vertices = mesh.vertices;
            float maximum = 0f;
            for (int cluster = 0; cluster < LeftIvyClusterCount; cluster++)
                maximum = Mathf.Max(maximum, StemSpan(vertices, starts[cluster, 0] - 4));
            return maximum;
        }

        private static float StemSpan(Vector3[] vertices, int start)
        {
            if (start < 0 || start + 4 > vertices.Length) return 0f;
            float maximum = 0f;
            for (int a = 0; a < 4; a++)
            for (int b = a + 1; b < 4; b++)
                maximum = Mathf.Max(maximum, Vector3.Distance(vertices[start + a], vertices[start + b]));
            return maximum;
        }

        private static float AverageFlowerHeadRadius(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            if (vertices == null || vertices.Length != FlowerHeads * HeadVertexCount) return float.PositiveInfinity;
            float sum = 0f;
            for (int head = 0; head < FlowerHeads; head++)
            {
                Vector3 centre = HeadCentre(vertices, head);
                float radius = 0f;
                int start = head * HeadVertexCount;
                for (int vertex = 0; vertex < HeadVertexCount; vertex++)
                    radius = Mathf.Max(radius, Vector2.Distance(centre, vertices[start + vertex]));
                sum += radius;
            }
            return sum / FlowerHeads;
        }

        private static float AverageFlowerCentreRadius(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            if (vertices == null || vertices.Length != FlowerHeads * FlowerCentreVertexCount) return float.PositiveInfinity;
            float sum = 0f;
            for (int head = 0; head < FlowerHeads; head++)
            {
                int start = head * FlowerCentreVertexCount;
                Vector3 centre = vertices[start];
                float radius = 0f;
                for (int vertex = 1; vertex < FlowerCentreVertexCount; vertex++)
                    radius = Mathf.Max(radius, Vector2.Distance(centre, vertices[start + vertex]));
                sum += radius;
            }
            return sum / FlowerHeads;
        }

        private static float FlowerValueRange(Mesh mesh)
        {
            Color[] colors = mesh.colors;
            if (colors == null || colors.Length != mesh.vertexCount) return 0f;
            float minimum = float.PositiveInfinity;
            float maximum = float.NegativeInfinity;
            for (int head = 0; head < FlowerHeads; head++)
            {
                float value = Luminance(colors[head * HeadVertexCount]);
                minimum = Mathf.Min(minimum, value);
                maximum = Mathf.Max(maximum, value);
            }
            return maximum - minimum;
        }

        private static float AverageNearestHeadDistance(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            if (vertices == null || vertices.Length != FlowerHeads * HeadVertexCount) return float.PositiveInfinity;
            float sum = 0f;
            for (int head = 0; head < FlowerHeads; head++)
            {
                Vector3 centre = HeadCentre(vertices, head);
                int zone = FlowerZone(head / 3);
                float nearest = float.PositiveInfinity;
                for (int other = 0; other < FlowerHeads; other++)
                {
                    if (other == head || FlowerZone(other / 3) != zone) continue;
                    nearest = Mathf.Min(nearest, Vector2.Distance(centre, HeadCentre(vertices, other)));
                }
                sum += nearest;
            }
            return sum / FlowerHeads;
        }

        private static Vector3 HeadCentre(Vector3[] vertices, int head)
        {
            Vector3 sum = Vector3.zero;
            int start = head * HeadVertexCount;
            for (int petal = 0; petal < FlowerPetalsPerHead; petal++)
                sum += vertices[start + petal * FlowerPetalVertexCount];
            return sum / FlowerPetalsPerHead;
        }

        private static int FlowerZone(int cluster)
        {
            if (cluster == 9 || cluster <= 1) return 0;
            if (cluster <= 4) return 1;
            return 2;
        }

        private static float Luminance(Color color) => color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;

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

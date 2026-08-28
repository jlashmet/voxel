using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ArchReferenceGrowthMassBreakupPassTests
    {
        private const int IvyLeavesPerCluster = 8;
        private const int IvyLeafVertexCount = 17;
        private const int IvyStemVertexCount = 4;
        private const int LeftIvyClusterCount = 12;
        private const int RightIvyClusterCount = 4;
        private const int TotalIvyClusterCount = LeftIvyClusterCount + RightIvyClusterCount;
        private const int FlowerClusterCount = 10;
        private const int FlowerHeadsPerCluster = 3;
        private const int FlowerPetalsPerHead = 5;
        private const int FlowerPetalVertexCount = 7;

        [UnityTest, Timeout(30000)]
        public IEnumerator FinalPassBreaksDiagonalBandIntoMassesAndGathersReadableBouquetsAcrossRebuild()
        {
            var host = new GameObject("Arch final composition regression");
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
                ArchReferenceGrowthEnglishIvyPass english = host.AddComponent<ArchReferenceGrowthEnglishIvyPass>();

                for (int frame = 0; frame < 28 && !english.EnglishApplied; frame++)
                    yield return null;

                Assert.That(detail.RefinementApplied, Is.True);
                Assert.That(lush.LushApplied, Is.True);
                Assert.That(english.EnglishApplied, Is.True);

                Mesh ivy = growth.HeroIvyMesh;
                Mesh petals = growth.HeroFlowerPetalMesh;
                Assert.That(ivy, Is.Not.Null);
                Assert.That(petals, Is.Not.Null);
                int vertexBudget = growth.HeroVertexCount;

                float preCompactness = AverageIvyZoneCompactness(ivy);
                float preLowerGap = IvyZoneVerticalGap(ivy, 0, 2, 3, 6);
                float preUpperGap = IvyZoneVerticalGap(ivy, 3, 6, 7, 11);
                float preFlowerCompactness = AverageFlowerZoneCompactness(petals);
                float preFlowerRadius = AverageFlowerHeadRadius(petals);

                ArchReferenceGrowthMassBreakupPass finalPass =
                    host.AddComponent<ArchReferenceGrowthMassBreakupPass>();
                for (int frame = 0; frame < 28 && !finalPass.CompositionApplied; frame++)
                    yield return null;

                Assert.That(finalPass.CompositionApplied, Is.True);
                Assert.That(growth.HeroIvyMesh, Is.SameAs(ivy));
                Assert.That(growth.HeroFlowerPetalMesh, Is.SameAs(petals));

                float compactness = AverageIvyZoneCompactness(ivy);
                float lowerGap = IvyZoneVerticalGap(ivy, 0, 2, 3, 6);
                float upperGap = IvyZoneVerticalGap(ivy, 3, 6, 7, 11);
                float flowerCompactness = AverageFlowerZoneCompactness(petals);
                float flowerRadius = AverageFlowerHeadRadius(petals);
                float flowerDepth = AverageFlowerHeadDepth(petals);

                Assert.That(compactness, Is.LessThan(preCompactness * 0.55f),
                    "Left foliage cluster centres must gather into a few masses instead of tracing the arch as a chain.");
                Assert.That(lowerGap, Is.GreaterThan(0.20f).And.GreaterThan(preLowerGap + 0.15f),
                    "A visible negative-space break is required between the lower and upper pier masses.");
                Assert.That(upperGap, Is.GreaterThan(0.20f).And.GreaterThan(preUpperGap + 0.15f),
                    "A visible negative-space break is required between the upper-pier and crown masses.");
                Assert.That(flowerCompactness, Is.LessThan(preFlowerCompactness * 0.42f),
                    "The existing flower clusters must gather into a few rich bouquets, not repeated path icons.");
                Assert.That(flowerRadius, Is.GreaterThan(preFlowerRadius * 1.20f),
                    "Bouquet heads must gain enough screen presence to read as blossoms at the saved pose.");
                Assert.That(flowerDepth, Is.GreaterThan(0.018f),
                    "Gathered blossoms must retain layered depth rather than flattening into star cards.");
                Assert.That(growth.HeroDrawCallCount, Is.EqualTo(3));
                Assert.That(growth.HeroVertexCount, Is.EqualTo(vertexBudget));
                Assert.That(growth.HeroVertexCount, Is.LessThanOrEqualTo(4096));

                float finalCompactness = compactness;
                float finalLowerGap = lowerGap;
                float finalUpperGap = upperGap;
                float finalFlowerCompactness = flowerCompactness;
                float finalFlowerRadius = flowerRadius;
                Mesh firstIvy = ivy;

                growth.enabled = false;
                yield return null;
                growth.enabled = true;

                for (int frame = 0; frame < 36; frame++)
                {
                    Mesh rebuilt = growth.HeroIvyMesh;
                    if (rebuilt != null && rebuilt != firstIvy && finalPass.CompositionApplied &&
                        IvyZoneVerticalGap(rebuilt, 0, 2, 3, 6) > 0.20f &&
                        IvyZoneVerticalGap(rebuilt, 3, 6, 7, 11) > 0.20f)
                        break;
                    yield return null;
                }

                Assert.That(growth.HeroIvyMesh, Is.Not.Null);
                Assert.That(growth.HeroIvyMesh, Is.Not.SameAs(firstIvy));
                Assert.That(finalPass.CompositionApplied, Is.True,
                    "Final composition must reapply through the production growth rebuild lifecycle.");
                Assert.That(AverageIvyZoneCompactness(growth.HeroIvyMesh),
                    Is.EqualTo(finalCompactness).Within(0.01f));
                Assert.That(IvyZoneVerticalGap(growth.HeroIvyMesh, 0, 2, 3, 6),
                    Is.EqualTo(finalLowerGap).Within(0.02f));
                Assert.That(IvyZoneVerticalGap(growth.HeroIvyMesh, 3, 6, 7, 11),
                    Is.EqualTo(finalUpperGap).Within(0.02f));
                Assert.That(AverageFlowerZoneCompactness(growth.HeroFlowerPetalMesh),
                    Is.EqualTo(finalFlowerCompactness).Within(0.01f));
                Assert.That(AverageFlowerHeadRadius(growth.HeroFlowerPetalMesh),
                    Is.EqualTo(finalFlowerRadius).Within(0.01f));
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

        private static float AverageIvyZoneCompactness(Mesh mesh)
        {
            if (mesh == null || !TryParseIvyLeafStarts(mesh.vertexCount, out int[,] starts))
                return float.PositiveInfinity;
            Vector3[] vertices = mesh.vertices;
            float sum = 0f;
            int count = 0;
            AccumulateIvyZoneCompactness(vertices, starts, 0, 2, ref sum, ref count);
            AccumulateIvyZoneCompactness(vertices, starts, 3, 6, ref sum, ref count);
            AccumulateIvyZoneCompactness(vertices, starts, 7, 11, ref sum, ref count);
            return count == 0 ? float.PositiveInfinity : sum / count;
        }

        private static void AccumulateIvyZoneCompactness(
            Vector3[] vertices, int[,] starts, int first, int last, ref float sum, ref int count)
        {
            Vector3 zone = Vector3.zero;
            int clusterCount = last - first + 1;
            var centres = new Vector3[clusterCount];
            for (int i = 0; i < clusterCount; i++)
            {
                centres[i] = MeasureIvyClusterCentre(vertices, starts, first + i);
                zone += centres[i];
            }
            zone /= clusterCount;
            foreach (Vector3 centre in centres)
            {
                sum += Vector2.Distance(new Vector2(centre.x, centre.y), new Vector2(zone.x, zone.y));
                count++;
            }
        }

        private static float IvyZoneVerticalGap(
            Mesh mesh, int lowerFirst, int lowerLast, int upperFirst, int upperLast)
        {
            if (mesh == null || !TryParseIvyLeafStarts(mesh.vertexCount, out int[,] starts))
                return float.NegativeInfinity;
            Vector3[] vertices = mesh.vertices;
            MeasureIvyZoneY(vertices, starts, lowerFirst, lowerLast, out _, out float lowerMax);
            MeasureIvyZoneY(vertices, starts, upperFirst, upperLast, out float upperMin, out _);
            return upperMin - lowerMax;
        }

        private static void MeasureIvyZoneY(
            Vector3[] vertices, int[,] starts, int first, int last, out float minimum, out float maximum)
        {
            minimum = float.PositiveInfinity;
            maximum = float.NegativeInfinity;
            for (int cluster = first; cluster <= last; cluster++)
            {
                for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
                {
                    int start = starts[cluster, leaf];
                    for (int vertex = 0; vertex < IvyLeafVertexCount; vertex++)
                    {
                        float y = vertices[start + vertex].y;
                        minimum = Mathf.Min(minimum, y);
                        maximum = Mathf.Max(maximum, y);
                    }
                }
            }
        }

        private static bool TryParseIvyLeafStarts(int vertexCount, out int[,] starts)
        {
            starts = new int[TotalIvyClusterCount, IvyLeavesPerCluster];
            int cursor = 0;
            int globalCluster = 0;
            if (!ParseIvyPath(vertexCount, LeftIvyClusterCount, ref cursor, ref globalCluster, starts))
                return false;
            return ParseIvyPath(vertexCount, RightIvyClusterCount, ref cursor, ref globalCluster, starts);
        }

        private static bool ParseIvyPath(
            int vertexCount, int clusterCount, ref int cursor, ref int globalCluster, int[,] starts)
        {
            for (int cluster = 0; cluster < clusterCount; cluster++, globalCluster++)
            {
                if (cluster > 0)
                {
                    if (cursor + IvyStemVertexCount > vertexCount) return false;
                    cursor += IvyStemVertexCount;
                }
                for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
                {
                    if (cursor + IvyLeafVertexCount > vertexCount) return false;
                    starts[globalCluster, leaf] = cursor;
                    cursor += IvyLeafVertexCount;
                    if ((leaf & 1) == 0)
                    {
                        if (cursor + IvyStemVertexCount > vertexCount) return false;
                        cursor += IvyStemVertexCount;
                    }
                }
            }
            return true;
        }

        private static Vector3 MeasureIvyClusterCentre(Vector3[] vertices, int[,] starts, int cluster)
        {
            Vector3 centre = Vector3.zero;
            for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
                centre += vertices[starts[cluster, leaf]];
            return centre / IvyLeavesPerCluster;
        }

        private static float AverageFlowerZoneCompactness(Mesh mesh)
        {
            if (mesh == null) return float.PositiveInfinity;
            Vector3[] vertices = mesh.vertices;
            int headVertexCount = FlowerPetalsPerHead * FlowerPetalVertexCount;
            var clusterCentres = new Vector3[FlowerClusterCount];
            var zoneCentres = new Vector3[3];
            var zoneCounts = new int[3];

            for (int cluster = 0; cluster < FlowerClusterCount; cluster++)
            {
                clusterCentres[cluster] = MeasureFlowerClusterCentre(vertices, cluster, headVertexCount);
                int zone = FlowerZone(cluster);
                zoneCentres[zone] += clusterCentres[cluster];
                zoneCounts[zone]++;
            }
            for (int zone = 0; zone < 3; zone++)
                zoneCentres[zone] /= zoneCounts[zone];

            float sum = 0f;
            for (int cluster = 0; cluster < FlowerClusterCount; cluster++)
            {
                int zone = FlowerZone(cluster);
                sum += Vector2.Distance(
                    new Vector2(clusterCentres[cluster].x, clusterCentres[cluster].y),
                    new Vector2(zoneCentres[zone].x, zoneCentres[zone].y));
            }
            return sum / FlowerClusterCount;
        }

        private static int FlowerZone(int cluster)
        {
            if (cluster == 9 || cluster <= 1) return 0;
            if (cluster <= 4) return 1;
            return 2;
        }

        private static Vector3 MeasureFlowerClusterCentre(
            Vector3[] vertices, int cluster, int headVertexCount)
        {
            Vector3 centre = Vector3.zero;
            for (int localHead = 0; localHead < FlowerHeadsPerCluster; localHead++)
            {
                int head = cluster * FlowerHeadsPerCluster + localHead;
                centre += MeasureHeadCentre(vertices, head * headVertexCount);
            }
            return centre / FlowerHeadsPerCluster;
        }

        private static Vector3 MeasureHeadCentre(Vector3[] vertices, int headStart)
        {
            Vector3 centre = Vector3.zero;
            for (int petal = 0; petal < FlowerPetalsPerHead; petal++)
                centre += vertices[headStart + petal * FlowerPetalVertexCount];
            return centre / FlowerPetalsPerHead;
        }

        private static float AverageFlowerHeadRadius(Mesh mesh)
        {
            if (mesh == null) return 0f;
            Vector3[] vertices = mesh.vertices;
            int headVertexCount = FlowerPetalsPerHead * FlowerPetalVertexCount;
            int headCount = FlowerClusterCount * FlowerHeadsPerCluster;
            float sum = 0f;
            for (int head = 0; head < headCount; head++)
            {
                int start = head * headVertexCount;
                Vector3 centre = MeasureHeadCentre(vertices, start);
                float radius = 0f;
                for (int i = 0; i < headVertexCount; i++)
                {
                    Vector3 delta = vertices[start + i] - centre;
                    radius = Mathf.Max(radius, new Vector2(delta.x, delta.y).magnitude);
                }
                sum += radius;
            }
            return sum / headCount;
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
            return sum / headCount;
        }
    }
}

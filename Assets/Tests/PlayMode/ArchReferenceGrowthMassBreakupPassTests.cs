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
        private const int LeftIvyClusterCount = 12;
        private const int RightIvyClusterCount = 4;
        private const int TotalIvyClusterCount = LeftIvyClusterCount + RightIvyClusterCount;
        private const int FlowerClusterCount = 10;
        private const int FlowerHeadsPerCluster = 3;
        private const int FlowerPetalsPerHead = 5;
        private const int FlowerPetalVertexCount = 7;
        private static readonly Color StemColor = new(0.07f, 0.24f, 0.04f, 1f);

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

                for (int frame = 0; frame < 28 && !english.EnglishApplied; frame++) yield return null;
                Assert.That(detail.RefinementApplied, Is.True);
                Assert.That(lush.LushApplied, Is.True);
                Assert.That(english.EnglishApplied, Is.True);

                Mesh ivy = growth.HeroIvyMesh;
                Mesh petals = growth.HeroFlowerPetalMesh;
                Assert.That(ivy, Is.Not.Null);
                Assert.That(petals, Is.Not.Null);
                Assert.That(TryFindIvyLeafStarts(ivy, out _, out int beforeLeaves), Is.True,
                    "The regression must positively recover leaf cards rather than infer them from stem spacing.");
                Assert.That(beforeLeaves, Is.EqualTo(128));
                int vertexBudget = growth.HeroVertexCount;

                ArchReferenceGrowthMassBreakupPass finalPass = host.AddComponent<ArchReferenceGrowthMassBreakupPass>();
                for (int frame = 0; frame < 28 && !finalPass.CompositionApplied; frame++) yield return null;

                Assert.That(finalPass.CompositionApplied, Is.True);
                Assert.That(growth.HeroIvyMesh, Is.SameAs(ivy));
                Assert.That(growth.HeroFlowerPetalMesh, Is.SameAs(petals));
                Assert.That(TryFindIvyLeafStarts(ivy, out _, out int afterLeaves), Is.True);
                Assert.That(afterLeaves, Is.EqualTo(128));

                Vector2 lowerMass = IvyZoneCentre(ivy, 0, 2);
                Vector2 upperMass = IvyZoneCentre(ivy, 3, 6);
                Vector2 crownMass = IvyZoneCentre(ivy, 7, 11);
                AssertEnvelope(lowerMass, -1.92f, -1.48f, 1.02f, 1.62f, "lower-pier foliage");
                AssertEnvelope(upperMass, -1.92f, -1.46f, 3.88f, 4.52f, "upper-pier foliage");
                AssertEnvelope(crownMass, -1.52f, -0.98f, 6.62f, 7.25f, "left-crown foliage");
                Assert.That(MaximumLeftClusterCentreX(ivy), Is.LessThan(-0.78f),
                    "No left foliage cluster may drift into the central arch opening.");
                Assert.That(IvyZoneVerticalGap(ivy, 0, 2, 3, 6), Is.GreaterThan(0.45f),
                    "The lower and upper pier masses need visible negative space.");
                Assert.That(IvyZoneVerticalGap(ivy, 3, 6, 7, 11), Is.GreaterThan(0.45f),
                    "The upper pier and crown masses need visible negative space.");

                Vector2 lowerSpan = IvyClusterCentreSpan(ivy, 0, 2);
                Vector2 upperSpan = IvyClusterCentreSpan(ivy, 3, 6);
                Vector2 crownSpan = IvyClusterCentreSpan(ivy, 7, 11);
                Assert.That(lowerSpan.y, Is.GreaterThan(0.90f),
                    "Lower foliage must grow vertically instead of collapsing into a shelf.");
                Assert.That(upperSpan.y, Is.GreaterThan(1.15f),
                    "Upper-pier foliage must have a tall irregular footprint instead of a shelf.");
                Assert.That(crownSpan.x, Is.GreaterThan(0.55f),
                    "Crown foliage must spread along the arch shoulder rather than disappear into one blob.");

                float averageLeafRadius = AverageLeftLeafRadius(ivy);
                float leafRadiusRange = LeftLeafRadiusRange(ivy);
                Assert.That(averageLeafRadius, Is.GreaterThan(0.125f).And.LessThan(0.185f),
                    "Individual ivy cards must stay readable and smaller than the rejected solid blobs.");
                Assert.That(leafRadiusRange, Is.GreaterThan(0.022f),
                    "Leaf scale variation is required so the mass edge does not become a repeated stamp.");
                Assert.That(AverageLeftLeafCentreZ(ivy), Is.LessThan(-0.17f).And.GreaterThan(-0.24f),
                    "Left foliage must remain layered just in front of the arch face.");

                Vector2 lowerBouquet = FlowerZoneCentre(petals, 0);
                Vector2 upperBouquet = FlowerZoneCentre(petals, 1);
                Vector2 crownBouquet = FlowerZoneCentre(petals, 2);
                AssertEnvelope(lowerBouquet, -1.86f, -1.30f, 1.62f, 2.22f, "lower bouquet");
                AssertEnvelope(upperBouquet, -1.86f, -1.28f, 4.42f, 5.02f, "upper bouquet");
                AssertEnvelope(crownBouquet, -1.52f, -0.88f, 6.58f, 7.24f, "crown bouquet");
                Assert.That(FlowerZoneHeadCentreSpan(petals, 0).magnitude, Is.GreaterThan(0.25f));
                Assert.That(FlowerZoneHeadCentreSpan(petals, 1).magnitude, Is.GreaterThan(0.25f));
                Assert.That(FlowerZoneHeadCentreSpan(petals, 2).x, Is.GreaterThan(0.30f));
                Assert.That(AverageFlowerHeadRadius(petals), Is.GreaterThan(0.085f).And.LessThan(0.130f));
                Assert.That(FlowerHeadRadiusRange(petals), Is.GreaterThan(0.020f),
                    "Bouquet heads need scale variation instead of repeating the same icon.");
                Assert.That(AveragePetalRoundness(petals), Is.GreaterThan(0.70f));
                Assert.That(AverageFlowerHeadDepth(petals), Is.GreaterThan(0.025f));
                Assert.That(growth.HeroDrawCallCount, Is.EqualTo(3));
                Assert.That(growth.HeroVertexCount, Is.EqualTo(vertexBudget).And.LessThanOrEqualTo(4096));

                Mesh firstIvy = ivy;
                Vector2 expectedLowerMass = lowerMass;
                Vector2 expectedUpperMass = upperMass;
                Vector2 expectedCrownMass = crownMass;
                Vector2 expectedLowerBouquet = lowerBouquet;
                Vector2 expectedUpperBouquet = upperBouquet;
                Vector2 expectedCrownBouquet = crownBouquet;
                float expectedLeafRadius = averageLeafRadius;
                float expectedFlowerRadius = AverageFlowerHeadRadius(petals);

                growth.enabled = false;
                yield return null;
                growth.enabled = true;
                for (int frame = 0; frame < 36; frame++)
                {
                    Mesh rebuilt = growth.HeroIvyMesh;
                    if (rebuilt != null && rebuilt != firstIvy && finalPass.CompositionApplied &&
                        TryFindIvyLeafStarts(rebuilt, out _, out int rebuiltCount) && rebuiltCount == 128 &&
                        MaximumLeftClusterCentreX(rebuilt) < -0.78f) break;
                    yield return null;
                }

                Assert.That(growth.HeroIvyMesh, Is.Not.Null.And.Not.SameAs(firstIvy));
                Assert.That(finalPass.CompositionApplied, Is.True);
                AssertVectorNear(IvyZoneCentre(growth.HeroIvyMesh, 0, 2), expectedLowerMass, 0.01f);
                AssertVectorNear(IvyZoneCentre(growth.HeroIvyMesh, 3, 6), expectedUpperMass, 0.01f);
                AssertVectorNear(IvyZoneCentre(growth.HeroIvyMesh, 7, 11), expectedCrownMass, 0.01f);
                AssertVectorNear(FlowerZoneCentre(growth.HeroFlowerPetalMesh, 0), expectedLowerBouquet, 0.01f);
                AssertVectorNear(FlowerZoneCentre(growth.HeroFlowerPetalMesh, 1), expectedUpperBouquet, 0.01f);
                AssertVectorNear(FlowerZoneCentre(growth.HeroFlowerPetalMesh, 2), expectedCrownBouquet, 0.01f);
                Assert.That(AverageLeftLeafRadius(growth.HeroIvyMesh), Is.EqualTo(expectedLeafRadius).Within(0.01f));
                Assert.That(AverageFlowerHeadRadius(growth.HeroFlowerPetalMesh), Is.EqualTo(expectedFlowerRadius).Within(0.01f));
                Assert.That(AverageFlowerHeadDepth(growth.HeroFlowerPetalMesh), Is.GreaterThan(0.025f));
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

        private static void AssertVectorNear(Vector2 actual, Vector2 expected, float tolerance)
        {
            Assert.That(Vector2.Distance(actual, expected), Is.LessThanOrEqualTo(tolerance));
        }

        private static void AssertEnvelope(Vector2 value, float minX, float maxX, float minY, float maxY, string label)
        {
            Assert.That(value.x, Is.InRange(minX, maxX), $"{label} must stay on the masonry-side x envelope.");
            Assert.That(value.y, Is.InRange(minY, maxY), $"{label} must stay in its authored support zone.");
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
                {
                    if (IsStemColor(colors[cursor + i])) { leafRun = false; break; }
                }
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

        private static Vector2 IvyZoneCentre(Mesh mesh, int firstCluster, int lastCluster)
        {
            if (!TryFindIvyLeafStarts(mesh, out int[,] starts, out _)) return new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector3[] vertices = mesh.vertices;
            Vector2 sum = Vector2.zero;
            int count = 0;
            for (int cluster = firstCluster; cluster <= lastCluster; cluster++)
            {
                Vector3 centre = MeasureIvyClusterCentre(vertices, starts, cluster);
                sum += new Vector2(centre.x, centre.y);
                count++;
            }
            return sum / Mathf.Max(1, count);
        }

        private static Vector2 IvyClusterCentreSpan(Mesh mesh, int firstCluster, int lastCluster)
        {
            if (!TryFindIvyLeafStarts(mesh, out int[,] starts, out _)) return Vector2.zero;
            Vector3[] vertices = mesh.vertices;
            Vector2 minimum = new(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new(float.NegativeInfinity, float.NegativeInfinity);
            for (int cluster = firstCluster; cluster <= lastCluster; cluster++)
            {
                Vector3 centre = MeasureIvyClusterCentre(vertices, starts, cluster);
                minimum.x = Mathf.Min(minimum.x, centre.x);
                minimum.y = Mathf.Min(minimum.y, centre.y);
                maximum.x = Mathf.Max(maximum.x, centre.x);
                maximum.y = Mathf.Max(maximum.y, centre.y);
            }
            return maximum - minimum;
        }

        private static float MaximumLeftClusterCentreX(Mesh mesh)
        {
            if (!TryFindIvyLeafStarts(mesh, out int[,] starts, out _)) return float.PositiveInfinity;
            Vector3[] vertices = mesh.vertices;
            float maximum = float.NegativeInfinity;
            for (int cluster = 0; cluster < LeftIvyClusterCount; cluster++)
                maximum = Mathf.Max(maximum, MeasureIvyClusterCentre(vertices, starts, cluster).x);
            return maximum;
        }

        private static float IvyZoneVerticalGap(Mesh mesh, int lowerFirst, int lowerLast, int upperFirst, int upperLast)
        {
            if (!TryFindIvyLeafStarts(mesh, out int[,] starts, out _)) return float.NegativeInfinity;
            Vector3[] vertices = mesh.vertices;
            MeasureIvyZoneY(vertices, starts, lowerFirst, lowerLast, out _, out float lowerMax);
            MeasureIvyZoneY(vertices, starts, upperFirst, upperLast, out float upperMin, out _);
            return upperMin - lowerMax;
        }

        private static void MeasureIvyZoneY(Vector3[] vertices, int[,] starts, int first, int last, out float minimum, out float maximum)
        {
            minimum = float.PositiveInfinity;
            maximum = float.NegativeInfinity;
            for (int cluster = first; cluster <= last; cluster++)
            for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
            for (int vertex = 0; vertex < IvyLeafVertexCount; vertex++)
            {
                float y = vertices[starts[cluster, leaf] + vertex].y;
                minimum = Mathf.Min(minimum, y);
                maximum = Mathf.Max(maximum, y);
            }
        }

        private static Vector3 MeasureIvyClusterCentre(Vector3[] vertices, int[,] starts, int cluster)
        {
            Vector3 centre = Vector3.zero;
            for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++) centre += vertices[starts[cluster, leaf]];
            return centre / IvyLeavesPerCluster;
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
                sum += LeafRadius(vertices, starts[cluster, leaf]);
                count++;
            }
            return sum / count;
        }

        private static float LeftLeafRadiusRange(Mesh mesh)
        {
            if (!TryFindIvyLeafStarts(mesh, out int[,] starts, out _)) return 0f;
            Vector3[] vertices = mesh.vertices;
            float minimum = float.PositiveInfinity;
            float maximum = float.NegativeInfinity;
            for (int cluster = 0; cluster < LeftIvyClusterCount; cluster++)
            for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
            {
                float radius = LeafRadius(vertices, starts[cluster, leaf]);
                minimum = Mathf.Min(minimum, radius);
                maximum = Mathf.Max(maximum, radius);
            }
            return maximum - minimum;
        }

        private static float LeafRadius(Vector3[] vertices, int start)
        {
            Vector3 centre = vertices[start];
            float radius = 0f;
            for (int vertex = 1; vertex < IvyLeafVertexCount; vertex++)
            {
                Vector3 delta = vertices[start + vertex] - centre;
                radius = Mathf.Max(radius, new Vector2(delta.x, delta.y).magnitude);
            }
            return radius;
        }

        private static float AverageLeftLeafCentreZ(Mesh mesh)
        {
            if (!TryFindIvyLeafStarts(mesh, out int[,] starts, out _)) return float.PositiveInfinity;
            Vector3[] vertices = mesh.vertices;
            float sum = 0f;
            int count = 0;
            for (int cluster = 0; cluster < LeftIvyClusterCount; cluster++)
            for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
            {
                sum += vertices[starts[cluster, leaf]].z;
                count++;
            }
            return sum / count;
        }

        private static Vector2 FlowerZoneCentre(Mesh mesh, int desiredZone)
        {
            if (mesh == null) return new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector3[] vertices = mesh.vertices;
            int headVertexCount = FlowerPetalsPerHead * FlowerPetalVertexCount;
            Vector2 sum = Vector2.zero;
            int count = 0;
            for (int cluster = 0; cluster < FlowerClusterCount; cluster++)
            {
                if (FlowerZone(cluster) != desiredZone) continue;
                for (int localHead = 0; localHead < FlowerHeadsPerCluster; localHead++)
                {
                    Vector3 centre = MeasureHeadCentre(vertices, (cluster * FlowerHeadsPerCluster + localHead) * headVertexCount);
                    sum += new Vector2(centre.x, centre.y);
                    count++;
                }
            }
            return sum / Mathf.Max(1, count);
        }

        private static Vector2 FlowerZoneHeadCentreSpan(Mesh mesh, int desiredZone)
        {
            if (mesh == null) return Vector2.zero;
            Vector3[] vertices = mesh.vertices;
            int headVertexCount = FlowerPetalsPerHead * FlowerPetalVertexCount;
            Vector2 minimum = new(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new(float.NegativeInfinity, float.NegativeInfinity);
            for (int cluster = 0; cluster < FlowerClusterCount; cluster++)
            {
                if (FlowerZone(cluster) != desiredZone) continue;
                for (int localHead = 0; localHead < FlowerHeadsPerCluster; localHead++)
                {
                    Vector3 centre = MeasureHeadCentre(vertices, (cluster * FlowerHeadsPerCluster + localHead) * headVertexCount);
                    minimum.x = Mathf.Min(minimum.x, centre.x);
                    minimum.y = Mathf.Min(minimum.y, centre.y);
                    maximum.x = Mathf.Max(maximum.x, centre.x);
                    maximum.y = Mathf.Max(maximum.y, centre.y);
                }
            }
            return maximum - minimum;
        }

        private static int FlowerZone(int cluster)
        {
            if (cluster == 9 || cluster <= 1) return 0;
            if (cluster <= 4) return 1;
            return 2;
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
            HeadRadiusBounds(mesh, out float minimum, out float maximum, out float average);
            return average;
        }

        private static float FlowerHeadRadiusRange(Mesh mesh)
        {
            HeadRadiusBounds(mesh, out float minimum, out float maximum, out _);
            return maximum - minimum;
        }

        private static void HeadRadiusBounds(Mesh mesh, out float minimum, out float maximum, out float average)
        {
            minimum = float.PositiveInfinity;
            maximum = float.NegativeInfinity;
            average = 0f;
            if (mesh == null) return;
            Vector3[] vertices = mesh.vertices;
            int headVertexCount = FlowerPetalsPerHead * FlowerPetalVertexCount;
            int headCount = FlowerClusterCount * FlowerHeadsPerCluster;
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
                minimum = Mathf.Min(minimum, radius);
                maximum = Mathf.Max(maximum, radius);
                average += radius;
            }
            average /= headCount;
        }

        private static float AveragePetalRoundness(Mesh mesh)
        {
            if (mesh == null) return 0f;
            Vector3[] vertices = mesh.vertices;
            int headCount = FlowerClusterCount * FlowerHeadsPerCluster;
            int headVertexCount = FlowerPetalsPerHead * FlowerPetalVertexCount;
            float sum = 0f;
            int count = 0;
            for (int head = 0; head < headCount; head++)
            for (int petal = 0; petal < FlowerPetalsPerHead; petal++)
            {
                int start = head * headVertexCount + petal * FlowerPetalVertexCount;
                Vector3 centre = vertices[start];
                float minimum = float.PositiveInfinity;
                float maximum = 0f;
                for (int rim = 1; rim < FlowerPetalVertexCount; rim++)
                {
                    Vector3 delta = vertices[start + rim] - centre;
                    float distance = new Vector2(delta.x, delta.y).magnitude;
                    minimum = Mathf.Min(minimum, distance);
                    maximum = Mathf.Max(maximum, distance);
                }
                if (maximum > 0.0001f) { sum += minimum / maximum; count++; }
            }
            return count == 0 ? 0f : sum / count;
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

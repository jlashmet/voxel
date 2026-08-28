using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Final one-shot composition correction for the ArchLookdev hero growth. It preserves the
    /// authored combined meshes while gathering the left ivy into discrete masonry-supported masses
    /// and rebuilding the existing flower topology into overlapping rounded rosette bouquets.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1300)]
    public sealed class ArchReferenceGrowthMassBreakupPass : MonoBehaviour
    {
        private const string ArchSceneName = "ArchLookdev";
        private const string HeroRootName = "Arch Reference Hero Growth";
        private const int IvyLeavesPerCluster = 8;
        private const int IvyLeafVertexCount = 17;
        private const int LeftIvyClusterCount = 12;
        private const int RightIvyClusterCount = 4;
        private const int TotalIvyClusterCount = LeftIvyClusterCount + RightIvyClusterCount;
        private const int FlowerClusterCount = 10;
        private const int FlowerHeadsPerCluster = 3;
        private const int FlowerPetalsPerHead = 5;
        private const int FlowerPetalVertexCount = 7;
        private const int FlowerCentreVertexCount = 9;

        private static readonly Color StemColor = new(0.07f, 0.24f, 0.04f, 1f);

        private Coroutine _applyRoutine;
        private Mesh _composedIvy;
        private Mesh _composedPetals;

        public bool CompositionApplied { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachStartupScene() => AttachToArchLookdev();

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == ArchSceneName) AttachToArchLookdev();
        }

        private static void AttachToArchLookdev()
        {
            ArchLookdev lookdev = Object.FindAnyObjectByType<ArchLookdev>();
            if (lookdev == null || lookdev.GetComponent<ArchReferenceGrowthMassBreakupPass>() != null) return;
            lookdev.gameObject.AddComponent<ArchReferenceGrowthMassBreakupPass>();
        }

        private void OnEnable() => ScheduleApply();

        private void OnDisable()
        {
            if (_applyRoutine != null) StopCoroutine(_applyRoutine);
            _applyRoutine = null;
            _composedIvy = null;
            _composedPetals = null;
            CompositionApplied = false;
        }

        private void OnTransformChildrenChanged()
        {
            if (isActiveAndEnabled) ScheduleApply();
        }

        private void ScheduleApply()
        {
            if (_applyRoutine != null) StopCoroutine(_applyRoutine);
            _applyRoutine = StartCoroutine(ApplyWhenEnglishPassIsReady());
        }

        private IEnumerator ApplyWhenEnglishPassIsReady()
        {
            CompositionApplied = false;
            for (int attempt = 0; attempt < 28; attempt++)
            {
                yield return null;

                ArchReferenceGrowth growth = GetComponent<ArchReferenceGrowth>();
                ArchReferenceGrowthEnglishIvyPass english = GetComponent<ArchReferenceGrowthEnglishIvyPass>();
                Mesh ivy = growth?.HeroIvyMesh;
                Mesh petals = growth?.HeroFlowerPetalMesh;
                if (growth == null || english == null || ivy == null || petals == null || !english.EnglishApplied)
                    continue;

                if (_composedIvy == ivy && _composedPetals == petals)
                {
                    CompositionApplied = true;
                    _applyRoutine = null;
                    yield break;
                }

                Transform heroRoot = FindDetachedHeroRoot();
                Mesh centres = FindChildMesh(heroRoot, "Flower Centres");
                if (heroRoot == null || centres == null) continue;

                BreakIvyIntoMasses(ivy);
                GatherRoundedBouquets(petals, centres);

                _composedIvy = ivy;
                _composedPetals = petals;
                CompositionApplied = true;
                _applyRoutine = null;
                yield break;
            }

            _applyRoutine = null;
        }

        private static void BreakIvyIntoMasses(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            if (vertices == null || vertices.Length == 0 || !TryFindIvyLeafStarts(mesh, out int[,] starts))
                return;

            // Keep the source's semantic vertical progression, but gather neighbouring authored
            // clusters far enough that the eye reads three masses with true masonry gaps, not a path.
            ContractIvyZone(vertices, starts, 0, 2, 0.18f, 0.72f, 0xA11u);
            ContractIvyZone(vertices, starts, 3, 6, 0.15f, 0.70f, 0xB22u);
            ContractIvyZone(vertices, starts, 7, 11, 0.14f, 0.68f, 0xC33u);

            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }

        private static void ContractIvyZone(
            Vector3[] vertices,
            int[,] starts,
            int firstCluster,
            int lastCluster,
            float clusterCompression,
            float leafCompression,
            uint seed)
        {
            int count = lastCluster - firstCluster + 1;
            var clusterCentres = new Vector3[count];
            Vector3 zoneCentre = Vector3.zero;
            for (int localCluster = 0; localCluster < count; localCluster++)
            {
                int cluster = firstCluster + localCluster;
                clusterCentres[localCluster] = MeasureIvyClusterCentre(vertices, starts, cluster);
                zoneCentre += clusterCentres[localCluster];
            }
            zoneCentre /= count;

            for (int localCluster = 0; localCluster < count; localCluster++)
            {
                int cluster = firstCluster + localCluster;
                Vector3 sourceCluster = clusterCentres[localCluster];
                Vector3 compressed = zoneCentre + (sourceCluster - zoneCentre) * clusterCompression;
                float angle = (localCluster * 137.50776f + SignedRandom(seed + 5u) * 18f) * Mathf.Deg2Rad;
                float ring = 0.035f + 0.015f * (localCluster % 3);
                Vector3 targetCluster = compressed + new Vector3(
                    Mathf.Cos(angle) * ring,
                    Mathf.Sin(angle) * ring,
                    SignedRandom(seed + (uint)(localCluster * 83 + 17)) * 0.020f);

                for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
                {
                    int start = starts[cluster, leaf];
                    Vector3 sourceLeaf = vertices[start];
                    Vector3 local = sourceLeaf - sourceCluster;
                    Vector3 targetLeaf = targetCluster + new Vector3(
                        local.x * leafCompression,
                        local.y * leafCompression,
                        local.z + SignedRandom(seed + (uint)(localCluster * 271 + leaf * 31 + 29)) * 0.018f);
                    TranslateRange(vertices, start, IvyLeafVertexCount, targetLeaf - sourceLeaf);
                }
            }
        }

        private static bool TryFindIvyLeafStarts(Mesh mesh, out int[,] starts)
        {
            starts = new int[TotalIvyClusterCount, IvyLeavesPerCluster];
            if (mesh == null) return false;
            Color[] colors = mesh.colors;
            int vertexCount = mesh.vertexCount;
            if (colors == null || colors.Length != vertexCount) return false;

            int cursor = 0;
            int found = 0;
            int expected = TotalIvyClusterCount * IvyLeavesPerCluster;
            while (cursor < vertexCount && found < expected)
            {
                while (cursor < vertexCount && IsStemColor(colors[cursor])) cursor++;
                if (cursor + IvyLeafVertexCount > vertexCount) return false;

                bool leafRun = true;
                for (int i = 0; i < IvyLeafVertexCount; i++)
                {
                    if (IsStemColor(colors[cursor + i]))
                    {
                        leafRun = false;
                        break;
                    }
                }
                if (!leafRun)
                {
                    cursor++;
                    continue;
                }

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

        private static Vector3 MeasureIvyClusterCentre(Vector3[] vertices, int[,] starts, int cluster)
        {
            Vector3 centre = Vector3.zero;
            for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
                centre += vertices[starts[cluster, leaf]];
            return centre / IvyLeavesPerCluster;
        }

        private static void GatherRoundedBouquets(Mesh petals, Mesh centres)
        {
            Vector3[] petalVertices = petals.vertices;
            Vector3[] petalNormals = petals.normals;
            Color[] petalColors = petals.colors;
            Vector3[] centreVertices = centres.vertices;
            Vector3[] centreNormals = centres.normals;
            Color[] centreColors = centres.colors;
            int headVertexCount = FlowerPetalsPerHead * FlowerPetalVertexCount;
            int expectedHeads = FlowerClusterCount * FlowerHeadsPerCluster;
            if (petalVertices == null || petalVertices.Length != expectedHeads * headVertexCount ||
                centreVertices == null || centreVertices.Length != expectedHeads * FlowerCentreVertexCount)
                return;

            var clusterCentres = new Vector3[FlowerClusterCount];
            var zoneCentres = new Vector3[3];
            var zoneCounts = new int[3];
            for (int cluster = 0; cluster < FlowerClusterCount; cluster++)
            {
                clusterCentres[cluster] = MeasureFlowerClusterCentre(petalVertices, cluster, headVertexCount);
                int zone = FlowerZone(cluster);
                zoneCentres[zone] += clusterCentres[cluster];
                zoneCounts[zone]++;
            }
            for (int zone = 0; zone < 3; zone++)
                zoneCentres[zone] /= Mathf.Max(1, zoneCounts[zone]);

            for (int cluster = 0; cluster < FlowerClusterCount; cluster++)
            {
                int zone = FlowerZone(cluster);
                int ordinal = FlowerZoneOrdinal(cluster);
                float clusterAngle = (ordinal * 137.50776f + zone * 29f) * Mathf.Deg2Rad;
                float clusterRing = 0.070f + 0.022f * ordinal;
                Vector3 targetCluster = zoneCentres[zone] + new Vector3(
                    Mathf.Cos(clusterAngle) * clusterRing,
                    Mathf.Sin(clusterAngle) * clusterRing,
                    -0.016f * ordinal);

                for (int localHead = 0; localHead < FlowerHeadsPerCluster; localHead++)
                {
                    int head = cluster * FlowerHeadsPerCluster + localHead;
                    Vector2 headOffset = localHead switch
                    {
                        0 => new Vector2(-0.060f, -0.035f),
                        1 => new Vector2( 0.060f, -0.025f),
                        _ => new Vector2( 0.000f,  0.065f),
                    };
                    float radius = Mathf.Lerp(0.155f, 0.185f, Random01((uint)(head * 593 + 43)));
                    Vector3 targetHead = targetCluster + new Vector3(
                        headOffset.x,
                        headOffset.y,
                        -0.013f * localHead);
                    Color blossom = BlossomColor(zone, ordinal, localHead);

                    RewriteRoundedRosette(
                        petalVertices, petalNormals, petalColors,
                        head * headVertexCount, head, targetHead, radius, blossom);
                    RewriteFlowerCentre(
                        centreVertices, centreNormals, centreColors,
                        head * FlowerCentreVertexCount, targetHead, radius * 0.22f);
                }
            }

            petals.vertices = petalVertices;
            if (petalNormals != null && petalNormals.Length == petalVertices.Length) petals.normals = petalNormals;
            if (petalColors != null && petalColors.Length == petalVertices.Length) petals.colors = petalColors;
            petals.RecalculateBounds();

            centres.vertices = centreVertices;
            if (centreNormals != null && centreNormals.Length == centreVertices.Length) centres.normals = centreNormals;
            if (centreColors != null && centreColors.Length == centreVertices.Length) centres.colors = centreColors;
            centres.RecalculateBounds();
        }

        private static void RewriteRoundedRosette(
            Vector3[] vertices,
            Vector3[] normals,
            Color[] colors,
            int headStart,
            int headIndex,
            Vector3 headCentre,
            float radius,
            Color blossom)
        {
            float rotation = SignedRandom((uint)(headIndex * 829 + 71)) * 16f * Mathf.Deg2Rad;
            for (int petal = 0; petal < FlowerPetalsPerHead; petal++)
            {
                int start = headStart + petal * FlowerPetalVertexCount;
                float angle = rotation + petal * (Mathf.PI * 2f / FlowerPetalsPerHead);
                Vector2 radial = new(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 tangent = new(-radial.y, radial.x);
                Vector3 lobeCentre = headCentre + new Vector3(
                    radial.x * radius * 0.22f,
                    radial.y * radius * 0.22f,
                    -0.018f + 0.004f * (petal & 1));

                vertices[start] = lobeCentre;
                if (normals != null && normals.Length == vertices.Length) normals[start] = Vector3.back;
                if (colors != null && colors.Length == vertices.Length) colors[start] = blossom;

                for (int rim = 0; rim < 6; rim++)
                {
                    float theta = rim * Mathf.PI * 2f / 6f;
                    float tangentAmount = Mathf.Cos(theta) * radius * 0.48f;
                    float radialAmount = Mathf.Sin(theta) * radius * 0.42f;
                    Vector2 offset = tangent * tangentAmount + radial * radialAmount;
                    float bowl = 0.010f + 0.006f * Mathf.Cos(theta);
                    vertices[start + 1 + rim] = new Vector3(
                        lobeCentre.x + offset.x,
                        lobeCentre.y + offset.y,
                        headCentre.z + bowl);
                    if (normals != null && normals.Length == vertices.Length)
                    {
                        normals[start + 1 + rim] = new Vector3(
                            offset.x * 0.55f,
                            offset.y * 0.55f,
                            -1f).normalized;
                    }
                    if (colors != null && colors.Length == vertices.Length)
                        colors[start + 1 + rim] = Color.Lerp(blossom, Color.white, 0.18f);
                }
            }
        }

        private static void RewriteFlowerCentre(
            Vector3[] vertices,
            Vector3[] normals,
            Color[] colors,
            int start,
            Vector3 headCentre,
            float radius)
        {
            if (start < 0 || start + FlowerCentreVertexCount > vertices.Length) return;
            vertices[start] = headCentre + new Vector3(0f, 0f, -0.026f);
            if (normals != null && normals.Length == vertices.Length) normals[start] = Vector3.back;
            if (colors != null && colors.Length == vertices.Length)
                colors[start] = new Color(0.95f, 0.58f, 0.12f, 1f);

            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 2f / 8f;
                vertices[start + 1 + i] = headCentre + new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    -0.020f);
                if (normals != null && normals.Length == vertices.Length) normals[start + 1 + i] = Vector3.back;
                if (colors != null && colors.Length == vertices.Length)
                    colors[start + 1 + i] = new Color(1.00f, 0.74f, 0.22f, 1f);
            }
        }

        private static Color BlossomColor(int zone, int ordinal, int localHead)
        {
            int variant = (zone * 5 + ordinal + localHead) % 4;
            return variant switch
            {
                0 => new Color(0.96f, 0.78f, 0.84f, 1f),
                1 => new Color(0.88f, 0.84f, 0.98f, 1f),
                2 => new Color(0.94f, 0.91f, 0.78f, 1f),
                _ => new Color(0.80f, 0.88f, 0.98f, 1f),
            };
        }

        private static int FlowerZone(int cluster)
        {
            if (cluster == 9 || cluster <= 1) return 0;
            if (cluster <= 4) return 1;
            return 2;
        }

        private static int FlowerZoneOrdinal(int cluster)
        {
            return cluster switch
            {
                9 => 0,
                0 => 1,
                1 => 2,
                2 => 0,
                3 => 1,
                4 => 2,
                5 => 0,
                6 => 1,
                7 => 2,
                _ => 3,
            };
        }

        private static Vector3 MeasureFlowerClusterCentre(Vector3[] vertices, int cluster, int headVertexCount)
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

        private static void TranslateRange(Vector3[] vertices, int start, int count, Vector3 delta)
        {
            for (int i = 0; i < count; i++) vertices[start + i] += delta;
        }

        private static Mesh FindChildMesh(Transform root, string name)
        {
            if (root == null) return null;
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            foreach (MeshFilter filter in filters)
                if (filter != null && filter.gameObject.name == name) return filter.sharedMesh;
            return null;
        }

        private static Transform FindDetachedHeroRoot()
        {
            Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
            foreach (Transform candidate in transforms)
            {
                if (candidate == null || candidate.name != HeroRootName) continue;
                GameObject value = candidate.gameObject;
                if (!value.activeInHierarchy || !value.scene.IsValid() || !value.scene.isLoaded) continue;
                return candidate;
            }
            return null;
        }

        private static float Random01(uint seed)
        {
            uint x = seed == 0u ? 0x9E3779B9u : seed;
            x ^= x >> 16;
            x *= 0x7FEB352Du;
            x ^= x >> 15;
            x *= 0x846CA68Bu;
            x ^= x >> 16;
            return (x & 0x00FFFFFFu) / 16777215f;
        }

        private static float SignedRandom(uint seed) => Random01(seed) * 2f - 1f;
    }
}

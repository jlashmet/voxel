using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Final one-shot composition correction for the ArchLookdev hero growth. The prior passes own
    /// topology and leaf/head construction; this pass keeps those same three meshes but moves the
    /// authored foliage into three stable masonry-supported zones and finishes rounded bouquets.
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
        private static readonly Vector2[] LeftMassAnchors =
        {
            new(-1.70f, 1.28f),
            new(-1.68f, 4.22f),
            new(-1.30f, 6.88f),
        };
        private static readonly Vector2[] BouquetAnchors =
        {
            new(-1.60f, 1.92f),
            new(-1.58f, 4.72f),
            new(-1.27f, 6.92f),
        };

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

                AnchorIvyToMasonry(ivy);
                GatherMasonryBouquets(petals, centres);
                TuneFinalMaterials(heroRoot);

                _composedIvy = ivy;
                _composedPetals = petals;
                CompositionApplied = true;
                _applyRoutine = null;
                yield break;
            }

            _applyRoutine = null;
        }

        private static void AnchorIvyToMasonry(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            if (vertices == null || vertices.Length == 0 || !TryFindIvyLeafStarts(mesh, out int[,] starts))
                return;

            RecomposeIvyZone(vertices, starts, 0, 0, 2, 0xA11u);
            RecomposeIvyZone(vertices, starts, 1, 3, 6, 0xB22u);
            RecomposeIvyZone(vertices, starts, 2, 7, 11, 0xC33u);

            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }

        private static void RecomposeIvyZone(
            Vector3[] vertices,
            int[,] starts,
            int zone,
            int firstCluster,
            int lastCluster,
            uint seed)
        {
            Vector2 anchor = LeftMassAnchors[zone];
            for (int cluster = firstCluster; cluster <= lastCluster; cluster++)
            {
                int ordinal = cluster - firstCluster;
                Vector2 clusterOffset = IvyClusterOffset(zone, ordinal);
                Vector3 clusterCentre = new(anchor.x + clusterOffset.x, anchor.y + clusterOffset.y, 0f);

                for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
                {
                    int start = starts[cluster, leaf];
                    Vector3 sourceCentre = vertices[start];
                    Vector2 layout = IvyLeafPacking(leaf);
                    float spreadX = zone == 2 ? 0.30f : 0.27f;
                    float spreadY = zone == 2 ? 0.24f : 0.30f;
                    Vector2 jitter = new(
                        SignedRandom(seed + (uint)(cluster * 109 + leaf * 31 + 7)) * 0.025f,
                        SignedRandom(seed + (uint)(cluster * 173 + leaf * 47 + 13)) * 0.025f);
                    float drape = IsDrape(cluster, leaf) ? (zone == 2 ? -0.18f : -0.24f) : 0f;
                    Vector3 targetCentre = new(
                        clusterCentre.x + layout.x * spreadX + jitter.x,
                        clusterCentre.y + layout.y * spreadY + jitter.y + drape,
                        sourceCentre.z + SignedRandom(seed + (uint)(cluster * 251 + leaf * 71 + 23)) * 0.018f);
                    TranslateRange(vertices, start, IvyLeafVertexCount, targetCentre - sourceCentre);
                }
            }
        }

        private static Vector2 IvyClusterOffset(int zone, int ordinal)
        {
            if (zone == 0)
            {
                return ordinal switch
                {
                    0 => new Vector2(-0.08f, -0.42f),
                    1 => new Vector2( 0.08f, -0.02f),
                    _ => new Vector2(-0.04f,  0.40f),
                };
            }
            if (zone == 1)
            {
                return ordinal switch
                {
                    0 => new Vector2( 0.02f, -0.48f),
                    1 => new Vector2(-0.10f, -0.16f),
                    2 => new Vector2( 0.08f,  0.17f),
                    _ => new Vector2(-0.03f,  0.48f),
                };
            }
            return ordinal switch
            {
                0 => new Vector2(-0.34f, -0.18f),
                1 => new Vector2(-0.18f,  0.02f),
                2 => new Vector2( 0.00f,  0.16f),
                3 => new Vector2( 0.17f,  0.23f),
                _ => new Vector2( 0.31f,  0.26f),
            };
        }

        private static Vector2 IvyLeafPacking(int leaf)
        {
            return leaf switch
            {
                0 => new Vector2(-0.72f, -0.38f),
                1 => new Vector2(-0.18f, -0.55f),
                2 => new Vector2( 0.42f, -0.38f),
                3 => new Vector2( 0.72f,  0.00f),
                4 => new Vector2( 0.30f,  0.42f),
                5 => new Vector2(-0.22f,  0.52f),
                6 => new Vector2(-0.68f,  0.30f),
                _ => new Vector2( 0.04f,  0.00f),
            };
        }

        private static bool IsDrape(int cluster, int leaf)
        {
            return leaf == 0 && (cluster == 1 || cluster == 4 || cluster == 7 || cluster == 9) ||
                   leaf == 1 && (cluster == 5 || cluster == 8);
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
                if (cursor + IvyLeafVertexCount > vertexCount) break;

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

        private static void GatherMasonryBouquets(Mesh petals, Mesh centres)
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

            var zoneDepth = new float[3];
            var zoneCount = new int[3];
            for (int cluster = 0; cluster < FlowerClusterCount; cluster++)
            {
                int zone = FlowerZone(cluster);
                zoneDepth[zone] += MeasureFlowerClusterCentre(petalVertices, cluster, headVertexCount).z;
                zoneCount[zone]++;
            }
            for (int zone = 0; zone < 3; zone++) zoneDepth[zone] /= Mathf.Max(1, zoneCount[zone]);

            for (int cluster = 0; cluster < FlowerClusterCount; cluster++)
            {
                int zone = FlowerZone(cluster);
                int ordinal = FlowerZoneOrdinal(cluster);
                Vector2 anchor = BouquetAnchors[zone];
                float angle = (ordinal * 137.50776f + zone * 31f) * Mathf.Deg2Rad;
                float ring = 0.10f + 0.025f * ordinal;
                Vector3 clusterCentre = new(
                    anchor.x + Mathf.Cos(angle) * ring,
                    anchor.y + Mathf.Sin(angle) * ring,
                    zoneDepth[zone] - 0.012f * ordinal);

                for (int localHead = 0; localHead < FlowerHeadsPerCluster; localHead++)
                {
                    int head = cluster * FlowerHeadsPerCluster + localHead;
                    Vector2 headOffset = localHead switch
                    {
                        0 => new Vector2(-0.075f, -0.040f),
                        1 => new Vector2( 0.075f, -0.030f),
                        _ => new Vector2( 0.000f,  0.075f),
                    };
                    float radius = Mathf.Lerp(0.132f, 0.152f, Random01((uint)(head * 593 + 43)));
                    Vector3 targetHead = clusterCentre + new Vector3(
                        headOffset.x,
                        headOffset.y,
                        -0.014f * localHead);
                    Color blossom = BlossomColor(zone, ordinal, localHead);

                    RewriteRoundedRosette(
                        petalVertices, petalNormals, petalColors,
                        head * headVertexCount, head, targetHead, radius, blossom);
                    RewriteFlowerCentre(
                        centreVertices, centreNormals, centreColors,
                        head * FlowerCentreVertexCount, targetHead, radius * 0.18f);
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
            float rotation = SignedRandom((uint)(headIndex * 829 + 71)) * 18f * Mathf.Deg2Rad;
            for (int petal = 0; petal < FlowerPetalsPerHead; petal++)
            {
                int start = headStart + petal * FlowerPetalVertexCount;
                float angle = rotation + petal * (Mathf.PI * 2f / FlowerPetalsPerHead);
                Vector2 radial = new(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 tangent = new(-radial.y, radial.x);
                Vector3 lobeCentre = headCentre + new Vector3(
                    radial.x * radius * 0.30f,
                    radial.y * radius * 0.30f,
                    -0.014f - 0.003f * (petal & 1));

                vertices[start] = lobeCentre + new Vector3(0f, 0f, -0.008f);
                if (normals != null && normals.Length == vertices.Length) normals[start] = Vector3.back;
                if (colors != null && colors.Length == vertices.Length) colors[start] = blossom;

                for (int rim = 0; rim < 6; rim++)
                {
                    float theta = rim * Mathf.PI * 2f / 6f;
                    float tangentAmount = Mathf.Cos(theta) * radius * 0.34f;
                    float radialAmount = Mathf.Sin(theta) * radius * 0.42f;
                    Vector2 offset = tangent * tangentAmount + radial * radialAmount;
                    float bowl = 0.006f + 0.006f * Mathf.Cos(theta);
                    vertices[start + 1 + rim] = new Vector3(
                        lobeCentre.x + offset.x,
                        lobeCentre.y + offset.y,
                        headCentre.z + bowl);
                    if (normals != null && normals.Length == vertices.Length)
                    {
                        normals[start + 1 + rim] = new Vector3(
                            offset.x * 0.50f,
                            offset.y * 0.50f,
                            -1f).normalized;
                    }
                    if (colors != null && colors.Length == vertices.Length)
                        colors[start + 1 + rim] = Color.Lerp(blossom, Color.white, 0.10f);
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
            Vector3 centre = headCentre + new Vector3(0f, 0f, -0.026f);
            vertices[start] = centre;
            if (normals != null && normals.Length == vertices.Length) normals[start] = Vector3.back;
            if (colors != null && colors.Length == vertices.Length)
                colors[start] = new Color(0.98f, 0.57f, 0.07f, 1f);

            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 2f / 8f;
                vertices[start + 1 + i] = centre + new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    0.007f);
                if (normals != null && normals.Length == vertices.Length) normals[start + 1 + i] = Vector3.back;
                if (colors != null && colors.Length == vertices.Length)
                    colors[start + 1 + i] = new Color(1.00f, 0.75f, 0.16f, 1f);
            }
        }

        private static Color BlossomColor(int zone, int ordinal, int localHead)
        {
            int variant = (zone * 5 + ordinal + localHead) % 4;
            return variant switch
            {
                0 => new Color(0.96f, 0.68f, 0.78f, 1f),
                1 => new Color(0.78f, 0.78f, 0.96f, 1f),
                2 => new Color(0.98f, 0.84f, 0.62f, 1f),
                _ => new Color(0.86f, 0.70f, 0.94f, 1f),
            };
        }

        private static void TuneFinalMaterials(Transform heroRoot)
        {
            MeshRenderer[] renderers = heroRoot.GetComponentsInChildren<MeshRenderer>(true);
            foreach (MeshRenderer renderer in renderers)
            {
                Material material = renderer?.sharedMaterial;
                if (material == null) continue;
                switch (renderer.gameObject.name)
                {
                    case "Lobed Ivy":
                        material.SetColor("_BaseColor", new Color(0.20f, 0.43f, 0.07f, 1f));
                        material.SetColor("_TipColor", new Color(0.58f, 0.72f, 0.18f, 1f));
                        break;
                    case "Flower Petals":
                        material.SetColor("_BaseColor", new Color(0.96f, 0.82f, 0.88f, 1f));
                        material.SetColor("_TipColor", new Color(1.00f, 0.93f, 0.88f, 1f));
                        break;
                    case "Flower Centres":
                        material.SetColor("_BaseColor", new Color(0.98f, 0.56f, 0.05f, 1f));
                        material.SetColor("_TipColor", new Color(1.00f, 0.80f, 0.13f, 1f));
                        break;
                }
            }
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

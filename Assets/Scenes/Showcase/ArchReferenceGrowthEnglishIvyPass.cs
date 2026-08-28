using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Final one-shot composition pass for the ArchLookdev hero growth. The earlier passes establish
    /// the bounded authored topology and world-space lifecycle; this pass reshapes those same meshes
    /// into dense masonry-supported English-ivy masses and small layered flower bouquets.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1200)]
    public sealed class ArchReferenceGrowthEnglishIvyPass : MonoBehaviour
    {
        private const string ArchSceneName = "ArchLookdev";
        private const string HeroRootName = "Arch Reference Hero Growth";
        private const int IvyLeavesPerCluster = 8;
        private const int IvyLeafVertexCount = 17;
        private const int IvyStemVertexCount = 4;
        private const int LeftIvyClusterCount = 12;
        private const int RightIvyClusterCount = 4;
        private const int FlowerClusterCount = 10;
        private const int FlowerHeadsPerCluster = 3;
        private const int FlowerPetalsPerHead = 5;
        private const int FlowerPetalVertexCount = 7;
        private const int FlowerCentreVertexCount = 9;
        private const float EnglishOutlineRadius = 0.52f;

        private static readonly Color StemColor = new(0.07f, 0.24f, 0.04f, 1f);

        // Three broad lobes with rounded shoulders. The source topology has sixteen rim vertices;
        // keeping the notches shallow avoids the maple/star silhouette exposed by experiment 012.
        private static readonly Vector2[] EnglishOutline =
        {
            new( 0.00f, -0.48f),
            new(-0.15f, -0.38f),
            new(-0.31f, -0.30f),
            new(-0.45f, -0.14f),
            new(-0.52f,  0.08f),
            new(-0.40f,  0.14f),
            new(-0.28f,  0.27f),
            new(-0.13f,  0.25f),
            new( 0.00f,  0.52f),
            new( 0.13f,  0.25f),
            new( 0.28f,  0.27f),
            new( 0.40f,  0.14f),
            new( 0.52f,  0.08f),
            new( 0.45f, -0.14f),
            new( 0.31f, -0.30f),
            new( 0.15f, -0.38f),
        };

        private Coroutine _applyRoutine;
        private Mesh _refinedIvy;
        private Mesh _refinedPetals;

        public bool EnglishApplied { get; private set; }

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
            if (lookdev == null || lookdev.GetComponent<ArchReferenceGrowthEnglishIvyPass>() != null) return;
            lookdev.gameObject.AddComponent<ArchReferenceGrowthEnglishIvyPass>();
        }

        private void OnEnable() => ScheduleApply();

        private void OnDisable()
        {
            if (_applyRoutine != null) StopCoroutine(_applyRoutine);
            _applyRoutine = null;
            _refinedIvy = null;
            _refinedPetals = null;
            EnglishApplied = false;
        }

        private void OnTransformChildrenChanged()
        {
            if (isActiveAndEnabled) ScheduleApply();
        }

        private void ScheduleApply()
        {
            if (_applyRoutine != null) StopCoroutine(_applyRoutine);
            _applyRoutine = StartCoroutine(ApplyWhenLushPassIsReady());
        }

        private IEnumerator ApplyWhenLushPassIsReady()
        {
            EnglishApplied = false;
            for (int attempt = 0; attempt < 24; attempt++)
            {
                yield return null;

                ArchReferenceGrowth growth = GetComponent<ArchReferenceGrowth>();
                ArchReferenceGrowthLushPass lush = GetComponent<ArchReferenceGrowthLushPass>();
                Mesh ivy = growth?.HeroIvyMesh;
                Mesh petals = growth?.HeroFlowerPetalMesh;
                if (growth == null || lush == null || ivy == null || petals == null || !lush.LushApplied)
                    continue;

                if (_refinedIvy == ivy && _refinedPetals == petals)
                {
                    EnglishApplied = true;
                    _applyRoutine = null;
                    yield break;
                }

                Transform heroRoot = FindDetachedHeroRoot();
                Mesh centres = FindChildMesh(heroRoot, "Flower Centres");
                if (heroRoot == null || centres == null) continue;

                RefineIvy(ivy);
                RefineFlowers(petals, centres);
                TuneMaterials(heroRoot);

                _refinedIvy = ivy;
                _refinedPetals = petals;
                EnglishApplied = true;
                _applyRoutine = null;
                yield break;
            }

            _applyRoutine = null;
        }

        private static void RefineIvy(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Color[] colors = mesh.colors;
            if (vertices == null || normals == null || colors == null ||
                vertices.Length != normals.Length || vertices.Length != colors.Length) return;

            int cursor = 0;
            int leafIndex = 0;
            cursor = RefinePath(vertices, normals, colors, cursor, LeftIvyClusterCount, true, ref leafIndex);
            RefinePath(vertices, normals, colors, cursor, RightIvyClusterCount, false, ref leafIndex);

            // Topology indices are the primary invariant. The color sweep is deliberately redundant:
            // it protects the visual result if an upstream authored stem is inserted or omitted while
            // retaining the same stem material/color contract.
            CollapseStemColoredQuads(vertices, colors);

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.colors = colors;
            mesh.RecalculateBounds();
        }

        private static int RefinePath(
            Vector3[] vertices,
            Vector3[] normals,
            Color[] colors,
            int cursor,
            int clusterCount,
            bool leftPath,
            ref int leafIndex)
        {
            for (int cluster = 0; cluster < clusterCount; cluster++)
            {
                int pathStem = -1;
                if (cluster > 0)
                {
                    pathStem = cursor;
                    cursor += IvyStemVertexCount;
                }

                var starts = new int[IvyLeavesPerCluster];
                var leafStems = new int[IvyLeavesPerCluster];
                Vector3 sourceCentre = Vector3.zero;
                for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
                {
                    if (cursor + IvyLeafVertexCount > vertices.Length) return vertices.Length;
                    starts[leaf] = cursor;
                    leafStems[leaf] = -1;
                    sourceCentre += vertices[cursor];
                    cursor += IvyLeafVertexCount;
                    if ((leaf & 1) == 0)
                    {
                        leafStems[leaf] = cursor;
                        cursor += IvyStemVertexCount;
                    }
                }
                sourceCentre /= IvyLeavesPerCluster;

                bool crown = leftPath && cluster >= 7;
                Vector3 massCentre = sourceCentre + (leftPath ? LeftMassShift(cluster) : RightMassShift(cluster));
                float spread = leftPath ? (crown ? 0.52f : 0.48f) : 0.33f;

                if (pathStem >= 0)
                    CollapseQuad(vertices, pathStem, QuadCentre(vertices, pathStem));

                for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
                {
                    Vector2 layout = LeafMassLayout(leaf, crown, leftPath);
                    Vector2 jitter = new(
                        SignedRandom((uint)(leafIndex * 991 + 83)) * (leftPath ? 0.035f : 0.018f),
                        SignedRandom((uint)(leafIndex * 577 + 131)) * (leftPath ? 0.035f : 0.018f));
                    float depth = leftPath
                        ? Mathf.Lerp(-0.095f, 0.035f, Random01((uint)(leafIndex * 863 + 73)))
                        : Mathf.Lerp(-0.025f, 0.012f, Random01((uint)(leafIndex * 863 + 73)));
                    Vector3 targetCentre = massCentre + new Vector3(
                        layout.x * spread + jitter.x,
                        layout.y * spread + jitter.y,
                        depth);

                    bool drape = leftPath && IsDrapeLeaf(cluster, leaf);
                    if (drape)
                        targetCentre += new Vector3(-0.025f, crown ? -0.32f : -0.42f, -0.025f);

                    float baseRadius = leftPath ? (crown ? 0.215f : 0.230f) : 0.123f;
                    float targetRadius = baseRadius * Mathf.Lerp(0.88f, 1.13f,
                        Random01((uint)(leafIndex * 499 + 181)));
                    if (drape) targetRadius *= 0.86f;
                    targetRadius = leftPath
                        ? Mathf.Clamp(targetRadius, 0.175f, 0.280f)
                        : Mathf.Clamp(targetRadius, 0.095f, 0.155f);

                    RewriteEnglishLeaf(vertices, normals, colors, starts[leaf], leafIndex,
                        targetCentre, targetRadius);

                    if (leafStems[leaf] >= 0)
                        CollapseQuad(vertices, leafStems[leaf], QuadCentre(vertices, leafStems[leaf]));
                    leafIndex++;
                }
            }

            return cursor;
        }

        private static Vector3 LeftMassShift(int cluster)
        {
            return cluster switch
            {
                0 => new Vector3(-0.30f,  0.10f, -0.010f),
                1 => new Vector3(-0.46f, -0.06f,  0.014f),
                2 => new Vector3(-0.32f, -0.18f, -0.018f),
                3 => new Vector3(-0.50f, -0.27f,  0.012f),
                4 => new Vector3(-0.34f,  0.06f, -0.016f),
                5 => new Vector3(-0.48f, -0.08f,  0.016f),
                6 => new Vector3(-0.28f, -0.17f, -0.020f),
                7 => new Vector3(-0.23f,  0.10f,  0.014f),
                8 => new Vector3(-0.10f,  0.17f, -0.020f),
                9 => new Vector3( 0.01f,  0.21f,  0.014f),
                10 => new Vector3(0.10f,  0.18f, -0.018f),
                _ => new Vector3( 0.18f,  0.10f,  0.012f),
            };
        }

        private static Vector3 RightMassShift(int cluster)
        {
            return cluster switch
            {
                0 => new Vector3( 0.04f, -0.02f,  0.004f),
                1 => new Vector3( 0.08f,  0.02f, -0.008f),
                2 => new Vector3(-0.02f,  0.08f,  0.008f),
                _ => new Vector3( 0.04f,  0.12f, -0.008f),
            };
        }

        private static Vector2 LeafMassLayout(int leaf, bool crown, bool leftPath)
        {
            if (!leftPath)
            {
                return leaf switch
                {
                    0 => new Vector2(-0.44f, -0.30f),
                    1 => new Vector2( 0.10f, -0.32f),
                    2 => new Vector2( 0.43f, -0.08f),
                    3 => new Vector2(-0.25f, -0.02f),
                    4 => new Vector2( 0.20f,  0.13f),
                    5 => new Vector2(-0.38f,  0.30f),
                    6 => new Vector2( 0.02f,  0.35f),
                    _ => new Vector2( 0.38f,  0.38f),
                };
            }

            if (crown)
            {
                return leaf switch
                {
                    0 => new Vector2(-0.62f, -0.17f),
                    1 => new Vector2(-0.23f, -0.30f),
                    2 => new Vector2( 0.20f, -0.25f),
                    3 => new Vector2( 0.58f, -0.12f),
                    4 => new Vector2(-0.48f,  0.20f),
                    5 => new Vector2(-0.09f,  0.16f),
                    6 => new Vector2( 0.29f,  0.22f),
                    _ => new Vector2( 0.61f,  0.29f),
                };
            }

            return leaf switch
            {
                0 => new Vector2(-0.50f, -0.27f),
                1 => new Vector2( 0.00f, -0.32f),
                2 => new Vector2( 0.43f, -0.18f),
                3 => new Vector2(-0.31f,  0.00f),
                4 => new Vector2( 0.18f,  0.05f),
                5 => new Vector2(-0.47f,  0.29f),
                6 => new Vector2( 0.02f,  0.31f),
                _ => new Vector2( 0.43f,  0.33f),
            };
        }

        private static bool IsDrapeLeaf(int cluster, int leaf)
        {
            return (leaf == 0 && (cluster == 1 || cluster == 4 || cluster == 7 || cluster == 9)) ||
                   (leaf == 1 && (cluster == 3 || cluster == 6 || cluster == 8)) ||
                   (leaf == 2 && (cluster == 2 || cluster == 5));
        }

        private static void RewriteEnglishLeaf(
            Vector3[] vertices,
            Vector3[] normals,
            Color[] colors,
            int start,
            int leafIndex,
            Vector3 targetCentre,
            float radius)
        {
            if (start < 0 || start + IvyLeafVertexCount > vertices.Length) return;

            float rotation = SignedRandom((uint)(leafIndex * 733 + 43)) * 22f * Mathf.Deg2Rad;
            float ca = Mathf.Cos(rotation);
            float sa = Mathf.Sin(rotation);
            float widthScale = Mathf.Lerp(0.90f, 1.12f, Random01((uint)(leafIndex * 307 + 71)));
            float heightScale = Mathf.Lerp(0.96f, 1.13f, Random01((uint)(leafIndex * 521 + 109)));
            Color baseColor = Color.Lerp(
                new Color(0.11f, 0.30f, 0.035f, 1f),
                new Color(0.48f, 0.67f, 0.13f, 1f),
                Random01((uint)(leafIndex * 421 + 193)));

            Vector3 centre = targetCentre + new Vector3(0f, 0f, -0.018f);
            vertices[start] = centre;
            normals[start] = Vector3.back;
            colors[start] = Color.Lerp(baseColor, new Color(0.72f, 0.83f, 0.28f, 1f), 0.16f);

            for (int i = 0; i < EnglishOutline.Length; i++)
            {
                Vector2 p = EnglishOutline[i] / EnglishOutlineRadius;
                float x = p.x * radius * widthScale;
                float y = p.y * radius * heightScale;
                float rx = x * ca - y * sa;
                float ry = x * sa + y * ca;
                float edgeDepth = 0.020f + 0.012f * Mathf.Abs(p.x) +
                    0.006f * Mathf.Max(0f, p.y);
                vertices[start + 1 + i] = centre + new Vector3(rx, ry, edgeDepth);
                normals[start + 1 + i] = new Vector3(-rx * 0.82f, -ry * 0.82f, -1f).normalized;
                float shade = Mathf.Lerp(0.07f, 0.23f, Mathf.Abs(p.x));
                colors[start + 1 + i] = Color.Lerp(baseColor,
                    new Color(0.055f, 0.20f, 0.018f, 1f), shade);
            }
        }

        private static void CollapseStemColoredQuads(Vector3[] vertices, Color[] colors)
        {
            for (int i = 0; i + 3 < vertices.Length; i++)
            {
                if (!IsStemColor(colors[i]) || !IsStemColor(colors[i + 1]) ||
                    !IsStemColor(colors[i + 2]) || !IsStemColor(colors[i + 3]))
                    continue;

                CollapseQuad(vertices, i, QuadCentre(vertices, i));
                i += 3;
            }
        }

        private static bool IsStemColor(Color color)
        {
            const float tolerance = 0.006f;
            return Mathf.Abs(color.r - StemColor.r) < tolerance &&
                   Mathf.Abs(color.g - StemColor.g) < tolerance &&
                   Mathf.Abs(color.b - StemColor.b) < tolerance;
        }

        private static Vector3 QuadCentre(Vector3[] vertices, int start)
        {
            if (start < 0 || start + 3 >= vertices.Length) return Vector3.zero;
            return (vertices[start] + vertices[start + 1] + vertices[start + 2] + vertices[start + 3]) * 0.25f;
        }

        private static void CollapseQuad(Vector3[] vertices, int start, Vector3 point)
        {
            if (start < 0 || start + 3 >= vertices.Length) return;
            for (int i = 0; i < 4; i++) vertices[start + i] = point;
        }

        private static void RefineFlowers(Mesh petals, Mesh centres)
        {
            Vector3[] petalVertices = petals.vertices;
            Vector3[] petalNormals = petals.normals;
            Color[] petalColors = petals.colors;
            Vector3[] centreVertices = centres.vertices;
            Vector3[] centreNormals = centres.normals;
            Color[] centreColors = centres.colors;
            int headVertexCount = FlowerPetalsPerHead * FlowerPetalVertexCount;
            int expectedHeads = FlowerClusterCount * FlowerHeadsPerCluster;
            if (petalVertices.Length != expectedHeads * headVertexCount ||
                centreVertices.Length != expectedHeads * FlowerCentreVertexCount) return;

            for (int cluster = 0; cluster < FlowerClusterCount; cluster++)
            {
                var oldHeads = new Vector3[FlowerHeadsPerCluster];
                Vector3 sourceCentre = Vector3.zero;
                for (int localHead = 0; localHead < FlowerHeadsPerCluster; localHead++)
                {
                    int head = cluster * FlowerHeadsPerCluster + localHead;
                    oldHeads[localHead] = MeasureHeadCentre(petalVertices, head * headVertexCount);
                    sourceCentre += oldHeads[localHead];
                }
                sourceCentre /= FlowerHeadsPerCluster;
                Vector3 bouquetCentre = sourceCentre + FlowerMassShift(cluster);

                for (int localHead = 0; localHead < FlowerHeadsPerCluster; localHead++)
                {
                    int head = cluster * FlowerHeadsPerCluster + localHead;
                    Vector2 local = localHead switch
                    {
                        0 => new Vector2(-0.105f, -0.040f),
                        1 => new Vector2( 0.105f, -0.025f),
                        _ => new Vector2( 0.000f,  0.105f),
                    };
                    Vector3 newHead = bouquetCentre + new Vector3(local.x, local.y, -0.014f * localHead);
                    float radius = FlowerHeadRadius(cluster, head);
                    Color blossom = BouquetColor(cluster, localHead);
                    RewriteRoundedFlowerHead(petalVertices, petalNormals, petalColors,
                        head * headVertexCount, head, newHead, radius, blossom);
                    RewriteFlowerCentre(centreVertices, centreNormals, centreColors,
                        head * FlowerCentreVertexCount, newHead, radius * 0.19f);
                }
            }

            petals.vertices = petalVertices;
            petals.normals = petalNormals;
            petals.colors = petalColors;
            petals.RecalculateBounds();
            centres.vertices = centreVertices;
            centres.normals = centreNormals;
            centres.colors = centreColors;
            centres.RecalculateBounds();
        }

        private static float FlowerHeadRadius(int cluster, int head)
        {
            float baseRadius = cluster >= 6 ? 0.150f : 0.140f;
            return baseRadius * Mathf.Lerp(0.92f, 1.10f, Random01((uint)(head * 811 + 37)));
        }

        private static Vector3 FlowerMassShift(int cluster)
        {
            if (cluster <= 2) return new Vector3(-0.20f, -0.02f, -0.105f);
            if (cluster <= 5) return new Vector3(-0.14f,  0.07f, -0.112f);
            if (cluster <= 8) return new Vector3(-0.01f,  0.10f, -0.116f);
            return new Vector3(-0.15f, 0.04f, -0.100f);
        }

        private static Vector3 MeasureHeadCentre(Vector3[] vertices, int headStart)
        {
            Vector3 centre = Vector3.zero;
            for (int petal = 0; petal < FlowerPetalsPerHead; petal++)
                centre += vertices[headStart + petal * FlowerPetalVertexCount];
            return centre / FlowerPetalsPerHead;
        }

        private static void RewriteRoundedFlowerHead(
            Vector3[] vertices,
            Vector3[] normals,
            Color[] colors,
            int headStart,
            int headIndex,
            Vector3 headCentre,
            float headRadius,
            Color blossom)
        {
            float headRotation = SignedRandom((uint)(headIndex * 947 + 211)) * 34f;
            for (int petal = 0; petal < FlowerPetalsPerHead; petal++)
            {
                int start = headStart + petal * FlowerPetalVertexCount;
                float angle = (headRotation + petal * (360f / FlowerPetalsPerHead)) * Mathf.Deg2Rad;
                Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 side = new(-direction.y, direction.x);
                float length = headRadius * Mathf.Lerp(0.90f, 1.03f,
                    Random01((uint)(headIndex * 379 + petal * 67 + 17)));
                float width = headRadius * Mathf.Lerp(0.54f, 0.64f,
                    Random01((uint)(headIndex * 431 + petal * 83 + 29)));
                Vector3 petalCentre = headCentre + new Vector3(
                    direction.x * headRadius * 0.25f,
                    direction.y * headRadius * 0.25f,
                    -0.010f - 0.002f * petal);

                Vector2[] outline =
                {
                    direction * (-0.38f * length),
                    direction * (-0.10f * length) + side * (0.45f * width),
                    direction * ( 0.28f * length) + side * (0.52f * width),
                    direction * ( 0.56f * length),
                    direction * ( 0.28f * length) - side * (0.52f * width),
                    direction * (-0.10f * length) - side * (0.45f * width),
                };

                vertices[start] = petalCentre + new Vector3(0f, 0f, -0.012f);
                normals[start] = Vector3.back;
                colors[start] = Color.Lerp(blossom, Color.white, 0.16f);
                for (int i = 0; i < outline.Length; i++)
                {
                    Vector2 offset = outline[i];
                    float depth = i == 3 ? 0.012f : (i == 0 ? 0.004f : 0.008f);
                    vertices[start + 1 + i] = petalCentre + new Vector3(offset.x, offset.y, depth);
                    normals[start + 1 + i] = new Vector3(-offset.x * 0.75f, -offset.y * 0.75f, -1f).normalized;
                    float edgeLight = i == 3 ? 0.14f : 0.05f;
                    colors[start + 1 + i] = Color.Lerp(blossom, Color.white, edgeLight);
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
            normals[start] = Vector3.back;
            colors[start] = new Color(1.00f, 0.73f, 0.10f, 1f);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * (Mathf.PI * 2f / 8f);
                vertices[start + 1 + i] = centre + new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    0.007f);
                normals[start + 1 + i] = new Vector3(0f, 0f, -1f);
                colors[start + 1 + i] = new Color(0.96f, 0.48f, 0.035f, 1f);
            }
        }

        private static Color BouquetColor(int cluster, int localHead)
        {
            int palette = (cluster + localHead) % 4;
            return palette switch
            {
                0 => new Color(0.98f, 0.83f, 0.84f, 1f),
                1 => new Color(0.96f, 0.92f, 0.72f, 1f),
                2 => new Color(0.80f, 0.82f, 0.96f, 1f),
                _ => new Color(0.91f, 0.78f, 0.94f, 1f),
            };
        }

        private static Mesh FindChildMesh(Transform root, string name)
        {
            if (root == null) return null;
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            foreach (MeshFilter filter in filters)
                if (filter != null && filter.gameObject.name == name) return filter.sharedMesh;
            return null;
        }

        private static void TuneMaterials(Transform heroRoot)
        {
            MeshRenderer[] renderers = heroRoot.GetComponentsInChildren<MeshRenderer>(true);
            foreach (MeshRenderer renderer in renderers)
            {
                Material material = renderer?.sharedMaterial;
                if (material == null) continue;
                switch (renderer.gameObject.name)
                {
                    case "Lobed Ivy":
                        material.SetColor("_BaseColor", new Color(0.22f, 0.47f, 0.075f, 1f));
                        material.SetColor("_TipColor", new Color(0.64f, 0.77f, 0.22f, 1f));
                        break;
                    case "Flower Petals":
                        material.SetColor("_BaseColor", Color.white);
                        material.SetColor("_TipColor", new Color(1.00f, 0.98f, 0.94f, 1f));
                        break;
                    case "Flower Centres":
                        material.SetColor("_BaseColor", new Color(0.98f, 0.57f, 0.055f, 1f));
                        material.SetColor("_TipColor", new Color(1.00f, 0.83f, 0.16f, 1f));
                        break;
                }
            }
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

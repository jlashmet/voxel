using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Final one-shot presentation pass for the ArchLookdev reference foliage. It consumes the
    /// already-bounded hero meshes and fixes the two defects exposed by the saved player frame:
    /// legacy stem quads surviving as long diagonals, and overly compressed leaf/head placement
    /// reading as a few dark stamps instead of a continuous, layered ivy mass.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1600)]
    public sealed class ArchReferenceGrowthAaaPass : MonoBehaviour
    {
        private const string ArchSceneName = "ArchLookdev";
        private const string HeroRootName = "Arch Reference Hero Growth";
        private const int IvyClusterCount = 16;
        private const int IvyLeavesPerCluster = 8;
        private const int IvyLeafVertexCount = 17;
        private const int FlowerHeads = 30;
        private const int FlowerPetalsPerHead = 5;
        private const int FlowerPetalVertexCount = 7;
        private const int FlowerHeadVertexCount = FlowerPetalsPerHead * FlowerPetalVertexCount;
        private const int FlowerCentreVertexCount = 9;

        private static readonly Color StemColor = new(0.07f, 0.24f, 0.04f, 1f);

        private static readonly Vector2[] Supports =
        {
            new(-1.62f, 0.78f),
            new(-1.70f, 1.46f),
            new(-1.56f, 2.14f),
            new(-1.68f, 2.82f),
            new(-1.54f, 3.52f),
            new(-1.67f, 4.22f),
            new(-1.52f, 4.92f),
            new(-1.61f, 5.60f),
            new(-1.53f, 6.25f),
            new(-1.44f, 6.84f),
            new(-1.28f, 7.34f),
            new(-1.00f, 7.73f),
            new(-0.61f, 7.98f),
            new(-0.16f, 8.08f),
            new( 0.30f, 8.01f),
            new( 1.60f, 4.82f),
        };

        private static readonly Vector2[] BouquetAnchors =
        {
            new(-1.55f, 1.58f),
            new(-1.61f, 3.20f),
            new(-1.54f, 4.84f),
            new(-1.47f, 6.34f),
            new(-1.13f, 7.43f),
            new(-0.46f, 7.98f),
        };

        // Broad English-ivy silhouette: five readable lobes with shallow shoulders rather than a
        // radial/star cutout. Sixteen perimeter vertices preserve the existing mesh topology.
        private static readonly Vector2[] IvyOutline =
        {
            new( 0.00f, -0.70f),
            new(-0.22f, -0.48f),
            new(-0.48f, -0.36f),
            new(-0.70f, -0.14f),
            new(-0.56f,  0.04f),
            new(-0.66f,  0.27f),
            new(-0.39f,  0.30f),
            new(-0.20f,  0.47f),
            new( 0.00f,  0.80f),
            new( 0.20f,  0.47f),
            new( 0.39f,  0.30f),
            new( 0.66f,  0.27f),
            new( 0.56f,  0.04f),
            new( 0.70f, -0.14f),
            new( 0.48f, -0.36f),
            new( 0.22f, -0.48f),
        };

        private static readonly Color[] LeafPalette =
        {
            new(0.22f, 0.43f, 0.055f, 1f),
            new(0.30f, 0.50f, 0.070f, 1f),
            new(0.39f, 0.57f, 0.095f, 1f),
            new(0.47f, 0.62f, 0.125f, 1f),
            new(0.33f, 0.52f, 0.120f, 1f),
            new(0.25f, 0.47f, 0.090f, 1f),
        };

        private static readonly Color[] BlossomPalette =
        {
            new(0.98f, 0.80f, 0.84f, 1f),
            new(0.95f, 0.70f, 0.78f, 1f),
            new(0.88f, 0.78f, 0.94f, 1f),
            new(0.99f, 0.88f, 0.80f, 1f),
            new(0.98f, 0.90f, 0.92f, 1f),
            new(0.91f, 0.76f, 0.88f, 1f),
        };

        private Coroutine _applyRoutine;
        private Mesh _composedIvy;
        private Mesh _composedPetals;

        public bool AaaCompositionApplied { get; private set; }

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
            if (lookdev == null || lookdev.GetComponent<ArchReferenceGrowthAaaPass>() != null) return;
            lookdev.gameObject.AddComponent<ArchReferenceGrowthAaaPass>();
        }

        private void OnEnable() => ScheduleApply();

        private void OnDisable()
        {
            if (_applyRoutine != null) StopCoroutine(_applyRoutine);
            _applyRoutine = null;
            _composedIvy = null;
            _composedPetals = null;
            AaaCompositionApplied = false;
        }

        private void OnTransformChildrenChanged()
        {
            if (isActiveAndEnabled) ScheduleApply();
        }

        private void ScheduleApply()
        {
            if (_applyRoutine != null) StopCoroutine(_applyRoutine);
            AaaCompositionApplied = false;
            _applyRoutine = StartCoroutine(ApplyWhenArchitecturalPassIsReady());
        }

        private IEnumerator ApplyWhenArchitecturalPassIsReady()
        {
            for (int attempt = 0; attempt < 48; attempt++)
            {
                yield return null;
                ArchReferenceGrowth growth = GetComponent<ArchReferenceGrowth>();
                ArchReferenceGrowthArchitecturalPass architectural = GetComponent<ArchReferenceGrowthArchitecturalPass>();
                Mesh ivy = growth?.HeroIvyMesh;
                Mesh petals = growth?.HeroFlowerPetalMesh;
                if (growth == null || architectural == null || !architectural.ArchitecturalCompositionApplied || ivy == null || petals == null)
                    continue;

                if (_composedIvy == ivy && _composedPetals == petals)
                {
                    AaaCompositionApplied = true;
                    _applyRoutine = null;
                    yield break;
                }

                Transform heroRoot = FindDetachedHeroRoot();
                Mesh centres = FindChildMesh(heroRoot, "Flower Centres");
                if (heroRoot == null || centres == null) continue;

                RecomposeIvy(ivy);
                RecomposeFlowers(petals, centres);
                TuneMaterials(heroRoot);

                _composedIvy = ivy;
                _composedPetals = petals;
                AaaCompositionApplied = true;
                _applyRoutine = null;
                yield break;
            }
            _applyRoutine = null;
        }

        private static void RecomposeIvy(Mesh mesh)
        {
            if (!TryFindIvyLeafStarts(mesh, out int[,] starts)) return;
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Color[] colors = mesh.colors;
            if (vertices == null || colors == null || colors.Length != vertices.Length) return;

            CollapseAllStemQuads(vertices, normals, colors);

            for (int cluster = 0; cluster < IvyClusterCount; cluster++)
            {
                Vector2 support = Supports[cluster];
                bool crown = cluster >= 9 && cluster <= 14;
                bool sparseRight = cluster == 15;
                for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
                {
                    uint seed = (uint)(0xB741 + cluster * 1009 + leaf * 191);
                    float golden = (leaf * 137.50776f + cluster * 29f + SignedRandom(seed) * 13f) * Mathf.Deg2Rad;
                    float normalized = (leaf + 0.65f) / IvyLeavesPerCluster;
                    float ring = Mathf.Lerp(0.10f, sparseRight ? 0.22f : crown ? 0.34f : 0.36f, Mathf.Sqrt(normalized));
                    float xStretch = sparseRight ? 0.78f : crown ? 1.18f : 0.92f;
                    float yStretch = sparseRight ? 0.92f : crown ? 0.68f : 1.05f;
                    Vector2 offset = new(
                        Mathf.Cos(golden) * ring * xStretch,
                        Mathf.Sin(golden) * ring * yStretch);
                    if (!sparseRight && leaf == 0 && (cluster == 1 || cluster == 4 || cluster == 7 || cluster == 9 || cluster == 12))
                    {
                        offset.x *= 0.45f;
                        offset.y -= crown ? 0.20f : 0.27f;
                    }

                    float scale = sparseRight
                        ? Mathf.Lerp(0.125f, 0.155f, Random01(seed ^ 0x9E3779B9u))
                        : Mathf.Lerp(0.145f, crown ? 0.195f : 0.205f, Random01(seed ^ 0x9E3779B9u));
                    float z = -0.195f - Random01(seed ^ 0xC2B2AE35u) * 0.095f;
                    float rotation = (SignedRandom(seed ^ 0x85EBCA6Bu) * 31f) + (crown ? -12f : 3f);
                    RewriteLeaf(vertices, normals, colors, starts[cluster, leaf],
                        new Vector3(support.x + offset.x, support.y + offset.y, z), scale, rotation, seed, crown);
                }
            }

            mesh.vertices = vertices;
            if (normals != null && normals.Length == vertices.Length) mesh.normals = normals;
            mesh.colors = colors;
            mesh.RecalculateBounds();
        }

        private static void CollapseAllStemQuads(Vector3[] vertices, Vector3[] normals, Color[] colors)
        {
            int cursor = 0;
            while (cursor < colors.Length)
            {
                if (!IsStemColor(colors[cursor]))
                {
                    cursor++;
                    continue;
                }

                int runStart = cursor;
                while (cursor < colors.Length && IsStemColor(colors[cursor])) cursor++;
                int runLength = cursor - runStart;
                for (int local = 0; local + 3 < runLength; local += 4)
                {
                    int start = runStart + local;
                    Vector3 centre = (vertices[start] + vertices[start + 1] + vertices[start + 2] + vertices[start + 3]) * 0.25f;
                    for (int i = 0; i < 4; i++)
                    {
                        vertices[start + i] = centre;
                        if (normals != null && normals.Length == vertices.Length) normals[start + i] = Vector3.back;
                    }
                }
            }
        }

        private static void RewriteLeaf(
            Vector3[] vertices, Vector3[] normals, Color[] colors, int start,
            Vector3 centre, float scale, float rotationDegrees, uint seed, bool crown)
        {
            if (start < 0 || start + IvyLeafVertexCount > vertices.Length) return;
            float angle = rotationDegrees * Mathf.Deg2Rad;
            float ca = Mathf.Cos(angle);
            float sa = Mathf.Sin(angle);
            float aspect = Mathf.Lerp(0.90f, 1.08f, Random01(seed ^ 0x7FEB352Du));
            Color leaf = LeafPalette[(int)(seed % (uint)LeafPalette.Length)];
            if (crown) leaf = Color.Lerp(leaf, new Color(0.49f, 0.63f, 0.13f, 1f), 0.12f);
            Color edge = Color.Lerp(leaf, new Color(0.69f, 0.76f, 0.22f, 1f), 0.28f);

            vertices[start] = centre + new Vector3(0f, 0f, -0.012f);
            if (normals != null && normals.Length == vertices.Length) normals[start] = Vector3.back;
            colors[start] = Color.Lerp(leaf, Color.black, 0.04f);
            for (int i = 0; i < IvyOutline.Length; i++)
            {
                Vector2 p = IvyOutline[i];
                float px = p.x * scale * aspect;
                float py = p.y * scale;
                float x = px * ca - py * sa;
                float y = px * sa + py * ca;
                float bowl = 0.006f + Mathf.Abs(p.y) * 0.011f;
                vertices[start + 1 + i] = centre + new Vector3(x, y, bowl);
                if (normals != null && normals.Length == vertices.Length)
                    normals[start + 1 + i] = new Vector3(x * 0.60f, y * 0.60f, -1f).normalized;
                colors[start + 1 + i] = Color.Lerp(leaf, edge, 0.30f + Mathf.Abs(p.y) * 0.45f);
            }
        }

        private static void RecomposeFlowers(Mesh petals, Mesh centres)
        {
            Vector3[] petalVertices = petals.vertices;
            Vector3[] petalNormals = petals.normals;
            Color[] petalColors = petals.colors;
            Vector3[] centreVertices = centres.vertices;
            Vector3[] centreNormals = centres.normals;
            Color[] centreColors = centres.colors;
            if (petalVertices == null || petalVertices.Length != FlowerHeads * FlowerHeadVertexCount ||
                centreVertices == null || centreVertices.Length != FlowerHeads * FlowerCentreVertexCount)
                return;

            for (int head = 0; head < FlowerHeads; head++)
            {
                int bouquet = head / 5;
                int ordinal = head % 5;
                Vector2 anchor = BouquetAnchors[bouquet];
                uint seed = (uint)(0xD31 + head * 977 + bouquet * 131);
                float angle = (ordinal * 137.50776f + bouquet * 17f + SignedRandom(seed) * 7f) * Mathf.Deg2Rad;
                float ring = ordinal == 0 ? 0.025f : Mathf.Lerp(0.13f, 0.25f, (ordinal - 1) / 3f);
                float xStretch = bouquet >= 4 ? 1.15f : 0.92f;
                float yStretch = bouquet >= 4 ? 0.76f : 1.02f;
                Vector3 target = new(
                    anchor.x + Mathf.Cos(angle) * ring * xStretch,
                    anchor.y + Mathf.Sin(angle) * ring * yStretch,
                    -0.295f - Random01(seed ^ 0xA341316Cu) * 0.075f);
                float radius = Mathf.Lerp(0.135f, 0.175f, Random01(seed ^ 0xC8013EA4u));
                Color blossom = BlossomPalette[(head + bouquet * 2) % BlossomPalette.Length];
                RewriteFlowerHead(petalVertices, petalNormals, petalColors, head, target, radius, seed, blossom);
                RewriteFlowerCentre(centreVertices, centreNormals, centreColors, head, target, radius * 0.12f);
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

        private static void RewriteFlowerHead(
            Vector3[] vertices, Vector3[] normals, Color[] colors, int head,
            Vector3 centre, float radius, uint seed, Color blossom)
        {
            int headStart = head * FlowerHeadVertexCount;
            float rotation = SignedRandom(seed ^ 0x517CC1B7u) * 24f * Mathf.Deg2Rad;
            for (int petal = 0; petal < FlowerPetalsPerHead; petal++)
            {
                int start = headStart + petal * FlowerPetalVertexCount;
                float angle = rotation + petal * Mathf.PI * 2f / FlowerPetalsPerHead;
                Vector2 radial = new(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 tangent = new(-radial.y, radial.x);
                Vector3 petalCentre = centre + new Vector3(radial.x * radius * 0.28f, radial.y * radius * 0.28f, -0.008f * (petal & 1));
                vertices[start] = petalCentre + new Vector3(0f, 0f, -0.006f);
                if (normals != null && normals.Length == vertices.Length) normals[start] = Vector3.back;
                if (colors != null && colors.Length == vertices.Length) colors[start] = blossom;
                for (int rim = 0; rim < 6; rim++)
                {
                    float theta = rim * Mathf.PI * 2f / 6f;
                    Vector2 offset = tangent * (Mathf.Cos(theta) * radius * 0.28f) +
                                     radial * (Mathf.Sin(theta) * radius * 0.55f);
                    vertices[start + 1 + rim] = new Vector3(
                        petalCentre.x + offset.x,
                        petalCentre.y + offset.y,
                        centre.z + 0.008f + 0.006f * Mathf.Cos(theta));
                    if (normals != null && normals.Length == vertices.Length)
                        normals[start + 1 + rim] = new Vector3(offset.x * 0.45f, offset.y * 0.45f, -1f).normalized;
                    if (colors != null && colors.Length == vertices.Length)
                        colors[start + 1 + rim] = Color.Lerp(blossom, Color.white, 0.13f + 0.05f * (rim % 3));
                }
            }
        }

        private static void RewriteFlowerCentre(
            Vector3[] vertices, Vector3[] normals, Color[] colors, int head, Vector3 target, float radius)
        {
            int start = head * FlowerCentreVertexCount;
            Vector3 centre = target + new Vector3(0f, 0f, -0.025f);
            Color core = new(0.82f, 0.58f, 0.15f, 1f);
            vertices[start] = centre;
            if (normals != null && normals.Length == vertices.Length) normals[start] = Vector3.back;
            if (colors != null && colors.Length == vertices.Length) colors[start] = core;
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 2f / 8f;
                vertices[start + 1 + i] = centre + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0.006f);
                if (normals != null && normals.Length == vertices.Length) normals[start + 1 + i] = Vector3.back;
                if (colors != null && colors.Length == vertices.Length) colors[start + 1 + i] = new Color(0.95f, 0.77f, 0.28f, 1f);
            }
        }

        private static void TuneMaterials(Transform heroRoot)
        {
            foreach (MeshRenderer renderer in heroRoot.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material material = renderer?.sharedMaterial;
                if (material == null) continue;
                switch (renderer.gameObject.name)
                {
                    case "Lobed Ivy":
                        material.SetColor("_BaseColor", new Color(0.46f, 0.62f, 0.12f, 1f));
                        material.SetColor("_TipColor", new Color(0.79f, 0.86f, 0.31f, 1f));
                        break;
                    case "Flower Petals":
                        material.SetColor("_BaseColor", new Color(0.96f, 0.80f, 0.86f, 1f));
                        material.SetColor("_TipColor", new Color(1.00f, 0.94f, 0.92f, 1f));
                        break;
                    case "Flower Centres":
                        material.SetColor("_BaseColor", new Color(0.84f, 0.60f, 0.17f, 1f));
                        material.SetColor("_TipColor", new Color(0.96f, 0.80f, 0.30f, 1f));
                        break;
                }
            }
        }

        public static Vector2 Support(int cluster) => Supports[Mathf.Clamp(cluster, 0, Supports.Length - 1)];
        public static Vector2 BouquetAnchor(int bouquet) => BouquetAnchors[Mathf.Clamp(bouquet, 0, BouquetAnchors.Length - 1)];

        private static bool TryFindIvyLeafStarts(Mesh mesh, out int[,] starts)
        {
            starts = new int[IvyClusterCount, IvyLeavesPerCluster];
            if (mesh == null) return false;
            Color[] colors = mesh.colors;
            int vertexCount = mesh.vertexCount;
            if (colors == null || colors.Length != vertexCount) return false;
            int cursor = 0;
            int found = 0;
            int expected = IvyClusterCount * IvyLeavesPerCluster;
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

        private static Mesh FindChildMesh(Transform root, string name)
        {
            if (root == null) return null;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                if (filter != null && filter.gameObject.name == name) return filter.sharedMesh;
            return null;
        }

        private static Transform FindDetachedHeroRoot()
        {
            foreach (Transform candidate in Resources.FindObjectsOfTypeAll<Transform>())
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

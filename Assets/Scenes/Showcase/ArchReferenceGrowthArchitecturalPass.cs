using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Reprojects the bounded ArchLookdev hero growth onto the actual hero-arch frame. Earlier
    /// passes establish leaf/blossom topology and readability; this final one-shot pass restores
    /// the architectural relationship that the mass-compression experiments lost: lower/upper
    /// left-pier growth, a dense haunch-to-crown arc, and only one sparse right-side cluster.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1500)]
    public sealed class ArchReferenceGrowthArchitecturalPass : MonoBehaviour
    {
        private const string ArchSceneName = "ArchLookdev";
        private const string HeroRootName = "Arch Reference Hero Growth";
        private const int IvyLeavesPerCluster = 8;
        private const int IvyLeafVertexCount = 17;
        private const int IvyClusterCount = 16;
        private const int FlowerHeads = 30;
        private const int FlowerPetalsPerHead = 5;
        private const int FlowerPetalVertexCount = 7;
        private const int FlowerCentreVertexCount = 9;
        private const int FlowerHeadVertexCount = FlowerPetalsPerHead * FlowerPetalVertexCount;

        // ArchLookdev's canonical hero preset is expressed in 0.1 m voxels: clearSpan=28,
        // pierHeight=64, ringThickness=7. Keep these as semantic frame dimensions rather than
        // captured screen coordinates.
        public const float ClearHalfSpan = 1.40f;
        public const float SpringlineY = 6.40f;
        public const float RingThickness = 0.70f;
        public const float OpeningCrownY = SpringlineY + ClearHalfSpan;

        private static readonly Color StemColor = new(0.07f, 0.24f, 0.04f, 1f);
        private static readonly Color[] LeafPalette =
        {
            new(0.17f, 0.37f, 0.045f, 1f),
            new(0.23f, 0.44f, 0.055f, 1f),
            new(0.31f, 0.50f, 0.075f, 1f),
            new(0.38f, 0.55f, 0.095f, 1f),
            new(0.27f, 0.47f, 0.115f, 1f),
        };
        private static readonly Color[] BlossomPalette =
        {
            new(0.96f, 0.63f, 0.72f, 1f),
            new(0.82f, 0.70f, 0.93f, 1f),
            new(0.98f, 0.80f, 0.66f, 1f),
            new(0.90f, 0.66f, 0.86f, 1f),
            new(0.98f, 0.76f, 0.82f, 1f),
            new(0.99f, 0.88f, 0.79f, 1f),
        };

        private Coroutine _applyRoutine;
        private Mesh _composedIvy;
        private Mesh _composedPetals;

        public bool ArchitecturalCompositionApplied { get; private set; }

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
            if (lookdev == null || lookdev.GetComponent<ArchReferenceGrowthArchitecturalPass>() != null) return;
            lookdev.gameObject.AddComponent<ArchReferenceGrowthArchitecturalPass>();
        }

        private void OnEnable() => ScheduleApply();

        private void OnDisable()
        {
            if (_applyRoutine != null) StopCoroutine(_applyRoutine);
            _applyRoutine = null;
            _composedIvy = null;
            _composedPetals = null;
            ArchitecturalCompositionApplied = false;
        }

        private void OnTransformChildrenChanged()
        {
            if (isActiveAndEnabled) ScheduleApply();
        }

        private void ScheduleApply()
        {
            if (_applyRoutine != null) StopCoroutine(_applyRoutine);
            ArchitecturalCompositionApplied = false;
            _applyRoutine = StartCoroutine(ApplyWhenReadableGrowthIsReady());
        }

        private IEnumerator ApplyWhenReadableGrowthIsReady()
        {
            for (int attempt = 0; attempt < 40; attempt++)
            {
                yield return null;

                ArchReferenceGrowth growth = GetComponent<ArchReferenceGrowth>();
                ArchReferenceGrowthReadabilityPass readability = GetComponent<ArchReferenceGrowthReadabilityPass>();
                Mesh ivy = growth?.HeroIvyMesh;
                Mesh petals = growth?.HeroFlowerPetalMesh;
                if (growth == null || readability == null || !readability.ReadabilityApplied || ivy == null || petals == null)
                    continue;

                if (_composedIvy == ivy && _composedPetals == petals)
                {
                    ArchitecturalCompositionApplied = true;
                    _applyRoutine = null;
                    yield break;
                }

                Transform heroRoot = FindDetachedHeroRoot();
                Mesh centres = FindChildMesh(heroRoot, "Flower Centres");
                if (heroRoot == null || centres == null) continue;

                RecomposeIvyOnArch(ivy);
                RecomposeBouquetsOnArch(petals, centres);
                TuneMaterials(heroRoot);

                _composedIvy = ivy;
                _composedPetals = petals;
                ArchitecturalCompositionApplied = true;
                _applyRoutine = null;
                yield break;
            }

            _applyRoutine = null;
        }

        private static void RecomposeIvyOnArch(Mesh mesh)
        {
            if (mesh == null || !TryFindIvyLeafStarts(mesh, out int[,] starts)) return;
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Color[] colors = mesh.colors;
            if (vertices == null || colors == null || colors.Length != vertices.Length) return;

            for (int cluster = 0; cluster < IvyClusterCount; cluster++)
            {
                Vector2 support = ClusterSupport(cluster);
                bool crown = cluster >= 7 && cluster <= 14;
                uint clusterSeed = (uint)(0xA531 + cluster * 1013);
                WriteLocalVine(vertices, normals, colors, starts[cluster, 0] - 4, support, cluster, crown);

                for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
                {
                    int start = starts[cluster, leaf];
                    uint seed = clusterSeed + (uint)(leaf * 197 + 29);
                    float angle = (leaf * 137.50776f + cluster * 23f + SignedRandom(seed) * 16f) * Mathf.Deg2Rad;
                    float radial = Mathf.Lerp(crown ? 0.11f : 0.12f, crown ? 0.25f : 0.27f,
                        Random01(seed ^ 0x68E31DA4u));
                    float xScale = crown ? 1.12f : 0.92f;
                    float yScale = crown ? 0.76f : 1.06f;
                    Vector2 local = new(
                        Mathf.Cos(angle) * radial * xScale,
                        Mathf.Sin(angle) * radial * yScale);
                    if (IsDrape(cluster, leaf))
                    {
                        local.x *= 0.55f;
                        local.y -= crown ? 0.18f : 0.25f;
                    }

                    float z = -0.185f - Random01(seed ^ 0xC2B2AE35u) * 0.080f;
                    Vector3 targetCentre = new(support.x + local.x, support.y + local.y, z);
                    float targetRadius = Mathf.Lerp(0.135f, crown ? 0.185f : 0.195f,
                        Random01(seed ^ 0x9E3779B9u));
                    RepositionLeaf(vertices, normals, colors, start, targetCentre, targetRadius, seed, crown);
                }
            }

            mesh.vertices = vertices;
            if (normals != null && normals.Length == vertices.Length) mesh.normals = normals;
            mesh.colors = colors;
            mesh.RecalculateBounds();
        }

        public static Vector2 ClusterSupport(int cluster)
        {
            if (cluster <= 2)
            {
                return cluster switch
                {
                    0 => new Vector2(-1.66f, 1.05f),
                    1 => new Vector2(-1.52f, 1.72f),
                    _ => new Vector2(-1.68f, 2.38f),
                };
            }

            if (cluster <= 6)
            {
                return cluster switch
                {
                    3 => new Vector2(-1.66f, 3.35f),
                    4 => new Vector2(-1.53f, 4.08f),
                    5 => new Vector2(-1.68f, 4.82f),
                    _ => new Vector2(-1.50f, 5.56f),
                };
            }

            if (cluster <= 14)
            {
                int ordinal = cluster - 7;
                float t = ordinal / 7f;
                float degrees = Mathf.Lerp(170f, 82f, t);
                float radians = degrees * Mathf.Deg2Rad;
                float radius = ClearHalfSpan + RingThickness * 0.38f;
                return new Vector2(
                    Mathf.Cos(radians) * radius,
                    SpringlineY + Mathf.Sin(radians) * radius);
            }

            // One deliberately sparse right-side accent. The other three former right clusters are
            // reallocated to the crown so the composition matches the reference's left-heavy mass.
            return new Vector2(ClearHalfSpan + RingThickness * 0.34f, SpringlineY - 1.85f);
        }

        private static bool IsDrape(int cluster, int leaf)
        {
            return leaf == 0 && (cluster == 1 || cluster == 4 || cluster == 7 || cluster == 10 || cluster == 13) ||
                   leaf == 1 && (cluster == 5 || cluster == 9 || cluster == 12);
        }

        private static void RepositionLeaf(
            Vector3[] vertices,
            Vector3[] normals,
            Color[] colors,
            int start,
            Vector3 targetCentre,
            float targetRadius,
            uint seed,
            bool crown)
        {
            if (start < 0 || start + IvyLeafVertexCount > vertices.Length) return;
            Vector3 oldCentre = vertices[start];
            float currentRadius = 0f;
            for (int i = 1; i < IvyLeafVertexCount; i++)
                currentRadius = Mathf.Max(currentRadius, Vector2.Distance(oldCentre, vertices[start + i]));
            float scale = currentRadius > 0.0001f ? targetRadius / currentRadius : 1f;
            Color baseColor = LeafPalette[(int)(seed % (uint)LeafPalette.Length)];
            if (crown) baseColor = Color.Lerp(baseColor, new Color(0.43f, 0.58f, 0.10f, 1f), 0.16f);
            Color rimColor = Color.Lerp(baseColor, new Color(0.67f, 0.74f, 0.17f, 1f), 0.34f);

            vertices[start] = targetCentre + new Vector3(0f, 0f, -0.010f);
            if (normals != null && normals.Length == vertices.Length) normals[start] = Vector3.back;
            colors[start] = Color.Lerp(baseColor, Color.black, 0.05f);

            for (int i = 1; i < IvyLeafVertexCount; i++)
            {
                Vector3 offset = vertices[start + i] - oldCentre;
                vertices[start + i] = targetCentre + new Vector3(
                    offset.x * scale,
                    offset.y * scale,
                    offset.z * 0.75f);
                if (normals != null && normals.Length == vertices.Length)
                {
                    Vector3 p = vertices[start + i] - targetCentre;
                    normals[start + i] = new Vector3(p.x * 0.55f, p.y * 0.55f, -1f).normalized;
                }
                float edge = i / (float)(IvyLeafVertexCount - 1);
                colors[start + i] = Color.Lerp(baseColor, rimColor, 0.24f + edge * 0.55f);
            }
        }

        private static void WriteLocalVine(
            Vector3[] vertices,
            Vector3[] normals,
            Color[] colors,
            int start,
            Vector2 support,
            int cluster,
            bool crown)
        {
            if (start < 0 || start + 4 > vertices.Length) return;
            for (int i = 0; i < 4; i++) if (!IsStemColor(colors[start + i])) return;

            uint seed = (uint)(cluster * 811 + 73);
            float angle = crown
                ? Mathf.Lerp(18f, -18f, Random01(seed)) * Mathf.Deg2Rad
                : (90f + SignedRandom(seed) * 12f) * Mathf.Deg2Rad;
            Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
            float length = Mathf.Lerp(crown ? 0.32f : 0.36f, crown ? 0.52f : 0.58f,
                Random01(seed ^ 0x9E3779B9u));
            float halfWidth = 0.011f;
            Vector2 perpendicular = new(-direction.y, direction.x);
            Vector2 a = support - direction * (length * 0.48f);
            Vector2 b = support + direction * (length * 0.52f);
            float z = -0.175f;

            vertices[start + 0] = new Vector3(a.x + perpendicular.x * halfWidth, a.y + perpendicular.y * halfWidth, z);
            vertices[start + 1] = new Vector3(a.x - perpendicular.x * halfWidth, a.y - perpendicular.y * halfWidth, z);
            vertices[start + 2] = new Vector3(b.x + perpendicular.x * halfWidth, b.y + perpendicular.y * halfWidth, z);
            vertices[start + 3] = new Vector3(b.x - perpendicular.x * halfWidth, b.y - perpendicular.y * halfWidth, z);
            for (int i = 0; i < 4; i++)
            {
                colors[start + i] = StemColor;
                if (normals != null && normals.Length == vertices.Length) normals[start + i] = Vector3.back;
            }
        }

        private static void RecomposeBouquetsOnArch(Mesh petals, Mesh centres)
        {
            if (petals == null || centres == null) return;
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
                int bouquet = BouquetForHead(head, out int ordinal, out int count);
                Vector2 anchor = BouquetAnchor(bouquet);
                uint seed = (uint)(head * 977 + bouquet * 131 + 0x55D);
                float angle = (ordinal * 137.50776f + bouquet * 19f + SignedRandom(seed) * 8f) * Mathf.Deg2Rad;
                float normalized = count <= 1 ? 0f : ordinal / (float)(count - 1);
                float ring = Mathf.Lerp(0.025f, bouquet >= 2 ? 0.26f : 0.23f, Mathf.Sqrt(normalized));
                float xScale = bouquet >= 2 ? 1.20f : 0.90f;
                float yScale = bouquet >= 2 ? 0.72f : 1.05f;
                Vector3 target = new(
                    anchor.x + Mathf.Cos(angle) * ring * xScale,
                    anchor.y + Mathf.Sin(angle) * ring * yScale,
                    -0.225f - Random01(seed ^ 0xA341316Cu) * 0.065f);
                float targetRadius = Mathf.Lerp(0.155f, 0.215f, Random01(seed ^ 0xC8013EA4u));
                Color blossom = BlossomPalette[(head * 5 + bouquet * 3) % BlossomPalette.Length];
                RepositionFlowerHead(petalVertices, petalNormals, petalColors, head, target, targetRadius, blossom);
                RewriteFlowerCentre(centreVertices, centreNormals, centreColors, head, target, targetRadius * 0.09f);
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

        private static int BouquetForHead(int head, out int ordinal, out int count)
        {
            if (head < 6) { ordinal = head; count = 6; return 0; }
            if (head < 14) { ordinal = head - 6; count = 8; return 1; }
            if (head < 22) { ordinal = head - 14; count = 8; return 2; }
            ordinal = head - 22; count = 8; return 3;
        }

        public static Vector2 BouquetAnchor(int bouquet)
        {
            return bouquet switch
            {
                0 => new Vector2(-1.54f, 1.78f),
                1 => new Vector2(-1.53f, 4.72f),
                2 => new Vector2(-1.38f, 6.92f),
                _ => new Vector2(-0.58f, 7.84f),
            };
        }

        private static void RepositionFlowerHead(
            Vector3[] vertices,
            Vector3[] normals,
            Color[] colors,
            int head,
            Vector3 target,
            float targetRadius,
            Color blossom)
        {
            int start = head * FlowerHeadVertexCount;
            Vector3 oldCentre = MeasureHeadCentre(vertices, head);
            float currentRadius = 0f;
            for (int i = 0; i < FlowerHeadVertexCount; i++)
                currentRadius = Mathf.Max(currentRadius, Vector2.Distance(oldCentre, vertices[start + i]));
            float scale = currentRadius > 0.0001f ? targetRadius / currentRadius : 1f;

            for (int i = 0; i < FlowerHeadVertexCount; i++)
            {
                Vector3 offset = vertices[start + i] - oldCentre;
                vertices[start + i] = target + new Vector3(offset.x * scale, offset.y * scale, offset.z * 0.78f);
                if (normals != null && normals.Length == vertices.Length)
                {
                    Vector3 p = vertices[start + i] - target;
                    normals[start + i] = new Vector3(p.x * 0.38f, p.y * 0.38f, -1f).normalized;
                }
                if (colors != null && colors.Length == vertices.Length)
                {
                    int local = i % FlowerPetalVertexCount;
                    colors[start + i] = Color.Lerp(blossom, Color.white, local == 0 ? 0.06f : 0.19f + 0.04f * (local % 3));
                }
            }
        }

        private static Vector3 MeasureHeadCentre(Vector3[] vertices, int head)
        {
            Vector3 sum = Vector3.zero;
            int start = head * FlowerHeadVertexCount;
            for (int petal = 0; petal < FlowerPetalsPerHead; petal++)
                sum += vertices[start + petal * FlowerPetalVertexCount];
            return sum / FlowerPetalsPerHead;
        }

        private static void RewriteFlowerCentre(
            Vector3[] vertices,
            Vector3[] normals,
            Color[] colors,
            int head,
            Vector3 target,
            float radius)
        {
            int start = head * FlowerCentreVertexCount;
            if (start < 0 || start + FlowerCentreVertexCount > vertices.Length) return;
            Vector3 centre = target + new Vector3(0f, 0f, -0.022f);
            Color core = new(0.79f, 0.58f, 0.18f, 1f);
            vertices[start] = centre;
            if (normals != null && normals.Length == vertices.Length) normals[start] = Vector3.back;
            if (colors != null && colors.Length == vertices.Length) colors[start] = core;
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 2f / 8f;
                vertices[start + 1 + i] = centre + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0.006f);
                if (normals != null && normals.Length == vertices.Length) normals[start + 1 + i] = Vector3.back;
                if (colors != null && colors.Length == vertices.Length)
                    colors[start + 1 + i] = new Color(0.93f, 0.76f, 0.29f, 1f);
            }
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
                        material.SetColor("_BaseColor", new Color(0.27f, 0.46f, 0.07f, 1f));
                        material.SetColor("_TipColor", new Color(0.61f, 0.70f, 0.18f, 1f));
                        break;
                    case "Flower Petals":
                        material.SetColor("_BaseColor", new Color(0.95f, 0.76f, 0.84f, 1f));
                        material.SetColor("_TipColor", new Color(1.00f, 0.92f, 0.88f, 1f));
                        break;
                    case "Flower Centres":
                        material.SetColor("_BaseColor", new Color(0.79f, 0.58f, 0.18f, 1f));
                        material.SetColor("_TipColor", new Color(0.94f, 0.78f, 0.30f, 1f));
                        break;
                }
            }
        }

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

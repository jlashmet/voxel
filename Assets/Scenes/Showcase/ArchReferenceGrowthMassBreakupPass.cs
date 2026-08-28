using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Final one-shot composition correction for the ArchLookdev hero growth. It preserves the
    /// existing three combined meshes while rebuilding the existing leaf/head vertices into layered,
    /// masonry-supported ivy masses and asymmetric bouquets.
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
            new(-1.70f, 1.34f),
            new(-1.68f, 4.20f),
            new(-1.24f, 6.91f),
        };
        private static readonly Vector2[] BouquetAnchors =
        {
            new(-1.58f, 1.92f),
            new(-1.57f, 4.72f),
            new(-1.20f, 6.90f),
        };
        private static readonly Vector2[] IvyOutline =
        {
            new( 0.00f, -0.70f),
            new(-0.22f, -0.50f),
            new(-0.48f, -0.44f),
            new(-0.72f, -0.20f),
            new(-0.58f,  0.02f),
            new(-0.82f,  0.28f),
            new(-0.48f,  0.30f),
            new(-0.27f,  0.49f),
            new( 0.00f,  0.92f),
            new( 0.27f,  0.49f),
            new( 0.48f,  0.30f),
            new( 0.82f,  0.28f),
            new( 0.58f,  0.02f),
            new( 0.72f, -0.20f),
            new( 0.48f, -0.44f),
            new( 0.22f, -0.50f),
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

                RebuildLayeredIvy(ivy);
                RebuildAsymmetricBouquets(petals, centres);
                TuneFinalMaterials(heroRoot);

                _composedIvy = ivy;
                _composedPetals = petals;
                CompositionApplied = true;
                _applyRoutine = null;
                yield break;
            }

            _applyRoutine = null;
        }

        private static void RebuildLayeredIvy(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Color[] colors = mesh.colors;
            if (vertices == null || vertices.Length == 0 || !TryFindIvyLeafStarts(mesh, out int[,] starts))
                return;

            RebuildIvyZone(vertices, normals, colors, starts, 0, 0, 2, 0xA11u);
            RebuildIvyZone(vertices, normals, colors, starts, 1, 3, 6, 0xB22u);
            RebuildIvyZone(vertices, normals, colors, starts, 2, 7, 11, 0xC33u);

            mesh.vertices = vertices;
            if (normals != null && normals.Length == vertices.Length) mesh.normals = normals;
            if (colors != null && colors.Length == vertices.Length) mesh.colors = colors;
            mesh.RecalculateBounds();
        }

        private static void RebuildIvyZone(
            Vector3[] vertices,
            Vector3[] normals,
            Color[] colors,
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
                Vector2 clusterCentre = anchor + clusterOffset;

                for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
                {
                    uint leafSeed = seed + (uint)(cluster * 977 + leaf * 131 + 17);
                    float angle = leaf * 137.50776f + cluster * 31f + SignedRandom(leafSeed) * 18f;
                    float radians = angle * Mathf.Deg2Rad;
                    float radial = Mathf.Lerp(0.08f, 0.24f, Random01(leafSeed ^ 0x68E31DA4u));
                    float verticalScale = zone == 2 ? 0.72f : 1.08f;
                    Vector2 offset = new(
                        Mathf.Cos(radians) * radial,
                        Mathf.Sin(radians) * radial * verticalScale);
                    if (IsDrape(cluster, leaf))
                    {
                        offset.x *= 0.55f;
                        offset.y -= zone == 2 ? 0.16f : 0.24f;
                    }

                    float radiusBase = zone == 2 ? 0.145f : 0.155f;
                    float radius = radiusBase + Random01(leafSeed ^ 0x9E3779B9u) * 0.040f;
                    float rotation = angle + SignedRandom(leafSeed ^ 0x85EBCA6Bu) * 36f;
                    float depth = -0.175f - Random01(leafSeed ^ 0xC2B2AE35u) * 0.045f;
                    Vector3 targetCentre = new(clusterCentre.x + offset.x, clusterCentre.y + offset.y, depth);
                    RewriteLeaf(
                        vertices, normals, colors, starts[cluster, leaf], targetCentre, radius, rotation,
                        leafSeed, zone);
                }
            }
        }

        private static Vector2 IvyClusterOffset(int zone, int ordinal)
        {
            if (zone == 0)
            {
                return ordinal switch
                {
                    0 => new Vector2(-0.12f, -0.58f),
                    1 => new Vector2( 0.08f, -0.02f),
                    _ => new Vector2(-0.05f,  0.56f),
                };
            }
            if (zone == 1)
            {
                return ordinal switch
                {
                    0 => new Vector2( 0.06f, -0.72f),
                    1 => new Vector2(-0.12f, -0.25f),
                    2 => new Vector2( 0.10f,  0.25f),
                    _ => new Vector2(-0.06f,  0.72f),
                };
            }
            return ordinal switch
            {
                0 => new Vector2(-0.38f, -0.24f),
                1 => new Vector2(-0.20f, -0.10f),
                2 => new Vector2(-0.02f,  0.05f),
                3 => new Vector2( 0.17f,  0.19f),
                _ => new Vector2( 0.35f,  0.29f),
            };
        }

        private static void RewriteLeaf(
            Vector3[] vertices,
            Vector3[] normals,
            Color[] colors,
            int start,
            Vector3 centre,
            float radius,
            float rotationDegrees,
            uint seed,
            int zone)
        {
            if (start < 0 || start + IvyLeafVertexCount > vertices.Length) return;
            float a = rotationDegrees * Mathf.Deg2Rad;
            float ca = Mathf.Cos(a);
            float sa = Mathf.Sin(a);
            float aspect = Mathf.Lerp(0.92f, 1.08f, Random01(seed ^ 0x7FEB352Du));
            Color dark = LeafColor(seed, zone);
            Color light = Color.Lerp(dark, new Color(0.72f, 0.78f, 0.26f, 1f), 0.24f);

            vertices[start] = centre + new Vector3(0f, 0f, -0.012f);
            if (normals != null && normals.Length == vertices.Length) normals[start] = Vector3.back;
            if (colors != null && colors.Length == vertices.Length) colors[start] = dark;

            for (int i = 0; i < IvyOutline.Length; i++)
            {
                Vector2 p = IvyOutline[i];
                float px = p.x * radius * aspect;
                float py = p.y * radius;
                float x = px * ca - py * sa;
                float y = px * sa + py * ca;
                float rimDepth = 0.008f + Mathf.Abs(p.y) * 0.010f;
                vertices[start + 1 + i] = centre + new Vector3(x, y, rimDepth);
                if (normals != null && normals.Length == vertices.Length)
                    normals[start + 1 + i] = new Vector3(x * 0.45f, y * 0.45f, -1f).normalized;
                if (colors != null && colors.Length == vertices.Length)
                    colors[start + 1 + i] = Color.Lerp(dark, light, 0.35f + 0.35f * Mathf.Abs(p.y));
            }
        }

        private static Color LeafColor(uint seed, int zone)
        {
            float t = Random01(seed ^ 0x846CA68Bu);
            Color deep = zone == 2
                ? new Color(0.16f, 0.34f, 0.055f, 1f)
                : new Color(0.13f, 0.31f, 0.045f, 1f);
            Color fresh = zone == 2
                ? new Color(0.42f, 0.55f, 0.10f, 1f)
                : new Color(0.34f, 0.49f, 0.075f, 1f);
            return Color.Lerp(deep, fresh, t);
        }

        private static bool IsDrape(int cluster, int leaf)
        {
            return leaf == 0 && (cluster == 1 || cluster == 4 || cluster == 7 || cluster == 9) ||
                   leaf == 1 && (cluster == 5 || cluster == 8 || cluster == 10);
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

        private static void RebuildAsymmetricBouquets(Mesh petals, Mesh centres)
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

            for (int cluster = 0; cluster < FlowerClusterCount; cluster++)
            {
                int zone = FlowerZone(cluster);
                int clusterOrdinal = FlowerZoneOrdinal(cluster);
                int zoneHeadCount = zone == 2 ? 12 : 9;
                for (int localHead = 0; localHead < FlowerHeadsPerCluster; localHead++)
                {
                    int head = cluster * FlowerHeadsPerCluster + localHead;
                    int ordinal = clusterOrdinal * FlowerHeadsPerCluster + localHead;
                    uint seed = (uint)(head * 593 + zone * 211 + 43);
                    float angle = (ordinal * 137.50776f + zone * 23f + SignedRandom(seed) * 12f) * Mathf.Deg2Rad;
                    float normalized = zoneHeadCount <= 1 ? 0f : ordinal / (float)(zoneHeadCount - 1);
                    float ring = Mathf.Lerp(0.035f, zone == 2 ? 0.27f : 0.23f, Mathf.Sqrt(normalized));
                    Vector2 anchor = BouquetAnchors[zone];
                    float xScale = zone == 2 ? 1.18f : 0.92f;
                    float yScale = zone == 2 ? 0.70f : 1.08f;
                    Vector3 targetHead = new(
                        anchor.x + Mathf.Cos(angle) * ring * xScale,
                        anchor.y + Mathf.Sin(angle) * ring * yScale,
                        -0.205f - Random01(seed ^ 0xA341316Cu) * 0.040f);
                    float radius = Mathf.Lerp(0.125f, 0.172f, Random01(seed ^ 0xC8013EA4u));
                    if ((ordinal + zone) % 5 == 0) radius *= 1.08f;
                    Color blossom = BlossomColor(zone, ordinal);

                    RewriteRoundedRosette(
                        petalVertices, petalNormals, petalColors,
                        head * headVertexCount, head, targetHead, radius, blossom);
                    RewriteFlowerCentre(
                        centreVertices, centreNormals, centreColors,
                        head * FlowerCentreVertexCount, targetHead, radius * 0.16f, blossom);
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
            float rotation = SignedRandom((uint)(headIndex * 829 + 71)) * 22f * Mathf.Deg2Rad;
            for (int petal = 0; petal < FlowerPetalsPerHead; petal++)
            {
                int start = headStart + petal * FlowerPetalVertexCount;
                float angle = rotation + petal * (Mathf.PI * 2f / FlowerPetalsPerHead);
                Vector2 radial = new(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 tangent = new(-radial.y, radial.x);
                Vector3 lobeCentre = headCentre + new Vector3(
                    radial.x * radius * 0.27f,
                    radial.y * radius * 0.27f,
                    -0.014f - 0.004f * (petal & 1));

                vertices[start] = lobeCentre + new Vector3(0f, 0f, -0.009f);
                if (normals != null && normals.Length == vertices.Length) normals[start] = Vector3.back;
                if (colors != null && colors.Length == vertices.Length) colors[start] = blossom;

                for (int rim = 0; rim < 6; rim++)
                {
                    float theta = rim * Mathf.PI * 2f / 6f;
                    float tangentAmount = Mathf.Cos(theta) * radius * 0.33f;
                    float radialAmount = Mathf.Sin(theta) * radius * 0.43f;
                    Vector2 offset = tangent * tangentAmount + radial * radialAmount;
                    float bowl = 0.006f + 0.006f * Mathf.Cos(theta);
                    vertices[start + 1 + rim] = new Vector3(
                        lobeCentre.x + offset.x,
                        lobeCentre.y + offset.y,
                        headCentre.z + bowl);
                    if (normals != null && normals.Length == vertices.Length)
                        normals[start + 1 + rim] = new Vector3(offset.x * 0.50f, offset.y * 0.50f, -1f).normalized;
                    if (colors != null && colors.Length == vertices.Length)
                        colors[start + 1 + rim] = Color.Lerp(blossom, Color.white, 0.08f);
                }
            }
        }

        private static void RewriteFlowerCentre(
            Vector3[] vertices,
            Vector3[] normals,
            Color[] colors,
            int start,
            Vector3 headCentre,
            float radius,
            Color blossom)
        {
            if (start < 0 || start + FlowerCentreVertexCount > vertices.Length) return;
            Color centreColor = Color.Lerp(new Color(0.96f, 0.48f, 0.05f, 1f), blossom, 0.12f);
            Vector3 centre = headCentre + new Vector3(0f, 0f, -0.027f);
            vertices[start] = centre;
            if (normals != null && normals.Length == vertices.Length) normals[start] = Vector3.back;
            if (colors != null && colors.Length == vertices.Length) colors[start] = centreColor;

            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 2f / 8f;
                vertices[start + 1 + i] = centre + new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    0.007f);
                if (normals != null && normals.Length == vertices.Length) normals[start + 1 + i] = Vector3.back;
                if (colors != null && colors.Length == vertices.Length)
                    colors[start + 1 + i] = Color.Lerp(centreColor, new Color(1.00f, 0.78f, 0.16f, 1f), 0.55f);
            }
        }

        private static Color BlossomColor(int zone, int ordinal)
        {
            int variant = (zone * 7 + ordinal * 3) % 5;
            return variant switch
            {
                0 => new Color(0.95f, 0.62f, 0.73f, 1f),
                1 => new Color(0.78f, 0.72f, 0.94f, 1f),
                2 => new Color(0.98f, 0.82f, 0.58f, 1f),
                3 => new Color(0.88f, 0.68f, 0.91f, 1f),
                _ => new Color(0.96f, 0.77f, 0.80f, 1f),
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
                        material.SetColor("_BaseColor", new Color(0.15f, 0.32f, 0.045f, 1f));
                        material.SetColor("_TipColor", new Color(0.47f, 0.59f, 0.12f, 1f));
                        break;
                    case "Flower Petals":
                        material.SetColor("_BaseColor", new Color(0.91f, 0.70f, 0.82f, 1f));
                        material.SetColor("_TipColor", new Color(1.00f, 0.88f, 0.86f, 1f));
                        break;
                    case "Flower Centres":
                        material.SetColor("_BaseColor", new Color(0.96f, 0.47f, 0.04f, 1f));
                        material.SetColor("_TipColor", new Color(1.00f, 0.76f, 0.12f, 1f));
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

        private static void TuneUnused() { }

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

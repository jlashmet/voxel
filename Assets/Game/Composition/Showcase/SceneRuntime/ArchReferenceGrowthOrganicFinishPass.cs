using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Final reference-language pass for ArchLookdev. The exact-topology cleanup owns correctness;
    /// this pass only reshapes those same bounded vertices into softer overlapping English ivy and
    /// irregular layered blossoms after the cleanup has completed. It adds no topology, draws,
    /// per-leaf GameObjects, or steady-state work.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1800)]
    public sealed class ArchReferenceGrowthOrganicFinishPass : MonoBehaviour
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

        private static readonly Vector2[] IvyOutline =
        {
            new( 0.00f, -0.66f),
            new(-0.18f, -0.52f),
            new(-0.40f, -0.42f),
            new(-0.58f, -0.24f),
            new(-0.50f, -0.06f),
            new(-0.60f,  0.14f),
            new(-0.36f,  0.18f),
            new(-0.16f,  0.34f),
            new( 0.00f,  0.74f),
            new( 0.17f,  0.35f),
            new( 0.36f,  0.19f),
            new( 0.61f,  0.15f),
            new( 0.51f, -0.05f),
            new( 0.59f, -0.23f),
            new( 0.40f, -0.42f),
            new( 0.18f, -0.52f),
        };

        private static readonly Color[] LeafPalette =
        {
            new(0.16f, 0.34f, 0.045f, 1f),
            new(0.22f, 0.42f, 0.060f, 1f),
            new(0.29f, 0.49f, 0.075f, 1f),
            new(0.36f, 0.55f, 0.095f, 1f),
            new(0.42f, 0.59f, 0.115f, 1f),
            new(0.27f, 0.47f, 0.085f, 1f),
            new(0.33f, 0.52f, 0.105f, 1f),
        };

        private static readonly Color[] BlossomPalette =
        {
            new(1.00f, 0.93f, 0.89f, 1f),
            new(0.98f, 0.87f, 0.88f, 1f),
            new(0.97f, 0.91f, 0.94f, 1f),
            new(1.00f, 0.96f, 0.92f, 1f),
            new(0.94f, 0.84f, 0.91f, 1f),
            new(0.99f, 0.90f, 0.86f, 1f),
        };

        private Coroutine _applyRoutine;
        private Mesh _finishedIvy;
        private Mesh _finishedPetals;

        public bool OrganicFinishApplied { get; private set; }

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
            if (lookdev == null || lookdev.GetComponent<ArchReferenceGrowthOrganicFinishPass>() != null) return;
            lookdev.gameObject.AddComponent<ArchReferenceGrowthOrganicFinishPass>();
        }

        private void OnEnable() => ScheduleApply();

        private void OnDisable()
        {
            if (_applyRoutine != null) StopCoroutine(_applyRoutine);
            _applyRoutine = null;
            _finishedIvy = null;
            _finishedPetals = null;
            OrganicFinishApplied = false;
        }

        private void OnTransformChildrenChanged()
        {
            if (isActiveAndEnabled) ScheduleApply();
        }

        private void ScheduleApply()
        {
            if (_applyRoutine != null) StopCoroutine(_applyRoutine);
            OrganicFinishApplied = false;
            _applyRoutine = StartCoroutine(ApplyWhenTopologyIsReady());
        }

        private IEnumerator ApplyWhenTopologyIsReady()
        {
            for (int attempt = 0; attempt < 72; attempt++)
            {
                yield return null;
                ArchReferenceGrowth growth = GetComponent<ArchReferenceGrowth>();
                ArchReferenceGrowthTopologyCleanupPass topology = GetComponent<ArchReferenceGrowthTopologyCleanupPass>();
                Mesh ivy = growth?.HeroIvyMesh;
                Mesh petals = growth?.HeroFlowerPetalMesh;
                if (growth == null || topology == null || !topology.TopologyCleanupApplied || ivy == null || petals == null)
                    continue;

                if (_finishedIvy == ivy && _finishedPetals == petals)
                {
                    OrganicFinishApplied = true;
                    _applyRoutine = null;
                    yield break;
                }

                Transform heroRoot = FindDetachedHeroRoot();
                Mesh centres = FindChildMesh(heroRoot, "Flower Centres");
                if (heroRoot == null || centres == null ||
                    !ArchReferenceGrowthTopologyCleanupPass.TryBuildTopology(
                        ivy.vertexCount, out int[,] leafStarts, out _))
                    continue;

                RefineIvy(ivy, leafStarts);
                RefineFlowers(petals, centres);
                TuneMaterials(heroRoot);
                _finishedIvy = ivy;
                _finishedPetals = petals;
                OrganicFinishApplied = true;
                _applyRoutine = null;
                yield break;
            }
            _applyRoutine = null;
        }

        private static void RefineIvy(Mesh mesh, int[,] leafStarts)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Color[] colors = mesh.colors;
            if (vertices == null || colors == null || colors.Length != vertices.Length) return;

            for (int cluster = 0; cluster < IvyClusterCount; cluster++)
            {
                Vector2 support = ArchReferenceGrowthAaaPass.Support(cluster);
                bool crown = cluster >= 9 && cluster <= 14;
                bool sparseRight = cluster == 15;
                var offsets = new Vector2[IvyLeavesPerCluster];
                Vector2 mean = Vector2.zero;

                for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
                {
                    uint seed = (uint)(0x4C93 + cluster * 1103 + leaf * 211);
                    float angle = (leaf * 137.50776f + cluster * 23f + SignedRandom(seed) * 19f) * Mathf.Deg2Rad;
                    float ring;
                    if (sparseRight)
                        ring = Mathf.Lerp(0.045f, 0.17f, (leaf + 0.4f) / IvyLeavesPerCluster);
                    else if (leaf < 3)
                        ring = Mathf.Lerp(0.045f, 0.12f, leaf / 2f);
                    else
                        ring = Mathf.Lerp(0.145f, crown ? 0.285f : 0.305f, (leaf - 3f) / 4f);

                    float xStretch = sparseRight ? 0.76f : crown ? 1.24f : 0.96f;
                    float yStretch = sparseRight ? 0.92f : crown ? 0.70f : 1.04f;
                    Vector2 offset = new(
                        Mathf.Cos(angle) * ring * xStretch,
                        Mathf.Sin(angle) * ring * yStretch);
                    if (!sparseRight && leaf == 0 &&
                        (cluster == 1 || cluster == 4 || cluster == 7 || cluster == 9 || cluster == 12))
                        offset.y -= crown ? 0.12f : 0.16f;
                    offsets[leaf] = offset;
                    mean += offset;
                }
                mean /= IvyLeavesPerCluster;

                for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
                {
                    uint seed = (uint)(0x4C93 + cluster * 1103 + leaf * 211);
                    Vector2 offset = offsets[leaf] - mean;
                    float scale = sparseRight
                        ? Mathf.Lerp(0.155f, 0.195f, Random01(seed ^ 0x9E3779B9u))
                        : Mathf.Lerp(0.225f, crown ? 0.275f : 0.295f, Random01(seed ^ 0x9E3779B9u));
                    float z = -0.115f - Random01(seed ^ 0xC2B2AE35u) * 0.255f;
                    float rotation = SignedRandom(seed ^ 0x85EBCA6Bu) * 39f + (crown ? -8f : 1f);
                    RewriteLeaf(
                        vertices, normals, colors, leafStarts[cluster, leaf],
                        new Vector3(support.x + offset.x, support.y + offset.y, z),
                        scale, rotation, seed, crown);
                }
            }

            mesh.vertices = vertices;
            if (normals != null && normals.Length == vertices.Length) mesh.normals = normals;
            mesh.colors = colors;
            mesh.RecalculateBounds();
        }

        private static void RewriteLeaf(
            Vector3[] vertices,
            Vector3[] normals,
            Color[] colors,
            int start,
            Vector3 centre,
            float scale,
            float rotationDegrees,
            uint seed,
            bool crown)
        {
            float angle = rotationDegrees * Mathf.Deg2Rad;
            float ca = Mathf.Cos(angle);
            float sa = Mathf.Sin(angle);
            float aspect = Mathf.Lerp(0.86f, 1.12f, Random01(seed ^ 0x7FEB352Du));
            float asymmetry = SignedRandom(seed ^ 0x13D73A21u) * 0.065f;
            float tiltX = SignedRandom(seed ^ 0x35A6F11Du) * 0.23f;
            float tiltY = SignedRandom(seed ^ 0xAC4C1B51u) * 0.18f;
            Color leaf = LeafPalette[(int)(seed % (uint)LeafPalette.Length)];
            if (crown) leaf = Color.Lerp(leaf, new Color(0.46f, 0.61f, 0.13f, 1f), 0.14f);
            Color edge = Color.Lerp(leaf, new Color(0.66f, 0.73f, 0.20f, 1f), 0.22f);

            vertices[start] = centre + new Vector3(asymmetry * scale, 0f, -0.020f);
            if (normals != null && normals.Length == vertices.Length)
                normals[start] = new Vector3(-tiltX, -tiltY, -1f).normalized;
            colors[start] = Color.Lerp(leaf, Color.black, 0.06f);

            for (int i = 0; i < IvyOutline.Length; i++)
            {
                Vector2 p = IvyOutline[i];
                float contourJitter = Mathf.Lerp(0.94f, 1.06f, Random01(seed ^ (uint)(0xA511E9B3u + i * 977u)));
                float px = (p.x + asymmetry * (0.35f + 0.65f * Mathf.Abs(p.y))) * scale * aspect * contourJitter;
                float py = p.y * scale * contourJitter;
                float x = px * ca - py * sa;
                float y = px * sa + py * ca;
                float bowl = 0.012f + (Mathf.Abs(p.x) * 0.010f + Mathf.Abs(p.y) * 0.020f) * scale;
                float depth = bowl + x * tiltX + y * tiltY;
                vertices[start + 1 + i] = centre + new Vector3(x, y, depth);
                if (normals != null && normals.Length == vertices.Length)
                    normals[start + 1 + i] =
                        new Vector3(-tiltX + x * 0.60f, -tiltY + y * 0.60f, -1f).normalized;
                colors[start + 1 + i] = Color.Lerp(leaf, edge, 0.18f + 0.32f * Mathf.Abs(p.y));
            }
        }

        private static void RefineFlowers(Mesh petals, Mesh centres)
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
                Vector2 anchor = ArchReferenceGrowthAaaPass.BouquetAnchor(bouquet);
                uint seed = (uint)(0x91D + head * 991 + bouquet * 149);
                float angle = (ordinal * 137.50776f + bouquet * 21f + SignedRandom(seed) * 11f) * Mathf.Deg2Rad;
                float ring = ordinal == 0 ? 0.010f : Mathf.Lerp(0.070f, 0.145f, (ordinal - 1f) / 3f);
                float xStretch = bouquet >= 4 ? 1.12f : 0.94f;
                float yStretch = bouquet >= 4 ? 0.78f : 1.00f;
                Vector3 target = new(
                    anchor.x + Mathf.Cos(angle) * ring * xStretch,
                    anchor.y + Mathf.Sin(angle) * ring * yStretch,
                    -0.335f - Random01(seed ^ 0xA341316Cu) * 0.085f);
                float headRadius = Mathf.Lerp(0.142f, 0.182f, Random01(seed ^ 0xC8013EA4u));
                Color blossom = BlossomPalette[(head + bouquet) % BlossomPalette.Length];
                RewriteFlowerHead(petalVertices, petalNormals, petalColors, head, target, headRadius, seed, blossom);
                RewriteFlowerCentre(
                    centreVertices, centreNormals, centreColors, head, target, headRadius * 0.055f);
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
            Vector3[] vertices,
            Vector3[] normals,
            Color[] colors,
            int head,
            Vector3 target,
            float radius,
            uint seed,
            Color blossom)
        {
            int headStart = head * FlowerHeadVertexCount;
            float baseRotation = SignedRandom(seed ^ 0x517CC1B7u) * 28f * Mathf.Deg2Rad;
            for (int petal = 0; petal < FlowerPetalsPerHead; petal++)
            {
                uint petalSeed = seed ^ (uint)(0x68E31DA4u + petal * 379u);
                float petalAngle = baseRotation + petal * Mathf.PI * 2f / FlowerPetalsPerHead +
                                   SignedRandom(petalSeed) * 0.13f;
                Vector2 radial = new(Mathf.Cos(petalAngle), Mathf.Sin(petalAngle));
                Vector2 tangent = new(-radial.y, radial.x);
                float centreDistance = radius * Mathf.Lerp(0.28f, 0.38f, Random01(petalSeed ^ 0x9E3779B9u));
                float halfLength = radius * Mathf.Lerp(0.42f, 0.50f, Random01(petalSeed ^ 0xC2B2AE35u));
                float halfWidth = radius * Mathf.Lerp(0.28f, 0.36f, Random01(petalSeed ^ 0x85EBCA6Bu));
                float z = -0.010f * petal + SignedRandom(petalSeed ^ 0x7FEB352Du) * 0.008f;
                Vector3 petalCentre = target + new Vector3(radial.x * centreDistance, radial.y * centreDistance, z);
                int start = headStart + petal * FlowerPetalVertexCount;
                vertices[start] = petalCentre + new Vector3(0f, 0f, -0.010f);
                if (normals != null && normals.Length == vertices.Length) normals[start] = Vector3.back;
                if (colors != null && colors.Length == vertices.Length)
                    colors[start] = Color.Lerp(blossom, new Color(0.92f, 0.70f, 0.75f, 1f), 0.07f);

                for (int rim = 0; rim < 6; rim++)
                {
                    float theta = rim * Mathf.PI * 2f / 6f;
                    Vector2 offset = radial * (Mathf.Cos(theta) * halfLength) +
                                     tangent * (Mathf.Sin(theta) * halfWidth);
                    float cup = 0.010f + 0.009f * Mathf.Cos(theta) - 0.004f * Mathf.Abs(Mathf.Sin(theta));
                    vertices[start + 1 + rim] = petalCentre + new Vector3(offset.x, offset.y, cup);
                    if (normals != null && normals.Length == vertices.Length)
                        normals[start + 1 + rim] = new Vector3(offset.x * 0.35f, offset.y * 0.35f, -1f).normalized;
                    if (colors != null && colors.Length == vertices.Length)
                        colors[start + 1 + rim] = Color.Lerp(blossom, Color.white, 0.08f + 0.09f * (rim % 3));
                }
            }
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
            Vector3 centre = target + new Vector3(0f, 0f, -0.040f);
            Color core = new(0.80f, 0.58f, 0.16f, 1f);
            vertices[start] = centre;
            if (normals != null && normals.Length == vertices.Length) normals[start] = Vector3.back;
            if (colors != null && colors.Length == vertices.Length) colors[start] = core;
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 2f / 8f;
                vertices[start + 1 + i] = centre +
                    new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0.006f);
                if (normals != null && normals.Length == vertices.Length) normals[start + 1 + i] = Vector3.back;
                if (colors != null && colors.Length == vertices.Length)
                    colors[start + 1 + i] = new Color(0.93f, 0.74f, 0.25f, 1f);
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
                        material.SetColor("_BaseColor", new Color(0.62f, 0.78f, 0.38f, 1f));
                        material.SetColor("_TipColor", new Color(0.86f, 0.92f, 0.62f, 1f));
                        break;
                    case "Flower Petals":
                        material.SetColor("_BaseColor", new Color(0.99f, 0.92f, 0.91f, 1f));
                        material.SetColor("_TipColor", new Color(1.00f, 0.99f, 0.97f, 1f));
                        break;
                    case "Flower Centres":
                        material.SetColor("_BaseColor", new Color(0.84f, 0.62f, 0.18f, 1f));
                        material.SetColor("_TipColor", new Color(0.96f, 0.79f, 0.29f, 1f));
                        break;
                }
            }
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

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Final reference-composition pass for ArchLookdev hero growth. The preceding detail pass owns
    /// per-leaf depth and flower-head separation; this pass redistributes the same batched topology
    /// into readable layered canopies and restores complete blossoms without adding renderers,
    /// vertices, draw calls, or steady-state work.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1100)]
    public sealed class ArchReferenceGrowthLushPass : MonoBehaviour
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
        private const int FlowerHeadCount = FlowerClusterCount * FlowerHeadsPerCluster;
        private const int FlowerPetalsPerHead = 5;
        private const int FlowerPetalVertexCount = 7;
        private const int FlowerCentreVertexCount = 9;
        private const int DetailVisiblePetalCount = FlowerHeadCount * 3;

        private Coroutine _applyRoutine;
        private Mesh _composedIvy;
        private Mesh _composedPetals;

        public bool LushApplied { get; private set; }

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
            if (lookdev == null || lookdev.GetComponent<ArchReferenceGrowthLushPass>() != null) return;
            lookdev.gameObject.AddComponent<ArchReferenceGrowthLushPass>();
        }

        private void OnEnable() => ScheduleApply();

        private void OnDisable()
        {
            if (_applyRoutine != null) StopCoroutine(_applyRoutine);
            _applyRoutine = null;
            _composedIvy = null;
            _composedPetals = null;
            LushApplied = false;
        }

        private void OnTransformChildrenChanged()
        {
            if (isActiveAndEnabled) ScheduleApply();
        }

        private void ScheduleApply()
        {
            if (_applyRoutine != null) StopCoroutine(_applyRoutine);
            _applyRoutine = StartCoroutine(ApplyWhenDetailPassIsReady());
        }

        private IEnumerator ApplyWhenDetailPassIsReady()
        {
            LushApplied = false;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                yield return null;

                ArchReferenceGrowth growth = GetComponent<ArchReferenceGrowth>();
                ArchReferenceGrowthDetailPass detail = GetComponent<ArchReferenceGrowthDetailPass>();
                Mesh ivy = growth?.HeroIvyMesh;
                Mesh petals = growth?.HeroFlowerPetalMesh;
                if (growth == null || detail == null || ivy == null || petals == null) continue;

                if (_composedIvy == ivy && _composedPetals == petals)
                {
                    LushApplied = true;
                    _applyRoutine = null;
                    yield break;
                }

                if (!detail.RefinementApplied || CountVisiblePetals(petals) != DetailVisiblePetalCount)
                    continue;

                Transform heroRoot = FindDetachedHeroRoot();
                Mesh centres = FindChildMesh(heroRoot, "Flower Centres");
                if (heroRoot == null || centres == null) continue;

                RecomposeIvy(ivy);
                RecomposeFlowerClusters(petals, centres);
                TuneHeroMaterials(heroRoot);

                _composedIvy = ivy;
                _composedPetals = petals;
                LushApplied = true;
                _applyRoutine = null;
                yield break;
            }
            _applyRoutine = null;
        }

        private static void RecomposeIvy(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Color[] colors = mesh.colors;
            if (vertices == null || normals == null || colors == null ||
                vertices.Length != normals.Length || vertices.Length != colors.Length) return;

            int cursor = 0;
            int leafIndex = 0;
            cursor = RecomposeIvyPath(vertices, normals, colors, cursor,
                LeftIvyClusterCount, true, ref leafIndex);
            RecomposeIvyPath(vertices, normals, colors, cursor,
                RightIvyClusterCount, false, ref leafIndex);

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.colors = colors;
            mesh.RecalculateBounds();
        }

        private static int RecomposeIvyPath(
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
                if (cluster > 0) cursor += IvyStemVertexCount;

                var starts = new int[IvyLeavesPerCluster];
                var stems = new int[IvyLeavesPerCluster];
                Vector3 clusterCentre = Vector3.zero;
                for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
                {
                    if (cursor + IvyLeafVertexCount > vertices.Length) return vertices.Length;
                    starts[leaf] = cursor;
                    stems[leaf] = -1;
                    clusterCentre += vertices[cursor];
                    cursor += IvyLeafVertexCount;
                    if ((leaf & 1) == 0)
                    {
                        stems[leaf] = cursor;
                        cursor += IvyStemVertexCount;
                    }
                }
                clusterCentre /= IvyLeavesPerCluster;

                bool crown = leftPath && cluster >= 7;
                Vector3 canopyCentre = clusterCentre + (leftPath
                    ? new Vector3(crown ? -0.16f : -0.20f, crown ? 0.10f : 0f, 0f)
                    : new Vector3(0.10f, 0f, 0f));
                float spread = leftPath ? (crown ? 0.68f : 0.74f) : 0.46f;

                for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
                {
                    Vector2 layout = LeafLayout(leaf, crown, leftPath);
                    Vector3 targetCentre = canopyCentre + new Vector3(
                        layout.x * spread,
                        layout.y * spread,
                        Mathf.Lerp(-0.040f, 0.026f,
                            Random01((uint)(leafIndex * 863 + 73))));
                    float baseRadius = leftPath ? (crown ? 0.185f : 0.205f) : 0.135f;
                    float targetRadius = baseRadius * Mathf.Lerp(0.84f, 1.20f,
                        Random01((uint)(leafIndex * 499 + 181)));
                    if (leftPath && (leafIndex % 11 == 0)) targetRadius *= 1.12f;

                    RecomposeLeaf(vertices, normals, colors, starts[leaf], leafIndex,
                        targetCentre, targetRadius);
                    if (stems[leaf] >= 0)
                        RewriteStem(vertices, stems[leaf], canopyCentre, targetCentre);
                    leafIndex++;
                }
            }
            return cursor;
        }

        private static Vector2 LeafLayout(int leaf, bool crown, bool leftPath)
        {
            if (!leftPath)
            {
                return leaf switch
                {
                    0 => new Vector2(-0.26f, -0.28f),
                    1 => new Vector2( 0.18f, -0.20f),
                    2 => new Vector2(-0.15f, -0.02f),
                    3 => new Vector2( 0.24f,  0.08f),
                    4 => new Vector2(-0.22f,  0.24f),
                    5 => new Vector2( 0.12f,  0.31f),
                    6 => new Vector2(-0.04f,  0.48f),
                    _ => new Vector2( 0.22f,  0.53f),
                };
            }

            if (crown)
            {
                return leaf switch
                {
                    0 => new Vector2(-0.48f, -0.20f),
                    1 => new Vector2(-0.20f, -0.34f),
                    2 => new Vector2( 0.10f, -0.28f),
                    3 => new Vector2( 0.38f, -0.16f),
                    4 => new Vector2(-0.40f,  0.14f),
                    5 => new Vector2(-0.08f,  0.11f),
                    6 => new Vector2( 0.23f,  0.17f),
                    _ => new Vector2( 0.49f,  0.27f),
                };
            }

            return leaf switch
            {
                0 => new Vector2(-0.44f, -0.27f),
                1 => new Vector2( 0.02f, -0.30f),
                2 => new Vector2(-0.29f,  0.00f),
                3 => new Vector2( 0.22f, -0.02f),
                4 => new Vector2(-0.47f,  0.26f),
                5 => new Vector2(-0.05f,  0.24f),
                6 => new Vector2( 0.31f,  0.30f),
                _ => new Vector2(-0.15f,  0.50f),
            };
        }

        private static void RecomposeLeaf(
            Vector3[] vertices,
            Vector3[] normals,
            Color[] colors,
            int start,
            int leafIndex,
            Vector3 targetCentre,
            float targetRadius)
        {
            Vector3 oldCentre = vertices[start];
            float maxRadius = 0f;
            for (int vertex = 1; vertex < IvyLeafVertexCount; vertex++)
            {
                Vector2 delta = new(
                    vertices[start + vertex].x - oldCentre.x,
                    vertices[start + vertex].y - oldCentre.y);
                maxRadius = Mathf.Max(maxRadius, delta.magnitude);
            }
            if (maxRadius < 0.0001f) return;

            float angle = SignedRandom((uint)(leafIndex * 733 + 43)) * 18f * Mathf.Deg2Rad;
            float ca = Mathf.Cos(angle);
            float sa = Mathf.Sin(angle);
            Color leafColor = Color.Lerp(
                new Color(0.22f, 0.43f, 0.075f, 1f),
                new Color(0.66f, 0.78f, 0.22f, 1f),
                Random01((uint)(leafIndex * 421 + 193)));

            vertices[start] = targetCentre;
            normals[start] = Vector3.back;
            colors[start] = Color.Lerp(leafColor, Color.white, 0.035f);
            for (int vertex = 1; vertex < IvyLeafVertexCount; vertex++)
            {
                Vector3 source = vertices[start + vertex] - oldCentre;
                float nx = source.x / maxRadius;
                float ny = source.y / maxRadius;
                Vector3 offset = new(
                    (nx * ca - ny * sa) * targetRadius,
                    (nx * sa + ny * ca) * targetRadius * 1.06f,
                    Mathf.Lerp(-0.006f, 0.010f,
                        Random01((uint)(leafIndex * 137 + vertex * 29 + 17))));
                vertices[start + vertex] = targetCentre + offset;
                normals[start + vertex] = new Vector3(-offset.x * 0.45f, -offset.y * 0.45f, -1f).normalized;
                colors[start + vertex] = Color.Lerp(
                    leafColor, new Color(0.13f, 0.30f, 0.045f, 1f), 0.10f);
            }
        }

        private static void RewriteStem(Vector3[] vertices, int start, Vector3 from, Vector3 to)
        {
            if (start < 0 || start + 3 >= vertices.Length) return;
            float width = Mathf.Max(0.012f, (vertices[start + 1] - vertices[start]).magnitude);
            Vector2 delta = new(to.x - from.x, to.y - from.y);
            if (delta.sqrMagnitude < 0.0001f) return;
            delta.Normalize();
            Vector2 perpendicular = new(-delta.y, delta.x);
            Vector3 half = new(perpendicular.x * width * 0.5f, perpendicular.y * width * 0.5f, 0f);
            vertices[start] = from - half;
            vertices[start + 1] = from + half;
            vertices[start + 2] = to + half;
            vertices[start + 3] = to - half;
        }

        private static void RecomposeFlowerClusters(Mesh petals, Mesh centres)
        {
            Vector3[] petalVertices = petals.vertices;
            Vector3[] petalNormals = petals.normals;
            Color[] petalColors = petals.colors;
            Vector3[] centreVertices = centres.vertices;
            Vector3[] centreNormals = centres.normals;
            Color[] centreColors = centres.colors;
            int headVertexCount = FlowerPetalsPerHead * FlowerPetalVertexCount;
            if (petalVertices.Length != FlowerHeadCount * headVertexCount ||
                centreVertices.Length != FlowerHeadCount * FlowerCentreVertexCount) return;

            for (int cluster = 0; cluster < FlowerClusterCount; cluster++)
            {
                Vector3 clusterCentre = Vector3.zero;
                for (int localHead = 0; localHead < FlowerHeadsPerCluster; localHead++)
                {
                    int head = cluster * FlowerHeadsPerCluster + localHead;
                    int headStart = head * headVertexCount;
                    clusterCentre += petalVertices[headStart + FlowerPetalVertexCount];
                }
                clusterCentre /= FlowerHeadsPerCluster;
                clusterCentre += cluster < 6
                    ? new Vector3(-0.16f, 0.01f, -0.025f)
                    : cluster < 9
                        ? new Vector3(-0.07f, 0.13f, -0.025f)
                        : new Vector3(-0.12f, 0.05f, -0.025f);

                float clusterRotation = SignedRandom((uint)(cluster * 947 + 59)) * 16f * Mathf.Deg2Rad;
                float ca = Mathf.Cos(clusterRotation);
                float sa = Mathf.Sin(clusterRotation);
                for (int localHead = 0; localHead < FlowerHeadsPerCluster; localHead++)
                {
                    int head = cluster * FlowerHeadsPerCluster + localHead;
                    Vector2 local = localHead switch
                    {
                        0 => new Vector2(-0.16f, -0.08f),
                        1 => new Vector2( 0.17f, -0.01f),
                        _ => new Vector2( 0.01f,  0.19f),
                    };
                    Vector2 rotated = new(local.x * ca - local.y * sa, local.x * sa + local.y * ca);
                    Vector3 headCentre = clusterCentre + new Vector3(rotated.x, rotated.y, -0.003f * localHead);

                    Color blossom = BlossomColor(head);
                    bool largeDaisy = head % 9 == 0 || (cluster >= 6 && localHead == 2);
                    float headRadius = (largeDaisy ? 0.195f : 0.145f) * Mathf.Lerp(
                        0.88f, 1.12f, Random01((uint)(head * 811 + 43)));
                    float rotation = Random01((uint)(head * 977 + 211)) * 72f;
                    for (int petal = 0; petal < FlowerPetalsPerHead; petal++)
                    {
                        float angle = (rotation + petal * 72f +
                            SignedRandom((uint)(head * 1031 + petal * 79 + 17)) * 5f) * Mathf.Deg2Rad;
                        Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                        float length = headRadius * Mathf.Lerp(0.82f, 0.98f,
                            Random01((uint)(head * 337 + petal * 61 + 29)));
                        float width = headRadius * Mathf.Lerp(0.38f, 0.48f,
                            Random01((uint)(head * 457 + petal * 89 + 37)));
                        Vector3 petalCentre = headCentre + new Vector3(
                            direction.x * headRadius * 0.34f,
                            direction.y * headRadius * 0.34f,
                            -0.002f * petal);
                        WritePetal(petalVertices, petalNormals, petalColors,
                            head * headVertexCount + petal * FlowerPetalVertexCount,
                            petalCentre, direction, length, width,
                            Color.Lerp(blossom, Color.white,
                                Random01((uint)(head * 127 + petal * 41)) * 0.08f));
                    }
                    WriteFlowerCentre(centreVertices, centreNormals, centreColors,
                        head * FlowerCentreVertexCount,
                        headCentre + new Vector3(0f, 0f, -0.010f), headRadius * 0.16f);
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

        private static void WritePetal(
            Vector3[] vertices, Vector3[] normals, Color[] colors, int start,
            Vector3 centre, Vector2 direction, float length, float width, Color color)
        {
            Vector2 side = new(-direction.y, direction.x);
            Vector2[] outline =
            {
                direction * (-0.44f * length),
                direction * (-0.16f * length) + side * (0.46f * width),
                direction * ( 0.18f * length) + side * (0.54f * width),
                direction * ( 0.54f * length),
                direction * ( 0.18f * length) - side * (0.54f * width),
                direction * (-0.16f * length) - side * (0.46f * width),
            };
            vertices[start] = centre;
            normals[start] = Vector3.back;
            colors[start] = Color.Lerp(color, Color.white, 0.05f);
            for (int i = 0; i < outline.Length; i++)
            {
                Vector2 offset = outline[i];
                vertices[start + 1 + i] = centre + new Vector3(offset.x, offset.y, 0f);
                normals[start + 1 + i] = new Vector3(-offset.x * 0.8f, -offset.y * 0.8f, -1f).normalized;
                colors[start + 1 + i] = color;
            }
        }

        private static void WriteFlowerCentre(
            Vector3[] vertices, Vector3[] normals, Color[] colors, int start,
            Vector3 centre, float radius)
        {
            vertices[start] = centre;
            normals[start] = Vector3.back;
            colors[start] = new Color(1.00f, 0.70f, 0.10f, 1f);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 0.25f;
                vertices[start + 1 + i] = centre + new Vector3(
                    Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
                normals[start + 1 + i] = Vector3.back;
                colors[start + 1 + i] = new Color(0.92f, 0.48f, 0.06f, 1f);
            }
        }

        private static Color BlossomColor(int head)
        {
            int palette = head % 10;
            if (palette <= 5)
                return new Color(0.98f, 0.95f, 0.84f, 1f);
            if (palette == 6)
                return new Color(0.98f, 0.52f, 0.12f, 1f);
            if (palette <= 8)
                return new Color(0.42f, 0.63f, 0.96f, 1f);
            return new Color(0.96f, 0.55f, 0.67f, 1f);
        }

        private static int CountVisiblePetals(Mesh mesh)
        {
            if (mesh == null) return 0;
            Vector3[] vertices = mesh.vertices;
            int visible = 0;
            for (int start = 0; start + FlowerPetalVertexCount <= vertices.Length;
                 start += FlowerPetalVertexCount)
            {
                Vector3 centre = vertices[start];
                float maxSqr = 0f;
                for (int vertex = 1; vertex < FlowerPetalVertexCount; vertex++)
                    maxSqr = Mathf.Max(maxSqr, (vertices[start + vertex] - centre).sqrMagnitude);
                if (maxSqr > 0.000001f) visible++;
            }
            return visible;
        }

        private static Mesh FindChildMesh(Transform root, string name)
        {
            if (root == null) return null;
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            foreach (MeshFilter filter in filters)
                if (filter != null && filter.gameObject.name == name) return filter.sharedMesh;
            return null;
        }

        private static void TuneHeroMaterials(Transform heroRoot)
        {
            MeshRenderer[] renderers = heroRoot.GetComponentsInChildren<MeshRenderer>(true);
            foreach (MeshRenderer renderer in renderers)
            {
                Material material = renderer?.sharedMaterial;
                if (material == null) continue;
                switch (renderer.gameObject.name)
                {
                    case "Lobed Ivy":
                        material.SetColor("_BaseColor", new Color(0.31f, 0.55f, 0.12f, 1f));
                        material.SetColor("_TipColor", new Color(0.76f, 0.86f, 0.31f, 1f));
                        break;
                    case "Flower Petals":
                        material.SetColor("_BaseColor", new Color(0.98f, 0.98f, 0.94f, 1f));
                        material.SetColor("_TipColor", Color.white);
                        break;
                    case "Flower Centres":
                        material.SetColor("_BaseColor", new Color(0.96f, 0.53f, 0.06f, 1f));
                        material.SetColor("_TipColor", new Color(1.00f, 0.82f, 0.18f, 1f));
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

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Final ArchLookdev readability pass. It keeps the authored mass placement/topology and uses
    /// the existing ivy stem/leaf and flower vertices to reveal layered leaves, short local vines,
    /// and overlapping bouquets without adding renderers or per-frame presentation work.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1400)]
    public sealed class ArchReferenceGrowthReadabilityPass : MonoBehaviour
    {
        private const string ArchSceneName = "ArchLookdev";
        private const string HeroRootName = "Arch Reference Hero Growth";
        private const int IvyLeavesPerCluster = 8;
        private const int IvyLeafVertexCount = 17;
        private const int LeftIvyClusterCount = 12;
        private const int TotalIvyClusterCount = 16;
        private const int FlowerHeads = 30;
        private const int FlowerPetalsPerHead = 5;
        private const int FlowerPetalVertexCount = 7;
        private const int FlowerCentreVertexCount = 9;
        private const int HeadVertexCount = FlowerPetalsPerHead * FlowerPetalVertexCount;

        private static readonly Color StemColor = new(0.07f, 0.24f, 0.04f, 1f);

        private Coroutine _applyRoutine;
        private Mesh _composedIvy;
        private Mesh _composedPetals;

        public bool ReadabilityApplied { get; private set; }

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
            if (lookdev == null || lookdev.GetComponent<ArchReferenceGrowthReadabilityPass>() != null) return;
            lookdev.gameObject.AddComponent<ArchReferenceGrowthReadabilityPass>();
        }

        private void OnEnable() => ScheduleApply();

        private void OnDisable()
        {
            if (_applyRoutine != null) StopCoroutine(_applyRoutine);
            _applyRoutine = null;
            _composedIvy = null;
            _composedPetals = null;
            ReadabilityApplied = false;
        }

        private void OnTransformChildrenChanged()
        {
            if (isActiveAndEnabled) ScheduleApply();
        }

        private void ScheduleApply()
        {
            if (_applyRoutine != null) StopCoroutine(_applyRoutine);
            ReadabilityApplied = false;
            _applyRoutine = StartCoroutine(ApplyWhenMassPassIsReady());
        }

        private IEnumerator ApplyWhenMassPassIsReady()
        {
            for (int attempt = 0; attempt < 36; attempt++)
            {
                yield return null;

                ArchReferenceGrowth growth = GetComponent<ArchReferenceGrowth>();
                ArchReferenceGrowthMassBreakupPass mass = GetComponent<ArchReferenceGrowthMassBreakupPass>();
                Mesh ivy = growth?.HeroIvyMesh;
                Mesh petals = growth?.HeroFlowerPetalMesh;
                if (growth == null || mass == null || !mass.CompositionApplied || ivy == null || petals == null)
                    continue;

                if (_composedIvy == ivy && _composedPetals == petals)
                {
                    ReadabilityApplied = true;
                    _applyRoutine = null;
                    yield break;
                }

                Transform heroRoot = FindDetachedHeroRoot();
                Mesh centres = FindChildMesh(heroRoot, "Flower Centres");
                if (heroRoot == null || centres == null) continue;

                RefineIvyLayers(ivy);
                RefineBouquetLayers(petals, centres);
                TuneReadabilityMaterials(heroRoot);

                _composedIvy = ivy;
                _composedPetals = petals;
                ReadabilityApplied = true;
                _applyRoutine = null;
                yield break;
            }

            _applyRoutine = null;
        }

        private static void RefineIvyLayers(Mesh mesh)
        {
            if (mesh == null || !TryFindIvyLeafStarts(mesh, out int[,] starts)) return;
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Color[] colors = mesh.colors;
            if (vertices == null || colors == null || colors.Length != vertices.Length) return;

            for (int cluster = 0; cluster < LeftIvyClusterCount; cluster++)
            {
                Vector3 clusterCentre = Vector3.zero;
                for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
                    clusterCentre += vertices[starts[cluster, leaf]];
                clusterCentre /= IvyLeavesPerCluster;
                WriteLocalVine(vertices, normals, colors, starts[cluster, 0] - 4, clusterCentre, cluster);

                for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
                {
                    int start = starts[cluster, leaf];
                    uint seed = (uint)(cluster * 977 + leaf * 131 + 0x519);
                    Vector3 oldCentre = vertices[start];
                    float scale = Mathf.Lerp(0.80f, 0.94f, Random01(seed ^ 0x68E31DA4u));
                    float depthShift = SignedRandom(seed ^ 0xC2B2AE35u) * 0.026f;
                    Vector3 newCentre = oldCentre + new Vector3(0f, 0f, depthShift);
                    Color baseColor = LeafLayerColor(seed, cluster);
                    Color tipColor = Color.Lerp(baseColor, new Color(0.72f, 0.78f, 0.20f, 1f), 0.34f);

                    vertices[start] = newCentre;
                    if (normals != null && normals.Length == vertices.Length) normals[start] = Vector3.back;
                    colors[start] = Color.Lerp(baseColor, Color.black, 0.08f);

                    for (int vertex = 1; vertex < IvyLeafVertexCount; vertex++)
                    {
                        Vector3 offset = vertices[start + vertex] - oldCentre;
                        vertices[start + vertex] = newCentre + new Vector3(
                            offset.x * scale,
                            offset.y * scale,
                            offset.z * 0.80f);
                        if (normals != null && normals.Length == vertices.Length)
                        {
                            Vector3 p = vertices[start + vertex] - newCentre;
                            normals[start + vertex] = new Vector3(p.x * 0.55f, p.y * 0.55f, -1f).normalized;
                        }
                        float rim = vertex / (float)(IvyLeafVertexCount - 1);
                        colors[start + vertex] = Color.Lerp(baseColor, tipColor, 0.35f + 0.45f * rim);
                    }
                }
            }

            mesh.vertices = vertices;
            if (normals != null && normals.Length == vertices.Length) mesh.normals = normals;
            mesh.colors = colors;
            mesh.RecalculateBounds();
        }

        private static void WriteLocalVine(
            Vector3[] vertices,
            Vector3[] normals,
            Color[] colors,
            int start,
            Vector3 clusterCentre,
            int cluster)
        {
            if (start < 0 || start + 4 > vertices.Length) return;
            for (int i = 0; i < 4; i++) if (!IsStemColor(colors[start + i])) return;

            uint seed = (uint)(cluster * 811 + 73);
            bool crown = cluster >= 7;
            Vector2 direction = crown
                ? new Vector2(0.88f, 0.48f).normalized
                : new Vector2(SignedRandom(seed) * 0.18f, 1f).normalized;
            float length = Mathf.Lerp(crown ? 0.30f : 0.34f, crown ? 0.44f : 0.50f, Random01(seed ^ 0x9E3779B9u));
            float halfWidth = 0.012f;
            Vector2 normal2 = new(-direction.y, direction.x);
            Vector2 centre2 = new(clusterCentre.x, clusterCentre.y);
            Vector2 a = centre2 - direction * (length * 0.50f);
            Vector2 b = centre2 + direction * (length * 0.50f);
            float z = clusterCentre.z + 0.010f;

            vertices[start + 0] = new Vector3(a.x + normal2.x * halfWidth, a.y + normal2.y * halfWidth, z);
            vertices[start + 1] = new Vector3(a.x - normal2.x * halfWidth, a.y - normal2.y * halfWidth, z);
            vertices[start + 2] = new Vector3(b.x + normal2.x * halfWidth, b.y + normal2.y * halfWidth, z);
            vertices[start + 3] = new Vector3(b.x - normal2.x * halfWidth, b.y - normal2.y * halfWidth, z);
            for (int i = 0; i < 4; i++)
            {
                colors[start + i] = StemColor;
                if (normals != null && normals.Length == vertices.Length) normals[start + i] = Vector3.back;
            }
        }

        private static Color LeafLayerColor(uint seed, int cluster)
        {
            Color[] palette =
            {
                new(0.18f, 0.38f, 0.045f, 1f),
                new(0.26f, 0.47f, 0.060f, 1f),
                new(0.36f, 0.54f, 0.085f, 1f),
                new(0.22f, 0.43f, 0.105f, 1f),
                new(0.43f, 0.58f, 0.115f, 1f),
            };
            int index = (int)(seed % (uint)palette.Length);
            Color value = palette[index];
            if (cluster >= 7) value = Color.Lerp(value, new Color(0.46f, 0.60f, 0.12f, 1f), 0.18f);
            return value;
        }

        private static void RefineBouquetLayers(Mesh petals, Mesh centres)
        {
            if (petals == null || centres == null) return;
            Vector3[] petalVertices = petals.vertices;
            Vector3[] petalNormals = petals.normals;
            Color[] petalColors = petals.colors;
            Vector3[] centreVertices = centres.vertices;
            Vector3[] centreNormals = centres.normals;
            Color[] centreColors = centres.colors;
            if (petalVertices == null || petalVertices.Length != FlowerHeads * HeadVertexCount ||
                centreVertices == null || centreVertices.Length != FlowerHeads * FlowerCentreVertexCount)
                return;

            Vector3[] oldHeadCentres = new Vector3[FlowerHeads];
            Vector3[] zoneCentres = new Vector3[3];
            int[] zoneCounts = new int[3];
            for (int head = 0; head < FlowerHeads; head++)
            {
                oldHeadCentres[head] = MeasureHeadCentre(petalVertices, head);
                int zone = FlowerZone(head / 3);
                zoneCentres[zone] += oldHeadCentres[head];
                zoneCounts[zone]++;
            }
            for (int zone = 0; zone < 3; zone++) zoneCentres[zone] /= Mathf.Max(1, zoneCounts[zone]);

            for (int head = 0; head < FlowerHeads; head++)
            {
                int zone = FlowerZone(head / 3);
                uint seed = (uint)(head * 593 + zone * 211 + 0xA7);
                Vector3 oldHead = oldHeadCentres[head];
                Vector3 targetHead = zoneCentres[zone] + (oldHead - zoneCentres[zone]) * (zone == 2 ? 0.84f : 0.78f);
                targetHead.z = -0.205f - Random01(seed ^ 0xA341316Cu) * 0.065f;
                float scale = Mathf.Lerp(1.18f, 1.48f, Random01(seed ^ 0xC8013EA4u));
                Color blossom = BlossomLayerColor(head, zone);

                int petalStart = head * HeadVertexCount;
                for (int vertex = 0; vertex < HeadVertexCount; vertex++)
                {
                    Vector3 offset = petalVertices[petalStart + vertex] - oldHead;
                    petalVertices[petalStart + vertex] = targetHead + new Vector3(
                        offset.x * scale,
                        offset.y * scale,
                        offset.z * 0.82f);
                    if (petalColors != null && petalColors.Length == petalVertices.Length)
                    {
                        int local = vertex % FlowerPetalVertexCount;
                        petalColors[petalStart + vertex] = local == 0
                            ? Color.Lerp(blossom, Color.white, 0.05f)
                            : Color.Lerp(blossom, Color.white, 0.20f + 0.08f * (local % 3));
                    }
                }

                int centreStart = head * FlowerCentreVertexCount;
                Vector3 oldCentre = centreVertices[centreStart];
                Vector3 targetCentre = targetHead + new Vector3(0f, 0f, -0.018f);
                for (int vertex = 0; vertex < FlowerCentreVertexCount; vertex++)
                {
                    Vector3 offset = centreVertices[centreStart + vertex] - oldCentre;
                    centreVertices[centreStart + vertex] = targetCentre + new Vector3(
                        offset.x * 0.52f,
                        offset.y * 0.52f,
                        offset.z * 0.65f);
                    if (centreNormals != null && centreNormals.Length == centreVertices.Length)
                        centreNormals[centreStart + vertex] = Vector3.back;
                    if (centreColors != null && centreColors.Length == centreVertices.Length)
                        centreColors[centreStart + vertex] = vertex == 0
                            ? new Color(0.72f, 0.46f, 0.10f, 1f)
                            : new Color(0.92f, 0.72f, 0.24f, 1f);
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

        private static Vector3 MeasureHeadCentre(Vector3[] vertices, int head)
        {
            Vector3 sum = Vector3.zero;
            int start = head * HeadVertexCount;
            for (int petal = 0; petal < FlowerPetalsPerHead; petal++)
                sum += vertices[start + petal * FlowerPetalVertexCount];
            return sum / FlowerPetalsPerHead;
        }

        private static Color BlossomLayerColor(int head, int zone)
        {
            Color[] palette =
            {
                new(0.94f, 0.58f, 0.70f, 1f),
                new(0.73f, 0.64f, 0.91f, 1f),
                new(0.98f, 0.80f, 0.62f, 1f),
                new(0.86f, 0.61f, 0.82f, 1f),
                new(0.96f, 0.72f, 0.76f, 1f),
                new(0.98f, 0.88f, 0.78f, 1f),
            };
            return palette[(head * 5 + zone * 3) % palette.Length];
        }

        private static void TuneReadabilityMaterials(Transform heroRoot)
        {
            MeshRenderer[] renderers = heroRoot.GetComponentsInChildren<MeshRenderer>(true);
            foreach (MeshRenderer renderer in renderers)
            {
                Material material = renderer?.sharedMaterial;
                if (material == null) continue;
                switch (renderer.gameObject.name)
                {
                    case "Lobed Ivy":
                        material.SetColor("_BaseColor", new Color(0.30f, 0.48f, 0.08f, 1f));
                        material.SetColor("_TipColor", new Color(0.60f, 0.70f, 0.18f, 1f));
                        break;
                    case "Flower Petals":
                        material.SetColor("_BaseColor", new Color(0.96f, 0.78f, 0.84f, 1f));
                        material.SetColor("_TipColor", new Color(1.00f, 0.92f, 0.88f, 1f));
                        break;
                    case "Flower Centres":
                        material.SetColor("_BaseColor", new Color(0.78f, 0.55f, 0.14f, 1f));
                        material.SetColor("_TipColor", new Color(0.95f, 0.77f, 0.28f, 1f));
                        break;
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

        private static int FlowerZone(int cluster)
        {
            if (cluster == 9 || cluster <= 1) return 0;
            if (cluster <= 4) return 1;
            return 2;
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

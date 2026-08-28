using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Restores the broad, overlapping foliage mass required by the ArchLookdev reference after the
    /// close-up detail pass has added depth and per-leaf variation. This component only mutates the
    /// already-batched hero meshes; it adds no renderers, vertices, draw calls, or steady-state work.
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
        private const int FlowerHeadCount = 30;
        private const int FlowerPetalsPerHead = 5;
        private const int FlowerPetalVertexCount = 7;
        private const int DetailVisiblePetalCount = FlowerHeadCount * 3;

        private Coroutine _applyRoutine;
        private Mesh _lushIvy;
        private Mesh _lushPetals;

        public bool LushApplied { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachStartupScene()
        {
            AttachToArchLookdev();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == ArchSceneName)
                AttachToArchLookdev();
        }

        private static void AttachToArchLookdev()
        {
            ArchLookdev lookdev = Object.FindAnyObjectByType<ArchLookdev>();
            if (lookdev == null || lookdev.GetComponent<ArchReferenceGrowthLushPass>() != null)
                return;
            lookdev.gameObject.AddComponent<ArchReferenceGrowthLushPass>();
        }

        private void OnEnable()
        {
            ScheduleApply();
        }

        private void OnDisable()
        {
            if (_applyRoutine != null)
                StopCoroutine(_applyRoutine);
            _applyRoutine = null;
            LushApplied = false;
        }

        private void OnTransformChildrenChanged()
        {
            if (isActiveAndEnabled)
                ScheduleApply();
        }

        private void ScheduleApply()
        {
            if (_applyRoutine != null)
                StopCoroutine(_applyRoutine);
            _applyRoutine = StartCoroutine(ApplyWhenDetailPassIsReady());
        }

        private IEnumerator ApplyWhenDetailPassIsReady()
        {
            LushApplied = false;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                yield return null;

                ArchReferenceGrowth growth = GetComponent<ArchReferenceGrowth>();
                ArchReferenceGrowthDetailPass detail = GetComponent<ArchReferenceGrowthDetailPass>();
                Mesh ivy = growth?.HeroIvyMesh;
                Mesh petals = growth?.HeroFlowerPetalMesh;
                if (growth == null || detail == null || ivy == null || petals == null)
                    continue;

                if (_lushIvy == ivy && _lushPetals == petals)
                {
                    LushApplied = true;
                    _applyRoutine = null;
                    yield break;
                }

                // The preceding detail pass deliberately collapses two petals per head. Requiring
                // that exact topology state makes this pass independent of coroutine callback order:
                // a rebuilt mesh is never inflated before its depth/detail treatment is complete.
                if (!detail.RefinementApplied || CountVisiblePetals(petals) != DetailVisiblePetalCount)
                    continue;

                RestoreBroadIvy(ivy);
                RestoreFivePetalFlowers(petals);
                TuneHeroMaterials();

                _lushIvy = ivy;
                _lushPetals = petals;
                LushApplied = true;
                _applyRoutine = null;
                yield break;
            }

            _applyRoutine = null;
        }

        private static void RestoreBroadIvy(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Color[] colors = mesh.colors;
            if (vertices == null || normals == null || colors == null ||
                vertices.Length != normals.Length || vertices.Length != colors.Length)
                return;

            int cursor = 0;
            int leafIndex = 0;
            cursor = RestoreIvyPath(
                vertices, normals, colors, cursor, LeftIvyClusterCount, ref leafIndex);
            RestoreIvyPath(
                vertices, normals, colors, cursor, RightIvyClusterCount, ref leafIndex);

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.colors = colors;
            mesh.RecalculateBounds();
        }

        private static int RestoreIvyPath(
            Vector3[] vertices,
            Vector3[] normals,
            Color[] colors,
            int cursor,
            int clusterCount,
            ref int leafIndex)
        {
            for (int cluster = 0; cluster < clusterCount; cluster++)
            {
                if (cluster > 0)
                    cursor += IvyStemVertexCount;

                var starts = new int[IvyLeavesPerCluster];
                Vector3 clusterCentre = Vector3.zero;
                for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
                {
                    if (cursor + IvyLeafVertexCount > vertices.Length)
                        return vertices.Length;

                    starts[leaf] = cursor;
                    clusterCentre += vertices[cursor];
                    cursor += IvyLeafVertexCount;
                    if ((leaf & 1) == 0)
                        cursor += IvyStemVertexCount;
                }
                clusterCentre /= IvyLeavesPerCluster;

                for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
                    RestoreLeaf(vertices, normals, colors, starts[leaf], leafIndex++, clusterCentre);
            }
            return cursor;
        }

        private static void RestoreLeaf(
            Vector3[] vertices,
            Vector3[] normals,
            Color[] colors,
            int start,
            int leafIndex,
            Vector3 clusterCentre)
        {
            Vector3 oldCentre = vertices[start];
            Vector3 radial = oldCentre - clusterCentre;
            radial.z = 0f;
            Vector3 newCentre = oldCentre + radial * 0.18f;

            float hierarchy = Random01((uint)(leafIndex * 977 + 31));
            float detailScale = hierarchy < 0.16f ? 1.12f : hierarchy < 0.58f ? 0.82f : 0.62f;
            float targetScale = hierarchy < 0.22f ? 1.45f : hierarchy < 0.70f ? 1.25f : 1.06f;
            float restore = targetScale / detailScale;
            float xRestore = restore * Mathf.Lerp(
                0.94f, 1.08f, Random01((uint)(leafIndex * 307 + 71)));
            float yRestore = restore * Mathf.Lerp(
                0.98f, 1.12f, Random01((uint)(leafIndex * 521 + 109)));

            Color leafColor = Color.Lerp(
                new Color(0.25f, 0.47f, 0.09f, 1f),
                new Color(0.63f, 0.76f, 0.23f, 1f),
                Random01((uint)(leafIndex * 421 + 193)));

            vertices[start] = newCentre;
            normals[start] = Vector3.back;
            colors[start] = Color.Lerp(leafColor, Color.white, 0.04f);
            for (int vertex = 1; vertex < IvyLeafVertexCount; vertex++)
            {
                Vector3 offset = vertices[start + vertex] - oldCentre;
                offset.x *= xRestore;
                offset.y *= yRestore;
                offset.z *= Mathf.Lerp(0.90f, 1.18f,
                    Random01((uint)(leafIndex * 137 + vertex * 29 + 17)));
                vertices[start + vertex] = newCentre + offset;
                normals[start + vertex] = new Vector3(
                    -offset.x * 0.28f,
                    -offset.y * 0.28f,
                    -1f).normalized;
                colors[start + vertex] = Color.Lerp(
                    leafColor,
                    new Color(0.13f, 0.31f, 0.045f, 1f),
                    0.10f);
            }
        }

        private static void RestoreFivePetalFlowers(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Color[] colors = mesh.colors;
            int headVertexCount = FlowerPetalsPerHead * FlowerPetalVertexCount;
            if (vertices == null || normals == null || colors == null ||
                vertices.Length != FlowerHeadCount * headVertexCount)
                return;

            for (int head = 0; head < FlowerHeadCount; head++)
            {
                int headStart = head * headVertexCount;
                // Petals 1 and 3 were collapsed by the detail pass to the exact post-spread head
                // centre, so either collapsed group is a stable source for reconstructing the head.
                Vector3 headCentre = vertices[headStart + FlowerPetalVertexCount];
                int sampleStart = headStart;
                Vector3 sampleCentre = vertices[sampleStart];
                float sampleRadius = 0f;
                for (int vertex = 1; vertex < FlowerPetalVertexCount; vertex++)
                {
                    Vector2 d = new(
                        vertices[sampleStart + vertex].x - sampleCentre.x,
                        vertices[sampleStart + vertex].y - sampleCentre.y);
                    sampleRadius = Mathf.Max(sampleRadius, d.magnitude);
                }

                float length = Mathf.Clamp(sampleRadius * 2.25f, 0.10f, 0.34f);
                float headVariation = Mathf.Lerp(0.90f, 1.12f,
                    Random01((uint)(head * 811 + 43)));
                length *= headVariation;
                float width = length * Mathf.Lerp(0.42f, 0.52f,
                    Random01((uint)(head * 613 + 101)));
                float rotation = Random01((uint)(head * 977 + 211)) * 72f;
                Color blossom = BlossomColor(head);

                for (int petal = 0; petal < FlowerPetalsPerHead; petal++)
                {
                    float jitter = SignedRandom((uint)(head * 1031 + petal * 79 + 17)) * 6f;
                    float angle = (rotation + petal * 72f + jitter) * Mathf.Deg2Rad;
                    Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                    float petalLength = length * Mathf.Lerp(
                        0.92f, 1.08f, Random01((uint)(head * 337 + petal * 61 + 29)));
                    float petalWidth = width * Mathf.Lerp(
                        0.90f, 1.10f, Random01((uint)(head * 457 + petal * 89 + 37)));
                    Vector3 petalCentre = headCentre + new Vector3(
                        direction.x * petalLength * 0.31f,
                        direction.y * petalLength * 0.31f,
                        -0.0025f * petal);
                    WritePetal(
                        vertices, normals, colors,
                        headStart + petal * FlowerPetalVertexCount,
                        petalCentre, direction, petalLength, petalWidth, blossom);
                }
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.colors = colors;
            mesh.RecalculateBounds();
        }

        private static void WritePetal(
            Vector3[] vertices,
            Vector3[] normals,
            Color[] colors,
            int start,
            Vector3 centre,
            Vector2 direction,
            float length,
            float width,
            Color color)
        {
            Vector2 side = new(-direction.y, direction.x);
            Vector2[] outline =
            {
                direction * (-0.46f * length),
                direction * (-0.17f * length) + side * (0.43f * width),
                direction * ( 0.18f * length) + side * (0.52f * width),
                direction * ( 0.52f * length),
                direction * ( 0.18f * length) - side * (0.52f * width),
                direction * (-0.17f * length) - side * (0.43f * width),
            };

            vertices[start] = centre;
            normals[start] = Vector3.back;
            colors[start] = Color.Lerp(color, Color.white, 0.04f);
            for (int i = 0; i < outline.Length; i++)
            {
                Vector2 offset = outline[i];
                vertices[start + 1 + i] = centre + new Vector3(offset.x, offset.y, 0f);
                normals[start + 1 + i] = new Vector3(
                    -offset.x * 0.7f,
                    -offset.y * 0.7f,
                    -1f).normalized;
                colors[start + 1 + i] = color;
            }
        }

        private static Color BlossomColor(int head)
        {
            float selector = Random01((uint)(head * 719 + 131));
            if (selector < 0.68f)
            {
                return Color.Lerp(
                    new Color(0.94f, 0.91f, 0.82f, 1f),
                    new Color(1.00f, 0.98f, 0.93f, 1f),
                    Random01((uint)(head * 281 + 67)));
            }
            if (selector < 0.84f)
                return new Color(0.94f, 0.52f, 0.61f, 1f);
            if (selector < 0.94f)
                return new Color(0.55f, 0.67f, 0.92f, 1f);
            return new Color(0.96f, 0.62f, 0.25f, 1f);
        }

        private static int CountVisiblePetals(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            int visible = 0;
            for (int start = 0; start + FlowerPetalVertexCount <= vertices.Length;
                 start += FlowerPetalVertexCount)
            {
                Vector3 centre = vertices[start];
                float maxSqr = 0f;
                for (int vertex = 1; vertex < FlowerPetalVertexCount; vertex++)
                    maxSqr = Mathf.Max(maxSqr, (vertices[start + vertex] - centre).sqrMagnitude);
                if (maxSqr > 0.000001f)
                    visible++;
            }
            return visible;
        }

        private static void TuneHeroMaterials()
        {
            Transform heroRoot = FindDetachedHeroRoot();
            if (heroRoot == null) return;

            MeshRenderer[] renderers = heroRoot.GetComponentsInChildren<MeshRenderer>(true);
            foreach (MeshRenderer renderer in renderers)
            {
                Material material = renderer?.sharedMaterial;
                if (material == null) continue;
                switch (renderer.gameObject.name)
                {
                    case "Lobed Ivy":
                        material.SetColor("_BaseColor", new Color(0.28f, 0.54f, 0.12f, 1f));
                        material.SetColor("_TipColor", new Color(0.72f, 0.84f, 0.30f, 1f));
                        break;
                    case "Flower Petals":
                        material.SetColor("_BaseColor", new Color(0.96f, 0.92f, 0.86f, 1f));
                        material.SetColor("_TipColor", new Color(1.00f, 0.99f, 0.95f, 1f));
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
                GameObject candidateObject = candidate.gameObject;
                if (!candidateObject.activeInHierarchy) continue;
                if (!candidateObject.scene.IsValid() || !candidateObject.scene.isLoaded) continue;
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

        private static float SignedRandom(uint seed)
        {
            return Random01(seed) * 2f - 1f;
        }
    }
}

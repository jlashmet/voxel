using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// One-shot close-up art refinement for ArchLookdev's authored ivy and blossoms.
    /// ArchReferenceGrowth still owns placement, batching and teardown; this pass only mutates
    /// each newly built combined mesh in place to add close-up depth/scale hierarchy.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public sealed class ArchReferenceGrowthDetailPass : MonoBehaviour
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

        private Coroutine _applyRoutine;
        private Mesh _refinedIvyMesh;
        private Mesh _refinedPetalMesh;
        private Vector3[] _pendingHeadDeltas;

        public bool RefinementApplied { get; private set; }

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
            if (scene.name == ArchSceneName)
                AttachToArchLookdev();
        }

        private static void AttachToArchLookdev()
        {
            ArchLookdev lookdev = Object.FindAnyObjectByType<ArchLookdev>();
            if (lookdev == null || lookdev.GetComponent<ArchReferenceGrowthDetailPass>() != null)
                return;
            lookdev.gameObject.AddComponent<ArchReferenceGrowthDetailPass>();
        }

        private void OnEnable() => ScheduleApply();

        private void OnDisable()
        {
            if (_applyRoutine != null)
                StopCoroutine(_applyRoutine);
            _applyRoutine = null;
            RefinementApplied = false;
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
            _applyRoutine = StartCoroutine(ApplyWhenGrowthIsReady());
        }

        private IEnumerator ApplyWhenGrowthIsReady()
        {
            RefinementApplied = false;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                // Growth and the world-space anchor react to the same child transition. Defer one
                // frame so this pass observes the detached production root without callback ordering.
                yield return null;

                ArchReferenceGrowth growth = GetComponent<ArchReferenceGrowth>();
                Mesh ivy = growth?.HeroIvyMesh;
                Mesh petals = growth?.HeroFlowerPetalMesh;
                if (ivy == null || petals == null)
                    continue;

                if (_refinedIvyMesh != ivy)
                {
                    RefineIvy(ivy);
                    _refinedIvyMesh = ivy;
                }

                if (_refinedPetalMesh != petals && _pendingHeadDeltas == null)
                    _pendingHeadDeltas = RefineFlowerPetals(petals);

                Transform heroRoot = FindDetachedHeroRoot();
                if (heroRoot == null)
                    continue;

                Mesh centreMesh = null;
                MeshRenderer[] renderers = heroRoot.GetComponentsInChildren<MeshRenderer>(true);
                foreach (MeshRenderer renderer in renderers)
                {
                    if (renderer == null) continue;
                    Material material = renderer.sharedMaterial;
                    switch (renderer.gameObject.name)
                    {
                        case "Lobed Ivy":
                            TuneMaterial(material,
                                new Color(0.22f, 0.46f, 0.10f, 1f),
                                new Color(0.70f, 0.82f, 0.28f, 1f));
                            break;
                        case "Flower Petals":
                            TuneMaterial(material,
                                new Color(0.88f, 0.45f, 0.54f, 1f),
                                new Color(1.00f, 0.86f, 0.86f, 1f));
                            break;
                        case "Flower Centres":
                            TuneMaterial(material,
                                new Color(0.72f, 0.31f, 0.07f, 1f),
                                new Color(0.96f, 0.66f, 0.18f, 1f));
                            centreMesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                            break;
                    }
                }

                if (_pendingHeadDeltas != null)
                {
                    if (centreMesh == null)
                        continue;
                    RefineFlowerCentres(centreMesh, _pendingHeadDeltas);
                    _pendingHeadDeltas = null;
                    _refinedPetalMesh = petals;
                }

                if (_refinedIvyMesh == ivy && _refinedPetalMesh == petals)
                {
                    RefinementApplied = true;
                    _applyRoutine = null;
                    yield break;
                }
            }

            _applyRoutine = null;
        }

        private static void RefineIvy(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Color[] colors = mesh.colors;
            if (vertices.Length != normals.Length || vertices.Length != colors.Length)
                return;

            int cursor = 0;
            int leafIndex = 0;
            cursor = RefineIvyPath(vertices, normals, colors, cursor, LeftIvyClusterCount, ref leafIndex);
            cursor = RefineIvyPath(vertices, normals, colors, cursor, RightIvyClusterCount, ref leafIndex);
            if (cursor != vertices.Length)
                Debug.LogWarning($"Arch ivy detail topology ended at {cursor} of {vertices.Length} vertices.");

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.colors = colors;
            mesh.RecalculateBounds();
        }

        private static int RefineIvyPath(
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

                for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
                {
                    if (cursor + IvyLeafVertexCount > vertices.Length)
                        return vertices.Length;
                    RefineLeaf(vertices, normals, colors, cursor, leafIndex++);
                    cursor += IvyLeafVertexCount;
                    if ((leaf & 1) == 0)
                        cursor += IvyStemVertexCount;
                }
            }
            return cursor;
        }

        private static void RefineLeaf(
            Vector3[] vertices,
            Vector3[] normals,
            Color[] colors,
            int start,
            int leafIndex)
        {
            Vector3 centre = vertices[start];
            var offsets = new Vector3[IvyLeafVertexCount - 1];
            for (int i = 0; i < offsets.Length; i++)
                offsets[i] = vertices[start + 1 + i] - centre;

            float hierarchy = Random01((uint)(leafIndex * 977 + 31));
            float scale = hierarchy < 0.16f ? 1.12f : hierarchy < 0.58f ? 0.82f : 0.62f;
            float depth = Mathf.Lerp(-0.052f, 0.022f, Random01((uint)(leafIndex * 613 + 97)));
            Color leafColor = Color.Lerp(
                new Color(0.30f, 0.53f, 0.13f, 1f),
                new Color(0.64f, 0.76f, 0.24f, 1f),
                Random01((uint)(leafIndex * 421 + 193)));

            centre.z += depth - 0.014f * scale;
            vertices[start] = centre;
            normals[start] = Vector3.back;
            colors[start] = Color.Lerp(leafColor, Color.white, 0.05f);

            for (int i = 0; i < offsets.Length; i++)
            {
                Vector3 previous = offsets[(i + offsets.Length - 1) % offsets.Length];
                Vector3 current = offsets[i];
                Vector3 next = offsets[(i + 1) % offsets.Length];
                Vector3 smoothed = (previous * 0.18f + current * 0.64f + next * 0.18f) * scale;
                smoothed.z = 0.006f * scale;
                vertices[start + 1 + i] = centre + smoothed;
                normals[start + 1 + i] = new Vector3(
                    -smoothed.x * 0.42f,
                    -smoothed.y * 0.42f,
                    -1f).normalized;
                colors[start + 1 + i] = Color.Lerp(
                    leafColor,
                    new Color(0.16f, 0.34f, 0.07f, 1f),
                    0.12f);
            }
        }

        private static Vector3[] RefineFlowerPetals(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Color[] colors = mesh.colors;
            int headCount = FlowerClusterCount * FlowerHeadsPerCluster;
            var headDeltas = new Vector3[headCount];
            int headVertexCount = FlowerPetalsPerHead * FlowerPetalVertexCount;
            if (vertices.Length != headCount * headVertexCount)
                return headDeltas;

            for (int cluster = 0; cluster < FlowerClusterCount; cluster++)
            {
                Vector3 clusterCentre = Vector3.zero;
                var oldHeadCentres = new Vector3[FlowerHeadsPerCluster];
                for (int head = 0; head < FlowerHeadsPerCluster; head++)
                {
                    int headIndex = cluster * FlowerHeadsPerCluster + head;
                    int headStart = headIndex * headVertexCount;
                    Vector3 headCentre = Vector3.zero;
                    for (int petal = 0; petal < FlowerPetalsPerHead; petal++)
                        headCentre += vertices[headStart + petal * FlowerPetalVertexCount];
                    headCentre /= FlowerPetalsPerHead;
                    oldHeadCentres[head] = headCentre;
                    clusterCentre += headCentre;
                }
                clusterCentre /= FlowerHeadsPerCluster;

                for (int head = 0; head < FlowerHeadsPerCluster; head++)
                {
                    int headIndex = cluster * FlowerHeadsPerCluster + head;
                    int headStart = headIndex * headVertexCount;
                    Vector3 oldHeadCentre = oldHeadCentres[head];
                    Vector3 spread = (oldHeadCentre - clusterCentre) * 1.48f;
                    Vector3 asymmetry = head switch
                    {
                        0 => new Vector3(-0.025f, -0.012f, 0.006f),
                        1 => new Vector3( 0.032f,  0.004f, -0.006f),
                        _ => new Vector3(-0.006f,  0.028f, 0.010f),
                    };
                    Vector3 newHeadCentre = clusterCentre + spread + asymmetry;
                    headDeltas[headIndex] = newHeadCentre - oldHeadCentre;

                    for (int petal = 0; petal < FlowerPetalsPerHead; petal++)
                    {
                        int petalStart = headStart + petal * FlowerPetalVertexCount;
                        Vector3 oldPetalCentre = vertices[petalStart];
                        bool keep = petal == 0 || petal == 2 || petal == 4;
                        if (!keep)
                        {
                            for (int vertex = 0; vertex < FlowerPetalVertexCount; vertex++)
                            {
                                vertices[petalStart + vertex] = newHeadCentre;
                                normals[petalStart + vertex] = Vector3.back;
                            }
                            continue;
                        }

                        Vector3 targetPetalCentre = newHeadCentre +
                            (oldPetalCentre - oldHeadCentre) * 0.58f;
                        Color petalColor = Color.Lerp(
                            new Color(0.88f, 0.40f, 0.50f, 1f),
                            new Color(1.00f, 0.79f, 0.82f, 1f),
                            Random01((uint)(headIndex * 101 + petal * 37 + 17)));

                        for (int vertex = 0; vertex < FlowerPetalVertexCount; vertex++)
                        {
                            Vector3 offset = vertices[petalStart + vertex] - oldPetalCentre;
                            offset.x *= 0.68f;
                            offset.y *= 0.74f;
                            offset.z = -0.004f * petal;
                            vertices[petalStart + vertex] = targetPetalCentre + offset;
                            normals[petalStart + vertex] = new Vector3(
                                -offset.x * 0.8f,
                                -offset.y * 0.8f,
                                -1f).normalized;
                            colors[petalStart + vertex] = petalColor;
                        }
                    }
                }
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.colors = colors;
            mesh.RecalculateBounds();
            return headDeltas;
        }

        private static void RefineFlowerCentres(Mesh mesh, Vector3[] headDeltas)
        {
            Vector3[] vertices = mesh.vertices;
            Color[] colors = mesh.colors;
            int headCount = FlowerClusterCount * FlowerHeadsPerCluster;
            if (vertices.Length != headCount * FlowerCentreVertexCount || headDeltas.Length != headCount)
                return;

            for (int head = 0; head < headCount; head++)
            {
                int start = head * FlowerCentreVertexCount;
                Vector3 oldCentre = vertices[start];
                Vector3 newCentre = oldCentre + headDeltas[head];
                vertices[start] = newCentre;
                colors[start] = new Color(0.92f, 0.56f, 0.13f, 1f);
                for (int vertex = 1; vertex < FlowerCentreVertexCount; vertex++)
                {
                    Vector3 offset = vertices[start + vertex] - oldCentre;
                    vertices[start + vertex] = newCentre + offset * 0.42f;
                    colors[start + vertex] = new Color(0.80f, 0.38f, 0.08f, 1f);
                }
            }

            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.RecalculateBounds();
        }

        private static void TuneMaterial(Material material, Color baseColor, Color tipColor)
        {
            if (material == null) return;
            material.SetColor("_BaseColor", baseColor);
            material.SetColor("_TipColor", tipColor);
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
    }
}

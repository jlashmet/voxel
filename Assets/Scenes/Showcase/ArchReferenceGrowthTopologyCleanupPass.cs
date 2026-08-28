using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Exact-topology finalizer for the ArchLookdev reference growth. Earlier passes establish the
    /// bounded hero meshes, but some discover ivy ranges from mutable colors. This pass treats the
    /// authored topology as authoritative: it rebuilds every real leaf, collapses every real stem,
    /// and finishes the existing flower heads into layered bouquets without adding topology, draws,
    /// GameObjects, or steady-state work.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1700)]
    public sealed class ArchReferenceGrowthTopologyCleanupPass : MonoBehaviour
    {
        private const string ArchSceneName = "ArchLookdev";
        private const string HeroRootName = "Arch Reference Hero Growth";
        private const int IvyLeavesPerCluster = 8;
        private const int IvyLeafVertexCount = 17;
        private const int StemVertexCount = 4;
        private const int LeftClusterCount = 12;
        private const int RightClusterCount = 4;
        private const int TotalClusterCount = LeftClusterCount + RightClusterCount;
        private const int OmittedLeafStemCluster = 13;
        private const int OmittedLeafStemLeaf = 2;
        private const int FlowerHeads = 30;
        private const int FlowerPetalsPerHead = 5;
        private const int FlowerPetalVertexCount = 7;
        private const int FlowerHeadVertexCount = FlowerPetalsPerHead * FlowerPetalVertexCount;
        private const int FlowerCentreVertexCount = 9;

        public const int ExpectedIvyVertexCount = 2484;
        public const int ExpectedStemQuadCount = 77;

        private static readonly Vector2[] IvyOutline =
        {
            new( 0.00f, -0.72f),
            new(-0.22f, -0.49f),
            new(-0.49f, -0.36f),
            new(-0.72f, -0.13f),
            new(-0.57f,  0.05f),
            new(-0.67f,  0.28f),
            new(-0.39f,  0.31f),
            new(-0.20f,  0.49f),
            new( 0.00f,  0.84f),
            new( 0.20f,  0.49f),
            new( 0.39f,  0.31f),
            new( 0.67f,  0.28f),
            new( 0.57f,  0.05f),
            new( 0.72f, -0.13f),
            new( 0.49f, -0.36f),
            new( 0.22f, -0.49f),
        };

        private static readonly Color[] LeafPalette =
        {
            new(0.20f, 0.40f, 0.055f, 1f),
            new(0.28f, 0.48f, 0.070f, 1f),
            new(0.37f, 0.56f, 0.090f, 1f),
            new(0.49f, 0.64f, 0.125f, 1f),
            new(0.32f, 0.52f, 0.115f, 1f),
            new(0.24f, 0.46f, 0.095f, 1f),
            new(0.43f, 0.59f, 0.105f, 1f),
        };

        private static readonly Color[] BlossomPalette =
        {
            new(0.98f, 0.76f, 0.82f, 1f),
            new(0.96f, 0.84f, 0.90f, 1f),
            new(0.89f, 0.79f, 0.95f, 1f),
            new(1.00f, 0.88f, 0.78f, 1f),
            new(0.99f, 0.92f, 0.91f, 1f),
            new(0.92f, 0.72f, 0.86f, 1f),
        };

        private Coroutine _applyRoutine;
        private Mesh _cleanedIvy;
        private Mesh _finishedPetals;

        public bool TopologyCleanupApplied { get; private set; }

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
            if (lookdev == null || lookdev.GetComponent<ArchReferenceGrowthTopologyCleanupPass>() != null) return;
            lookdev.gameObject.AddComponent<ArchReferenceGrowthTopologyCleanupPass>();
        }

        private void OnEnable() => ScheduleApply();

        private void OnDisable()
        {
            if (_applyRoutine != null) StopCoroutine(_applyRoutine);
            _applyRoutine = null;
            _cleanedIvy = null;
            _finishedPetals = null;
            TopologyCleanupApplied = false;
        }

        private void OnTransformChildrenChanged()
        {
            if (isActiveAndEnabled) ScheduleApply();
        }

        private void ScheduleApply()
        {
            if (_applyRoutine != null) StopCoroutine(_applyRoutine);
            TopologyCleanupApplied = false;
            _applyRoutine = StartCoroutine(ApplyWhenAaaPassIsReady());
        }

        private IEnumerator ApplyWhenAaaPassIsReady()
        {
            for (int attempt = 0; attempt < 56; attempt++)
            {
                yield return null;
                ArchReferenceGrowth growth = GetComponent<ArchReferenceGrowth>();
                ArchReferenceGrowthAaaPass aaa = GetComponent<ArchReferenceGrowthAaaPass>();
                Mesh ivy = growth?.HeroIvyMesh;
                Mesh petals = growth?.HeroFlowerPetalMesh;
                if (growth == null || aaa == null || !aaa.AaaCompositionApplied || ivy == null || petals == null)
                    continue;

                if (_cleanedIvy == ivy && _finishedPetals == petals)
                {
                    TopologyCleanupApplied = true;
                    _applyRoutine = null;
                    yield break;
                }

                Transform heroRoot = FindDetachedHeroRoot();
                Mesh centres = FindChildMesh(heroRoot, "Flower Centres");
                if (heroRoot == null || centres == null ||
                    !TryBuildTopology(ivy.vertexCount, out int[,] leafStarts, out int[] stemStarts))
                    continue;

                RebuildExactIvy(ivy, leafStarts, stemStarts);
                FinishFlowers(petals, centres);
                TuneFinalMaterials(heroRoot);
                _cleanedIvy = ivy;
                _finishedPetals = petals;
                TopologyCleanupApplied = true;
                _applyRoutine = null;
                yield break;
            }
            _applyRoutine = null;
        }

        private static void RebuildExactIvy(Mesh mesh, int[,] leafStarts, int[] stemStarts)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Color[] colors = mesh.colors;
            if (vertices == null || colors == null || colors.Length != vertices.Length) return;

            CollapseStemQuads(vertices, normals, stemStarts);

            for (int cluster = 0; cluster < TotalClusterCount; cluster++)
            {
                Vector2 support = ArchReferenceGrowthAaaPass.Support(cluster);
                bool crown = cluster >= 9 && cluster <= 14;
                bool sparseRight = cluster == 15;
                var offsets = new Vector2[IvyLeavesPerCluster];
                Vector2 mean = Vector2.zero;

                for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
                {
                    uint seed = (uint)(0xB741 + cluster * 1009 + leaf * 191);
                    float angle = (leaf * 137.50776f + cluster * 31f + SignedRandom(seed) * 17f) * Mathf.Deg2Rad;
                    float normalized = (leaf + 0.55f) / IvyLeavesPerCluster;
                    float ring = Mathf.Lerp(
                        sparseRight ? 0.08f : 0.16f,
                        sparseRight ? 0.26f : crown ? 0.49f : 0.52f,
                        Mathf.Sqrt(normalized));
                    float xStretch = sparseRight ? 0.75f : crown ? 1.16f : 1.02f;
                    float yStretch = sparseRight ? 0.88f : crown ? 0.72f : 1.04f;
                    Vector2 offset = new(
                        Mathf.Cos(angle) * ring * xStretch,
                        Mathf.Sin(angle) * ring * yStretch);
                    if (!sparseRight && leaf == 0 &&
                        (cluster == 1 || cluster == 4 || cluster == 7 || cluster == 9 || cluster == 12))
                    {
                        offset.x *= 0.42f;
                        offset.y -= crown ? 0.24f : 0.34f;
                    }
                    offsets[leaf] = offset;
                    mean += offset;
                }
                mean /= IvyLeavesPerCluster;

                for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
                {
                    uint seed = (uint)(0xB741 + cluster * 1009 + leaf * 191);
                    Vector2 offset = offsets[leaf] - mean;
                    float scale = sparseRight
                        ? Mathf.Lerp(0.145f, 0.175f, Random01(seed ^ 0x9E3779B9u))
                        : Mathf.Lerp(0.180f, crown ? 0.235f : 0.250f, Random01(seed ^ 0x9E3779B9u));
                    float z = -0.165f - Random01(seed ^ 0xC2B2AE35u) * 0.225f;
                    float rotation = SignedRandom(seed ^ 0x85EBCA6Bu) * 46f + (crown ? -10f : 2f);
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

        private static void CollapseStemQuads(Vector3[] vertices, Vector3[] normals, int[] stemStarts)
        {
            for (int s = 0; s < stemStarts.Length; s++)
            {
                int start = stemStarts[s];
                Vector3 centre = Vector3.zero;
                for (int i = 0; i < StemVertexCount; i++) centre += vertices[start + i];
                centre *= 0.25f;
                for (int i = 0; i < StemVertexCount; i++)
                {
                    vertices[start + i] = centre;
                    if (normals != null && normals.Length == vertices.Length) normals[start + i] = Vector3.back;
                }
            }
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
            float aspect = Mathf.Lerp(0.84f, 1.14f, Random01(seed ^ 0x7FEB352Du));
            float tiltX = SignedRandom(seed ^ 0x35A6F11Du) * 0.20f;
            float tiltY = SignedRandom(seed ^ 0xAC4C1B51u) * 0.16f;
            Color leaf = LeafPalette[(int)(seed % (uint)LeafPalette.Length)];
            if (crown) leaf = Color.Lerp(leaf, new Color(0.52f, 0.66f, 0.14f, 1f), 0.14f);
            Color edge = Color.Lerp(leaf, new Color(0.72f, 0.79f, 0.24f, 1f), 0.30f);

            vertices[start] = centre + new Vector3(0f, 0f, -0.018f);
            if (normals != null && normals.Length == vertices.Length)
                normals[start] = new Vector3(-tiltX, -tiltY, -1f).normalized;
            colors[start] = Color.Lerp(leaf, Color.black, 0.035f);

            for (int i = 0; i < IvyOutline.Length; i++)
            {
                Vector2 p = IvyOutline[i];
                float px = p.x * scale * aspect;
                float py = p.y * scale;
                float x = px * ca - py * sa;
                float y = px * sa + py * ca;
                float bowl = 0.010f + Mathf.Abs(p.y) * 0.020f;
                float depth = bowl + x * tiltX + y * tiltY;
                vertices[start + 1 + i] = centre + new Vector3(x, y, depth);
                if (normals != null && normals.Length == vertices.Length)
                    normals[start + 1 + i] =
                        new Vector3(-tiltX + x * 0.55f, -tiltY + y * 0.55f, -1f).normalized;
                colors[start + 1 + i] =
                    Color.Lerp(leaf, edge, 0.28f + Mathf.Abs(p.y) * 0.48f);
            }
        }

        private static void FinishFlowers(Mesh petals, Mesh centres)
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
                uint seed = (uint)(0xD31 + head * 977 + bouquet * 131);
                float angle = (ordinal * 137.50776f + bouquet * 17f + SignedRandom(seed) * 8f) * Mathf.Deg2Rad;
                float ring = ordinal == 0 ? 0.015f : Mathf.Lerp(0.075f, 0.165f, (ordinal - 1) / 3f);
                float xStretch = bouquet >= 4 ? 1.18f : 0.92f;
                float yStretch = bouquet >= 4 ? 0.76f : 1.02f;
                Vector3 target = new(
                    anchor.x + Mathf.Cos(angle) * ring * xStretch,
                    anchor.y + Mathf.Sin(angle) * ring * yStretch,
                    -0.315f - Random01(seed ^ 0xA341316Cu) * 0.105f);
                float targetRadius = Mathf.Lerp(0.195f, 0.255f, Random01(seed ^ 0xC8013EA4u));
                Color blossom = BlossomPalette[(head + bouquet * 2) % BlossomPalette.Length];
                MoveAndScaleFlowerHead(
                    petalVertices, petalNormals, petalColors, head, target, targetRadius, blossom);
                MoveAndScaleFlowerCentre(
                    centreVertices, centreNormals, centreColors, head, target, targetRadius * 0.075f);
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

        private static void MoveAndScaleFlowerHead(
            Vector3[] vertices,
            Vector3[] normals,
            Color[] colors,
            int head,
            Vector3 target,
            float targetRadius,
            Color blossom)
        {
            int start = head * FlowerHeadVertexCount;
            Vector3 oldCentre = MeasureFlowerHeadCentre(vertices, head);
            float oldRadius = 0f;
            for (int i = 0; i < FlowerHeadVertexCount; i++)
                oldRadius = Mathf.Max(oldRadius, Vector2.Distance(oldCentre, vertices[start + i]));
            float scale = oldRadius > 0.0001f ? targetRadius / oldRadius : 1f;

            for (int i = 0; i < FlowerHeadVertexCount; i++)
            {
                Vector3 offset = vertices[start + i] - oldCentre;
                vertices[start + i] = target + new Vector3(offset.x * scale, offset.y * scale, offset.z * 0.9f);
                if (normals != null && normals.Length == vertices.Length)
                {
                    Vector3 p = vertices[start + i] - target;
                    normals[start + i] = new Vector3(p.x * 0.42f, p.y * 0.42f, -1f).normalized;
                }
                if (colors != null && colors.Length == vertices.Length)
                {
                    int local = i % FlowerPetalVertexCount;
                    colors[start + i] = Color.Lerp(blossom, Color.white, local == 0 ? 0.03f : 0.14f + 0.04f * (local % 3));
                }
            }
        }

        private static Vector3 MeasureFlowerHeadCentre(Vector3[] vertices, int head)
        {
            Vector3 sum = Vector3.zero;
            int start = head * FlowerHeadVertexCount;
            for (int petal = 0; petal < FlowerPetalsPerHead; petal++)
                sum += vertices[start + petal * FlowerPetalVertexCount];
            return sum / FlowerPetalsPerHead;
        }

        private static void MoveAndScaleFlowerCentre(
            Vector3[] vertices,
            Vector3[] normals,
            Color[] colors,
            int head,
            Vector3 target,
            float radius)
        {
            int start = head * FlowerCentreVertexCount;
            Vector3 centre = target + new Vector3(0f, 0f, -0.030f);
            Color core = new(0.82f, 0.57f, 0.14f, 1f);
            vertices[start] = centre;
            if (normals != null && normals.Length == vertices.Length) normals[start] = Vector3.back;
            if (colors != null && colors.Length == vertices.Length) colors[start] = core;
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 2f / 8f;
                vertices[start + 1 + i] = centre +
                    new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0.007f);
                if (normals != null && normals.Length == vertices.Length) normals[start + 1 + i] = Vector3.back;
                if (colors != null && colors.Length == vertices.Length)
                    colors[start + 1 + i] = new Color(0.96f, 0.79f, 0.28f, 1f);
            }
        }

        private static void TuneFinalMaterials(Transform heroRoot)
        {
            foreach (MeshRenderer renderer in heroRoot.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material material = renderer?.sharedMaterial;
                if (material == null) continue;
                switch (renderer.gameObject.name)
                {
                    case "Lobed Ivy":
                        material.SetColor("_BaseColor", new Color(0.78f, 0.90f, 0.56f, 1f));
                        material.SetColor("_TipColor", new Color(0.96f, 0.98f, 0.78f, 1f));
                        break;
                    case "Flower Petals":
                        material.SetColor("_BaseColor", new Color(0.98f, 0.90f, 0.94f, 1f));
                        material.SetColor("_TipColor", new Color(1.00f, 0.98f, 0.97f, 1f));
                        break;
                    case "Flower Centres":
                        material.SetColor("_BaseColor", new Color(0.88f, 0.67f, 0.22f, 1f));
                        material.SetColor("_TipColor", new Color(0.98f, 0.84f, 0.36f, 1f));
                        break;
                }
            }
        }

        public static bool TryBuildTopology(int vertexCount, out int[,] leafStarts, out int[] stemStarts)
        {
            leafStarts = new int[TotalClusterCount, IvyLeavesPerCluster];
            var stems = new List<int>(ExpectedStemQuadCount);
            int cursor = 0;
            int globalCluster = 0;
            if (vertexCount != ExpectedIvyVertexCount ||
                !AppendPath(LeftClusterCount, ref globalCluster, ref cursor, leafStarts, stems) ||
                !AppendPath(RightClusterCount, ref globalCluster, ref cursor, leafStarts, stems) ||
                globalCluster != TotalClusterCount || cursor != vertexCount || stems.Count != ExpectedStemQuadCount)
            {
                stemStarts = System.Array.Empty<int>();
                return false;
            }
            stemStarts = stems.ToArray();
            return true;
        }

        private static bool AppendPath(
            int clusterCount,
            ref int globalCluster,
            ref int cursor,
            int[,] leafStarts,
            List<int> stemStarts)
        {
            for (int localCluster = 0; localCluster < clusterCount; localCluster++, globalCluster++)
            {
                if (localCluster > 0)
                {
                    stemStarts.Add(cursor);
                    cursor += StemVertexCount;
                }
                for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
                {
                    leafStarts[globalCluster, leaf] = cursor;
                    cursor += IvyLeafVertexCount;
                    if ((leaf & 1) == 0 && HasAuthoredLeafStem(globalCluster, leaf))
                    {
                        stemStarts.Add(cursor);
                        cursor += StemVertexCount;
                    }
                }
            }
            return true;
        }

        private static bool HasAuthoredLeafStem(int globalCluster, int leaf)
        {
            return globalCluster != OmittedLeafStemCluster || leaf != OmittedLeafStemLeaf;
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

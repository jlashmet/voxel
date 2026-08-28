using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Exact-topology finalizer for the ArchLookdev hero ivy. BuildIvyMesh has a fixed layout:
    /// two paths (12 left clusters, 4 right clusters), 17 vertices per leaf, and four vertices per
    /// authored stem quad. One near-zero right-side leaf stem is intentionally omitted by AddStem,
    /// so the production mesh contains 2,484 vertices and 77 stem quads. Earlier color-indexed
    /// presentation passes can target the wrong ranges after colors change; this pass therefore
    /// rebuilds every real leaf polygon by exact index and collapses every real stem quad.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1700)]
    public sealed class ArchReferenceGrowthTopologyCleanupPass : MonoBehaviour
    {
        private const string ArchSceneName = "ArchLookdev";
        private const int IvyLeavesPerCluster = 8;
        private const int IvyLeafVertexCount = 17;
        private const int StemVertexCount = 4;
        private const int LeftClusterCount = 12;
        private const int RightClusterCount = 4;
        private const int TotalClusterCount = LeftClusterCount + RightClusterCount;
        private const int OmittedLeafStemCluster = 13;
        private const int OmittedLeafStemLeaf = 2;

        public const int ExpectedIvyVertexCount = 2484;
        public const int ExpectedStemQuadCount = 77;

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

        private Coroutine _applyRoutine;
        private Mesh _cleanedMesh;

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
            _cleanedMesh = null;
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
                if (growth == null || aaa == null || !aaa.AaaCompositionApplied || ivy == null) continue;

                if (_cleanedMesh == ivy)
                {
                    TopologyCleanupApplied = true;
                    _applyRoutine = null;
                    yield break;
                }

                if (!TryBuildTopology(ivy.vertexCount, out int[,] leafStarts, out int[] stemStarts)) continue;
                RebuildExactIvy(ivy, leafStarts, stemStarts);
                _cleanedMesh = ivy;
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
                    if (!sparseRight && leaf == 0 &&
                        (cluster == 1 || cluster == 4 || cluster == 7 || cluster == 9 || cluster == 12))
                    {
                        offset.x *= 0.45f;
                        offset.y -= crown ? 0.20f : 0.27f;
                    }

                    float scale = sparseRight
                        ? Mathf.Lerp(0.125f, 0.155f, Random01(seed ^ 0x9E3779B9u))
                        : Mathf.Lerp(0.145f, crown ? 0.195f : 0.205f, Random01(seed ^ 0x9E3779B9u));
                    float z = -0.195f - Random01(seed ^ 0xC2B2AE35u) * 0.095f;
                    float rotation = SignedRandom(seed ^ 0x85EBCA6Bu) * 31f + (crown ? -12f : 3f);
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
            // AddStem suppresses segments shorter than 0.01 m. The deterministic right-path
            // cluster 1 / leaf 2 centre lands inside that threshold, so no quad is authored there.
            return globalCluster != OmittedLeafStemCluster || leaf != OmittedLeafStemLeaf;
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

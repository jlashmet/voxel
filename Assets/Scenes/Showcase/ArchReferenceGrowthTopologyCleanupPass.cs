using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Exact-topology cleanup for the ArchLookdev hero ivy. BuildIvyMesh has a fixed layout:
    /// two paths (12 left clusters, 4 right clusters), 17 vertices per leaf, and four vertices per
    /// stem quad. Earlier color-based cleanup could miss mutated stem colors, leaving a long
    /// diagonal in the saved player frame. This pass collapses every known stem quad directly.
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

                if (!TryBuildTopology(ivy.vertexCount, out _, out int[] stemStarts)) continue;
                CollapseStemQuads(ivy, stemStarts);
                _cleanedMesh = ivy;
                TopologyCleanupApplied = true;
                _applyRoutine = null;
                yield break;
            }
            _applyRoutine = null;
        }

        private static void CollapseStemQuads(Mesh mesh, int[] stemStarts)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
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
            mesh.vertices = vertices;
            if (normals != null && normals.Length == vertices.Length) mesh.normals = normals;
            mesh.RecalculateBounds();
        }

        public static bool TryBuildTopology(int vertexCount, out int[,] leafStarts, out int[] stemStarts)
        {
            leafStarts = new int[TotalClusterCount, IvyLeavesPerCluster];
            var stems = new List<int>(78);
            int cursor = 0;
            int globalCluster = 0;
            if (!AppendPath(LeftClusterCount, ref globalCluster, ref cursor, leafStarts, stems) ||
                !AppendPath(RightClusterCount, ref globalCluster, ref cursor, leafStarts, stems) ||
                globalCluster != TotalClusterCount || cursor != vertexCount || stems.Count != 78)
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
                    if ((leaf & 1) == 0)
                    {
                        stemStarts.Add(cursor);
                        cursor += StemVertexCount;
                    }
                }
            }
            return true;
        }
    }
}

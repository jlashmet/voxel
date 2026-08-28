using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Final composition correction for the ArchLookdev reference growth. It contracts the existing
    /// left-side ivy clusters into three distinct masonry-supported masses and gathers the existing
    /// flower heads into a few richer bouquets. No renderers, vertices, or steady-state work are added.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1300)]
    public sealed class ArchReferenceGrowthMassBreakupPass : MonoBehaviour
    {
        private const string ArchSceneName = "ArchLookdev";
        private const string HeroRootName = "Arch Reference Hero Growth";
        private const int IvyLeavesPerCluster = 8;
        private const int IvyLeafVertexCount = 17;
        private const int IvyStemVertexCount = 4;
        private const int LeftIvyClusterCount = 12;
        private const int RightIvyClusterCount = 4;
        private const int TotalIvyClusterCount = LeftIvyClusterCount + RightIvyClusterCount;
        private const int FlowerClusterCount = 10;
        private const int FlowerHeadsPerCluster = 3;
        private const int FlowerPetalsPerHead = 5;
        private const int FlowerPetalVertexCount = 7;
        private const int FlowerCentreVertexCount = 9;

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

                BreakIvyIntoMasses(ivy);
                GatherFlowerBouquets(petals, centres);

                _composedIvy = ivy;
                _composedPetals = petals;
                CompositionApplied = true;
                _applyRoutine = null;
                yield break;
            }

            _applyRoutine = null;
        }

        private static void BreakIvyIntoMasses(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            if (vertices == null || vertices.Length == 0) return;
            if (!TryParseIvyLeafStarts(vertices.Length, out int[,] starts)) return;

            ContractIvyZone(vertices, starts, 0, 2, 0.42f, 0xA11u);
            ContractIvyZone(vertices, starts, 3, 6, 0.34f, 0xB22u);
            ContractIvyZone(vertices, starts, 7, 11, 0.31f, 0xC33u);

            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }

        private static void ContractIvyZone(
            Vector3[] vertices,
            int[,] starts,
            int firstCluster,
            int lastCluster,
            float clusterCompression,
            uint seed)
        {
            int count = lastCluster - firstCluster + 1;
            var clusterCentres = new Vector3[count];
            Vector3 zoneCentre = Vector3.zero;
            for (int localCluster = 0; localCluster < count; localCluster++)
            {
                int cluster = firstCluster + localCluster;
                clusterCentres[localCluster] = MeasureIvyClusterCentre(vertices, starts, cluster);
                zoneCentre += clusterCentres[localCluster];
            }
            zoneCentre /= count;

            for (int localCluster = 0; localCluster < count; localCluster++)
            {
                int cluster = firstCluster + localCluster;
                Vector3 sourceCluster = clusterCentres[localCluster];
                Vector3 compressed = zoneCentre + (sourceCluster - zoneCentre) * clusterCompression;
                float xJitter = SignedRandom(seed + (uint)(localCluster * 67 + 11)) * 0.055f;
                float yJitter = SignedRandom(seed + (uint)(localCluster * 89 + 31)) * 0.045f;
                Vector3 targetCluster = compressed + new Vector3(xJitter, yJitter, 0f);

                for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
                {
                    int start = starts[cluster, leaf];
                    Vector3 sourceLeaf = vertices[start];
                    Vector3 local = sourceLeaf - sourceCluster;
                    Vector3 targetLeaf = targetCluster + new Vector3(local.x * 0.84f, local.y * 0.80f, local.z);
                    TranslateRange(vertices, start, IvyLeafVertexCount, targetLeaf - sourceLeaf);
                }
            }
        }

        private static bool TryParseIvyLeafStarts(int vertexCount, out int[,] starts)
        {
            starts = new int[TotalIvyClusterCount, IvyLeavesPerCluster];
            int cursor = 0;
            int globalCluster = 0;
            if (!ParseIvyPath(vertexCount, LeftIvyClusterCount, ref cursor, ref globalCluster, starts))
                return false;
            return ParseIvyPath(vertexCount, RightIvyClusterCount, ref cursor, ref globalCluster, starts);
        }

        private static bool ParseIvyPath(
            int vertexCount,
            int clusterCount,
            ref int cursor,
            ref int globalCluster,
            int[,] starts)
        {
            for (int cluster = 0; cluster < clusterCount; cluster++, globalCluster++)
            {
                if (cluster > 0)
                {
                    if (cursor + IvyStemVertexCount > vertexCount) return false;
                    cursor += IvyStemVertexCount;
                }

                for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
                {
                    if (cursor + IvyLeafVertexCount > vertexCount) return false;
                    starts[globalCluster, leaf] = cursor;
                    cursor += IvyLeafVertexCount;
                    if ((leaf & 1) == 0)
                    {
                        if (cursor + IvyStemVertexCount > vertexCount) return false;
                        cursor += IvyStemVertexCount;
                    }
                }
            }
            return true;
        }

        private static Vector3 MeasureIvyClusterCentre(Vector3[] vertices, int[,] starts, int cluster)
        {
            Vector3 centre = Vector3.zero;
            for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
                centre += vertices[starts[cluster, leaf]];
            return centre / IvyLeavesPerCluster;
        }

        private static void GatherFlowerBouquets(Mesh petals, Mesh centres)
        {
            Vector3[] petalVertices = petals.vertices;
            Vector3[] centreVertices = centres.vertices;
            int headVertexCount = FlowerPetalsPerHead * FlowerPetalVertexCount;
            int expectedHeads = FlowerClusterCount * FlowerHeadsPerCluster;
            if (petalVertices == null || centreVertices == null ||
                petalVertices.Length != expectedHeads * headVertexCount ||
                centreVertices.Length != expectedHeads * FlowerCentreVertexCount)
                return;

            var clusterCentres = new Vector3[FlowerClusterCount];
            for (int cluster = 0; cluster < FlowerClusterCount; cluster++)
                clusterCentres[cluster] = MeasureFlowerClusterCentre(petalVertices, cluster, headVertexCount);

            var zoneCentres = new Vector3[3];
            var zoneCounts = new int[3];
            for (int cluster = 0; cluster < FlowerClusterCount; cluster++)
            {
                int zone = FlowerZone(cluster);
                zoneCentres[zone] += clusterCentres[cluster];
                zoneCounts[zone]++;
            }
            for (int zone = 0; zone < zoneCentres.Length; zone++)
                zoneCentres[zone] /= Mathf.Max(1, zoneCounts[zone]);

            for (int cluster = 0; cluster < FlowerClusterCount; cluster++)
            {
                int zone = FlowerZone(cluster);
                int ordinal = FlowerZoneOrdinal(cluster);
                Vector3 sourceCluster = clusterCentres[cluster];
                Vector3 targetCluster = zoneCentres[zone] + (sourceCluster - zoneCentres[zone]) * 0.12f;
                float angle = (ordinal * 137.50776f + zone * 31f) * Mathf.Deg2Rad;
                float ring = 0.075f + 0.018f * ordinal;
                targetCluster += new Vector3(Mathf.Cos(angle) * ring, Mathf.Sin(angle) * ring, -0.012f * ordinal);

                for (int localHead = 0; localHead < FlowerHeadsPerCluster; localHead++)
                {
                    int head = cluster * FlowerHeadsPerCluster + localHead;
                    int petalStart = head * headVertexCount;
                    int centreStart = head * FlowerCentreVertexCount;
                    Vector3 sourceHead = MeasureHeadCentre(petalVertices, petalStart);
                    Vector3 localHeadOffset = sourceHead - sourceCluster;
                    Vector3 targetHead = targetCluster + new Vector3(
                        localHeadOffset.x * 0.88f,
                        localHeadOffset.y * 0.88f,
                        localHeadOffset.z - 0.006f * localHead);

                    float scale = 1.30f + 0.05f * SignedRandom((uint)(head * 593 + 43));
                    ScaleAndTranslateRange(petalVertices, petalStart, headVertexCount, sourceHead, targetHead, scale);

                    Vector3 sourceCentre = centreVertices[centreStart];
                    Vector3 targetCentre = targetHead + (sourceCentre - sourceHead);
                    ScaleAndTranslateRange(
                        centreVertices, centreStart, FlowerCentreVertexCount, sourceCentre, targetCentre, scale);
                }
            }

            petals.vertices = petalVertices;
            petals.RecalculateBounds();
            centres.vertices = centreVertices;
            centres.RecalculateBounds();
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

        private static Vector3 MeasureFlowerClusterCentre(Vector3[] vertices, int cluster, int headVertexCount)
        {
            Vector3 centre = Vector3.zero;
            for (int localHead = 0; localHead < FlowerHeadsPerCluster; localHead++)
            {
                int head = cluster * FlowerHeadsPerCluster + localHead;
                centre += MeasureHeadCentre(vertices, head * headVertexCount);
            }
            return centre / FlowerHeadsPerCluster;
        }

        private static Vector3 MeasureHeadCentre(Vector3[] vertices, int headStart)
        {
            Vector3 centre = Vector3.zero;
            for (int petal = 0; petal < FlowerPetalsPerHead; petal++)
                centre += vertices[headStart + petal * FlowerPetalVertexCount];
            return centre / FlowerPetalsPerHead;
        }

        private static void TranslateRange(Vector3[] vertices, int start, int count, Vector3 delta)
        {
            for (int i = 0; i < count; i++)
                vertices[start + i] += delta;
        }

        private static void ScaleAndTranslateRange(
            Vector3[] vertices,
            int start,
            int count,
            Vector3 sourceCentre,
            Vector3 targetCentre,
            float scale)
        {
            for (int i = 0; i < count; i++)
                vertices[start + i] = targetCentre + (vertices[start + i] - sourceCentre) * scale;
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

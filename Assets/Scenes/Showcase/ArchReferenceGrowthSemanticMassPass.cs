using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Final macro-composition pass for the ArchLookdev reference growth. The preceding finish owns
    /// leaf and blossom shape; this pass only translates those existing bounded vertices into three
    /// masonry-supported growth masses (lower pier, upper haunch, crown) plus one sparse right accent.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(2000)]
    public sealed class ArchReferenceGrowthSemanticMassPass : MonoBehaviour
    {
        private const string ArchSceneName = "ArchLookdev";
        private const string HeroRootName = "Arch Reference Hero Growth";
        private const int ClusterCount = 16;
        private const int LeavesPerCluster = 8;
        private const int LeafVertexCount = 17;
        private const int FlowerHeads = 30;
        private const int HeadsPerBouquet = 5;
        private const int PetalsPerHead = 5;
        private const int PetalVertexCount = 7;
        private const int HeadVertexCount = PetalsPerHead * PetalVertexCount;
        private const int CentreVertexCount = 9;
        private const float OpeningSpringlineY = 6.40f;
        private const float MasonryAttachmentOffset = 0.34f;

        private static readonly Vector2[] LowerOffsets =
        {
            new(-0.32f, -0.44f), new(0.24f, -0.30f), new(-0.18f, 0.02f),
            new(0.34f, 0.20f), new(-0.08f, 0.52f),
        };

        private static readonly Vector2[] HaunchOffsets =
        {
            new(-0.36f, -0.42f), new(0.22f, -0.30f), new(-0.18f, 0.02f),
            new(0.34f, 0.20f), new(-0.02f, 0.50f),
        };

        private static readonly Vector2[] CrownOffsets =
        {
            new(-0.64f, -0.16f), new(-0.31f, 0.08f), new(0.00f, 0.20f),
            new(0.32f, 0.08f), new(0.63f, -0.20f),
        };

        private Coroutine _routine;
        private Mesh _composedIvy;
        private Mesh _composedFlowers;
        public bool SemanticMassApplied { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachStartupScene() => Attach();

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == ArchSceneName) Attach();
        }

        private static void Attach()
        {
            ArchLookdev lookdev = Object.FindAnyObjectByType<ArchLookdev>();
            if (lookdev == null || lookdev.GetComponent<ArchReferenceGrowthSemanticMassPass>() != null) return;
            lookdev.gameObject.AddComponent<ArchReferenceGrowthSemanticMassPass>();
        }

        private void OnEnable() => Schedule();

        private void OnDisable()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            _composedIvy = null;
            _composedFlowers = null;
            SemanticMassApplied = false;
        }

        private void OnTransformChildrenChanged()
        {
            if (isActiveAndEnabled) Schedule();
        }

        private void Schedule()
        {
            if (_routine != null) StopCoroutine(_routine);
            SemanticMassApplied = false;
            _routine = StartCoroutine(ApplyWhenReferenceReady());
        }

        private IEnumerator ApplyWhenReferenceReady()
        {
            for (int attempt = 0; attempt < 96; attempt++)
            {
                yield return null;
                ArchReferenceGrowth growth = GetComponent<ArchReferenceGrowth>();
                ArchReferenceGrowthReferenceFinishPass reference = GetComponent<ArchReferenceGrowthReferenceFinishPass>();
                Mesh ivy = growth?.HeroIvyMesh;
                Mesh flowers = growth?.HeroFlowerPetalMesh;
                if (growth == null || reference == null || !reference.ReferenceFinishApplied || ivy == null || flowers == null)
                    continue;

                if (_composedIvy == ivy && _composedFlowers == flowers)
                {
                    SemanticMassApplied = true;
                    _routine = null;
                    yield break;
                }

                Transform heroRoot = FindHeroRoot();
                Mesh centres = FindMesh(heroRoot, "Flower Centres");
                if (heroRoot == null || centres == null ||
                    !ArchReferenceGrowthTopologyCleanupPass.TryBuildTopology(ivy.vertexCount, out int[,] leaves, out _))
                    continue;

                ComposeIvyMasses(ivy, leaves);
                ComposeBouquets(flowers, centres);
                _composedIvy = ivy;
                _composedFlowers = flowers;
                SemanticMassApplied = true;
                _routine = null;
                yield break;
            }
            _routine = null;
        }

        private static void ComposeIvyMasses(Mesh mesh, int[,] starts)
        {
            Vector3[] vertices = mesh.vertices;
            if (vertices == null || vertices.Length == 0) return;

            for (int cluster = 0; cluster < ClusterCount; cluster++)
            {
                Vector3 current = ClusterCentre(vertices, starts, cluster);
                Vector2 target = ClusterTarget(cluster);
                Vector3 delta = new(target.x - current.x, target.y - current.y, 0f);
                for (int leaf = 0; leaf < LeavesPerCluster; leaf++)
                {
                    int start = starts[cluster, leaf];
                    for (int i = 0; i < LeafVertexCount; i++) vertices[start + i] += delta;
                }
            }

            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }

        private static void ComposeBouquets(Mesh petals, Mesh centres)
        {
            Vector3[] pv = petals.vertices;
            Vector3[] cv = centres.vertices;
            if (pv == null || pv.Length != FlowerHeads * HeadVertexCount ||
                cv == null || cv.Length != FlowerHeads * CentreVertexCount) return;

            for (int bouquet = 0; bouquet < FlowerHeads / HeadsPerBouquet; bouquet++)
            {
                Vector3 current = BouquetCentre(pv, bouquet);
                Vector2 target = BouquetTarget(bouquet);
                Vector3 delta = new(target.x - current.x, target.y - current.y, 0f);
                for (int local = 0; local < HeadsPerBouquet; local++)
                {
                    int head = bouquet * HeadsPerBouquet + local;
                    int petalStart = head * HeadVertexCount;
                    for (int i = 0; i < HeadVertexCount; i++) pv[petalStart + i] += delta;
                    int centreStart = head * CentreVertexCount;
                    for (int i = 0; i < CentreVertexCount; i++) cv[centreStart + i] += delta;
                }
            }

            petals.vertices = pv;
            petals.RecalculateBounds();
            centres.vertices = cv;
            centres.RecalculateBounds();
        }

        public static Vector2 MassCenter(int mass)
        {
            if (mass < 0 || mass > 2) return Vector2.zero;
            int start = mass * 5;
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < 5; i++) sum += ArchReferenceGrowthAaaPass.Support(start + i);
            return sum / 5f;
        }

        public static Vector2 ClusterTarget(int cluster)
        {
            if (cluster < 0 || cluster >= ClusterCount) return Vector2.zero;
            if (cluster == 15) return ArchReferenceGrowthAaaPass.Support(15);
            int mass = cluster / 5;
            int local = cluster % 5;
            Vector2 offset = mass == 0 ? LowerOffsets[local] : mass == 1 ? HaunchOffsets[local] : CrownOffsets[local];
            return AttachToMasonry(mass, MassCenter(mass) + offset);
        }

        public static Vector2 BouquetTarget(int bouquet)
        {
            if (bouquet < 0 || bouquet >= 6) return Vector2.zero;
            int mass = bouquet / 2;
            bool second = (bouquet & 1) != 0;
            Vector2 offset;
            if (mass == 0) offset = second ? new Vector2(0.24f, 0.26f) : new Vector2(-0.22f, -0.12f);
            else if (mass == 1) offset = second ? new Vector2(0.22f, 0.28f) : new Vector2(-0.24f, -0.10f);
            else offset = second ? new Vector2(0.32f, 0.10f) : new Vector2(-0.34f, -0.05f);
            return AttachToMasonry(mass, MassCenter(mass) + offset);
        }

        private static Vector2 AttachToMasonry(int mass, Vector2 target)
        {
            if (mass < 2) return target + Vector2.left * MasonryAttachmentOffset;
            Vector2 fromOpening = target - new Vector2(0f, OpeningSpringlineY);
            if (fromOpening.sqrMagnitude < 0.0001f) return target + Vector2.up * MasonryAttachmentOffset;
            return target + fromOpening.normalized * MasonryAttachmentOffset;
        }

        private static Vector3 ClusterCentre(Vector3[] vertices, int[,] starts, int cluster)
        {
            Vector3 sum = Vector3.zero;
            for (int leaf = 0; leaf < LeavesPerCluster; leaf++) sum += vertices[starts[cluster, leaf]];
            return sum / LeavesPerCluster;
        }

        private static Vector3 BouquetCentre(Vector3[] vertices, int bouquet)
        {
            Vector3 sum = Vector3.zero;
            for (int local = 0; local < HeadsPerBouquet; local++)
                sum += HeadCentre(vertices, bouquet * HeadsPerBouquet + local);
            return sum / HeadsPerBouquet;
        }

        private static Vector3 HeadCentre(Vector3[] vertices, int head)
        {
            Vector3 sum = Vector3.zero;
            int start = head * HeadVertexCount;
            for (int petal = 0; petal < PetalsPerHead; petal++) sum += vertices[start + petal * PetalVertexCount];
            return sum / PetalsPerHead;
        }

        private static Mesh FindMesh(Transform root, string name)
        {
            if (root == null) return null;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                if (filter != null && filter.gameObject.name == name) return filter.sharedMesh;
            return null;
        }

        private static Transform FindHeroRoot()
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
    }
}

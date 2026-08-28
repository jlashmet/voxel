using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Bounded final presentation correction for the reference ArchLookdev foliage. The semantic
    /// pass already owns attachment and mass placement; this pass changes only existing vertex
    /// relief/coverage and the three existing material multipliers.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(2100)]
    public sealed class ArchReferenceGrowthFinalPresentationPass : MonoBehaviour
    {
        private const string ArchSceneName = "ArchLookdev";
        private const string HeroRootName = "Arch Reference Hero Growth";
        private const int ClusterCount = 16;
        private const int LeavesPerCluster = 8;
        private const int LeafVertexCount = 17;
        private const int FlowerHeads = 30;
        private const int PetalsPerHead = 5;
        private const int PetalVertexCount = 7;
        private const int HeadVertexCount = PetalsPerHead * PetalVertexCount;
        private const int CentreVertexCount = 9;

        private Coroutine _routine;
        private Mesh _finishedIvy;
        private Mesh _finishedFlowers;
        public bool FinalPresentationApplied { get; private set; }

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
            if (lookdev == null || lookdev.GetComponent<ArchReferenceGrowthFinalPresentationPass>() != null) return;
            lookdev.gameObject.AddComponent<ArchReferenceGrowthFinalPresentationPass>();
        }

        private void OnEnable() => Schedule();

        private void OnDisable()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            _finishedIvy = null;
            _finishedFlowers = null;
            FinalPresentationApplied = false;
        }

        private void OnTransformChildrenChanged()
        {
            if (isActiveAndEnabled) Schedule();
        }

        private void Schedule()
        {
            if (_routine != null) StopCoroutine(_routine);
            FinalPresentationApplied = false;
            _routine = StartCoroutine(ApplyWhenSemanticReady());
        }

        private IEnumerator ApplyWhenSemanticReady()
        {
            for (int attempt = 0; attempt < 120; attempt++)
            {
                yield return null;
                ArchReferenceGrowth growth = GetComponent<ArchReferenceGrowth>();
                ArchReferenceGrowthSemanticMassPass semantic = GetComponent<ArchReferenceGrowthSemanticMassPass>();
                Mesh ivy = growth?.HeroIvyMesh;
                Mesh flowers = growth?.HeroFlowerPetalMesh;
                if (growth == null || semantic == null || !semantic.SemanticMassApplied || ivy == null || flowers == null)
                    continue;

                if (_finishedIvy == ivy && _finishedFlowers == flowers)
                {
                    FinalPresentationApplied = true;
                    _routine = null;
                    yield break;
                }

                Transform heroRoot = FindHeroRoot();
                Mesh centres = FindMesh(heroRoot, "Flower Centres");
                if (heroRoot == null || centres == null ||
                    !ArchReferenceGrowthTopologyCleanupPass.TryBuildTopology(ivy.vertexCount, out int[,] leaves, out _))
                    continue;

                EnhanceIvy(ivy, leaves);
                EnhanceFlowers(flowers, centres);
                TuneMaterials(heroRoot);
                _finishedIvy = ivy;
                _finishedFlowers = flowers;
                FinalPresentationApplied = true;
                _routine = null;
                yield break;
            }
            _routine = null;
        }

        private static void EnhanceIvy(Mesh mesh, int[,] starts)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            for (int cluster = 0; cluster < ClusterCount; cluster++)
            {
                float coverage = cluster == 15 ? 1.00f : cluster >= 10 ? 1.22f : 1.28f;
                for (int leaf = 0; leaf < LeavesPerCluster; leaf++)
                {
                    int start = starts[cluster, leaf];
                    Vector3 centre = vertices[start];
                    for (int i = 1; i < LeafVertexCount; i++)
                    {
                        Vector3 offset = vertices[start + i] - centre;
                        offset.x *= coverage;
                        offset.y *= coverage;
                        offset.z *= 1.60f;
                        vertices[start + i] = centre + offset;
                    }
                    if (normals != null && normals.Length == vertices.Length)
                    {
                        for (int i = 0; i < LeafVertexCount; i++)
                        {
                            Vector3 n = normals[start + i];
                            normals[start + i] = new Vector3(n.x * 1.45f, n.y * 1.45f, n.z).normalized;
                        }
                    }
                }
            }
            mesh.vertices = vertices;
            if (normals != null && normals.Length == vertices.Length) mesh.normals = normals;
            mesh.RecalculateBounds();
        }

        private static void EnhanceFlowers(Mesh petals, Mesh centres)
        {
            Vector3[] pv = petals.vertices;
            Vector3[] pn = petals.normals;
            Vector3[] cv = centres.vertices;
            if (pv == null || pv.Length != FlowerHeads * HeadVertexCount ||
                cv == null || cv.Length != FlowerHeads * CentreVertexCount) return;

            for (int head = 0; head < FlowerHeads; head++)
            {
                Vector3 centre = HeadCentre(pv, head);
                int petalStart = head * HeadVertexCount;
                for (int i = 0; i < HeadVertexCount; i++)
                {
                    Vector3 offset = pv[petalStart + i] - centre;
                    offset.x *= 1.14f;
                    offset.y *= 1.14f;
                    offset.z *= 1.25f;
                    pv[petalStart + i] = centre + offset;
                }
                if (pn != null && pn.Length == pv.Length)
                {
                    for (int i = 0; i < HeadVertexCount; i++)
                    {
                        Vector3 n = pn[petalStart + i];
                        pn[petalStart + i] = new Vector3(n.x * 1.25f, n.y * 1.25f, n.z).normalized;
                    }
                }

                int centreStart = head * CentreVertexCount;
                Vector3 discCentre = cv[centreStart];
                for (int i = 1; i < CentreVertexCount; i++)
                    cv[centreStart + i] = discCentre + (cv[centreStart + i] - discCentre) * 1.12f;
            }

            petals.vertices = pv;
            if (pn != null && pn.Length == pv.Length) petals.normals = pn;
            petals.RecalculateBounds();
            centres.vertices = cv;
            centres.RecalculateBounds();
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
                        material.SetColor("_BaseColor", new Color(0.46f, 0.68f, 0.28f, 1f));
                        material.SetColor("_TipColor", new Color(0.74f, 0.84f, 0.48f, 1f));
                        break;
                    case "Flower Petals":
                        material.SetColor("_BaseColor", new Color(1.00f, 0.76f, 0.82f, 1f));
                        material.SetColor("_TipColor", new Color(1.00f, 0.93f, 0.90f, 1f));
                        break;
                    case "Flower Centres":
                        material.SetColor("_BaseColor", new Color(0.86f, 0.58f, 0.12f, 1f));
                        material.SetColor("_TipColor", new Color(0.98f, 0.80f, 0.26f, 1f));
                        break;
                }
            }
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

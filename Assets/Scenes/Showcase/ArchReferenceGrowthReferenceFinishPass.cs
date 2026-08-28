using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Last reference-shaping stage for ArchLookdev. It consumes the already-correct exact topology
    /// and separates the authored leaves/blossoms into a bushy natural read without adding geometry.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1900)]
    public sealed class ArchReferenceGrowthReferenceFinishPass : MonoBehaviour
    {
        private const string ArchSceneName = "ArchLookdev";
        private const string HeroRootName = "Arch Reference Hero Growth";
        private const int ClusterCount = 16;
        private const int LeavesPerCluster = 8;
        private const int FlowerHeads = 30;
        private const int PetalsPerHead = 5;
        private const int PetalVertexCount = 7;
        private const int HeadVertexCount = PetalsPerHead * PetalVertexCount;
        private const int CentreVertexCount = 9;

        private static readonly Vector2[] LeafOutline =
        {
            new( 0.00f, -0.60f),
            new(-0.20f, -0.49f),
            new(-0.40f, -0.37f),
            new(-0.55f, -0.18f),
            new(-0.50f,  0.00f),
            new(-0.55f,  0.14f),
            new(-0.38f,  0.18f),
            new(-0.21f,  0.28f),
            new( 0.00f,  0.56f),
            new( 0.21f,  0.28f),
            new( 0.38f,  0.18f),
            new( 0.55f,  0.14f),
            new( 0.50f,  0.00f),
            new( 0.55f, -0.18f),
            new( 0.40f, -0.37f),
            new( 0.20f, -0.49f),
        };

        private static readonly Color[] Greens =
        {
            new(0.12f, 0.30f, 0.035f, 1f),
            new(0.17f, 0.37f, 0.045f, 1f),
            new(0.22f, 0.43f, 0.055f, 1f),
            new(0.28f, 0.49f, 0.070f, 1f),
            new(0.34f, 0.54f, 0.085f, 1f),
            new(0.40f, 0.58f, 0.105f, 1f),
            new(0.25f, 0.46f, 0.080f, 1f),
            new(0.31f, 0.51f, 0.095f, 1f),
        };

        private static readonly Color[] Blossoms =
        {
            new(1.00f, 0.94f, 0.90f, 1f),
            new(0.99f, 0.88f, 0.88f, 1f),
            new(0.98f, 0.92f, 0.94f, 1f),
            new(1.00f, 0.97f, 0.93f, 1f),
            new(0.97f, 0.86f, 0.91f, 1f),
            new(1.00f, 0.91f, 0.87f, 1f),
        };

        private Coroutine _routine;
        private Mesh _finishedIvy;
        private Mesh _finishedFlowers;
        public bool ReferenceFinishApplied { get; private set; }

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
            if (lookdev == null || lookdev.GetComponent<ArchReferenceGrowthReferenceFinishPass>() != null) return;
            lookdev.gameObject.AddComponent<ArchReferenceGrowthReferenceFinishPass>();
        }

        private void OnEnable() => Schedule();

        private void OnDisable()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            _finishedIvy = null;
            _finishedFlowers = null;
            ReferenceFinishApplied = false;
        }

        private void OnTransformChildrenChanged()
        {
            if (isActiveAndEnabled) Schedule();
        }

        private void Schedule()
        {
            if (_routine != null) StopCoroutine(_routine);
            ReferenceFinishApplied = false;
            _routine = StartCoroutine(ApplyWhenOrganicReady());
        }

        private IEnumerator ApplyWhenOrganicReady()
        {
            for (int attempt = 0; attempt < 84; attempt++)
            {
                yield return null;
                ArchReferenceGrowth growth = GetComponent<ArchReferenceGrowth>();
                ArchReferenceGrowthOrganicFinishPass organic = GetComponent<ArchReferenceGrowthOrganicFinishPass>();
                Mesh ivy = growth?.HeroIvyMesh;
                Mesh flowers = growth?.HeroFlowerPetalMesh;
                if (growth == null || organic == null || !organic.OrganicFinishApplied || ivy == null || flowers == null)
                    continue;

                if (_finishedIvy == ivy && _finishedFlowers == flowers)
                {
                    ReferenceFinishApplied = true;
                    _routine = null;
                    yield break;
                }

                Transform heroRoot = FindHeroRoot();
                Mesh centres = FindMesh(heroRoot, "Flower Centres");
                if (heroRoot == null || centres == null ||
                    !ArchReferenceGrowthTopologyCleanupPass.TryBuildTopology(ivy.vertexCount, out int[,] leaves, out _))
                    continue;

                ShapeIvy(ivy, leaves);
                ShapeFlowers(flowers, centres);
                TuneMaterials(heroRoot);
                _finishedIvy = ivy;
                _finishedFlowers = flowers;
                ReferenceFinishApplied = true;
                _routine = null;
                yield break;
            }
            _routine = null;
        }

        private static void ShapeIvy(Mesh mesh, int[,] starts)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Color[] colors = mesh.colors;
            for (int cluster = 0; cluster < ClusterCount; cluster++)
            {
                Vector2 support = ArchReferenceGrowthAaaPass.Support(cluster);
                bool crown = cluster >= 9 && cluster <= 14;
                bool sparse = cluster == 15;
                var offsets = new Vector2[LeavesPerCluster];
                Vector2 mean = Vector2.zero;
                for (int leaf = 0; leaf < LeavesPerCluster; leaf++)
                {
                    uint seed = (uint)(0x761D + cluster * 1217 + leaf * 263);
                    float angle = (leaf * 137.50776f + cluster * 37f + Signed(seed) * 21f) * Mathf.Deg2Rad;
                    float normalized = (leaf + 0.65f) / LeavesPerCluster;
                    float ring = Mathf.Lerp(sparse ? 0.06f : 0.10f, sparse ? 0.19f : crown ? 0.34f : 0.36f, Mathf.Sqrt(normalized));
                    float xStretch = sparse ? 0.76f : crown ? 1.26f : 1.00f;
                    float yStretch = sparse ? 0.90f : crown ? 0.70f : 1.08f;
                    Vector2 offset = new(Mathf.Cos(angle) * ring * xStretch, Mathf.Sin(angle) * ring * yStretch);
                    if (!sparse && leaf == 0 && (cluster == 1 || cluster == 4 || cluster == 7 || cluster == 9 || cluster == 12))
                        offset.y -= crown ? 0.14f : 0.18f;
                    offsets[leaf] = offset;
                    mean += offset;
                }
                mean /= LeavesPerCluster;

                for (int leaf = 0; leaf < LeavesPerCluster; leaf++)
                {
                    uint seed = (uint)(0x761D + cluster * 1217 + leaf * 263);
                    Vector2 offset = offsets[leaf] - mean;
                    float scale = sparse
                        ? Mathf.Lerp(0.115f, 0.145f, Random01(seed ^ 0x9E3779B9u))
                        : Mathf.Lerp(0.140f, crown ? 0.180f : 0.190f, Random01(seed ^ 0x9E3779B9u));
                    float rotation = Signed(seed ^ 0x85EBCA6Bu) * 48f + (crown ? -8f : 1f);
                    float z = -0.10f - Random01(seed ^ 0xC2B2AE35u) * 0.30f;
                    WriteLeaf(vertices, normals, colors, starts[cluster, leaf],
                        new Vector3(support.x + offset.x, support.y + offset.y, z), scale, rotation, seed, crown);
                }
            }
            mesh.vertices = vertices;
            if (normals != null && normals.Length == vertices.Length) mesh.normals = normals;
            mesh.colors = colors;
            mesh.RecalculateBounds();
        }

        private static void WriteLeaf(Vector3[] vertices, Vector3[] normals, Color[] colors, int start,
            Vector3 centre, float scale, float rotationDegrees, uint seed, bool crown)
        {
            float angle = rotationDegrees * Mathf.Deg2Rad;
            float ca = Mathf.Cos(angle);
            float sa = Mathf.Sin(angle);
            float aspect = Mathf.Lerp(0.82f, 1.16f, Random01(seed ^ 0x7FEB352Du));
            float tiltX = Signed(seed ^ 0x35A6F11Du) * 0.24f;
            float tiltY = Signed(seed ^ 0xAC4C1B51u) * 0.20f;
            Color baseColor = Greens[(int)(seed % (uint)Greens.Length)];
            if (crown) baseColor = Color.Lerp(baseColor, new Color(0.43f, 0.58f, 0.13f, 1f), 0.12f);
            Color rimColor = Color.Lerp(baseColor, new Color(0.58f, 0.69f, 0.18f, 1f), 0.24f);

            vertices[start] = centre + new Vector3(0f, 0f, -0.014f);
            if (normals != null && normals.Length == vertices.Length) normals[start] = new Vector3(-tiltX, -tiltY, -1f).normalized;
            colors[start] = Color.Lerp(baseColor, Color.black, 0.04f);
            for (int i = 0; i < LeafOutline.Length; i++)
            {
                Vector2 p = LeafOutline[i];
                float jitter = Mathf.Lerp(0.92f, 1.08f, Random01(seed ^ (uint)(0xA511E9B3u + i * 811u)));
                float px = p.x * scale * aspect * jitter;
                float py = p.y * scale * jitter;
                float x = px * ca - py * sa;
                float y = px * sa + py * ca;
                float depth = 0.008f + x * tiltX + y * tiltY + (Mathf.Abs(p.x) + Mathf.Abs(p.y)) * 0.004f;
                vertices[start + 1 + i] = centre + new Vector3(x, y, depth);
                if (normals != null && normals.Length == vertices.Length)
                    normals[start + 1 + i] = new Vector3(-tiltX + x * 0.65f, -tiltY + y * 0.65f, -1f).normalized;
                colors[start + 1 + i] = Color.Lerp(baseColor, rimColor, 0.14f + 0.32f * Mathf.Abs(p.y));
            }
        }

        private static void ShapeFlowers(Mesh petals, Mesh centres)
        {
            Vector3[] pv = petals.vertices;
            Vector3[] pn = petals.normals;
            Color[] pc = petals.colors;
            Vector3[] cv = centres.vertices;
            Vector3[] cn = centres.normals;
            Color[] cc = centres.colors;
            if (pv == null || pv.Length != FlowerHeads * HeadVertexCount || cv == null || cv.Length != FlowerHeads * CentreVertexCount) return;

            for (int head = 0; head < FlowerHeads; head++)
            {
                int bouquet = head / 5;
                int ordinal = head % 5;
                Vector2 anchor = ArchReferenceGrowthAaaPass.BouquetAnchor(bouquet);
                uint seed = (uint)(0xB19 + head * 1031 + bouquet * 157);
                float angle = (ordinal * 137.50776f + bouquet * 29f + Signed(seed) * 10f) * Mathf.Deg2Rad;
                float ring = ordinal == 0 ? 0f : Mathf.Lerp(0.12f, 0.22f, (ordinal - 1f) / 3f);
                Vector3 target = new(
                    anchor.x + Mathf.Cos(angle) * ring * (bouquet >= 4 ? 1.12f : 0.94f),
                    anchor.y + Mathf.Sin(angle) * ring * (bouquet >= 4 ? 0.78f : 1.00f),
                    -0.36f - Random01(seed ^ 0xA341316Cu) * 0.08f);
                float radius = Mathf.Lerp(0.075f, 0.105f, Random01(seed ^ 0xC8013EA4u));
                WriteFlowerHead(pv, pn, pc, head, target, radius, seed, Blossoms[(head + bouquet) % Blossoms.Length]);
                WriteCentre(cv, cn, cc, head, target, radius * 0.075f);
            }
            petals.vertices = pv;
            if (pn != null && pn.Length == pv.Length) petals.normals = pn;
            if (pc != null && pc.Length == pv.Length) petals.colors = pc;
            petals.RecalculateBounds();
            centres.vertices = cv;
            if (cn != null && cn.Length == cv.Length) centres.normals = cn;
            if (cc != null && cc.Length == cv.Length) centres.colors = cc;
            centres.RecalculateBounds();
        }

        private static void WriteFlowerHead(Vector3[] vertices, Vector3[] normals, Color[] colors, int head,
            Vector3 target, float radius, uint seed, Color blossom)
        {
            int headStart = head * HeadVertexCount;
            float baseRotation = Signed(seed ^ 0x517CC1B7u) * 30f * Mathf.Deg2Rad;
            for (int petal = 0; petal < PetalsPerHead; petal++)
            {
                uint ps = seed ^ (uint)(0x68E31DA4u + petal * 431u);
                float a = baseRotation + petal * Mathf.PI * 2f / PetalsPerHead + Signed(ps) * 0.14f;
                Vector2 radial = new(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 tangent = new(-radial.y, radial.x);
                float centreDistance = radius * Mathf.Lerp(0.34f, 0.43f, Random01(ps ^ 0x9E3779B9u));
                float halfLength = radius * Mathf.Lerp(0.48f, 0.57f, Random01(ps ^ 0xC2B2AE35u));
                float halfWidth = radius * Mathf.Lerp(0.27f, 0.34f, Random01(ps ^ 0x85EBCA6Bu));
                Vector3 petalCentre = target + new Vector3(radial.x * centreDistance, radial.y * centreDistance,
                    -0.008f * petal + Signed(ps ^ 0x7FEB352Du) * 0.005f);
                int start = headStart + petal * PetalVertexCount;
                vertices[start] = petalCentre + new Vector3(0f, 0f, -0.007f);
                if (normals != null && normals.Length == vertices.Length) normals[start] = Vector3.back;
                if (colors != null && colors.Length == vertices.Length) colors[start] = blossom;
                for (int rim = 0; rim < 6; rim++)
                {
                    float theta = rim * Mathf.PI * 2f / 6f;
                    Vector2 offset = radial * (Mathf.Cos(theta) * halfLength) + tangent * (Mathf.Sin(theta) * halfWidth);
                    vertices[start + 1 + rim] = petalCentre + new Vector3(offset.x, offset.y, 0.006f + 0.006f * Mathf.Cos(theta));
                    if (normals != null && normals.Length == vertices.Length)
                        normals[start + 1 + rim] = new Vector3(offset.x * 0.35f, offset.y * 0.35f, -1f).normalized;
                    if (colors != null && colors.Length == vertices.Length)
                        colors[start + 1 + rim] = Color.Lerp(blossom, Color.white, 0.07f + 0.08f * (rim % 3));
                }
            }
        }

        private static void WriteCentre(Vector3[] vertices, Vector3[] normals, Color[] colors, int head, Vector3 target, float radius)
        {
            int start = head * CentreVertexCount;
            Vector3 centre = target + new Vector3(0f, 0f, -0.04f);
            vertices[start] = centre;
            if (normals != null && normals.Length == vertices.Length) normals[start] = Vector3.back;
            if (colors != null && colors.Length == vertices.Length) colors[start] = new Color(0.80f, 0.57f, 0.15f, 1f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2f / 8f;
                vertices[start + 1 + i] = centre + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0.005f);
                if (normals != null && normals.Length == vertices.Length) normals[start + 1 + i] = Vector3.back;
                if (colors != null && colors.Length == vertices.Length) colors[start + 1 + i] = new Color(0.93f, 0.73f, 0.23f, 1f);
            }
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
                        material.SetColor("_BaseColor", new Color(0.91f, 0.95f, 0.84f, 1f));
                        material.SetColor("_TipColor", new Color(0.98f, 0.99f, 0.91f, 1f));
                        break;
                    case "Flower Petals":
                        material.SetColor("_BaseColor", new Color(0.99f, 0.96f, 0.95f, 1f));
                        material.SetColor("_TipColor", Color.white);
                        break;
                    case "Flower Centres":
                        material.SetColor("_BaseColor", new Color(0.89f, 0.69f, 0.22f, 1f));
                        material.SetColor("_TipColor", new Color(0.98f, 0.84f, 0.34f, 1f));
                        break;
                }
            }
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
                GameObject go = candidate.gameObject;
                if (!go.activeInHierarchy || !go.scene.IsValid() || !go.scene.isLoaded) continue;
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

        private static float Signed(uint seed) => Random01(seed) * 2f - 1f;
    }
}

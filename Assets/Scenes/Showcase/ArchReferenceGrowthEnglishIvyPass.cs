using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Final one-shot silhouette/composition correction for the ArchLookdev hero growth. It mutates
    /// the already-batched meshes in place after the lush pass, so foliage ownership, renderer count,
    /// topology, and steady-state cost remain unchanged.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1200)]
    public sealed class ArchReferenceGrowthEnglishIvyPass : MonoBehaviour
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
        private const float EnglishOutlineRadius = 0.58f;

        // Broad English-ivy silhouette: one crown point, restrained upper shoulders, and one lateral
        // lobe per side. The intermediate points round the shoulders instead of creating radial teeth.
        private static readonly Vector2[] s_EnglishOutline =
        {
            new( 0.00f, -0.43f),
            new(-0.11f, -0.31f),
            new(-0.27f, -0.25f),
            new(-0.39f, -0.11f),
            new(-0.50f,  0.10f),
            new(-0.35f,  0.14f),
            new(-0.25f,  0.29f),
            new(-0.10f,  0.25f),
            new( 0.00f,  0.58f),
            new( 0.10f,  0.25f),
            new( 0.25f,  0.29f),
            new( 0.35f,  0.14f),
            new( 0.50f,  0.10f),
            new( 0.39f, -0.11f),
            new( 0.27f, -0.25f),
            new( 0.11f, -0.31f),
        };

        private Coroutine _applyRoutine;
        private Mesh _refinedIvy;
        private Mesh _refinedPetals;

        public bool EnglishApplied { get; private set; }

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
            if (lookdev == null || lookdev.GetComponent<ArchReferenceGrowthEnglishIvyPass>() != null) return;
            lookdev.gameObject.AddComponent<ArchReferenceGrowthEnglishIvyPass>();
        }

        private void OnEnable() => ScheduleApply();

        private void OnDisable()
        {
            if (_applyRoutine != null) StopCoroutine(_applyRoutine);
            _applyRoutine = null;
            _refinedIvy = null;
            _refinedPetals = null;
            EnglishApplied = false;
        }

        private void OnTransformChildrenChanged()
        {
            if (isActiveAndEnabled) ScheduleApply();
        }

        private void ScheduleApply()
        {
            if (_applyRoutine != null) StopCoroutine(_applyRoutine);
            _applyRoutine = StartCoroutine(ApplyWhenLushPassIsReady());
        }

        private IEnumerator ApplyWhenLushPassIsReady()
        {
            EnglishApplied = false;
            for (int attempt = 0; attempt < 20; attempt++)
            {
                yield return null;

                ArchReferenceGrowth growth = GetComponent<ArchReferenceGrowth>();
                ArchReferenceGrowthLushPass lush = GetComponent<ArchReferenceGrowthLushPass>();
                Mesh ivy = growth?.HeroIvyMesh;
                Mesh petals = growth?.HeroFlowerPetalMesh;
                if (growth == null || lush == null || ivy == null || petals == null || !lush.LushApplied)
                    continue;

                if (_refinedIvy == ivy && _refinedPetals == petals)
                {
                    EnglishApplied = true;
                    _applyRoutine = null;
                    yield break;
                }

                Transform heroRoot = FindDetachedHeroRoot();
                Mesh centres = FindChildMesh(heroRoot, "Flower Centres");
                if (heroRoot == null || centres == null) continue;

                RefineIvy(ivy);
                RefineFlowers(petals, centres);
                TuneMaterials(heroRoot);

                _refinedIvy = ivy;
                _refinedPetals = petals;
                EnglishApplied = true;
                _applyRoutine = null;
                yield break;
            }
            _applyRoutine = null;
        }

        private static void RefineIvy(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Color[] colors = mesh.colors;
            if (vertices == null || normals == null || colors == null ||
                vertices.Length != normals.Length || vertices.Length != colors.Length) return;

            int cursor = 0;
            int leafIndex = 0;
            cursor = RefinePath(vertices, normals, colors, cursor, LeftIvyClusterCount, true, ref leafIndex);
            RefinePath(vertices, normals, colors, cursor, RightIvyClusterCount, false, ref leafIndex);

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.colors = colors;
            mesh.RecalculateBounds();
        }

        private static int RefinePath(
            Vector3[] vertices,
            Vector3[] normals,
            Color[] colors,
            int cursor,
            int clusterCount,
            bool leftPath,
            ref int leafIndex)
        {
            for (int cluster = 0; cluster < clusterCount; cluster++)
            {
                int pathStem = -1;
                if (cluster > 0)
                {
                    pathStem = cursor;
                    cursor += IvyStemVertexCount;
                }

                var starts = new int[IvyLeavesPerCluster];
                var leafStems = new int[IvyLeavesPerCluster];
                Vector3 clusterCentre = Vector3.zero;
                for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
                {
                    if (cursor + IvyLeafVertexCount > vertices.Length) return vertices.Length;
                    starts[leaf] = cursor;
                    leafStems[leaf] = -1;
                    clusterCentre += vertices[cursor];
                    cursor += IvyLeafVertexCount;
                    if ((leaf & 1) == 0)
                    {
                        leafStems[leaf] = cursor;
                        cursor += IvyStemVertexCount;
                    }
                }
                clusterCentre /= IvyLeavesPerCluster;

                Vector3 massShift = leftPath ? LeftMassShift(cluster) : RightMassShift(cluster);
                Vector3 refinedCentre = clusterCentre + massShift;

                if (pathStem >= 0)
                    CollapseQuad(vertices, pathStem, refinedCentre + new Vector3(0f, 0f, 0.04f));

                for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
                {
                    Vector3 oldCentre = vertices[starts[leaf]];
                    Vector3 local = oldCentre - clusterCentre;
                    float localScale = leftPath ? 1.08f : 0.96f;
                    Vector3 targetCentre = refinedCentre + local * localScale;
                    if (leftPath && IsDrapeLeaf(cluster, leaf))
                        targetCentre += new Vector3(-0.02f, -0.22f, -0.018f);

                    float radius = MeasureLeafRadius(vertices, starts[leaf]);
                    radius = Mathf.Clamp(radius * 1.03f, leftPath ? 0.125f : 0.105f,
                        leftPath ? 0.215f : 0.165f);
                    RewriteEnglishLeaf(vertices, normals, colors, starts[leaf], leafIndex, targetCentre, radius);

                    if (leafStems[leaf] >= 0)
                        RewriteStem(vertices, leafStems[leaf], refinedCentre, targetCentre,
                            leftPath ? 0.010f : 0.009f);
                    leafIndex++;
                }
            }
            return cursor;
        }

        private static Vector3 LeftMassShift(int cluster)
        {
            return cluster switch
            {
                0 => new Vector3(-0.10f, -0.06f, -0.010f),
                1 => new Vector3(-0.24f, -0.10f,  0.012f),
                2 => new Vector3(-0.15f,  0.00f, -0.018f),
                3 => new Vector3(-0.30f,  0.06f,  0.010f),
                4 => new Vector3(-0.05f,  0.25f, -0.012f),
                5 => new Vector3(-0.23f,  0.29f,  0.016f),
                6 => new Vector3(-0.04f,  0.36f, -0.020f),
                7 => new Vector3( 0.08f,  0.48f,  0.012f),
                8 => new Vector3( 0.16f,  0.53f, -0.018f),
                9 => new Vector3( 0.27f,  0.54f,  0.014f),
                10 => new Vector3(0.36f,  0.48f, -0.016f),
                _ => new Vector3( 0.45f,  0.38f,  0.010f),
            };
        }

        private static Vector3 RightMassShift(int cluster)
        {
            return cluster switch
            {
                0 => new Vector3( 0.02f, -0.04f,  0.004f),
                1 => new Vector3( 0.08f,  0.01f, -0.010f),
                2 => new Vector3(-0.03f,  0.08f,  0.008f),
                _ => new Vector3( 0.05f,  0.14f, -0.008f),
            };
        }

        private static bool IsDrapeLeaf(int cluster, int leaf)
        {
            return (leaf == 0 && (cluster == 1 || cluster == 4 || cluster == 7 || cluster == 9)) ||
                   (leaf == 1 && (cluster == 3 || cluster == 6 || cluster == 8));
        }

        private static float MeasureLeafRadius(Vector3[] vertices, int start)
        {
            if (start < 0 || start + IvyLeafVertexCount > vertices.Length) return 0.15f;
            Vector3 centre = vertices[start];
            float radius = 0f;
            for (int vertex = 1; vertex < IvyLeafVertexCount; vertex++)
            {
                Vector3 delta = vertices[start + vertex] - centre;
                radius = Mathf.Max(radius, new Vector2(delta.x, delta.y).magnitude);
            }
            return radius;
        }

        private static void RewriteEnglishLeaf(
            Vector3[] vertices,
            Vector3[] normals,
            Color[] colors,
            int start,
            int leafIndex,
            Vector3 targetCentre,
            float radius)
        {
            if (start < 0 || start + IvyLeafVertexCount > vertices.Length) return;

            float rotation = SignedRandom((uint)(leafIndex * 733 + 43)) * 18f * Mathf.Deg2Rad;
            float ca = Mathf.Cos(rotation);
            float sa = Mathf.Sin(rotation);
            float widthScale = Mathf.Lerp(0.92f, 1.10f, Random01((uint)(leafIndex * 307 + 71)));
            float heightScale = Mathf.Lerp(0.96f, 1.08f, Random01((uint)(leafIndex * 521 + 109)));
            Color baseColor = Color.Lerp(
                new Color(0.16f, 0.34f, 0.045f, 1f),
                new Color(0.54f, 0.70f, 0.16f, 1f),
                Random01((uint)(leafIndex * 421 + 193)));

            Vector3 centre = targetCentre + new Vector3(0f, 0f, -0.012f);
            vertices[start] = centre;
            normals[start] = new Vector3(0f, 0f, -1f);
            colors[start] = Color.Lerp(baseColor, new Color(0.82f, 0.90f, 0.42f, 1f), 0.20f);

            for (int i = 0; i < s_EnglishOutline.Length; i++)
            {
                Vector2 p = s_EnglishOutline[i] / EnglishOutlineRadius;
                float x = p.x * radius * widthScale;
                float y = p.y * radius * heightScale;
                float rx = x * ca - y * sa;
                float ry = x * sa + y * ca;
                float edgeDepth = 0.010f + 0.006f * Mathf.Abs(p.x);
                Vector3 offset = new(rx, ry, edgeDepth);
                vertices[start + 1 + i] = centre + offset;
                normals[start + 1 + i] = new Vector3(-rx * 0.55f, -ry * 0.55f, -1f).normalized;
                float shade = Mathf.Lerp(0.08f, 0.19f, Mathf.Abs(p.x));
                colors[start + 1 + i] = Color.Lerp(baseColor,
                    new Color(0.08f, 0.23f, 0.025f, 1f), shade);
            }
        }

        private static void CollapseQuad(Vector3[] vertices, int start, Vector3 point)
        {
            if (start < 0 || start + 3 >= vertices.Length) return;
            for (int i = 0; i < 4; i++) vertices[start + i] = point;
        }

        private static void RewriteStem(Vector3[] vertices, int start, Vector3 from, Vector3 to, float width)
        {
            if (start < 0 || start + 3 >= vertices.Length) return;
            Vector2 delta = new(to.x - from.x, to.y - from.y);
            if (delta.sqrMagnitude < 0.0001f)
            {
                CollapseQuad(vertices, start, from);
                return;
            }
            delta.Normalize();
            Vector2 perpendicular = new(-delta.y, delta.x);
            Vector3 half = new(perpendicular.x * width * 0.5f, perpendicular.y * width * 0.5f, 0f);
            vertices[start] = from - half;
            vertices[start + 1] = from + half;
            vertices[start + 2] = to + half;
            vertices[start + 3] = to - half;
        }

        private static void RefineFlowers(Mesh petals, Mesh centres)
        {
            Vector3[] petalVertices = petals.vertices;
            Vector3[] petalNormals = petals.normals;
            Color[] petalColors = petals.colors;
            Vector3[] centreVertices = centres.vertices;
            Vector3[] centreNormals = centres.normals;
            Color[] centreColors = centres.colors;
            int headVertexCount = FlowerPetalsPerHead * FlowerPetalVertexCount;
            int expectedHeads = FlowerClusterCount * FlowerHeadsPerCluster;
            if (petalVertices.Length != expectedHeads * headVertexCount ||
                centreVertices.Length != expectedHeads * FlowerCentreVertexCount) return;

            for (int cluster = 0; cluster < FlowerClusterCount; cluster++)
            {
                var oldHeads = new Vector3[FlowerHeadsPerCluster];
                Vector3 clusterCentre = Vector3.zero;
                for (int localHead = 0; localHead < FlowerHeadsPerCluster; localHead++)
                {
                    int head = cluster * FlowerHeadsPerCluster + localHead;
                    oldHeads[localHead] = MeasureHeadCentre(petalVertices, head * headVertexCount);
                    clusterCentre += oldHeads[localHead];
                }
                clusterCentre /= FlowerHeadsPerCluster;
                Vector3 bouquetCentre = clusterCentre + FlowerMassShift(cluster);

                for (int localHead = 0; localHead < FlowerHeadsPerCluster; localHead++)
                {
                    int head = cluster * FlowerHeadsPerCluster + localHead;
                    Vector2 local = localHead switch
                    {
                        0 => new Vector2(-0.085f, -0.045f),
                        1 => new Vector2( 0.085f, -0.025f),
                        _ => new Vector2( 0.000f,  0.092f),
                    };
                    Vector3 newHead = bouquetCentre + new Vector3(local.x, local.y, -0.004f * localHead);
                    float scale = Mathf.Lerp(1.10f, 1.22f, Random01((uint)(head * 811 + 37)));
                    Color blossom = BouquetColor(cluster, localHead);
                    RewriteFlowerHead(petalVertices, petalNormals, petalColors,
                        head * headVertexCount, oldHeads[localHead], newHead, scale, blossom);
                    RewriteFlowerCentre(centreVertices, centreNormals, centreColors,
                        head * FlowerCentreVertexCount, newHead + new Vector3(0f, 0f, -0.012f), scale);
                }
            }

            petals.vertices = petalVertices;
            petals.normals = petalNormals;
            petals.colors = petalColors;
            petals.RecalculateBounds();
            centres.vertices = centreVertices;
            centres.normals = centreNormals;
            centres.colors = centreColors;
            centres.RecalculateBounds();
        }

        private static Vector3 FlowerMassShift(int cluster)
        {
            if (cluster <= 2) return new Vector3(-0.18f, -0.02f, -0.055f);
            if (cluster <= 5) return new Vector3(-0.13f,  0.13f, -0.060f);
            if (cluster <= 8) return new Vector3( 0.02f,  0.18f, -0.060f);
            return new Vector3(-0.06f, 0.06f, -0.050f);
        }

        private static Vector3 MeasureHeadCentre(Vector3[] vertices, int headStart)
        {
            Vector3 centre = Vector3.zero;
            for (int petal = 0; petal < FlowerPetalsPerHead; petal++)
                centre += vertices[headStart + petal * FlowerPetalVertexCount];
            return centre / FlowerPetalsPerHead;
        }

        private static void RewriteFlowerHead(
            Vector3[] vertices,
            Vector3[] normals,
            Color[] colors,
            int headStart,
            Vector3 oldHead,
            Vector3 newHead,
            float scale,
            Color blossom)
        {
            int count = FlowerPetalsPerHead * FlowerPetalVertexCount;
            for (int i = 0; i < count; i++)
            {
                Vector3 local = vertices[headStart + i] - oldHead;
                local *= scale;
                local.z -= 0.006f * (i % FlowerPetalVertexCount) / (FlowerPetalVertexCount - 1f);
                vertices[headStart + i] = newHead + local;
                normals[headStart + i] = new Vector3(-local.x * 0.28f, -local.y * 0.28f, -1f).normalized;
                float centreBias = (i % FlowerPetalVertexCount) == 0 ? 0.10f : 0f;
                colors[headStart + i] = Color.Lerp(blossom, Color.white, centreBias);
            }
        }

        private static void RewriteFlowerCentre(
            Vector3[] vertices,
            Vector3[] normals,
            Color[] colors,
            int start,
            Vector3 newCentre,
            float scale)
        {
            if (start < 0 || start + FlowerCentreVertexCount > vertices.Length) return;
            Vector3 oldCentre = vertices[start];
            for (int i = 0; i < FlowerCentreVertexCount; i++)
            {
                Vector3 local = vertices[start + i] - oldCentre;
                vertices[start + i] = newCentre + local * scale;
                normals[start + i] = Vector3.back;
                colors[start + i] = i == 0
                    ? new Color(1.00f, 0.72f, 0.10f, 1f)
                    : new Color(0.94f, 0.48f, 0.045f, 1f);
            }
        }

        private static Color BouquetColor(int cluster, int localHead)
        {
            int palette = (cluster + localHead) % 5;
            return palette switch
            {
                0 => new Color(0.99f, 0.96f, 0.82f, 1f),
                1 => new Color(0.96f, 0.63f, 0.72f, 1f),
                2 => new Color(0.63f, 0.77f, 0.98f, 1f),
                3 => new Color(1.00f, 0.82f, 0.52f, 1f),
                _ => new Color(0.91f, 0.83f, 0.98f, 1f),
            };
        }

        private static Mesh FindChildMesh(Transform root, string name)
        {
            if (root == null) return null;
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            foreach (MeshFilter filter in filters)
                if (filter != null && filter.gameObject.name == name) return filter.sharedMesh;
            return null;
        }

        private static void TuneMaterials(Transform heroRoot)
        {
            MeshRenderer[] renderers = heroRoot.GetComponentsInChildren<MeshRenderer>(true);
            foreach (MeshRenderer renderer in renderers)
            {
                Material material = renderer?.sharedMaterial;
                if (material == null) continue;
                switch (renderer.gameObject.name)
                {
                    case "Lobed Ivy":
                        material.SetColor("_BaseColor", new Color(0.25f, 0.49f, 0.09f, 1f));
                        material.SetColor("_TipColor", new Color(0.68f, 0.80f, 0.25f, 1f));
                        break;
                    case "Flower Petals":
                        material.SetColor("_BaseColor", Color.white);
                        material.SetColor("_TipColor", new Color(1.00f, 0.97f, 0.92f, 1f));
                        break;
                    case "Flower Centres":
                        material.SetColor("_BaseColor", new Color(0.98f, 0.56f, 0.06f, 1f));
                        material.SetColor("_TipColor", new Color(1.00f, 0.84f, 0.18f, 1f));
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

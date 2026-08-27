using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Deterministic, art-directed growth for the ArchLookdev reference-match preset.
    ///
    /// World vegetation still uses the shared semantic renderer. The hero ivy and flowers are a
    /// deliberately different representation: this scene is a close-up reference target, and the
    /// generic card stamps could not express the individual lobed leaves and clustered blossoms in
    /// that target after several density/color/scale passes. The authored presentation remains one
    /// bounded mesh build with no per-leaf GameObjects or per-frame geometry work.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArchReferenceGrowth : MonoBehaviour
    {
        private const string FoliageShaderName = "VoxelEngine/ProceduralVegetationFoliage";
        private const int IvyLeavesPerCluster = 8;

        private static readonly IvyCluster[] s_LeftIvy =
        {
            new(-1.90f, 0.48f, 0.62f, 0xA100u),
            new(-1.72f, 1.28f, 0.64f, 0xA200u),
            new(-1.90f, 2.10f, 0.66f, 0xA300u),
            new(-1.68f, 2.94f, 0.68f, 0xA400u),
            new(-1.88f, 3.80f, 0.68f, 0xA500u),
            new(-1.64f, 4.68f, 0.70f, 0xA600u),
            new(-1.72f, 5.54f, 0.70f, 0xA700u),
            new(-1.42f, 6.28f, 0.74f, 0xB100u),
            new(-1.08f, 6.78f, 0.76f, 0xB200u),
            new(-0.70f, 7.20f, 0.74f, 0xB300u),
            new(-0.28f, 7.50f, 0.70f, 0xB400u),
            new( 0.18f, 7.68f, 0.62f, 0xB500u),
        };

        private static readonly IvyCluster[] s_RightIvy =
        {
            new(1.76f, 1.60f, 0.38f, 0xC100u),
            new(1.66f, 3.58f, 0.40f, 0xC200u),
            new(1.48f, 5.20f, 0.42f, 0xC300u),
            new(1.28f, 6.42f, 0.42f, 0xC400u),
        };

        private static readonly FlowerCluster[] s_Flowers =
        {
            new(-1.48f, 2.66f, 0.58f, 0xAF01u),
            new(-1.82f, 3.56f, 0.50f, 0xAF04u),
            new(-1.52f, 4.22f, 0.60f, 0xAF02u),
            new(-1.26f, 5.14f, 0.54f, 0xAF05u),
            new(-1.30f, 5.96f, 0.62f, 0xAF03u),
            new(-1.04f, 6.74f, 0.56f, 0xBF03u),
            new(-0.62f, 7.48f, 0.62f, 0xBF01u),
            new(-0.18f, 7.34f, 0.52f, 0xBF04u),
            new( 0.12f, 7.88f, 0.56f, 0xBF02u),
            new(-1.58f, 0.24f, 0.44f, 0xEF01u),
        };

        private readonly List<VegetationInstance> _instances = new(4);
        private IVegetationBatchRenderer _renderer;
        private GameObject _heroRoot;
        private Mesh _ivyMesh;
        private Mesh _flowerPetalMesh;
        private Mesh _flowerCentreMesh;
        private Material _ivyMaterial;
        private Material _petalMaterial;
        private Material _centreMaterial;
        private float _originalCloudOpacity;
        private bool _environmentApplied;

        public int InstanceCount => SemanticInstanceCount + HeroLeafCount + HeroFlowerHeadCount;
        public int SemanticInstanceCount => _renderer?.InstanceCount ?? 0;
        public IReadOnlyList<VegetationInstance> Instances => _instances;
        public int HeroLeafCount { get; private set; }
        public int HeroFlowerHeadCount { get; private set; }
        public int HeroDrawCallCount => _heroRoot != null ? 3 : 0;
        public int HeroVertexCount =>
            (_ivyMesh != null ? _ivyMesh.vertexCount : 0) +
            (_flowerPetalMesh != null ? _flowerPetalMesh.vertexCount : 0) +
            (_flowerCentreMesh != null ? _flowerCentreMesh.vertexCount : 0);
        public Mesh HeroIvyMesh => _ivyMesh;
        public Mesh HeroFlowerPetalMesh => _flowerPetalMesh;

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
            AttachToArchLookdev();
        }

        private static void AttachToArchLookdev()
        {
            ArchLookdev lookdev = Object.FindAnyObjectByType<ArchLookdev>();
            if (lookdev == null || lookdev.GetComponent<ArchReferenceGrowth>() != null) return;
            lookdev.gameObject.AddComponent<ArchReferenceGrowth>();
        }

        private void OnEnable()
        {
            _renderer = VegetationLifeRenderingComposition.EnsureVegetationBatchRenderer(gameObject);
            BuildGroundAccents();
            _renderer.SetInstances(_instances);
            BuildHeroPresentation();
            _originalCloudOpacity = RenderingComposition.GetCloudOpacity();
            ApplyReferenceEnvironment();
            _environmentApplied = true;
        }

        private void OnDisable()
        {
            _renderer?.Clear();
            DestroyHeroPresentation();
            if (_environmentApplied)
            {
                RenderingComposition.SetCloudOpacity(_originalCloudOpacity);
                _environmentApplied = false;
            }
        }

        private void ApplyReferenceEnvironment()
        {
            // Match the reference's clean studio-like presentation. A nearly white horizon and
            // zenith keep the foliage colors honest and let the warm masonry silhouette read clearly.
            Camera camera = GetComponent<Camera>();
            if (camera != null)
                camera.backgroundColor = new Color(0.985f, 0.985f, 0.980f, 1f);
            RenderingComposition.SetSky(
                new Color(0.990f, 0.990f, 0.985f, 1f),
                new Color(0.965f, 0.970f, 0.965f, 1f));
            RenderingComposition.SetCloudOpacity(0f);
        }

        private void BuildGroundAccents()
        {
            _instances.Clear();
            float3 up = new(0f, 1f, 0f);
            AddSemantic(VegetationKind.Fern, -2.04f, 0.02f, -0.12f, up, 0.46f, 0xE01u);
            AddSemantic(VegetationKind.Fern,  1.92f, 0.02f, -0.08f, up, 0.30f, 0xE03u);
        }

        private void BuildHeroPresentation()
        {
            DestroyHeroPresentation();

            Shader shader = Shader.Find(FoliageShaderName);
            if (shader == null)
            {
                Debug.LogError($"Arch reference foliage shader was not found: {FoliageShaderName}");
                return;
            }

            _ivyMesh = BuildIvyMesh(out int leafCount);
            _flowerPetalMesh = BuildFlowerPetalMesh(out int flowerHeadCount);
            _flowerCentreMesh = BuildFlowerCentreMesh();
            HeroLeafCount = leafCount;
            HeroFlowerHeadCount = flowerHeadCount;

            _ivyMaterial = CreateMaterial(
                shader,
                "Arch Reference Ivy",
                new Color(0.10f, 0.27f, 0.055f, 1f),
                new Color(0.48f, 0.67f, 0.16f, 1f));
            _petalMaterial = CreateMaterial(
                shader,
                "Arch Reference Flower Petals",
                new Color(0.88f, 0.54f, 0.50f, 1f),
                new Color(1.00f, 0.93f, 0.82f, 1f));
            _centreMaterial = CreateMaterial(
                shader,
                "Arch Reference Flower Centres",
                new Color(0.96f, 0.55f, 0.08f, 1f),
                new Color(1.00f, 0.84f, 0.20f, 1f));

            _heroRoot = new GameObject("Arch Reference Hero Growth");
            _heroRoot.hideFlags = HideFlags.DontSave;
            _heroRoot.layer = gameObject.layer;
            _heroRoot.transform.SetParent(transform, false);
            CreateMeshChild("Lobed Ivy", _ivyMesh, _ivyMaterial);
            CreateMeshChild("Flower Petals", _flowerPetalMesh, _petalMaterial);
            CreateMeshChild("Flower Centres", _flowerCentreMesh, _centreMaterial);
        }

        private void CreateMeshChild(string name, Mesh mesh, Material material)
        {
            var child = new GameObject(name);
            child.hideFlags = HideFlags.DontSave;
            child.layer = gameObject.layer;
            child.transform.SetParent(_heroRoot.transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            child.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Material CreateMaterial(
            Shader shader, string name, Color baseColor, Color tipColor)
        {
            var material = new Material(shader)
            {
                name = name,
                enableInstancing = false,
                hideFlags = HideFlags.DontSave,
            };
            material.SetColor("_BaseColor", baseColor);
            material.SetColor("_TipColor", tipColor);
            material.SetColor("_EmissionColor", Color.black);
            material.SetFloat("_EmissionStrength", 0f);
            material.SetFloat("_Shape", 4f);
            material.SetFloat("_WindStrength", 0f);
            material.SetFloat("_Cutoff", 0f);
            return material;
        }

        private static Mesh BuildIvyMesh(out int leafCount)
        {
            var vertices = new List<Vector3>(2200);
            var normals = new List<Vector3>(2200);
            var uv = new List<Vector2>(2200);
            var colors = new List<Color>(2200);
            var triangles = new List<int>(4200);
            leafCount = 0;

            AddIvyPath(s_LeftIvy, vertices, normals, uv, colors, triangles, ref leafCount);
            AddIvyPath(s_RightIvy, vertices, normals, uv, colors, triangles, ref leafCount);

            return BuildMesh("Arch Reference Lobed Ivy", vertices, normals, uv, colors, triangles);
        }

        private static void AddIvyPath(
            IvyCluster[] clusters,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uv,
            List<Color> colors,
            List<int> triangles,
            ref int leafCount)
        {
            const float z = -0.105f;
            for (int i = 0; i < clusters.Length; i++)
            {
                IvyCluster cluster = clusters[i];
                Vector3 centre = new(cluster.X, cluster.Y, z - 0.003f * (i % 4));
                if (i > 0)
                {
                    IvyCluster previous = clusters[i - 1];
                    AddStem(
                        vertices, normals, uv, colors, triangles,
                        new Vector3(previous.X, previous.Y, z + 0.012f),
                        new Vector3(cluster.X, cluster.Y, z + 0.012f),
                        Mathf.Lerp(0.026f, 0.040f, cluster.Scale));
                }

                for (int leaf = 0; leaf < IvyLeavesPerCluster; leaf++)
                {
                    uint seed = cluster.Seed + (uint)(leaf * 97 + 11);
                    float angle = leaf * 137.50776f + SignedRandom(seed ^ 0xB5297A4Du) * 24f;
                    float radians = angle * Mathf.Deg2Rad;
                    float radial = cluster.Scale * Mathf.Lerp(0.12f, 0.46f, Random01(seed ^ 0x68E31DA4u));
                    float verticalBias = cluster.Scale * Mathf.Lerp(-0.12f, 0.18f, Random01(seed ^ 0x1B56C4E9u));
                    Vector3 leafCentre = centre + new Vector3(
                        Mathf.Cos(radians) * radial,
                        Mathf.Sin(radians) * radial + verticalBias,
                        -0.004f * (leaf % 3));
                    float leafScale = cluster.Scale * Mathf.Lerp(0.38f, 0.58f, Random01(seed ^ 0x9E3779B9u));
                    float leafRotation = angle + SignedRandom(seed ^ 0x85EBCA6Bu) * 34f;
                    Color color = Color.Lerp(
                        new Color(0.12f, 0.39f, 0.065f, 1f),
                        new Color(0.52f, 0.68f, 0.14f, 1f),
                        Random01(seed ^ 0xC2B2AE35u));
                    AddLobedLeaf(
                        vertices, normals, uv, colors, triangles,
                        leafCentre, leafScale, leafRotation, color);
                    if ((leaf & 1) == 0)
                    {
                        AddStem(
                            vertices, normals, uv, colors, triangles,
                            centre + new Vector3(0f, 0f, 0.010f),
                            leafCentre + new Vector3(0f, 0f, 0.010f),
                            0.018f);
                    }
                    leafCount++;
                }
            }
        }

        private static void AddLobedLeaf(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uv,
            List<Color> colors,
            List<int> triangles,
            Vector3 centre,
            float scale,
            float rotationDegrees,
            Color color)
        {
            // Twelve perimeter points make a broad, recognisably ivy-like five-lobed silhouette.
            // A simple fan keeps the mesh deterministic and cheap while avoiding the repeated card
            // outline that the captured hero view rejected.
            Vector2[] shape =
            {
                new( 0.00f, -0.46f),
                new(-0.20f, -0.18f),
                new(-0.52f, -0.04f),
                new(-0.25f,  0.12f),
                new(-0.43f,  0.34f),
                new(-0.12f,  0.28f),
                new( 0.00f,  0.54f),
                new( 0.12f,  0.28f),
                new( 0.43f,  0.34f),
                new( 0.25f,  0.12f),
                new( 0.52f, -0.04f),
                new( 0.20f, -0.18f),
            };

            int start = vertices.Count;
            vertices.Add(centre);
            normals.Add(Vector3.back);
            uv.Add(new Vector2(0.5f, 0.5f));
            colors.Add(color);

            float a = rotationDegrees * Mathf.Deg2Rad;
            float ca = Mathf.Cos(a);
            float sa = Mathf.Sin(a);
            for (int i = 0; i < shape.Length; i++)
            {
                Vector2 p = shape[i];
                float x = (p.x * ca - p.y * sa) * scale;
                float y = (p.x * sa + p.y * ca) * scale * 1.08f;
                vertices.Add(centre + new Vector3(x, y, 0f));
                normals.Add(Vector3.back);
                uv.Add(new Vector2(0.5f, 0.5f));
                colors.Add(color);
            }

            for (int i = 0; i < shape.Length; i++)
            {
                triangles.Add(start);
                triangles.Add(start + 1 + i);
                triangles.Add(start + 1 + ((i + 1) % shape.Length));
            }
        }

        private static void AddStem(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uv,
            List<Color> colors,
            List<int> triangles,
            Vector3 from,
            Vector3 to,
            float width)
        {
            Vector2 delta = new(to.x - from.x, to.y - from.y);
            if (delta.sqrMagnitude < 0.0001f) return;
            delta.Normalize();
            Vector2 perpendicular = new(-delta.y, delta.x);
            Vector3 half = new(perpendicular.x * width * 0.5f, perpendicular.y * width * 0.5f, 0f);
            int start = vertices.Count;
            Color stemColor = new(0.055f, 0.20f, 0.035f, 1f);
            vertices.Add(from - half);
            vertices.Add(from + half);
            vertices.Add(to + half);
            vertices.Add(to - half);
            for (int i = 0; i < 4; i++)
            {
                normals.Add(Vector3.back);
                uv.Add(new Vector2(0.5f, 0.5f));
                colors.Add(stemColor);
            }
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        }

        private static Mesh BuildFlowerPetalMesh(out int flowerHeadCount)
        {
            var vertices = new List<Vector3>(1500);
            var normals = new List<Vector3>(1500);
            var uv = new List<Vector2>(1500);
            var colors = new List<Color>(1500);
            var triangles = new List<int>(2700);
            flowerHeadCount = 0;

            foreach (FlowerCluster cluster in s_Flowers)
            {
                for (int head = 0; head < 3; head++)
                {
                    uint seed = cluster.Seed + (uint)(head * 131 + 7);
                    Vector2 offset = head switch
                    {
                        0 => new Vector2(-0.15f, -0.04f),
                        1 => new Vector2( 0.13f,  0.04f),
                        _ => new Vector2( 0.01f,  0.20f),
                    } * cluster.Scale;
                    Vector3 centre = new(
                        cluster.X + offset.x,
                        cluster.Y + offset.y,
                        -0.145f - head * 0.004f);
                    float radius = cluster.Scale * Mathf.Lerp(0.28f, 0.35f, Random01(seed));
                    float rotation = Random01(seed ^ 0x9E3779B9u) * 60f;
                    AddFlowerHeadPetals(vertices, normals, uv, colors, triangles, centre, radius, rotation, seed);
                    flowerHeadCount++;
                }
            }

            return BuildMesh("Arch Reference Flower Petals", vertices, normals, uv, colors, triangles);
        }

        private static Mesh BuildFlowerCentreMesh()
        {
            var vertices = new List<Vector3>(320);
            var normals = new List<Vector3>(320);
            var uv = new List<Vector2>(320);
            var colors = new List<Color>(320);
            var triangles = new List<int>(640);

            foreach (FlowerCluster cluster in s_Flowers)
            {
                for (int head = 0; head < 3; head++)
                {
                    uint seed = cluster.Seed + (uint)(head * 131 + 7);
                    Vector2 offset = head switch
                    {
                        0 => new Vector2(-0.15f, -0.04f),
                        1 => new Vector2( 0.13f,  0.04f),
                        _ => new Vector2( 0.01f,  0.20f),
                    } * cluster.Scale;
                    Vector3 centre = new(
                        cluster.X + offset.x,
                        cluster.Y + offset.y,
                        -0.158f - head * 0.004f);
                    float radius = cluster.Scale * Mathf.Lerp(0.28f, 0.35f, Random01(seed));
                    AddDisc(vertices, normals, uv, colors, triangles, centre, radius * 0.27f, 8,
                        new Color(1.0f, 0.72f, 0.08f, 1f));
                }
            }

            return BuildMesh("Arch Reference Flower Centres", vertices, normals, uv, colors, triangles);
        }

        private static void AddFlowerHeadPetals(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uv,
            List<Color> colors,
            List<int> triangles,
            Vector3 centre,
            float radius,
            float rotationDegrees,
            uint seed)
        {
            const int petalCount = 6;
            for (int petal = 0; petal < petalCount; petal++)
            {
                float angle = rotationDegrees + petal * (360f / petalCount);
                float radians = angle * Mathf.Deg2Rad;
                Vector2 direction = new(Mathf.Cos(radians), Mathf.Sin(radians));
                Vector3 petalCentre = centre + new Vector3(direction.x, direction.y, 0f) * (radius * 0.48f);
                float length = radius * Mathf.Lerp(0.92f, 1.10f, Random01(seed + (uint)petal * 17u));
                float width = radius * Mathf.Lerp(0.42f, 0.54f, Random01(seed + (uint)petal * 31u));
                Color color = Color.Lerp(
                    new Color(0.92f, 0.53f, 0.50f, 1f),
                    new Color(1.00f, 0.91f, 0.80f, 1f),
                    Random01(seed + (uint)petal * 47u));
                AddEllipse(vertices, normals, uv, colors, triangles, petalCentre, direction, length, width, color);
            }
        }

        private static void AddEllipse(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uv,
            List<Color> colors,
            List<int> triangles,
            Vector3 centre,
            Vector2 direction,
            float length,
            float width,
            Color color)
        {
            const int segments = 6;
            Vector2 side = new(-direction.y, direction.x);
            int start = vertices.Count;
            vertices.Add(centre);
            normals.Add(Vector3.back);
            uv.Add(new Vector2(0.5f, 0.5f));
            colors.Add(color);

            for (int i = 0; i < segments; i++)
            {
                float angle = i * (Mathf.PI * 2f / segments);
                Vector2 offset = direction * (Mathf.Cos(angle) * length * 0.5f)
                               + side * (Mathf.Sin(angle) * width * 0.5f);
                vertices.Add(centre + new Vector3(offset.x, offset.y, 0f));
                normals.Add(Vector3.back);
                uv.Add(new Vector2(0.5f, 0.5f));
                colors.Add(color);
            }
            for (int i = 0; i < segments; i++)
            {
                triangles.Add(start);
                triangles.Add(start + 1 + i);
                triangles.Add(start + 1 + ((i + 1) % segments));
            }
        }

        private static void AddDisc(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uv,
            List<Color> colors,
            List<int> triangles,
            Vector3 centre,
            float radius,
            int segments,
            Color color)
        {
            int start = vertices.Count;
            vertices.Add(centre);
            normals.Add(Vector3.back);
            uv.Add(new Vector2(0.5f, 0.5f));
            colors.Add(color);
            for (int i = 0; i < segments; i++)
            {
                float angle = i * (Mathf.PI * 2f / segments);
                vertices.Add(centre + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
                normals.Add(Vector3.back);
                uv.Add(new Vector2(0.5f, 0.5f));
                colors.Add(color);
            }
            for (int i = 0; i < segments; i++)
            {
                triangles.Add(start);
                triangles.Add(start + 1 + i);
                triangles.Add(start + 1 + ((i + 1) % segments));
            }
        }

        private static Mesh BuildMesh(
            string name,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uv,
            List<Color> colors,
            List<int> triangles)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        private void DestroyHeroPresentation()
        {
            HeroLeafCount = 0;
            HeroFlowerHeadCount = 0;
            DestroyGenerated(_heroRoot);
            DestroyGenerated(_ivyMesh);
            DestroyGenerated(_flowerPetalMesh);
            DestroyGenerated(_flowerCentreMesh);
            DestroyGenerated(_ivyMaterial);
            DestroyGenerated(_petalMaterial);
            DestroyGenerated(_centreMaterial);
            _heroRoot = null;
            _ivyMesh = null;
            _flowerPetalMesh = null;
            _flowerCentreMesh = null;
            _ivyMaterial = null;
            _petalMaterial = null;
            _centreMaterial = null;
        }

        private static void DestroyGenerated(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Object.Destroy(value);
            else Object.DestroyImmediate(value);
        }

        private void AddSemantic(
            VegetationKind kind,
            float x,
            float y,
            float z,
            float3 normal,
            float scale,
            uint seed)
        {
            _instances.Add(new VegetationInstance
            {
                PositionMetres = new float3(x, y, z),
                SurfaceNormal = normal,
                Kind = kind,
                Seed = seed,
                Scale = scale,
            });
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

        private readonly struct IvyCluster
        {
            public readonly float X;
            public readonly float Y;
            public readonly float Scale;
            public readonly uint Seed;

            public IvyCluster(float x, float y, float scale, uint seed)
            {
                X = x;
                Y = y;
                Scale = scale;
                Seed = seed;
            }
        }

        private readonly struct FlowerCluster
        {
            public readonly float X;
            public readonly float Y;
            public readonly float Scale;
            public readonly uint Seed;

            public FlowerCluster(float x, float y, float scale, uint seed)
            {
                X = x;
                Y = y;
                Scale = scale;
                Seed = seed;
            }
        }
    }
}

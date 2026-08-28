using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Worldbuilding-gallery-only meadow presentation. Grass geometry is packed once when the
    /// gallery population is built; every frame after that only updates four shader uniforms and
    /// submits the already-built mesh. Other scenes and vegetation kinds keep the normal renderer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldbuildingGalleryMeadowRenderer : MonoBehaviour
    {
        public const string ShaderName = "VoxelEngine/WorldbuildingGalleryMeadowGrass";
        private const float PushRadius = 1.05f;
        private const int RibbonSegments = 4;

        private static readonly Color Dark = new(0.21f, 0.44f, 0.11f, 1f);
        private static readonly Color Medium = new(0.34f, 0.62f, 0.18f, 1f);
        private static readonly Color Fresh = new(0.49f, 0.76f, 0.25f, 1f);
        private static readonly Color Sunny = new(0.70f, 0.90f, 0.40f, 1f);

        private Mesh _mesh;
        private Material _material;
        private Transform _player;

        public int BladeCount { get; private set; }
        public int VertexCount => _mesh != null ? _mesh.vertexCount : 0;
        public int TriangleCount => _mesh != null ? (int)(_mesh.GetIndexCount(0) / 3) : 0;

        public void Publish(
            IReadOnlyList<VegetationInstance> vegetation,
            IVegetationBatchRenderer fallbackRenderer)
        {
            var fallback = new List<VegetationInstance>(vegetation?.Count ?? 0);
            var meadow = new List<VegetationInstance>(vegetation?.Count ?? 0);

            if (vegetation != null)
            {
                for (int i = 0; i < vegetation.Count; i++)
                {
                    VegetationInstance instance = vegetation[i];
                    if (IsMeadowTuft(instance.Kind)) meadow.Add(instance);
                    else fallback.Add(instance);
                }
            }

            fallbackRenderer?.SetInstances(fallback);
            Rebuild(meadow);
        }

        public void Rebuild(IReadOnlyList<VegetationInstance> meadow)
        {
            if (_mesh != null)
            {
                Destroy(_mesh);
                _mesh = null;
            }

            BladeCount = 0;
            _mesh = BuildPackedMeadow(meadow, out int blades);
            BladeCount = blades;

            if (_material == null)
            {
                Shader shader = Shader.Find(ShaderName);
                if (shader == null)
                {
                    Debug.LogError($"Worldbuilding gallery meadow shader was not found: {ShaderName}");
                    return;
                }
                _material = new Material(shader)
                {
                    name = "Worldbuilding Gallery Meadow Grass",
                    hideFlags = HideFlags.DontSave,
                };
            }
        }

        private void LateUpdate()
        {
            if (_mesh == null || _material == null || BladeCount == 0) return;

            Camera camera = Camera.main;
            Vector3 cameraRight = camera != null ? camera.transform.right : Vector3.right;
            cameraRight.y = 0f;
            if (cameraRight.sqrMagnitude < 0.0001f) cameraRight = Vector3.right;
            cameraRight.Normalize();

            if (_player == null)
            {
#pragma warning disable CS0618
                CharacterController controller = FindObjectOfType<CharacterController>();
#pragma warning restore CS0618
                if (controller != null) _player = controller.transform;
            }

            Vector3 player = _player != null
                ? _player.position
                : new Vector3(100000f, 100000f, 100000f);

            _material.SetFloat("_GrassTime", Time.time);
            _material.SetVector("_GrassPlayerPositionWS", new Vector4(player.x, player.y, player.z, 1f));
            _material.SetFloat("_GrassPushRadius", PushRadius);
            _material.SetVector("_GrassCameraRightWS", new Vector4(cameraRight.x, 0f, cameraRight.z, 0f));
            Graphics.DrawMesh(_mesh, Matrix4x4.identity, _material, 0, camera, 0, null, false, false, false);
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
        }

        internal static bool IsMeadowTuft(VegetationKind kind) =>
            kind == VegetationKind.Grass ||
            kind == VegetationKind.Clover ||
            kind == VegetationKind.Weed ||
            kind == VegetationKind.Nettle;

        internal static Mesh BuildPackedMeadow(
            IReadOnlyList<VegetationInstance> meadow,
            out int bladeCount)
        {
            var vertices = new List<Vector3>(4096);
            var colors = new List<Color>(4096);
            var uv0 = new List<Vector2>(4096);
            var uv1 = new List<Vector2>(4096);
            var uv2 = new List<Vector2>(4096);
            var uv3 = new List<Vector2>(4096);
            var triangles = new List<int>(8192);
            bladeCount = 0;

            if (meadow != null)
            {
                for (int i = 0; i < meadow.Count; i++)
                {
                    VegetationInstance instance = meadow[i];
                    Vector3 anchor = new(
                        instance.PositionMetres.x,
                        instance.PositionMetres.y + 0.015f,
                        instance.PositionMetres.z);

                    float coverage = CoverageField(anchor.x, anchor.z);
                    float dense = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.30f, 0.72f, coverage));
                    int bladesHere = coverage < 0.30f ? 0 : Mathf.RoundToInt(Mathf.Lerp(3f, 13f, dense));

                    for (int blade = 0; blade < bladesHere; blade++)
                    {
                        uint seed = Hash(instance.Seed, (uint)(blade + 1));
                        float angle = Random01(seed) * Mathf.PI * 2f;
                        float radius = Mathf.Sqrt(Random01(seed ^ 0x9E3779B9u)) * 0.56f;
                        Vector3 root = anchor + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                        float colourRegion = ColourField(root.x, root.z);
                        float groundShade = GroundShadeField(root.x, root.z);
                        Color regional = Palette(colourRegion);
                        regional *= Mathf.Lerp(0.86f, 1.08f, groundShade);
                        regional.a = 1f;

                        float scale = Mathf.Max(0.35f, instance.Scale);
                        float height = Mathf.Lerp(0.26f, 0.58f, Random01(seed ^ 0x85EBCA6Bu)) * scale;
                        float halfWidth = Mathf.Lerp(0.028f, 0.055f, Random01(seed ^ 0xC2B2AE35u)) * scale;
                        float lean = Mathf.Lerp(-0.075f, 0.075f, Random01(seed ^ 0x27D4EB2Fu));
                        float phase = Random01(seed ^ 0x165667B1u) * Mathf.PI * 2f;

                        AddRibbon(vertices, colors, uv0, uv1, uv2, uv3, triangles,
                            root, height, halfWidth, lean, phase, regional);
                        bladeCount++;
                    }
                }
            }

            var mesh = new Mesh
            {
                name = "Worldbuilding Gallery Packed Meadow",
                hideFlags = HideFlags.DontSave,
                indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16,
            };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1);
            mesh.SetUVs(2, uv2);
            mesh.SetUVs(3, uv3);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateBounds();
            Bounds bounds = mesh.bounds;
            bounds.Expand(new Vector3(1.5f, 0.5f, 1.5f));
            mesh.bounds = bounds;
            return mesh;
        }

        private static void AddRibbon(
            List<Vector3> vertices,
            List<Color> colors,
            List<Vector2> uv0,
            List<Vector2> uv1,
            List<Vector2> uv2,
            List<Vector2> uv3,
            List<int> triangles,
            Vector3 root,
            float height,
            float halfWidth,
            float lean,
            float phase,
            Color regional)
        {
            int start = vertices.Count;
            for (int row = 0; row <= RibbonSegments; row++)
            {
                float t = row / (float)RibbonSegments;
                float taper = 1f - t;
                float centre = lean * t * t;
                float localY = height * t;
                Color rootColor = regional * 0.72f; rootColor.a = 1f;
                Color tipColor = regional * 1.08f; tipColor.a = 1f;
                Color color = Color.Lerp(rootColor, tipColor, t);

                for (int side = -1; side <= 1; side += 2)
                {
                    float lateral = centre + side * halfWidth * taper;
                    vertices.Add(root + new Vector3(lateral, localY, 0f));
                    colors.Add(color);
                    uv0.Add(new Vector2(root.x, root.z));
                    uv1.Add(new Vector2(root.y, lateral));
                    uv2.Add(new Vector2(localY, t));
                    uv3.Add(new Vector2(phase, 0f));
                }
            }

            for (int segment = 0; segment < RibbonSegments; segment++)
            {
                int a = start + segment * 2;
                int b = a + 1;
                int c = a + 2;
                int d = a + 3;
                triangles.Add(a); triangles.Add(c); triangles.Add(b);
                triangles.Add(b); triangles.Add(c); triangles.Add(d);
            }
        }

        internal static float CoverageField(float x, float z) =>
            Fbm(x * 0.042f + 13.7f, z * 0.042f - 21.4f);

        internal static float ColourField(float x, float z) =>
            Fbm(x * 0.031f - 41.2f, z * 0.031f + 7.9f);

        internal static float GroundShadeField(float x, float z) =>
            Fbm(x * 0.024f + 64.1f, z * 0.024f + 52.6f);

        private static float Fbm(float x, float z)
        {
            float a = Mathf.PerlinNoise(x, z);
            float b = Mathf.PerlinNoise(x * 2.03f + 17.1f, z * 2.03f - 9.4f);
            float c = Mathf.PerlinNoise(x * 4.09f - 3.7f, z * 4.09f + 28.6f);
            return a * 0.58f + b * 0.29f + c * 0.13f;
        }

        private static Color Palette(float value)
        {
            if (value < 0.33f) return Color.Lerp(Dark, Medium, value / 0.33f);
            if (value < 0.66f) return Color.Lerp(Medium, Fresh, (value - 0.33f) / 0.33f);
            return Color.Lerp(Fresh, Sunny, (value - 0.66f) / 0.34f);
        }

        private static uint Hash(uint seed, uint value)
        {
            uint h = seed == 0u ? 0x9E3779B9u : seed;
            h ^= value + 0x85EBCA6Bu + (h << 6) + (h >> 2);
            h ^= h >> 16; h *= 0x7FEB352Du;
            h ^= h >> 15; h *= 0x846CA68Bu;
            h ^= h >> 16;
            return h == 0u ? 1u : h;
        }

        private static float Random01(uint seed) => (Hash(seed, 0xA341316Cu) & 0x00FFFFFFu) / 16777216f;
    }
}

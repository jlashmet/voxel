using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Rendering.Runtime.Vegetation
{
    /// <summary>
    /// Construction-time presentation batch for semantic grass. Grass instances are packed into
    /// spatial meshes so regional colour remains coherent in world space while per-instance density,
    /// wind, camera-facing reconstruction, and character push remain deterministic presentation.
    /// </summary>
    internal sealed class ProceduralGrassBatch : IDisposable
    {
        internal const float ChunkSizeMetres = 32f;
        private const int RibbonSegments = 4;

        private static readonly Color Dark = new Color(0.21f, 0.44f, 0.11f, 1f);
        private static readonly Color Medium = new Color(0.34f, 0.62f, 0.18f, 1f);
        private static readonly Color Fresh = new Color(0.49f, 0.76f, 0.25f, 1f);
        private static readonly Color Sunny = new Color(0.70f, 0.90f, 0.40f, 1f);
        private static readonly int GrassTimeId = Shader.PropertyToID("_GrassTime");
        private static readonly Func<float> DefaultGrassTimeSource = ReadDefaultGrassTime;

        private readonly Dictionary<Vector2Int, List<VegetationInstance>> _pending =
            new Dictionary<Vector2Int, List<VegetationInstance>>();
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly MaterialPropertyBlock _drawProperties = new MaterialPropertyBlock();
        private readonly Func<float> _grassTimeSource;

        internal int ChunkCount => _meshes.Count;
        internal int BladeCount { get; private set; }
        internal int VertexCount { get; private set; }
        internal int TriangleCount { get; private set; }
        internal float LastSubmittedGrassTime => _drawProperties.GetFloat(GrassTimeId);

        internal ProceduralGrassBatch() : this(DefaultGrassTimeSource)
        {
        }

        internal ProceduralGrassBatch(Func<float> grassTimeSource)
        {
            _grassTimeSource = grassTimeSource ?? throw new ArgumentNullException(nameof(grassTimeSource));
        }

        internal static bool IsGrass(VegetationKind kind) =>
            kind == VegetationKind.Grass || kind == VegetationKind.Nettle;

        internal void Add(in VegetationInstance instance)
        {
            Vector2Int key = ChunkKey(instance.PositionMetres.x, instance.PositionMetres.z);
            if (!_pending.TryGetValue(key, out List<VegetationInstance> instances))
            {
                instances = new List<VegetationInstance>();
                _pending.Add(key, instances);
            }
            instances.Add(instance);
        }

        internal void Rebuild()
        {
            ReleaseMeshes();
            BladeCount = 0;
            VertexCount = 0;
            TriangleCount = 0;
            if (_pending.Count == 0) return;

            var keys = new List<Vector2Int>(_pending.Keys);
            keys.Sort((left, right) =>
            {
                int x = left.x.CompareTo(right.x);
                return x != 0 ? x : left.y.CompareTo(right.y);
            });

            for (int i = 0; i < keys.Count; i++)
            {
                Mesh mesh = BuildChunk(_pending[keys[i]], out int blades);
                if (mesh == null) continue;
                _meshes.Add(mesh);
                BladeCount += blades;
                VertexCount += mesh.vertexCount;
                TriangleCount += (int)(mesh.GetIndexCount(0) / 3);
            }
        }

        internal void Draw(Material material)
        {
            if (material == null || _meshes.Count == 0) return;

            // Graphics.DrawMesh queues work for later rendering. Per-draw state must therefore be
            // snapshotted in a property block instead of relying on mutable shared material state.
            // Ambient wind uses unscaled presentation time so dialogue/gameplay pausing cannot
            // freeze the meadow. The block is retained by this batch: no per-frame allocation.
            _drawProperties.Clear();
            _drawProperties.SetFloat(GrassTimeId, _grassTimeSource());

            for (int i = 0; i < _meshes.Count; i++)
                Graphics.DrawMesh(
                    _meshes[i], Matrix4x4.identity, material, 0, null, 0, _drawProperties);
        }

        internal void Clear()
        {
            foreach (KeyValuePair<Vector2Int, List<VegetationInstance>> pair in _pending)
                pair.Value.Clear();
            _pending.Clear();
            ReleaseMeshes();
            BladeCount = 0;
            VertexCount = 0;
            TriangleCount = 0;
        }

        public void Dispose() => Clear();

        private void ReleaseMeshes()
        {
            for (int i = 0; i < _meshes.Count; i++)
            {
                Mesh mesh = _meshes[i];
                if (mesh == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(mesh);
                else UnityEngine.Object.DestroyImmediate(mesh);
            }
            _meshes.Clear();
        }

        private static Vector2Int ChunkKey(float x, float z) => new Vector2Int(
            Mathf.FloorToInt(x / ChunkSizeMetres),
            Mathf.FloorToInt(z / ChunkSizeMetres));

        private static float ReadDefaultGrassTime() => Time.unscaledTime;

        private static Mesh BuildChunk(IReadOnlyList<VegetationInstance> grass, out int bladeCount)
        {
            var vertices = new List<Vector3>(4096);
            var colors = new List<Color>(4096);
            var uv0 = new List<Vector2>(4096);
            var uv1 = new List<Vector2>(4096);
            var uv2 = new List<Vector2>(4096);
            var uv3 = new List<Vector2>(4096);
            var triangles = new List<int>(8192);
            bladeCount = 0;

            if (grass != null)
            {
                for (int i = 0; i < grass.Count; i++)
                    AddInstance(grass[i], vertices, colors, uv0, uv1, uv2, uv3, triangles, ref bladeCount);
            }

            if (vertices.Count == 0) return null;

            var mesh = new Mesh
            {
                name = "Procedural Grass Packed Chunk",
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
            bounds.Expand(new Vector3(0.8f, 0.4f, 0.8f));
            mesh.bounds = bounds;
            return mesh;
        }

        private static void AddInstance(
            in VegetationInstance instance,
            List<Vector3> vertices,
            List<Color> colors,
            List<Vector2> uv0,
            List<Vector2> uv1,
            List<Vector2> uv2,
            List<Vector2> uv3,
            List<int> triangles,
            ref int bladeCount)
        {
            Vector3 normal = new Vector3(
                instance.SurfaceNormal.x,
                instance.SurfaceNormal.y,
                instance.SurfaceNormal.z);
            if (normal.sqrMagnitude < 0.0001f) normal = Vector3.up;
            normal.Normalize();

            Vector3 tangent = Vector3.Cross(normal, Vector3.forward);
            if (tangent.sqrMagnitude < 0.001f) tangent = Vector3.Cross(normal, Vector3.right);
            if (tangent.sqrMagnitude < 0.001f) tangent = Vector3.right;
            tangent.Normalize();
            Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;

            Vector3 anchor = new Vector3(
                instance.PositionMetres.x,
                instance.PositionMetres.y,
                instance.PositionMetres.z) + normal * 0.015f;

            // VegetationPlacement is authoritative about whether grass exists. Once a semantic
            // Grass instance reaches presentation, always render it; the seed may vary only the
            // local blade field inside that placement.
            int bladesHere = ProceduralGrassPresentation.BladeCountForSeed(instance.Seed);

            for (int blade = 0; blade < bladesHere; blade++)
            {
                uint seed = Hash(instance.Seed, (uint)(blade + 1));
                float angle = Random01(seed) * Mathf.PI * 2f;
                float radius = Mathf.Sqrt(Random01(seed ^ 0x9E3779B9u)) * 0.60f;
                Vector3 root = anchor
                             + tangent * (Mathf.Cos(angle) * radius)
                             + bitangent * (Mathf.Sin(angle) * radius);

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
                Color rootColor = regional * 0.72f;
                Color tipColor = regional * 1.08f;
                rootColor.a = 1f;
                tipColor.a = 1f;
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

        internal static int BladeCountForSeed(uint seed) =>
            ProceduralGrassPresentation.BladeCountForSeed(seed);

        // Retained as a regression oracle for the old renderer-owned macro coverage decision. It is
        // intentionally not consulted by production geometry generation.
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

        private static float Random01(uint seed) =>
            (Hash(seed, 0xA341316Cu) & 0x00FFFFFFu) / 16777216f;
    }
}

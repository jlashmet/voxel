using System;
using System.Collections.Generic;
using UnityEngine;
using VoxelEngine.Rendering.Api;

namespace VoxelEngine.Rendering.Runtime.FarWorld
{
    /// <summary>
    /// Batched renderer for semantic far structures. Proxy meshes are immutable and cached by
    /// semantic proxy key+tier; structure instances are matrices only, never persistent GameObjects.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProceduralFarStructureRenderer : MonoBehaviour, IFarStructureRenderer
    {
        private const int MaxInstancesPerDraw = 1023;

        private readonly Dictionary<BatchKey, List<Matrix4x4>> _batches =
            new Dictionary<BatchKey, List<Matrix4x4>>();
        private readonly Dictionary<BatchKey, Mesh> _meshCache =
            new Dictionary<BatchKey, Mesh>();
        private readonly Dictionary<string, Material> _materialCache =
            new Dictionary<string, Material>(StringComparer.Ordinal);
        private readonly Matrix4x4[] _scratch = new Matrix4x4[MaxInstancesPerDraw];
        private int _instanceCount;

        public int InstanceCount => _instanceCount;
        public int CachedMeshCount => _meshCache.Count;
        public int PersistentInstanceObjectCount => 0;

        public void SetInstances(IReadOnlyList<FarStructureInstance> instances)
        {
            ClearBatches();
            if (instances == null) return;

            for (int i = 0; i < instances.Count; i++)
            {
                FarStructureInstance instance = instances[i];
                if (instance.Tier == FarStructureTier.Culled) continue;

                var key = new BatchKey(instance.ProxyKey, instance.StyleKey, instance.Tier);
                if (!_batches.TryGetValue(key, out List<Matrix4x4> matrices))
                {
                    matrices = new List<Matrix4x4>();
                    _batches.Add(key, matrices);
                }

                matrices.Add(ToMatrix(instance));
                _instanceCount++;
            }
        }

        public void Clear()
        {
            ClearBatches();
        }

        public void DrawNow()
        {
            foreach (KeyValuePair<BatchKey, List<Matrix4x4>> pair in _batches)
            {
                List<Matrix4x4> matrices = pair.Value;
                if (matrices.Count == 0) continue;

                Mesh mesh = MeshFor(pair.Key.ProxyKey, pair.Key.Tier);
                Material material = MaterialFor(pair.Key.StyleKey);
                if (mesh == null || material == null) continue;

                for (int start = 0; start < matrices.Count; start += MaxInstancesPerDraw)
                {
                    int count = Mathf.Min(MaxInstancesPerDraw, matrices.Count - start);
                    for (int i = 0; i < count; i++)
                        _scratch[i] = matrices[start + i];
                    Graphics.DrawMeshInstanced(mesh, 0, material, _scratch, count);
                }
            }
        }

        private void LateUpdate()
        {
            DrawNow();
        }

        private void OnDestroy()
        {
            foreach (Mesh mesh in _meshCache.Values)
                if (mesh != null) Destroy(mesh);
            _meshCache.Clear();

            foreach (Material material in _materialCache.Values)
                if (material != null) Destroy(material);
            _materialCache.Clear();
        }

        public string BatchKeyFor(in FarStructureInstance instance) =>
            new BatchKey(instance.ProxyKey, instance.StyleKey, instance.Tier).ToString();

        private Mesh MeshFor(string proxyKey, FarStructureTier tier)
        {
            var key = new BatchKey(proxyKey, string.Empty, tier);
            if (_meshCache.TryGetValue(key, out Mesh existing)) return existing;

            Mesh mesh = BuildProxyMesh(proxyKey, tier);
            mesh.name = $"FarProxy-{proxyKey}-{tier}";
            _meshCache.Add(key, mesh);
            return mesh;
        }

        private Material MaterialFor(string styleKey)
        {
            styleKey = styleKey ?? string.Empty;
            if (_materialCache.TryGetValue(styleKey, out Material existing)) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) return null;

            var material = new Material(shader)
            {
                name = $"FarStructure-{styleKey}",
                enableInstancing = true
            };
            uint hash = StableHash(styleKey);
            material.color = new Color(
                0.35f + ((hash >> 0) & 0xFF) / 255f * 0.25f,
                0.32f + ((hash >> 8) & 0xFF) / 255f * 0.22f,
                0.28f + ((hash >> 16) & 0xFF) / 255f * 0.20f,
                1f);
            _materialCache.Add(styleKey, material);
            return material;
        }

        private static Matrix4x4 ToMatrix(in FarStructureInstance instance)
        {
            var position = new Vector3(instance.Position.x, instance.Position.y, instance.Position.z);
            var rotation = new Quaternion(
                instance.Rotation.value.x,
                instance.Rotation.value.y,
                instance.Rotation.value.z,
                instance.Rotation.value.w);
            var scale = new Vector3(instance.Scale.x, instance.Scale.y, instance.Scale.z);
            return Matrix4x4.TRS(position, rotation, scale);
        }

        private void ClearBatches()
        {
            foreach (List<Matrix4x4> matrices in _batches.Values)
                matrices.Clear();
            _instanceCount = 0;
        }

        private static Mesh BuildProxyMesh(string proxyKey, FarStructureTier tier)
        {
            bool castle = Contains(proxyKey, "castle") || Contains(proxyKey, "keep") || Contains(proxyKey, "fort");
            var builder = new ProxyMeshBuilder();

            if (tier == FarStructureTier.Horizon)
            {
                builder.AddBox(new Vector3(0f, 0.45f, 0f), new Vector3(1f, 0.9f, 1f));
                if (castle)
                    builder.AddBox(new Vector3(0f, 0.7f, 0f), new Vector3(0.32f, 0.6f, 0.32f));
            }
            else if (castle)
            {
                builder.AddBox(new Vector3(0f, 0.22f, 0f), new Vector3(1f, 0.44f, 1f));
                builder.AddBox(new Vector3(0f, 0.58f, 0f), new Vector3(0.38f, 0.72f, 0.38f));
                AddTower(builder, -0.38f, -0.38f, tier);
                AddTower(builder, 0.38f, -0.38f, tier);
                AddTower(builder, -0.38f, 0.38f, tier);
                AddTower(builder, 0.38f, 0.38f, tier);
            }
            else
            {
                builder.AddBox(new Vector3(0f, 0.34f, 0f), new Vector3(1f, 0.68f, 1f));
                if (tier == FarStructureTier.Mid)
                    builder.AddRoofPrism(0.66f, 1f, 1f, 0.34f);
                else
                    builder.AddRoofPrism(0.65f, 1f, 1f, 0.22f);
            }

            return builder.Build();
        }

        private static void AddTower(ProxyMeshBuilder builder, float x, float z, FarStructureTier tier)
        {
            float width = tier == FarStructureTier.Mid ? 0.22f : 0.18f;
            builder.AddBox(new Vector3(x, 0.56f, z), new Vector3(width, 0.76f, width));
        }

        private static bool Contains(string value, string token) =>
            value != null && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;

        private static uint StableHash(string value)
        {
            uint hash = 2166136261u;
            if (value == null) return hash;
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619u;
            }
            return hash;
        }

        private readonly struct BatchKey : IEquatable<BatchKey>
        {
            public readonly string ProxyKey;
            public readonly string StyleKey;
            public readonly FarStructureTier Tier;

            public BatchKey(string proxyKey, string styleKey, FarStructureTier tier)
            {
                ProxyKey = proxyKey ?? string.Empty;
                StyleKey = styleKey ?? string.Empty;
                Tier = tier;
            }

            public bool Equals(BatchKey other) =>
                Tier == other.Tier
                && string.Equals(ProxyKey, other.ProxyKey, StringComparison.Ordinal)
                && string.Equals(StyleKey, other.StyleKey, StringComparison.Ordinal);

            public override bool Equals(object obj) => obj is BatchKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = (int)Tier;
                    hash = hash * 397 ^ StringComparer.Ordinal.GetHashCode(ProxyKey);
                    hash = hash * 397 ^ StringComparer.Ordinal.GetHashCode(StyleKey);
                    return hash;
                }
            }

            public override string ToString() => $"{ProxyKey}|{StyleKey}|{Tier}";
        }

        private sealed class ProxyMeshBuilder
        {
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<int> _triangles = new List<int>();

            public void AddBox(Vector3 center, Vector3 size)
            {
                Vector3 h = size * 0.5f;
                int b = _vertices.Count;
                _vertices.Add(center + new Vector3(-h.x, -h.y, -h.z));
                _vertices.Add(center + new Vector3(h.x, -h.y, -h.z));
                _vertices.Add(center + new Vector3(h.x, h.y, -h.z));
                _vertices.Add(center + new Vector3(-h.x, h.y, -h.z));
                _vertices.Add(center + new Vector3(-h.x, -h.y, h.z));
                _vertices.Add(center + new Vector3(h.x, -h.y, h.z));
                _vertices.Add(center + new Vector3(h.x, h.y, h.z));
                _vertices.Add(center + new Vector3(-h.x, h.y, h.z));
                AddQuad(b + 0, b + 1, b + 2, b + 3);
                AddQuad(b + 5, b + 4, b + 7, b + 6);
                AddQuad(b + 4, b + 0, b + 3, b + 7);
                AddQuad(b + 1, b + 5, b + 6, b + 2);
                AddQuad(b + 3, b + 2, b + 6, b + 7);
                AddQuad(b + 4, b + 5, b + 1, b + 0);
            }

            public void AddRoofPrism(float baseY, float width, float depth, float roofHeight)
            {
                float hx = width * 0.5f;
                float hz = depth * 0.5f;
                int b = _vertices.Count;
                _vertices.Add(new Vector3(-hx, baseY, -hz));
                _vertices.Add(new Vector3(hx, baseY, -hz));
                _vertices.Add(new Vector3(0f, baseY + roofHeight, -hz));
                _vertices.Add(new Vector3(-hx, baseY, hz));
                _vertices.Add(new Vector3(hx, baseY, hz));
                _vertices.Add(new Vector3(0f, baseY + roofHeight, hz));
                AddTri(b + 0, b + 1, b + 2);
                AddTri(b + 4, b + 3, b + 5);
                AddQuad(b + 0, b + 2, b + 5, b + 3);
                AddQuad(b + 2, b + 1, b + 4, b + 5);
                AddQuad(b + 1, b + 0, b + 3, b + 4);
            }

            public Mesh Build()
            {
                var mesh = new Mesh();
                mesh.SetVertices(_vertices);
                mesh.SetTriangles(_triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }

            private void AddQuad(int a, int b, int c, int d)
            {
                AddTri(a, b, c);
                AddTri(a, c, d);
            }

            private void AddTri(int a, int b, int c)
            {
                _triangles.Add(a);
                _triangles.Add(b);
                _triangles.Add(c);
            }
        }
    }
}

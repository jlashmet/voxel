using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Rendering.Api;

namespace VoxelEngine.Rendering.Runtime.FarWorld
{
    /// <summary>
    /// Renderer for already-selected semantic far features. The renderer intentionally knows
    /// nothing about producer categories or named game content; geometry/style keys are opaque.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProceduralFarFeatureRenderer : MonoBehaviour, IFarFeatureRenderer
    {
        private const int MaxInstancesPerDraw = 1023;

        private readonly Dictionary<BatchKey, List<Matrix4x4>> _batches = new();
        private readonly Dictionary<string, Mesh> _meshCache = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Material> _materialCache = new(StringComparer.Ordinal);
        private readonly Matrix4x4[] _drawMatrices = new Matrix4x4[MaxInstancesPerDraw];
        private int _instanceCount;

        public int InstanceCount => _instanceCount;
        public int PersistentInstanceObjectCount => 0;

        public void SetInstances(IReadOnlyList<FarFeatureInstance> instances)
        {
            ClearBatches();
            if (instances == null) return;

            for (int i = 0; i < instances.Count; i++)
            {
                FarFeatureInstance instance = instances[i];
                if (instance.Tier == FarFeatureTier.Culled) continue;

                var key = new BatchKey(instance.GeometryKey, instance.StyleKey, instance.Tier);
                if (!_batches.TryGetValue(key, out List<Matrix4x4> matrices))
                {
                    matrices = new List<Matrix4x4>();
                    _batches.Add(key, matrices);
                }

                matrices.Add(Matrix4x4.TRS(
                    ToVector3(instance.Position),
                    ToQuaternion(instance.Rotation),
                    ToVector3(instance.Scale)));
                _instanceCount++;
            }
        }

        public void Clear()
        {
            ClearBatches();
        }

        public void DrawNow()
        {
            foreach (KeyValuePair<BatchKey, List<Matrix4x4>> batch in _batches)
            {
                Mesh mesh = GetMesh(batch.Key.GeometryKey);
                Material material = GetMaterial(batch.Key.StyleKey);
                List<Matrix4x4> matrices = batch.Value;
                for (int offset = 0; offset < matrices.Count; offset += MaxInstancesPerDraw)
                {
                    int count = Mathf.Min(MaxInstancesPerDraw, matrices.Count - offset);
                    for (int i = 0; i < count; i++) _drawMatrices[i] = matrices[offset + i];
                    Graphics.DrawMeshInstanced(
                        mesh,
                        0,
                        material,
                        _drawMatrices,
                        count,
                        null,
                        ShadowCastingMode.Off,
                        receiveShadows: false,
                        layer: gameObject.layer);
                }
            }
        }

        public string BatchKeyFor(FarFeatureInstance instance) =>
            new BatchKey(instance.GeometryKey, instance.StyleKey, instance.Tier).ToString();

        private void LateUpdate()
        {
            if (enabled) DrawNow();
        }

        private void ClearBatches()
        {
            foreach (List<Matrix4x4> matrices in _batches.Values) matrices.Clear();
            _instanceCount = 0;
        }

        private Mesh GetMesh(string geometryKey)
        {
            string key = geometryKey ?? string.Empty;
            if (_meshCache.TryGetValue(key, out Mesh mesh)) return mesh;

            // T008 keeps geometry keys opaque. T010 replaces this neutral fallback with the
            // generic baked-geometry payload while retaining the same render contract.
            mesh = BuildFallbackMesh();
            mesh.name = string.IsNullOrEmpty(key) ? "FarFeature-Default" : $"FarFeature-{key}";
            _meshCache.Add(key, mesh);
            return mesh;
        }

        private Material GetMaterial(string styleKey)
        {
            string key = styleKey ?? string.Empty;
            if (_materialCache.TryGetValue(key, out Material material)) return material;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader)
            {
                name = string.IsNullOrEmpty(key) ? "FarFeature-Default" : $"FarFeature-{key}",
                hideFlags = HideFlags.DontSave,
            };
            material.enableInstancing = true;
            _materialCache.Add(key, material);
            return material;
        }

        private static Mesh BuildFallbackMesh()
        {
            var mesh = new Mesh { hideFlags = HideFlags.DontSave };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f),
                new Vector3(0.5f, 1f, -0.5f), new Vector3(-0.5f, 1f, -0.5f),
                new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f),
                new Vector3(0.5f, 1f, 0.5f), new Vector3(-0.5f, 1f, 0.5f),
            };
            mesh.triangles = new[]
            {
                0, 2, 1, 0, 3, 2,
                4, 5, 6, 4, 6, 7,
                0, 1, 5, 0, 5, 4,
                3, 7, 6, 3, 6, 2,
                1, 2, 6, 1, 6, 5,
                0, 4, 7, 0, 7, 3,
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDestroy()
        {
            foreach (Mesh mesh in _meshCache.Values)
                if (mesh != null) DestroyImmediate(mesh);
            foreach (Material material in _materialCache.Values)
                if (material != null) DestroyImmediate(material);
            _meshCache.Clear();
            _materialCache.Clear();
        }

        private static Vector3 ToVector3(float3 value) => new(value.x, value.y, value.z);
        private static Quaternion ToQuaternion(quaternion value) =>
            new(value.value.x, value.value.y, value.value.z, value.value.w);

        private readonly struct BatchKey : IEquatable<BatchKey>
        {
            public BatchKey(string geometryKey, string styleKey, FarFeatureTier tier)
            {
                GeometryKey = geometryKey ?? string.Empty;
                StyleKey = styleKey ?? string.Empty;
                Tier = tier;
            }

            public string GeometryKey { get; }
            public string StyleKey { get; }
            public FarFeatureTier Tier { get; }

            public bool Equals(BatchKey other) =>
                Tier == other.Tier
                && string.Equals(GeometryKey, other.GeometryKey, StringComparison.Ordinal)
                && string.Equals(StyleKey, other.StyleKey, StringComparison.Ordinal);

            public override bool Equals(object obj) => obj is BatchKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = (int)Tier;
                    hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(GeometryKey);
                    hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(StyleKey);
                    return hash;
                }
            }

            public override string ToString() => $"{GeometryKey}|{StyleKey}|{(byte)Tier}";
        }
    }
}

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
        private const int CylinderSegments = 12;
        private const int FrustumSegments = 24;

        private readonly Dictionary<BatchKey, List<Matrix4x4>> _batches = new();
        private readonly Dictionary<string, FarFeatureGeometry> _geometrySources = new(StringComparer.Ordinal);
        private readonly Dictionary<string, FarFeaturePresentation> _styleSources = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Mesh> _meshCache = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Material> _materialCache = new(StringComparer.Ordinal);
        private readonly Matrix4x4[] _drawMatrices = new Matrix4x4[MaxInstancesPerDraw];
        private int _instanceCount;
        private static readonly bool s_TraceHandoff =
            string.Equals(Environment.GetEnvironmentVariable("VOXEL_FAR_HANDOFF_TRACE"), "1", StringComparison.Ordinal);
        private float _nextHandoffTrace = 30f;
        private readonly List<FarFeatureInstance> _sourceInstances = new();
        private static readonly HashSet<ProceduralFarFeatureRenderer> s_SurfaceConsumers = new();
        private bool _useSurfaceReplacementHandoff;
        public bool UseSurfaceReplacementHandoff
        {
            get => _useSurfaceReplacementHandoff;
            set
            {
                _useSurfaceReplacementHandoff = value;
                if (value && isActiveAndEnabled) s_SurfaceConsumers.Add(this);
                else
                {
                    s_SurfaceConsumers.Remove(this);
                    if (!value) RebuildBatches(_sourceInstances, null);
                }
            }
        }
        public int NearReplacementCount { get; private set; }

        private void OnEnable()
        {
            if (_useSurfaceReplacementHandoff) s_SurfaceConsumers.Add(this);
        }
        private void OnDisable() => s_SurfaceConsumers.Remove(this);

        internal static void PrepareSurfaceConsumers(List<ProceduralFarFeatureRenderer> destination,
                                                      Func<Bounds, bool> hasReplacement)
        {
            destination.Clear();
            foreach (var renderer in s_SurfaceConsumers)
            {
                if (renderer == null || !renderer.isActiveAndEnabled
                    || !renderer.UseSurfaceReplacementHandoff) continue;
                renderer.RebuildBatches(renderer._sourceInstances, hasReplacement);
                destination.Add(renderer);
            }
        }

        internal void RecordSurfaceDraws(CommandBuffer command)
        {
            foreach (var batch in _batches)
            {
                if (batch.Value.Count == 0) continue;
                Mesh mesh = GetMesh(batch.Key.GeometryKey);
                Material material = GetMaterial(batch.Key.StyleKey);
                for (int offset = 0; offset < batch.Value.Count; offset += MaxInstancesPerDraw)
                {
                    int count = Mathf.Min(MaxInstancesPerDraw, batch.Value.Count - offset);
                    for (int i = 0; i < count; i++) _drawMatrices[i] = batch.Value[offset + i];
                    command.DrawMeshInstanced(mesh, 0, material, 0, _drawMatrices, count);
                }
            }
        }

        public int InstanceCount => _instanceCount;
        public int PersistentInstanceObjectCount => 0;

        public void SetInstances(IReadOnlyList<FarFeatureInstance> instances)
        {
            _sourceInstances.Clear();
            if (instances != null)
                for (int i = 0; i < instances.Count; i++) _sourceInstances.Add(instances[i]);
            if (!UseSurfaceReplacementHandoff) RebuildBatches(_sourceInstances, null);

        }

        private void RebuildBatches(IReadOnlyList<FarFeatureInstance> instances,
                                    Func<Bounds, bool> hasReplacement)
        {
            ClearBatches();
            NearReplacementCount = 0;
            bool trace = s_TraceHandoff && hasReplacement != null && Time.unscaledTime >= _nextHandoffTrace;
            int traced = 0;
            if (trace) _nextHandoffTrace = Time.unscaledTime + 10f;
            for (int i = 0; i < instances.Count; i++)
            {
                FarFeatureInstance instance = instances[i];
                if (instance.Tier == FarFeatureTier.Culled) continue;
                if (hasReplacement != null && hasReplacement(new Bounds(
                    ToVector3(instance.BoundsCenter), ToVector3(instance.BoundsExtents * 2f))))
                {
                    NearReplacementCount++;
                    continue;
                }

                if (trace && traced++ < 4)
                    Debug.Log($"FAR HANDOFF retained id={instance.StableId:X16} key={instance.GeometryKey} "
                        + $"center={instance.BoundsCenter} extents={instance.BoundsExtents} t={Time.unscaledTime:0.0}");
                RegisterGeometry(instance);
                RegisterStyle(instance);
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
            _sourceInstances.Clear();
            NearReplacementCount = 0;
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

        internal Mesh ResolveMesh(FarFeatureInstance instance)
        {
            RegisterGeometry(instance);
            return GetMesh(instance.GeometryKey);
        }

        internal Material ResolveMaterial(FarFeatureInstance instance)
        {
            RegisterStyle(instance);
            return GetMaterial(instance.StyleKey);
        }

        private void LateUpdate()
        {
            if (enabled && !UseSurfaceReplacementHandoff) DrawNow();
        }

        private void ClearBatches()
        {
            foreach (List<Matrix4x4> matrices in _batches.Values) matrices.Clear();
            _instanceCount = 0;
        }

        private void RegisterGeometry(FarFeatureInstance instance)
        {
            if (instance.Geometry == null) return;
            string key = instance.GeometryKey ?? string.Empty;
            if (_geometrySources.TryGetValue(key, out FarFeatureGeometry existing)
                && ReferenceEquals(existing, instance.Geometry))
                return;

            _geometrySources[key] = instance.Geometry;
            if (_meshCache.TryGetValue(key, out Mesh stale))
            {
                _meshCache.Remove(key);
                if (stale != null) DestroyImmediate(stale);
            }
        }

        private void RegisterStyle(FarFeatureInstance instance)
        {
            string key = instance.StyleKey ?? string.Empty;
            FarFeaturePresentation presentation = instance.Presentation;
            if (_styleSources.TryGetValue(key, out FarFeaturePresentation existing)
                && existing.Albedo.Equals(presentation.Albedo)
                && existing.Roughness.Equals(presentation.Roughness))
                return;

            _styleSources[key] = presentation;
            if (_materialCache.TryGetValue(key, out Material stale))
            {
                _materialCache.Remove(key);
                if (stale != null) DestroyImmediate(stale);
            }
        }

        private Mesh GetMesh(string geometryKey)
        {
            string key = geometryKey ?? string.Empty;
            if (_meshCache.TryGetValue(key, out Mesh mesh)) return mesh;

            mesh = _geometrySources.TryGetValue(key, out FarFeatureGeometry geometry)
                ? BuildGeometryMesh(geometry)
                : BuildFallbackMesh();
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

            FarFeaturePresentation presentation = _styleSources.TryGetValue(key, out FarFeaturePresentation value)
                ? value
                : default;
            ApplySharedPresentation(material, presentation);

            _materialCache.Add(key, material);
            return material;
        }

        private static void ApplySharedPresentation(Material material, FarFeaturePresentation presentation)
        {
            float4 albedo = presentation.Albedo;
            float roughness = presentation.Roughness;
            // Older render-ready fixtures intentionally omit resolved presentation. Preserve the
            // historical neutral material for those callers while production composition supplies
            // an alpha-one resolved value from the installed catalogue.
            if (!(albedo.w > 0f))
            {
                albedo = new float4(1f, 1f, 1f, 1f);
                roughness = 0.76f;
            }

            Color baseColour = new(albedo.x, albedo.y, albedo.z, albedo.w);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", baseColour);
            if (material.HasProperty("_Color")) material.SetColor("_Color", baseColour);

            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 1f - Mathf.Clamp01(roughness));
        }

        private static Mesh BuildGeometryMesh(FarFeatureGeometry geometry)
        {
            var vertices = new List<Vector3>(geometry.PrimitiveCount * 16);
            var triangles = new List<int>(geometry.PrimitiveCount * 36);
            for (int i = 0; i < geometry.PrimitiveCount; i++)
            {
                FarFeatureGeometryPrimitive primitive = geometry.GetPrimitive(i);
                switch (primitive.Shape)
                {
                    case FarFeatureGeometryShape.Frustum:
                        AppendFrustum(vertices, triangles, primitive);
                        break;
                    case FarFeatureGeometryShape.Ramp:
                        AppendRamp(vertices, triangles, primitive);
                        break;
                    case FarFeatureGeometryShape.Cylinder:
                    case FarFeatureGeometryShape.Annulus:
                    case FarFeatureGeometryShape.ArcWedge:
                        AppendCylinder(vertices, triangles, primitive.Min, primitive.Max, primitive.Axis);
                        break;
                    default:
                        // Other primitive approximations remain separate visual acceptance work;
                        // preserving their AABB is not evidence of canonical silhouette parity.
                        AppendBox(vertices, triangles, primitive.Min, primitive.Max);
                        break;
                }
            }

            if (vertices.Count == 0) return BuildFallbackMesh();
            var mesh = new Mesh { hideFlags = HideFlags.DontSave };
            if (vertices.Count > ushort.MaxValue) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AppendFrustum(
            List<Vector3> vertices, List<int> triangles, FarFeatureGeometryPrimitive primitive)
        {
            FarFeatureFrustum caps = primitive.Frustum;
            int radialA = (primitive.Axis + 1) % 3;
            int radialB = (primitive.Axis + 2) % 3;
            int start = vertices.Count;
            for (int end = 0; end < 2; end++)
            {
                float3 center = end == 0 ? caps.LowerCenter : caps.UpperCenter;
                float3 radii = end == 0 ? caps.LowerRadii : caps.UpperRadii;
                for (int segment = 0; segment < FrustumSegments; segment++)
                {
                    float angle = (2f * math.PI * segment) / FrustumSegments;
                    float3 point = center;
                    point[radialA] += math.cos(angle) * radii[radialA];
                    point[radialB] += math.sin(angle) * radii[radialB];
                    vertices.Add(ToVector3(point));
                }
            }

            int lowerCenter = vertices.Count;
            vertices.Add(ToVector3(caps.LowerCenter));
            int upperCenter = vertices.Count;
            vertices.Add(ToVector3(caps.UpperCenter));
            for (int segment = 0; segment < FrustumSegments; segment++)
            {
                int next = (segment + 1) % FrustumSegments;
                int lower = start + segment;
                int lowerNext = start + next;
                int upper = start + FrustumSegments + segment;
                int upperNext = start + FrustumSegments + next;
                // The cyclic radial basis has radialA x radialB == positive extrusion axis.
                // This winding therefore faces outward for X, Y and Z without per-axis recipes.
                triangles.Add(lower); triangles.Add(lowerNext); triangles.Add(upperNext);
                triangles.Add(lower); triangles.Add(upperNext); triangles.Add(upper);
                triangles.Add(lowerCenter); triangles.Add(lowerNext); triangles.Add(lower);
                triangles.Add(upperCenter); triangles.Add(upper); triangles.Add(upperNext);
            }
        }

        private static void AppendRamp(
            List<Vector3> vertices, List<int> triangles, FarFeatureGeometryPrimitive primitive)
        {
            int start = vertices.Count;
            // Interpolate canonical cell-centre heights, then cap the upper half-cell.
            // A zero-to-one wedge undershoots steep ramps by half a column's rise.
            // The profile stays constant-size regardless of the authored voxel dimensions.
            float halfCell = 0.5f / primitive.RampRunCells;
            float2[] profile =
            {
                new(0, 0), new(1, 0), new(1, 1),
                new(1f - halfCell, 1), new(0, halfCell)
            };
            for (int side = 0; side < 2; side++)
            foreach (float2 point in profile)
            {
                float3 p = new(point.x, point.y, side);
                if (primitive.Direction < 0) p.x = 1f - p.x;
                if (primitive.Axis == 2) p = p.zyx;
                vertices.Add(ToVector3(primitive.Min + p * (primitive.Max - primitive.Min)));
            }
            bool reverse = (primitive.Axis == 2) != (primitive.Direction < 0);
            void Triangle(int a, int b, int c)
            {
                triangles.Add(start + a);
                triangles.Add(start + (reverse ? c : b));
                triangles.Add(start + (reverse ? b : c));
            }
            for (int i = 1; i < 4; i++)
            {
                Triangle(0, i + 1, i);
                Triangle(5, i + 5, i + 6);
            }
            for (int i = 0; i < 5; i++)
            {
                int next = (i + 1) % 5;
                Triangle(i, next, next + 5);
                Triangle(i, next + 5, i + 5);
            }
        }

        private static void AppendBox(List<Vector3> vertices, List<int> triangles, float3 minValue, float3 maxValue)
        {
            Vector3 min = ToVector3(minValue);
            Vector3 max = ToVector3(maxValue);
            int start = vertices.Count;
            vertices.Add(new Vector3(min.x, min.y, min.z));
            vertices.Add(new Vector3(max.x, min.y, min.z));
            vertices.Add(new Vector3(max.x, max.y, min.z));
            vertices.Add(new Vector3(min.x, max.y, min.z));
            vertices.Add(new Vector3(min.x, min.y, max.z));
            vertices.Add(new Vector3(max.x, min.y, max.z));
            vertices.Add(new Vector3(max.x, max.y, max.z));
            vertices.Add(new Vector3(min.x, max.y, max.z));

            int[] local =
            {
                0, 2, 1, 0, 3, 2,
                4, 5, 6, 4, 6, 7,
                0, 1, 5, 0, 5, 4,
                3, 7, 6, 3, 6, 2,
                1, 2, 6, 1, 6, 5,
                0, 4, 7, 0, 7, 3,
            };
            for (int i = 0; i < local.Length; i++) triangles.Add(start + local[i]);
        }

        private static void AppendCylinder(
            List<Vector3> vertices,
            List<int> triangles,
            float3 min,
            float3 max,
            byte axis)
        {
            float3 center = (min + max) * 0.5f;
            float3 half = math.max((max - min) * 0.5f, new float3(0.0001f));
            int start = vertices.Count;
            for (int end = -1; end <= 1; end += 2)
            {
                for (int segment = 0; segment < CylinderSegments; segment++)
                {
                    float angle = (2f * math.PI * segment) / CylinderSegments;
                    float c = math.cos(angle);
                    float s = math.sin(angle);
                    vertices.Add(ToVector3(CylinderPoint(center, half, axis, end, c, s)));
                }
            }

            int lowerCenter = vertices.Count;
            vertices.Add(ToVector3(CylinderPoint(center, half, axis, -1, 0f, 0f)));
            int upperCenter = vertices.Count;
            vertices.Add(ToVector3(CylinderPoint(center, half, axis, 1, 0f, 0f)));

            for (int segment = 0; segment < CylinderSegments; segment++)
            {
                int next = (segment + 1) % CylinderSegments;
                int lower = start + segment;
                int lowerNext = start + next;
                int upper = start + CylinderSegments + segment;
                int upperNext = start + CylinderSegments + next;
                triangles.Add(lower);
                triangles.Add(upperNext);
                triangles.Add(upper);
                triangles.Add(lower);
                triangles.Add(lowerNext);
                triangles.Add(upperNext);

                triangles.Add(lowerCenter);
                triangles.Add(lowerNext);
                triangles.Add(lower);
                triangles.Add(upperCenter);
                triangles.Add(upper);
                triangles.Add(upperNext);
            }
        }

        private static float3 CylinderPoint(float3 center, float3 half, byte axis, int end, float c, float s)
        {
            switch (axis)
            {
                case 0:
                    return center + new float3(end * half.x, c * half.y, s * half.z);
                case 2:
                    return center + new float3(c * half.x, s * half.y, end * half.z);
                default:
                    return center + new float3(c * half.x, end * half.y, s * half.z);
            }
        }

        private static Mesh BuildFallbackMesh()
        {
            var vertices = new List<Vector3>(8);
            var triangles = new List<int>(36);
            AppendBox(vertices, triangles, new float3(-0.5f, 0f, -0.5f), new float3(0.5f, 1f, 0.5f));
            var mesh = new Mesh { hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDestroy()
        {
            s_SurfaceConsumers.Remove(this);
            foreach (Mesh mesh in _meshCache.Values)
                if (mesh != null) DestroyImmediate(mesh);
            foreach (Material material in _materialCache.Values)
                if (material != null) DestroyImmediate(material);
            _meshCache.Clear();
            _geometrySources.Clear();
            _styleSources.Clear();
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

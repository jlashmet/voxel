using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Rendering.Runtime.GpuVoxel;

namespace VoxelEngine.Rendering.Runtime.FarWorld
{
    /// <summary>
    /// Renderer for already-selected semantic far features. The renderer intentionally knows
    /// nothing about producer categories or named game content; geometry/style keys are opaque.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProceduralFarFeatureRenderer : MonoBehaviour, IFarFeatureRenderer
    {
        [SerializeField, Min(1)] private int maximumInstances = 65536;
        private const int CylinderSegments = 12;
        private const int FrustumSegments = 24;

        private readonly List<GpuFarInstanceBatch> _gpuBatches = new();
        private GpuFarDrawSet _gpuDrawSet;
        private FarFeatureSelectionSettings? _selectionSettings;
        private float _selectionRadius, _selectionVoxelSize;

        public void ConfigureGpuSelection(FarFeatureSelectionSettings settings, float radius, float voxelSize)
        {
            if (!(radius > 0) || !math.isfinite(radius) || !(voxelSize > 0) || !math.isfinite(voxelSize)
                || !(settings.FocalPixels > 0)) throw new ArgumentOutOfRangeException(nameof(radius));
            _selectionSettings = settings;
            _selectionRadius = radius;
            _selectionVoxelSize = voxelSize;
        }
        internal int BatchBuildCount { get; private set; }
        private readonly Dictionary<string, FarFeatureGeometry> _geometrySources = new(StringComparer.Ordinal);
        private readonly Dictionary<string, FarFeaturePresentation> _styleSources = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Mesh> _meshCache = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Material> _materialCache = new(StringComparer.Ordinal);
        private readonly Dictionary<FarFeaturePresentation, Material> _resolvedMaterials = new();
        private int _instanceCount;
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
                    if (!value) UpdateReplacement(null);
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
                                                      Func<Bounds, bool> hasReplacement, Camera camera = null,
                                                      GpuFarCoverageDispatcher gpuCoverage = null)
        {
            destination.Clear();
            foreach (var renderer in s_SurfaceConsumers)
            {
                if (renderer == null || !renderer.isActiveAndEnabled
                    || !renderer.UseSurfaceReplacementHandoff) continue;
                if(renderer._gpuDrawSet != null && (gpuCoverage != null || renderer._selectionSettings.HasValue))
                {
                    renderer._gpuDrawSet.Prepare(gpuCoverage, renderer._selectionSettings,
                        camera != null ? (float3)camera.transform.position : float3.zero,
                        renderer._selectionRadius, renderer._selectionVoxelSize);
                    renderer._instanceCount=renderer._gpuDrawSet.VisibleCount;
                    renderer.NearReplacementCount=renderer._gpuDrawSet.NearCount;
                }
                else renderer.UpdateReplacement(hasReplacement);
                destination.Add(renderer);
            }
        }

        internal void RecordSurfaceDraws(CommandBuffer command)
        {
            if (_gpuDrawSet != null && _gpuDrawSet.Prepared) _gpuDrawSet.Record(command);
            else foreach (var batch in _gpuBatches) batch.RecordDraws(command);
        }

        public int InstanceCount => _instanceCount;
        public int PersistentInstanceObjectCount => 0;

        public void SetInstances(IReadOnlyList<FarFeatureInstance> instances)
        {
            if (instances != null && instances.Count > maximumInstances)
                throw new InvalidOperationException($"Far instance capacity {maximumInstances} exceeded by {instances.Count}.");
            if (MatchesSource(instances)) return;
            var previousDrawSet = _gpuDrawSet;
            _gpuDrawSet = null;
            try
            {
            ClearBatches();
            _sourceInstances.Clear();
            if (instances != null)
                for (int i = 0; i < instances.Count; i++) _sourceInstances.Add(instances[i]);
            var groups = new Dictionary<BatchKey, List<FarFeatureInstance>>();
            foreach (var instance in _sourceInstances)
            {
                if (instance.Tier == FarFeatureTier.Culled) continue;
                RegisterGeometry(instance);
                RegisterStyle(instance);
                var key = new BatchKey(instance.GeometryKey, instance.StyleKey, instance.Tier);
                if (!groups.TryGetValue(key, out var group)) groups.Add(key, group = new List<FarFeatureInstance>());
                group.Add(instance);
            }
            var shader = Resources.Load<ComputeShader>("GpuFarInstanceCompact");
            var bounds = new List<Bounds>();
            var flags = new List<FarFeatureVisualFlags>();
            var ids = new List<ulong>();
            try
            {
                foreach (var group in groups)
                {
                    Mesh mesh = GetMesh(group.Key.GeometryKey);
                    var materials = new Material[mesh.subMeshCount];
                    for (int submesh = 0; submesh < materials.Length; submesh++)
                        materials[submesh] = GetSubmeshMaterial(group.Key, submesh);
                    _gpuBatches.Add(new GpuFarInstanceBatch(shader, mesh, materials, group.Value));
                    foreach(var instance in group.Value)
                    {
                        bounds.Add(new Bounds(instance.BoundsCenter,instance.BoundsExtents*2));
                        flags.Add(instance.Flags);
                        ids.Add(instance.StableId);
                    }
                }
                BatchBuildCount++;
                UpdateReplacement(null);
                if(bounds.Count>0)
                {
                    _gpuDrawSet=new GpuFarDrawSet(_gpuBatches,bounds,flags,ids);
                    _gpuDrawSet.InheritSelection(previousDrawSet);
                }
            }
            catch { ClearBatches(); throw; }
            }
            finally { previousDrawSet?.Dispose(); }
        }

        private bool MatchesSource(IReadOnlyList<FarFeatureInstance> instances)
        {
            int count = instances?.Count ?? 0;
            if (_sourceInstances.Count != count) return false;
            for (int i = 0; i < count; i++)
            {
                var a = _sourceInstances[i]; var b = instances[i];
                if (a.StableId != b.StableId || a.Tier != b.Tier || a.Flags != b.Flags
                    || !a.Position.Equals(b.Position) || !a.Rotation.Equals(b.Rotation)
                    || !a.Scale.Equals(b.Scale) || !a.BoundsCenter.Equals(b.BoundsCenter)
                    || !a.BoundsExtents.Equals(b.BoundsExtents) || a.GeometryKey != b.GeometryKey
                    || a.StyleKey != b.StyleKey || !ReferenceEquals(a.Geometry, b.Geometry)
                    || !a.Presentation.Equals(b.Presentation)) return false;
            }
            return true;
        }

        private void UpdateReplacement(Func<Bounds, bool> hasReplacement)
        {
            _gpuDrawSet?.UseLegacyDraw();
            _instanceCount = NearReplacementCount = 0;
            foreach (var batch in _gpuBatches)
            {
                batch.UpdateReplacement(hasReplacement);
                batch.Prepare();
                _instanceCount += batch.VisibleCount;
                NearReplacementCount += batch.Count - batch.VisibleCount;
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
            foreach (var batch in _gpuBatches) batch.DrawNow(gameObject.layer);
        }

        public string BatchKeyFor(FarFeatureInstance instance) =>
            new BatchKey(instance.GeometryKey, instance.StyleKey, instance.Tier).ToString();

        internal Mesh ResolveMesh(FarFeatureInstance instance)
        {
            RegisterGeometry(instance);
            return GetMesh(instance.GeometryKey);
        }

        internal Material ResolveMaterial(FarFeatureInstance instance, int submesh = 0)
        {
            RegisterStyle(instance);
            RegisterGeometry(instance);
            return GetSubmeshMaterial(new BatchKey(instance.GeometryKey, instance.StyleKey, instance.Tier), submesh);
        }

        private void LateUpdate()
        {
            if (enabled && !UseSurfaceReplacementHandoff) DrawNow();
        }

        private void ClearBatches()
        {
            _gpuDrawSet?.Dispose();_gpuDrawSet=null;
            foreach (var batch in _gpuBatches) batch.Dispose();
            _gpuBatches.Clear();
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
            RegisterPresentation(key, instance.Presentation);
        }

        private void RegisterPresentation(string key, FarFeaturePresentation presentation)
        {
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

        private Material GetSubmeshMaterial(BatchKey key, int submesh)
        {
            if (!_geometrySources.TryGetValue(key.GeometryKey ?? string.Empty, out var geometry)
                || geometry.PresentationCount == 0) return GetMaterial(key.StyleKey);
            var presentation = geometry.GetPresentation(submesh);
            if (_resolvedMaterials.TryGetValue(presentation, out Material cached)) return cached;
            Material material = CreateMaterial("FarFeature-Resolved", presentation);
            _resolvedMaterials.Add(presentation, material);
            return material;
        }

        private Material GetMaterial(string styleKey)
        {
            string key = styleKey ?? string.Empty;
            if (_materialCache.TryGetValue(key, out Material material)) return material;
            FarFeaturePresentation presentation = _styleSources.TryGetValue(key, out var value) ? value : default;
            material = CreateMaterial(string.IsNullOrEmpty(key) ? "FarFeature-Default" : $"FarFeature-{key}", presentation);
            _materialCache.Add(key, material);
            return material;
        }

        private static Material CreateMaterial(string name, FarFeaturePresentation presentation)
        {
            Shader shader = Resources.Load<Shader>("GpuFarFeatureLit");
            if (shader == null) throw new InvalidOperationException("GPU far feature shader is missing.");
            var material = new Material(shader) { name = name, hideFlags = HideFlags.DontSave, enableInstancing = true };
            ApplySharedPresentation(material, presentation);
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
            var submeshes = new List<int>[Math.Max(1, geometry.PresentationCount)];
            for (int slot = 0; slot < submeshes.Length; slot++) submeshes[slot] = new List<int>();
            for (int i = 0; i < geometry.PrimitiveCount; i++)
            {
                FarFeatureGeometryPrimitive primitive = geometry.GetPrimitive(i);
                List<int> triangles = submeshes[primitive.PresentationSlot];
                switch (primitive.Shape)
                {
                    case FarFeatureGeometryShape.Frustum:
                        AppendFrustum(vertices, triangles, primitive);
                        break;
                    case FarFeatureGeometryShape.Ramp:
                        AppendRamp(vertices, triangles, primitive);
                        break;
                    case FarFeatureGeometryShape.Prism:
                        AppendPrism(vertices, triangles, primitive);
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
            mesh.subMeshCount = submeshes.Length;
            for (int slot = 0; slot < submeshes.Length; slot++) mesh.SetTriangles(submeshes[slot], slot);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AppendPrism(List<Vector3> vertices, List<int> triangles,
            FarFeatureGeometryPrimitive primitive)
        {
            int width = primitive.PrismWidthCells;
            int height = primitive.PrismHeightCells;
            // These profiles are piecewise linear under the canonical integer predicates.
            // Sample cell centres at the ends and either side of the ridge; interpolation
            // differs by at most one voxel from integer rounding, independent of roof size.
            var columns = new List<int> { 0, (width - 1) / 2, width / 2, width - 1 };
            columns.Sort();
            var top = new List<Vector2>(6);
            int previous = -1;
            foreach (int column in columns)
            {
                if (column == previous) continue;
                previous = column;
                int x = primitive.Direction < 0 ? width - 1 - column : column;
                int occupied;
                switch (primitive.PrismProfile)
                {
                    case FarFeaturePrismProfile.Gable:
                        occupied = height - (int)((long)math.abs(2 * x - (width - 1)) * height / width);
                        break;
                    case FarFeaturePrismProfile.Shed:
                        occupied = (int)((long)(x + 1) * height / width);
                        break;
                    case FarFeaturePrismProfile.Arch:
                        int half = width / 2;
                        occupied = half == 0 ? height : math.clamp(
                            height - (int)(((long)math.abs(x - half) * height + half - 1) / half) + 1,
                            0, height);
                        break;
                    default: occupied = height; break;
                }
                top.Add(new Vector2((column + 0.5f) / width, (float)occupied / height));
            }
            top.Insert(0, new Vector2(0, top[0].y));
            top.Add(new Vector2(1, top[top.Count - 1].y));
            var profile = new List<Vector2>(top.Count + 2) { Vector2.zero, Vector2.right };
            for (int i = top.Count - 1; i >= 0; i--) profile.Add(top[i]);
            int extrusion = primitive.Axis == 0 ? 0 : 2;
            int horizontal = extrusion == 0 ? 2 : 0;
            Vector3 Point(Vector2 uv, int end)
            {
                float3 result = primitive.Min;
                result[horizontal] = math.lerp(primitive.Min[horizontal], primitive.Max[horizontal], uv.x);
                result.y = math.lerp(primitive.Min.y, primitive.Max.y, uv.y);
                result[extrusion] = end == 0 ? primitive.Min[extrusion] : primitive.Max[extrusion];
                return ToVector3(result);
            }
            void Triangle(int a, int b, int c, bool reverse)
            {
                triangles.Add(a); triangles.Add(reverse ? c : b); triangles.Add(reverse ? b : c);
            }
            // Caps and side strips have separate vertices so walls cannot smooth roof normals.
            for (int end = 0; end < 2; end++)
            {
                int start = vertices.Count;
                foreach (Vector2 uv in profile) vertices.Add(Point(uv, end));
                for (int i = 1; i + 1 < profile.Count; i++)
                    Triangle(start, start + i, start + i + 1, (end == 0) != (extrusion == 0));
            }
            for (int i = 0; i < profile.Count; i++)
            {
                int next = (i + 1) % profile.Count;
                int start = vertices.Count;
                vertices.Add(Point(profile[i], 0)); vertices.Add(Point(profile[next], 0));
                vertices.Add(Point(profile[next], 1)); vertices.Add(Point(profile[i], 1));
                Triangle(start, start + 1, start + 2, extrusion == 0);
                Triangle(start, start + 2, start + 3, extrusion == 0);
            }
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
            Vector3[] corners =
            {
                new(min.x, min.y, min.z), new(max.x, min.y, min.z),
                new(max.x, max.y, min.z), new(min.x, max.y, min.z),
                new(min.x, min.y, max.z), new(max.x, min.y, max.z),
                new(max.x, max.y, max.z), new(min.x, max.y, max.z)
            };
            int[] faces = { 0, 3, 2, 1, 4, 5, 6, 7, 0, 1, 5, 4,
                            3, 7, 6, 2, 1, 2, 6, 5, 0, 4, 7, 3 };
            // A box has six planar faces. Sharing its eight corner vertices causes
            // RecalculateNormals to round masonry edges and introduce diagonal gradients.
            for (int face = 0; face < faces.Length; face += 4)
            {
                int start = vertices.Count;
                for (int corner = 0; corner < 4; corner++) vertices.Add(corners[faces[face + corner]]);
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
            }
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
            ClearBatches();
            s_SurfaceConsumers.Remove(this);
            foreach (Mesh mesh in _meshCache.Values)
                if (mesh != null) DestroyImmediate(mesh);
            foreach (Material material in _materialCache.Values)
                if (material != null) DestroyImmediate(material);
            foreach (Material material in _resolvedMaterials.Values)
                if (material != null) DestroyImmediate(material);
            _resolvedMaterials.Clear();
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

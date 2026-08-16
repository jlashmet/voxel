using System.Collections.Generic;
using UnityEngine;
using VoxelEngine.Core.Vegetation;

namespace VoxelEngine.Rendering.Vegetation
{
    /// <summary>
    /// Draws lightweight vegetation directly from semantic instances. One shared mesh/material is
    /// used per growth strategy and instances are submitted in GPU-instanced batches; there is no
    /// GameObject or prefab per blade, flower, moss patch or vine.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProceduralVegetationBatchRenderer : MonoBehaviour
    {
        private const int MaxInstancesPerDraw = 1023;

        private readonly Dictionary<VegetationKind, List<Matrix4x4>> _batches =
            new Dictionary<VegetationKind, List<Matrix4x4>>();
        private readonly Matrix4x4[] _scratch = new Matrix4x4[MaxInstancesPerDraw];
        private MaterialPropertyBlock _properties;
        private int _instanceCount;

        public int InstanceCount => _instanceCount;

        public void SetInstances(IReadOnlyList<VegetationInstance> instances)
        {
            Clear();
            if (instances == null) return;

            for (int i = 0; i < instances.Count; i++)
            {
                VegetationInstance instance = instances[i];
                if (!_batches.TryGetValue(instance.Kind, out List<Matrix4x4> matrices))
                {
                    matrices = new List<Matrix4x4>();
                    _batches.Add(instance.Kind, matrices);
                }

                matrices.Add(BuildMatrix(instance));
                _instanceCount++;
            }
        }

        public void Clear()
        {
            foreach (KeyValuePair<VegetationKind, List<Matrix4x4>> pair in _batches)
                pair.Value.Clear();
            _instanceCount = 0;
        }

        private void LateUpdate()
        {
            DrawNow();
        }

        public void DrawNow()
        {
            if (_instanceCount == 0) return;
            if (_properties == null) _properties = new MaterialPropertyBlock();
            ProceduralVegetationMaterials.ApplyLighting();
            ProceduralTreeMaterials.ApplyLighting();

            foreach (KeyValuePair<VegetationKind, List<Matrix4x4>> pair in _batches)
            {
                if (pair.Value.Count == 0) continue;

                VegetationProfile profile = VegetationCatalogue.Get(pair.Key);
                VegetationRenderStyle style = ProceduralVegetationMaterials.StyleFor(pair.Key);
                Mesh mesh = ProceduralVegetationMeshLibrary.MeshFor(style.ShaderClass, profile.GrowthForm);
                Material material = ProceduralVegetationMaterials.MaterialFor(pair.Key);
                if (mesh == null || material == null) continue;

                _properties.Clear();
                ProceduralVegetationMaterials.Configure(_properties, pair.Key);

                List<Matrix4x4> matrices = pair.Value;
                for (int start = 0; start < matrices.Count; start += MaxInstancesPerDraw)
                {
                    int count = Mathf.Min(MaxInstancesPerDraw, matrices.Count - start);
                    for (int i = 0; i < count; i++)
                        _scratch[i] = matrices[start + i];

                    Graphics.DrawMeshInstanced(mesh, 0, material, _scratch, count, _properties);
                }
            }
        }

        private static Matrix4x4 BuildMatrix(in VegetationInstance instance)
        {
            Vector3 position = new Vector3(
                instance.PositionMetres.x,
                instance.PositionMetres.y,
                instance.PositionMetres.z);
            Vector3 normal = new Vector3(
                instance.SurfaceNormal.x,
                instance.SurfaceNormal.y,
                instance.SurfaceNormal.z);
            if (normal.sqrMagnitude < 0.0001f) normal = Vector3.up;
            normal.Normalize();

            float scale = Mathf.Max(0.05f, instance.Scale);
            float yaw = Random01(instance.Seed) * 360f;
            VegetationProfile profile = VegetationCatalogue.Get(instance.Kind);
            VegetationRenderStyle style = ProceduralVegetationMaterials.StyleFor(instance.Kind);

            Quaternion rotation;
            Vector3 localScale;
            switch (style.ShaderClass)
            {
                case VegetationShaderClass.Surface:
                    rotation = Quaternion.FromToRotation(Vector3.forward, normal)
                               * Quaternion.AngleAxis(yaw, Vector3.forward);
                    localScale = new Vector3(0.90f * scale, 0.90f * scale, 1f);
                    position += normal * 0.018f;
                    break;

                case VegetationShaderClass.Vine:
                    rotation = Quaternion.LookRotation(normal, Vector3.up);
                    float direction = profile.GrowthForm == VegetationGrowthForm.Climber ? 1f : -1f;
                    localScale = new Vector3(0.42f * scale, direction * 2.6f * scale, 1f);
                    position += normal * 0.022f;
                    break;

                case VegetationShaderClass.Woody:
                    Vector3 seedDirection = new Vector3(
                        Mathf.Cos(yaw * Mathf.Deg2Rad),
                        0f,
                        Mathf.Sin(yaw * Mathf.Deg2Rad));
                    Vector3 tangent = Vector3.ProjectOnPlane(seedDirection, normal);
                    if (tangent.sqrMagnitude < 0.001f)
                        tangent = Vector3.Cross(normal, Vector3.right);
                    if (tangent.sqrMagnitude < 0.001f)
                        tangent = Vector3.forward;
                    tangent.Normalize();
                    rotation = Quaternion.FromToRotation(Vector3.up, tangent);
                    localScale = new Vector3(0.22f * scale, 1.35f * scale, 0.22f * scale);
                    position += normal * 0.06f;
                    break;

                default:
                    rotation = Quaternion.FromToRotation(Vector3.up, normal)
                               * Quaternion.AngleAxis(yaw, Vector3.up);
                    GetFoliageScale(profile.GrowthForm, scale, out float width, out float height);
                    localScale = new Vector3(width, height, width);
                    break;
            }

            return Matrix4x4.TRS(position, rotation, localScale);
        }

        private static void GetFoliageScale(
            VegetationGrowthForm growthForm,
            float scale,
            out float width,
            out float height)
        {
            switch (growthForm)
            {
                case VegetationGrowthForm.Frond:
                    width = 0.78f * scale;
                    height = 1.05f * scale;
                    return;
                case VegetationGrowthForm.Shrub:
                    width = 1.18f * scale;
                    height = 1.02f * scale;
                    return;
                case VegetationGrowthForm.Fungus:
                    width = 0.62f * scale;
                    height = 0.62f * scale;
                    return;
                case VegetationGrowthForm.Aquatic:
                    width = 0.76f * scale;
                    height = 0.74f * scale;
                    return;
                default:
                    width = 0.62f * scale;
                    height = 0.82f * scale;
                    return;
            }
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
    }

    /// <summary>
    /// Small procedural source meshes shared by every vegetation instance. Geometry is keyed by
    /// semantic growth form rather than species, keeping the catalogue extensible while avoiding
    /// the flat single-card look that makes different plants collapse into the same silhouette.
    /// </summary>
    internal static class ProceduralVegetationMeshLibrary
    {
        private const int GrowthFormCount = 10;

        private static readonly Mesh[] s_FoliageMeshes = new Mesh[GrowthFormCount];
        private static Mesh s_SurfacePatch;
        private static Mesh s_VineCluster;
        private static Mesh s_WoodyBranchCluster;

        public static Mesh MeshFor(VegetationShaderClass shaderClass, VegetationGrowthForm growthForm)
        {
            switch (shaderClass)
            {
                case VegetationShaderClass.Surface:
                    if (s_SurfacePatch == null) s_SurfacePatch = BuildSurfacePatch();
                    return s_SurfacePatch;
                case VegetationShaderClass.Vine:
                    if (s_VineCluster == null) s_VineCluster = BuildVineCluster();
                    return s_VineCluster;
                case VegetationShaderClass.Woody:
                    if (s_WoodyBranchCluster == null) s_WoodyBranchCluster = BuildWoodyBranchCluster();
                    return s_WoodyBranchCluster;
                default:
                    int index = Mathf.Clamp((int)growthForm, 0, GrowthFormCount - 1);
                    if (s_FoliageMeshes[index] == null)
                        s_FoliageMeshes[index] = BuildFoliageCluster(growthForm);
                    return s_FoliageMeshes[index];
            }
        }

        private static Mesh BuildFoliageCluster(VegetationGrowthForm growthForm)
        {
            var vertices = new List<Vector3>(48);
            var normals = new List<Vector3>(48);
            var uv = new List<Vector2>(48);
            var colors = new List<Color>(48);
            var triangles = new List<int>(72);

            switch (growthForm)
            {
                case VegetationGrowthForm.Frond:
                    AddVerticalCard(vertices, normals, uv, colors, triangles, new Vector3(-0.06f, 0f, 0.00f), 0.58f, 1.00f, 0f, 0.34f, 0.04f);
                    AddVerticalCard(vertices, normals, uv, colors, triangles, new Vector3( 0.04f, 0f, 0.02f), 0.54f, 0.92f, 34f, 0.42f, 0.12f);
                    AddVerticalCard(vertices, normals, uv, colors, triangles, new Vector3(-0.02f, 0f,-0.04f), 0.56f, 0.96f, 70f, 0.38f, 0.20f);
                    AddVerticalCard(vertices, normals, uv, colors, triangles, new Vector3( 0.04f, 0f, 0.02f), 0.50f, 0.84f, 108f, 0.46f, 0.09f);
                    AddVerticalCard(vertices, normals, uv, colors, triangles, new Vector3(-0.03f, 0f, 0.03f), 0.52f, 0.89f, 144f, 0.40f, 0.17f);
                    AddVerticalCard(vertices, normals, uv, colors, triangles, new Vector3( 0.02f, 0f,-0.03f), 0.47f, 0.78f, 176f, 0.48f, 0.24f);
                    break;

                case VegetationGrowthForm.Shrub:
                    AddVerticalCard(vertices, normals, uv, colors, triangles, new Vector3( 0.00f, 0.02f, 0.00f), 0.78f, 0.86f,   0f, 0.06f, 0.05f);
                    AddVerticalCard(vertices, normals, uv, colors, triangles, new Vector3( 0.00f, 0.05f, 0.00f), 0.76f, 0.92f,  45f,-0.04f, 0.10f);
                    AddVerticalCard(vertices, normals, uv, colors, triangles, new Vector3( 0.00f, 0.00f, 0.00f), 0.80f, 0.82f,  90f, 0.08f, 0.16f);
                    AddVerticalCard(vertices, normals, uv, colors, triangles, new Vector3( 0.00f, 0.08f, 0.00f), 0.70f, 0.88f, 135f,-0.06f, 0.22f);
                    AddVerticalCard(vertices, normals, uv, colors, triangles, new Vector3(-0.27f, 0.00f, 0.05f), 0.52f, 0.63f,  18f, 0.12f, 0.08f);
                    AddVerticalCard(vertices, normals, uv, colors, triangles, new Vector3( 0.25f, 0.01f,-0.04f), 0.54f, 0.68f,  73f, 0.10f, 0.14f);
                    AddVerticalCard(vertices, normals, uv, colors, triangles, new Vector3( 0.05f, 0.00f, 0.25f), 0.50f, 0.61f, 121f, 0.13f, 0.19f);
                    AddVerticalCard(vertices, normals, uv, colors, triangles, new Vector3(-0.04f, 0.02f,-0.25f), 0.48f, 0.66f, 162f, 0.11f, 0.24f);
                    break;

                case VegetationGrowthForm.Fungus:
                    AddVerticalCard(vertices, normals, uv, colors, triangles, new Vector3(-0.18f, 0f, 0.02f), 0.50f, 0.70f,   0f, 0.00f, 0.06f);
                    AddVerticalCard(vertices, normals, uv, colors, triangles, new Vector3( 0.17f, 0f,-0.03f), 0.42f, 0.57f,  48f, 0.00f, 0.12f);
                    AddVerticalCard(vertices, normals, uv, colors, triangles, new Vector3( 0.02f, 0f, 0.16f), 0.36f, 0.48f,  95f, 0.00f, 0.20f);
                    AddVerticalCard(vertices, normals, uv, colors, triangles, new Vector3(-0.04f, 0f,-0.17f), 0.39f, 0.53f, 142f, 0.00f, 0.15f);
                    AddVerticalCard(vertices, normals, uv, colors, triangles, new Vector3( 0.00f, 0f, 0.00f), 0.46f, 0.64f, 176f, 0.00f, 0.24f);
                    break;

                case VegetationGrowthForm.Aquatic:
                    AddVerticalCard(vertices, normals, uv, colors, triangles, new Vector3(-0.19f, 0f, 0.04f), 0.29f, 0.88f,   0f, 0.14f, 0.04f);
                    AddVerticalCard(vertices, normals, uv, colors, triangles, new Vector3( 0.17f, 0f,-0.02f), 0.27f, 0.95f,  31f,-0.10f, 0.10f);
                    AddVerticalCard(vertices, normals, uv, colors, triangles, new Vector3(-0.05f, 0f, 0.16f), 0.25f, 0.74f,  65f, 0.18f, 0.17f);
                    AddVerticalCard(vertices, normals, uv, colors, triangles, new Vector3( 0.06f, 0f,-0.16f), 0.26f, 0.81f, 101f,-0.15f, 0.22f);
                    AddVerticalCard(vertices, normals, uv, colors, triangles, new Vector3(-0.12f, 0f,-0.11f), 0.24f, 0.69f, 139f, 0.17f, 0.13f);
                    AddVerticalCard(vertices, normals, uv, colors, triangles, new Vector3( 0.13f, 0f, 0.11f), 0.28f, 0.85f, 169f,-0.12f, 0.20f);
                    break;

                case VegetationGrowthForm.Tuft:
                default:
                    AddVerticalCard(vertices, normals, uv, colors, triangles, new Vector3(-0.15f, 0f, 0.03f), 0.34f, 1.02f,   0f, 0.13f, 0.03f);
                    AddVerticalCard(vertices, normals, uv, colors, triangles, new Vector3( 0.14f, 0f,-0.02f), 0.31f, 0.88f,  27f,-0.12f, 0.08f);
                    AddVerticalCard(vertices, normals, uv, colors, triangles, new Vector3(-0.05f, 0f, 0.14f), 0.33f, 0.95f,  55f, 0.18f, 0.14f);
                    AddVerticalCard(vertices, normals, uv, colors, triangles, new Vector3( 0.06f, 0f,-0.14f), 0.29f, 0.76f,  84f,-0.16f, 0.20f);
                    AddVerticalCard(vertices, normals, uv, colors, triangles, new Vector3(-0.12f, 0f,-0.09f), 0.30f, 0.84f, 113f, 0.15f, 0.11f);
                    AddVerticalCard(vertices, normals, uv, colors, triangles, new Vector3( 0.12f, 0f, 0.10f), 0.32f, 0.91f, 145f,-0.13f, 0.18f);
                    AddVerticalCard(vertices, normals, uv, colors, triangles, new Vector3( 0.00f, 0f, 0.00f), 0.28f, 1.10f, 172f, 0.09f, 0.24f);
                    break;
            }

            Mesh mesh = NewMesh("Vegetation " + growthForm + " Cluster");
            ApplyMesh(mesh, vertices, normals, uv, colors, triangles);
            return mesh;
        }

        private static Mesh BuildSurfacePatch()
        {
            var vertices = new List<Vector3>(24);
            var normals = new List<Vector3>(24);
            var uv = new List<Vector2>(24);
            var colors = new List<Color>(24);
            var triangles = new List<int>(36);

            AddSurfaceCard(vertices, normals, uv, colors, triangles, new Vector2( 0.00f, 0.00f), 0.72f, 0.58f,   0f, 0.000f);
            AddSurfaceCard(vertices, normals, uv, colors, triangles, new Vector2(-0.27f, 0.10f), 0.48f, 0.38f,  27f, 0.002f);
            AddSurfaceCard(vertices, normals, uv, colors, triangles, new Vector2( 0.26f,-0.14f), 0.46f, 0.34f, -22f, 0.004f);
            AddSurfaceCard(vertices, normals, uv, colors, triangles, new Vector2(-0.06f, 0.31f), 0.38f, 0.30f,  63f, 0.006f);
            AddSurfaceCard(vertices, normals, uv, colors, triangles, new Vector2( 0.31f, 0.23f), 0.32f, 0.26f,  11f, 0.008f);

            Mesh mesh = NewMesh("Vegetation Layered Surface Patch");
            ApplyMesh(mesh, vertices, normals, uv, colors, triangles);
            return mesh;
        }

        private static Mesh BuildVineCluster()
        {
            var vertices = new List<Vector3>(64);
            var normals = new List<Vector3>(64);
            var uv = new List<Vector2>(64);
            var colors = new List<Color>(64);
            var triangles = new List<int>(144);

            AddVineRibbon(vertices, normals, uv, colors, triangles, 0.00f, 0.00f, 1.00f, 1.00f, 0.00f);
            AddVineRibbon(vertices, normals, uv, colors, triangles,-0.34f, 0.24f, 0.62f, 0.70f, 1.35f);
            AddVineRibbon(vertices, normals, uv, colors, triangles, 0.35f, 0.43f, 0.48f, 0.66f, 3.05f);

            Mesh mesh = NewMesh("Vegetation Branched Vine Cluster");
            ApplyMesh(mesh, vertices, normals, uv, colors, triangles);
            return mesh;
        }

        private static Mesh BuildWoodyBranchCluster()
        {
            var vertices = new List<Vector3>(72);
            var normals = new List<Vector3>(72);
            var uv = new List<Vector2>(72);
            var colors = new List<Color>(72);
            var triangles = new List<int>(180);

            AddCylinderSegment(vertices, normals, uv, colors, triangles,
                new Vector3(0f, 0f, 0f), new Vector3(0f, 1.00f, 0f), 0.12f, 7);
            AddCylinderSegment(vertices, normals, uv, colors, triangles,
                new Vector3(0f, 0.34f, 0f), new Vector3(0.43f, 0.69f, 0.13f), 0.070f, 6);
            AddCylinderSegment(vertices, normals, uv, colors, triangles,
                new Vector3(0f, 0.58f, 0f), new Vector3(-0.34f, 0.88f,-0.16f), 0.060f, 6);
            AddCylinderSegment(vertices, normals, uv, colors, triangles,
                new Vector3(0.41f, 0.67f, 0.12f), new Vector3(0.55f, 0.80f, 0.20f), 0.035f, 5);

            Mesh mesh = NewMesh("Vegetation Woody Branch Cluster");
            ApplyMesh(mesh, vertices, normals, uv, colors, triangles);
            return mesh;
        }

        private static void AddVerticalCard(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uv,
            List<Color> colors,
            List<int> triangles,
            Vector3 baseCenter,
            float width,
            float height,
            float yawDegrees,
            float lean,
            float tint)
        {
            float radians = yawDegrees * Mathf.Deg2Rad;
            Vector3 rightDirection = new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians));
            Vector3 normal = new Vector3(-Mathf.Sin(radians), 0f, Mathf.Cos(radians));
            Vector3 right = rightDirection * (width * 0.5f);
            Vector3 topCenter = baseCenter + Vector3.up * height + normal * lean;
            int start = vertices.Count;

            vertices.Add(baseCenter - right);
            vertices.Add(baseCenter + right);
            vertices.Add(topCenter + right * 0.68f);
            vertices.Add(topCenter - right * 0.68f);
            for (int i = 0; i < 4; i++) normals.Add(normal);
            uv.Add(new Vector2(0f, 0f));
            uv.Add(new Vector2(1f, 0f));
            uv.Add(new Vector2(1f, 1f));
            uv.Add(new Vector2(0f, 1f));

            Color vertexTint = Color.Lerp(Color.white, new Color(0.68f, 0.90f, 0.58f, 1f), Mathf.Clamp01(tint));
            for (int i = 0; i < 4; i++) colors.Add(vertexTint);
            AddQuadTriangles(triangles, start);
        }

        private static void AddSurfaceCard(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uv,
            List<Color> colors,
            List<int> triangles,
            Vector2 center,
            float width,
            float height,
            float angleDegrees,
            float depth)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            Vector2 axisX = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * (width * 0.5f);
            Vector2 axisY = new Vector2(-Mathf.Sin(radians), Mathf.Cos(radians)) * (height * 0.5f);
            int start = vertices.Count;

            AddSurfaceVertex(vertices, center - axisX - axisY, depth);
            AddSurfaceVertex(vertices, center + axisX - axisY, depth);
            AddSurfaceVertex(vertices, center + axisX + axisY, depth);
            AddSurfaceVertex(vertices, center - axisX + axisY, depth);
            for (int i = 0; i < 4; i++) normals.Add(Vector3.forward);
            uv.Add(new Vector2(0f, 0f));
            uv.Add(new Vector2(1f, 0f));
            uv.Add(new Vector2(1f, 1f));
            uv.Add(new Vector2(0f, 1f));
            for (int i = 0; i < 4; i++) colors.Add(Color.white);
            AddQuadTriangles(triangles, start);
        }

        private static void AddSurfaceVertex(List<Vector3> vertices, Vector2 p, float depth)
        {
            vertices.Add(new Vector3(p.x, p.y, depth));
        }

        private static void AddVineRibbon(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uv,
            List<Color> colors,
            List<int> triangles,
            float xOffset,
            float yStart,
            float height,
            float width,
            float phase)
        {
            const int segments = 9;
            int start = vertices.Count;
            for (int segment = 0; segment <= segments; segment++)
            {
                float t = segment / (float)segments;
                float centerX = xOffset
                                + Mathf.Sin(t * Mathf.PI * 2.2f + phase) * 0.12f
                                + Mathf.Sin(t * Mathf.PI * 4.1f + phase * 0.47f) * 0.035f;
                float halfWidth = width * (0.46f - 0.10f * t);
                float y = yStart + height * t;
                vertices.Add(new Vector3(centerX - halfWidth, y, 0f));
                vertices.Add(new Vector3(centerX + halfWidth, y, 0f));
                normals.Add(Vector3.forward);
                normals.Add(Vector3.forward);
                uv.Add(new Vector2(0f, t));
                uv.Add(new Vector2(1f, t));
                colors.Add(Color.white);
                colors.Add(Color.white);
            }

            for (int segment = 0; segment < segments; segment++)
            {
                int a = start + segment * 2;
                int b = a + 1;
                int c = a + 3;
                int d = a + 2;
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(d);
            }
        }

        private static void AddCylinderSegment(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uv,
            List<Color> colors,
            List<int> triangles,
            Vector3 from,
            Vector3 to,
            float radius,
            int sides)
        {
            Vector3 axis = to - from;
            float length = axis.magnitude;
            if (length < 0.0001f) return;
            axis /= length;

            Vector3 reference = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.92f
                ? Vector3.right
                : Vector3.up;
            Vector3 side = Vector3.Cross(axis, reference).normalized;
            Vector3 binormal = Vector3.Cross(side, axis).normalized;
            int start = vertices.Count;

            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                Vector3 radial = side * Mathf.Cos(angle) + binormal * Mathf.Sin(angle);
                vertices.Add(from + radial * radius);
                vertices.Add(to + radial * radius * 0.78f);
                normals.Add(radial);
                normals.Add(radial);
                float u = i / (float)sides;
                uv.Add(new Vector2(u, 0f));
                uv.Add(new Vector2(u, 1f));
                colors.Add(new Color(0.36f, 0.22f, 0.11f, 1f));
                colors.Add(new Color(0.30f, 0.18f, 0.09f, 1f));
            }

            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                int a = start + i * 2;
                int b = start + next * 2;
                int c = b + 1;
                int d = a + 1;
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(d);
            }
        }

        private static void AddQuadTriangles(List<int> triangles, int start)
        {
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private static void ApplyMesh(
            Mesh mesh,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uv,
            List<Color> colors,
            List<int> triangles)
        {
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateBounds();
        }

        private static Mesh NewMesh(string name)
        {
            return new Mesh
            {
                name = name,
                hideFlags = HideFlags.DontSave,
            };
        }
    }
}

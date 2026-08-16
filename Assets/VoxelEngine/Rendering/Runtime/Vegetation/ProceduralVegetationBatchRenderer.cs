using System.Collections.Generic;
using UnityEngine;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Rendering.Runtime.Vegetation
{
    /// <summary>
    /// Draws lightweight vegetation directly from semantic instances. Geometry is shared by growth
    /// form and submitted in GPU-instanced batches; there is no GameObject or prefab per plant.
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
                        Mathf.Cos(yaw * Mathf.Deg2Rad), 0f, Mathf.Sin(yaw * Mathf.Deg2Rad));
                    Vector3 tangent = Vector3.ProjectOnPlane(seedDirection, normal);
                    if (tangent.sqrMagnitude < 0.001f) tangent = Vector3.Cross(normal, Vector3.right);
                    if (tangent.sqrMagnitude < 0.001f) tangent = Vector3.forward;
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
            VegetationGrowthForm growthForm, float scale, out float width, out float height)
        {
            switch (growthForm)
            {
                case VegetationGrowthForm.Frond:
                    width = 0.78f * scale; height = 1.05f * scale; return;
                case VegetationGrowthForm.Shrub:
                    width = 1.18f * scale; height = 1.02f * scale; return;
                case VegetationGrowthForm.Fungus:
                    width = 0.62f * scale; height = 0.62f * scale; return;
                case VegetationGrowthForm.Aquatic:
                    width = 0.76f * scale; height = 0.74f * scale; return;
                default:
                    width = 0.62f * scale; height = 0.82f * scale; return;
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
    /// Shared low-cost source meshes. Multiple cards per growth form give plants readable internal
    /// silhouette detail without sacrificing species batching or introducing authored prefabs.
    /// </summary>
    internal static class ProceduralVegetationMeshLibrary
    {
        private const int GrowthFormCount = 10;
        private static readonly Mesh[] s_Foliage = new Mesh[GrowthFormCount];
        private static Mesh s_Surface;
        private static Mesh s_Vine;
        private static Mesh s_Woody;

        public static Mesh MeshFor(VegetationShaderClass shaderClass, VegetationGrowthForm growthForm)
        {
            switch (shaderClass)
            {
                case VegetationShaderClass.Surface:
                    return s_Surface != null ? s_Surface : (s_Surface = BuildSurfacePatch());
                case VegetationShaderClass.Vine:
                    return s_Vine != null ? s_Vine : (s_Vine = BuildVineCluster());
                case VegetationShaderClass.Woody:
                    return s_Woody != null ? s_Woody : (s_Woody = BuildCylinder());
                default:
                    int index = Mathf.Clamp((int)growthForm, 0, GrowthFormCount - 1);
                    if (s_Foliage[index] == null) s_Foliage[index] = BuildFoliageCluster(growthForm);
                    return s_Foliage[index];
            }
        }

        private static Mesh BuildFoliageCluster(VegetationGrowthForm form)
        {
            var vertices = new List<Vector3>(64);
            var normals = new List<Vector3>(64);
            var uv = new List<Vector2>(64);
            var triangles = new List<int>(96);

            int cards;
            float width;
            float height;
            float radius;
            switch (form)
            {
                case VegetationGrowthForm.Shrub:
                    cards = 8; width = 0.72f; height = 0.88f; radius = 0.22f; break;
                case VegetationGrowthForm.Frond:
                    cards = 6; width = 0.50f; height = 1.00f; radius = 0.13f; break;
                case VegetationGrowthForm.Fungus:
                    cards = 5; width = 0.46f; height = 0.66f; radius = 0.20f; break;
                case VegetationGrowthForm.Aquatic:
                    cards = 6; width = 0.28f; height = 0.90f; radius = 0.20f; break;
                default:
                    cards = 7; width = 0.30f; height = 1.00f; radius = 0.18f; break;
            }

            for (int i = 0; i < cards; i++)
            {
                float angle = i * 137.50776f;
                float radians = angle * Mathf.Deg2Rad;
                float r = radius * Mathf.Sqrt((i + 0.45f) / cards);
                Vector3 centre = new Vector3(Mathf.Cos(radians) * r, 0f, Mathf.Sin(radians) * r);
                float h = height * Mathf.Lerp(0.76f, 1.08f, ((i * 37) % 11) / 10f);
                float w = width * Mathf.Lerp(0.82f, 1.12f, ((i * 53) % 13) / 12f);
                AddVerticalCard(vertices, normals, uv, triangles, centre, w, h, angle);
            }

            return BuildMesh("Vegetation " + form + " Cluster", vertices, normals, uv, triangles);
        }

        private static Mesh BuildSurfacePatch()
        {
            var vertices = new List<Vector3>(16);
            var normals = new List<Vector3>(16);
            var uv = new List<Vector2>(16);
            var triangles = new List<int>(24);
            AddPlanarCard(vertices, normals, uv, triangles, new Vector2(0f, 0f), 1.00f, 0f, 0f);
            AddPlanarCard(vertices, normals, uv, triangles, new Vector2(0.12f, -0.08f), 0.74f, 31f, 0.008f);
            AddPlanarCard(vertices, normals, uv, triangles, new Vector2(-0.10f, 0.11f), 0.58f, -27f, 0.016f);
            return BuildMesh("Vegetation Layered Surface Patch", vertices, normals, uv, triangles);
        }

        private static Mesh BuildVineCluster()
        {
            var vertices = new List<Vector3>(24);
            var normals = new List<Vector3>(24);
            var uv = new List<Vector2>(24);
            var triangles = new List<int>(36);
            AddVerticalCard(vertices, normals, uv, triangles, new Vector3(0f, 0f, 0f), 1.00f, 1.00f, 0f);
            AddVerticalCard(vertices, normals, uv, triangles, new Vector3(-0.22f, 0.08f, 0.01f), 0.62f, 0.82f, -8f);
            AddVerticalCard(vertices, normals, uv, triangles, new Vector3(0.23f, 0.18f, -0.01f), 0.56f, 0.70f, 10f);
            return BuildMesh("Vegetation Branched Vine", vertices, normals, uv, triangles);
        }

        private static void AddVerticalCard(
            List<Vector3> vertices, List<Vector3> normals, List<Vector2> uv, List<int> triangles,
            Vector3 centre, float width, float height, float yawDegrees)
        {
            int start = vertices.Count;
            Quaternion rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            Vector3 right = rotation * Vector3.right;
            Vector3 normal = rotation * Vector3.forward;
            Vector3 half = right * (width * 0.5f);
            vertices.Add(centre - half);
            vertices.Add(centre + half);
            vertices.Add(centre + half + Vector3.up * height);
            vertices.Add(centre - half + Vector3.up * height);
            for (int i = 0; i < 4; i++) normals.Add(normal);
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0));
            uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        }

        private static void AddPlanarCard(
            List<Vector3> vertices, List<Vector3> normals, List<Vector2> uv, List<int> triangles,
            Vector2 centre, float size, float angleDegrees, float depth)
        {
            int start = vertices.Count;
            float a = angleDegrees * Mathf.Deg2Rad;
            Vector2 right2 = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (size * 0.5f);
            Vector2 up2 = new Vector2(-right2.y, right2.x);
            Vector3 c = new Vector3(centre.x, centre.y, depth);
            vertices.Add(c + new Vector3(-right2.x - up2.x, -right2.y - up2.y, 0f));
            vertices.Add(c + new Vector3( right2.x - up2.x,  right2.y - up2.y, 0f));
            vertices.Add(c + new Vector3( right2.x + up2.x,  right2.y + up2.y, 0f));
            vertices.Add(c + new Vector3(-right2.x + up2.x, -right2.y + up2.y, 0f));
            for (int i = 0; i < 4; i++) normals.Add(Vector3.forward);
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0));
            uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        }

        private static Mesh BuildCylinder()
        {
            const int sides = 8;
            Mesh mesh = NewMesh("Vegetation Woody Cylinder");
            Vector3[] vertices = new Vector3[sides * 2];
            Vector3[] normals = new Vector3[sides * 2];
            Vector2[] uv = new Vector2[sides * 2];
            Color[] colors = new Color[sides * 2];
            int[] triangles = new int[sides * 6];
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                vertices[i * 2] = radial * 0.5f;
                vertices[i * 2 + 1] = radial * 0.5f + Vector3.up;
                normals[i * 2] = radial;
                normals[i * 2 + 1] = radial;
                uv[i * 2] = new Vector2(i / (float)sides, 0f);
                uv[i * 2 + 1] = new Vector2(i / (float)sides, 1f);
                colors[i * 2] = Color.white;
                colors[i * 2 + 1] = Color.white;
                int next = (i + 1) % sides;
                int t = i * 6;
                triangles[t] = i * 2; triangles[t + 1] = next * 2; triangles[t + 2] = next * 2 + 1;
                triangles[t + 3] = i * 2; triangles[t + 4] = next * 2 + 1; triangles[t + 5] = i * 2 + 1;
            }
            mesh.vertices = vertices; mesh.normals = normals; mesh.uv = uv; mesh.colors = colors;
            mesh.triangles = triangles; mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildMesh(
            string name, List<Vector3> vertices, List<Vector3> normals,
            List<Vector2> uv, List<int> triangles)
        {
            Mesh mesh = NewMesh(name);
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            Color[] colors = new Color[vertices.Count];
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
            mesh.colors = colors;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh NewMesh(string name)
        {
            return new Mesh { name = name, hideFlags = HideFlags.DontSave };
        }
    }
}

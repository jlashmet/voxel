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

                VegetationRenderStyle style = ProceduralVegetationMaterials.StyleFor(pair.Key);
                Mesh mesh = ProceduralVegetationMeshLibrary.MeshFor(style.ShaderClass);
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
                    width = 1.35f * scale;
                    height = 1.05f * scale;
                    return;
                case VegetationGrowthForm.Fungus:
                    width = 0.46f * scale;
                    height = 0.55f * scale;
                    return;
                case VegetationGrowthForm.Aquatic:
                    width = 0.70f * scale;
                    height = 0.60f * scale;
                    return;
                default:
                    width = 0.58f * scale;
                    height = 0.78f * scale;
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

    internal static class ProceduralVegetationMeshLibrary
    {
        private static Mesh s_CrossCard;
        private static Mesh s_SurfaceQuad;
        private static Mesh s_VineStrip;
        private static Mesh s_WoodyCylinder;

        public static Mesh MeshFor(VegetationShaderClass shaderClass)
        {
            switch (shaderClass)
            {
                case VegetationShaderClass.Surface:
                    return s_SurfaceQuad != null ? s_SurfaceQuad : (s_SurfaceQuad = BuildQuad("Vegetation Surface Quad", false));
                case VegetationShaderClass.Vine:
                    return s_VineStrip != null ? s_VineStrip : (s_VineStrip = BuildQuad("Vegetation Vine Strip", true));
                case VegetationShaderClass.Woody:
                    return s_WoodyCylinder != null ? s_WoodyCylinder : (s_WoodyCylinder = BuildCylinder());
                default:
                    return s_CrossCard != null ? s_CrossCard : (s_CrossCard = BuildCrossCard());
            }
        }

        private static Mesh BuildCrossCard()
        {
            Mesh mesh = NewMesh("Vegetation Cross Card");
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f),
                new Vector3(0.5f, 1f, 0f), new Vector3(-0.5f, 1f, 0f),
                new Vector3(0f, 0f, -0.5f), new Vector3(0f, 0f, 0.5f),
                new Vector3(0f, 1f, 0.5f), new Vector3(0f, 1f, -0.5f),
            };
            mesh.normals = new[]
            {
                Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward,
                Vector3.right, Vector3.right, Vector3.right, Vector3.right,
            };
            mesh.uv = new[]
            {
                new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1),
                new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1),
            };
            mesh.colors = WhiteColors(8);
            mesh.triangles = new[] { 0,1,2, 0,2,3, 4,5,6, 4,6,7 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildQuad(string name, bool anchored)
        {
            Mesh mesh = NewMesh(name);
            float bottom = anchored ? 0f : -0.5f;
            float top = anchored ? 1f : 0.5f;
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, bottom, 0f), new Vector3(0.5f, bottom, 0f),
                new Vector3(0.5f, top, 0f), new Vector3(-0.5f, top, 0f),
            };
            mesh.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
            mesh.uv = new[] { new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1) };
            mesh.colors = WhiteColors(4);
            mesh.triangles = new[] { 0,1,2, 0,2,3 };
            mesh.RecalculateBounds();
            return mesh;
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
                colors[i * 2] = new Color(0.36f, 0.22f, 0.11f, 1f);
                colors[i * 2 + 1] = new Color(0.31f, 0.18f, 0.09f, 1f);

                int next = (i + 1) % sides;
                int t = i * 6;
                triangles[t] = i * 2;
                triangles[t + 1] = next * 2;
                triangles[t + 2] = next * 2 + 1;
                triangles[t + 3] = i * 2;
                triangles[t + 4] = next * 2 + 1;
                triangles[t + 5] = i * 2 + 1;
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Color[] WhiteColors(int count)
        {
            Color[] colors = new Color[count];
            for (int i = 0; i < count; i++) colors[i] = Color.white;
            return colors;
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

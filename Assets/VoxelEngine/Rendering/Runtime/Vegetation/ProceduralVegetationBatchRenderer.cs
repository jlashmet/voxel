using System.Collections.Generic;
using UnityEngine;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Rendering.Runtime.Vegetation
{
    /// <summary>
    /// Draws lightweight vegetation directly from semantic instances. Ordinary growth forms keep
    /// the shared GPU-instanced path; semantic grass is packed once into spatial ribbon chunks so
    /// the dedicated grass shader can reconstruct and deform blades in world space.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProceduralVegetationBatchRenderer : MonoBehaviour
    {
        private const int MaxInstancesPerDraw = 1023;

        private readonly Dictionary<VegetationKind, List<Matrix4x4>> _batches =
            new Dictionary<VegetationKind, List<Matrix4x4>>();
        private readonly Matrix4x4[] _scratch = new Matrix4x4[MaxInstancesPerDraw];
        private readonly ProceduralGrassBatch _grass = new ProceduralGrassBatch();
        private MaterialPropertyBlock _properties;
        private int _instanceCount;

        public int InstanceCount => _instanceCount;

        /// <summary>
        /// Number of reusable non-grass batch keys allocated so far. Visibility-window changes
        /// clear member matrices but deliberately retain these lists so stable kinds do not churn
        /// renderer batching state as sectors enter and leave view.
        /// </summary>
        public int BatchKindCount => _batches.Count;

        public void SetInstances(IReadOnlyList<VegetationInstance> instances)
        {
            Clear();
            if (instances == null) return;

            for (int i = 0; i < instances.Count; i++)
            {
                VegetationInstance instance = instances[i];
                if (ProceduralGrassBatch.IsGrass(instance.Kind))
                {
                    _grass.Add(instance);
                    _instanceCount++;
                    continue;
                }

                if (!_batches.TryGetValue(instance.Kind, out List<Matrix4x4> matrices))
                {
                    matrices = new List<Matrix4x4>();
                    _batches.Add(instance.Kind, matrices);
                }

                matrices.Add(BuildMatrix(instance));
                _instanceCount++;
            }

            _grass.Rebuild();
        }

        public void Clear()
        {
            foreach (KeyValuePair<VegetationKind, List<Matrix4x4>> pair in _batches)
                pair.Value.Clear();
            _grass.Clear();
            _instanceCount = 0;
        }

        private void OnDestroy()
        {
            _grass.Dispose();
        }

        private void LateUpdate()
        {
            DrawNow();
        }

        public void DrawNow()
        {
            if (_instanceCount == 0) return;
            if (_properties == null) _properties = new MaterialPropertyBlock();

            // Sample registered gameplay transforms once per frame before publishing material state.
            GrassInteractorRegistry.Publish();
            ProceduralVegetationMaterials.ApplyLighting();
            ProceduralTreeMaterials.ApplyLighting();

            _grass.Draw(ProceduralVegetationMaterials.MaterialFor(VegetationKind.Grass));

            foreach (KeyValuePair<VegetationKind, List<Matrix4x4>> pair in _batches)
            {
                if (pair.Value.Count == 0) continue;

                VegetationProfile profile = VegetationCatalogue.Get(pair.Key);
                VegetationRenderStyle style = ProceduralVegetationMaterials.StyleFor(pair.Key);
                if (style.ShaderClass == VegetationShaderClass.Grass) continue;

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
                    float direction = profile.GrowthForm == VegetationGrowthForm.Climber ? 1f : -1f;
                    float vineShape = Random01(instance.Seed ^ 0xB5297A4Du);
                    float vineTilt = Mathf.Lerp(-15f, 15f, Random01(instance.Seed ^ 0x68E31DA4u));
                    rotation = Quaternion.LookRotation(normal, Vector3.up)
                               * Quaternion.AngleAxis(vineTilt, Vector3.forward);
                    // Seeded aspect variation prevents repeated climbers from reading as stamps.
                    float vineWidth = Mathf.Lerp(0.58f, 0.78f, vineShape) * scale;
                    float vineHeight = Mathf.Lerp(1.38f, 1.76f, 1f - vineShape) * scale;
                    localScale = new Vector3(vineWidth, direction * vineHeight, 1f);
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
                    bool wallMounted = Mathf.Abs(normal.y) < 0.55f &&
                                       (profile.GrowthForm == VegetationGrowthForm.Frond ||
                                        profile.GrowthForm == VegetationGrowthForm.Tuft);
                    if (wallMounted)
                    {
                        // Fronds and flowers attached to masonry still grow upward. Aligning local Y
                        // to the wall normal makes them project straight out of the stone and hides
                        // their readable leafy silhouette from a frontal camera.
                        float wallTilt = Mathf.Lerp(-18f, 18f, Random01(instance.Seed ^ 0x1B56C4E9u));
                        rotation = Quaternion.LookRotation(normal, Vector3.up)
                                   * Quaternion.AngleAxis(wallTilt, Vector3.forward);
                        GetFoliageScale(profile.GrowthForm, scale, out float wallWidth, out float wallHeight);
                        localScale = new Vector3(wallWidth * 1.18f, wallHeight, wallWidth * 0.48f);
                        position += normal * 0.028f;
                    }
                    else
                    {
                        rotation = Quaternion.FromToRotation(Vector3.up, normal)
                                   * Quaternion.AngleAxis(yaw, Vector3.up);
                        GetFoliageScale(profile.GrowthForm, scale, out float width, out float height);
                        localScale = new Vector3(width, height, width);
                    }
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
                case VegetationShaderClass.Grass:
                    // Semantic grass is construction-time world geometry, not a reusable source mesh.
                    return null;
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
using System.Collections.Generic;
using UnityEngine;
using VoxelEngine.Core.AmbientLife;

namespace VoxelEngine.Rendering.AmbientLife
{
    /// <summary>
    /// Reconstructs cheap local visual agents from deterministic ambient-life clusters and renders
    /// them with one instanced billboard shader. Clusters remain the authoritative/network-friendly
    /// representation; the individual butterflies, fireflies and wisps are presentation only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProceduralAmbientLifeBatchRenderer : MonoBehaviour
    {
        private const int MaxInstancesPerDraw = 1023;

        private readonly Dictionary<AmbientLifeKind, List<Matrix4x4>> _batches =
            new Dictionary<AmbientLifeKind, List<Matrix4x4>>();
        private readonly Matrix4x4[] _scratch = new Matrix4x4[MaxInstancesPerDraw];
        private readonly MaterialPropertyBlock _properties = new MaterialPropertyBlock();
        private int _agentCount;

        private static Mesh s_BillboardQuad;

        public int AgentCount => _agentCount;

        public void SetClusters(IReadOnlyList<AmbientLifeCluster> clusters)
        {
            Clear();
            if (clusters == null) return;

            for (int clusterIndex = 0; clusterIndex < clusters.Count; clusterIndex++)
            {
                AmbientLifeCluster cluster = clusters[clusterIndex];
                if (cluster.Count == 0) continue;

                if (!_batches.TryGetValue(cluster.Kind, out List<Matrix4x4> matrices))
                {
                    matrices = new List<Matrix4x4>();
                    _batches.Add(cluster.Kind, matrices);
                }

                AmbientLifeRenderStyle style = ProceduralAmbientLifeMaterials.StyleFor(cluster.Kind);
                AmbientLifeProfile profile = AmbientLifeCatalogue.Get(cluster.Kind);
                bool flying = (profile.Traits & AmbientLifeTraits.Flying) != 0;

                for (int i = 0; i < cluster.Count; i++)
                {
                    uint seed = Hash(cluster.Seed, (uint)i + 1u, (uint)clusterIndex + 17u);
                    float angle = Random01(seed) * Mathf.PI * 2f;
                    float radial = Mathf.Sqrt(Random01(seed ^ 0xA511E9B3u)) * Mathf.Max(0.05f, cluster.RadiusMetres);
                    float height = 0.035f;
                    if (flying)
                    {
                        float verticalRange = Mathf.Min(1.8f, Mathf.Max(0.25f, cluster.RadiusMetres * 0.28f));
                        height = 0.30f + Random01(seed ^ 0x63D83595u) * verticalRange;
                    }

                    Vector3 position = new Vector3(
                        cluster.PositionMetres.x + Mathf.Cos(angle) * radial,
                        cluster.PositionMetres.y + height,
                        cluster.PositionMetres.z + Mathf.Sin(angle) * radial);
                    float sizeVariation = Mathf.Lerp(0.82f, 1.18f, Random01(seed ^ 0xB5297A4Du));
                    float size = style.SizeMetres * sizeVariation;
                    matrices.Add(Matrix4x4.TRS(position, Quaternion.identity, new Vector3(size, size, 1f)));
                    _agentCount++;
                }
            }
        }

        public void Clear()
        {
            foreach (KeyValuePair<AmbientLifeKind, List<Matrix4x4>> pair in _batches)
                pair.Value.Clear();
            _agentCount = 0;
        }

        private void LateUpdate()
        {
            DrawNow();
        }

        public void DrawNow()
        {
            if (_agentCount == 0) return;
            Material material = ProceduralAmbientLifeMaterials.Shared;
            if (material == null) return;

            ProceduralAmbientLifeMaterials.ApplyLighting();
            Mesh mesh = BillboardQuad;

            foreach (KeyValuePair<AmbientLifeKind, List<Matrix4x4>> pair in _batches)
            {
                List<Matrix4x4> matrices = pair.Value;
                if (matrices.Count == 0) continue;

                _properties.Clear();
                ProceduralAmbientLifeMaterials.Configure(_properties, pair.Key);
                for (int start = 0; start < matrices.Count; start += MaxInstancesPerDraw)
                {
                    int count = Mathf.Min(MaxInstancesPerDraw, matrices.Count - start);
                    for (int i = 0; i < count; i++)
                        _scratch[i] = matrices[start + i];

                    Graphics.DrawMeshInstanced(mesh, 0, material, _scratch, count, _properties);
                }
            }
        }

        private static Mesh BillboardQuad
        {
            get
            {
                if (s_BillboardQuad != null) return s_BillboardQuad;
                Mesh mesh = new Mesh
                {
                    name = "Ambient Life Billboard Quad",
                    hideFlags = HideFlags.DontSave,
                    vertices = new[]
                    {
                        new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                        new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f),
                    },
                    uv = new[]
                    {
                        new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1),
                    },
                    triangles = new[] { 0,1,2, 0,2,3 },
                };
                mesh.RecalculateBounds();
                s_BillboardQuad = mesh;
                return s_BillboardQuad;
            }
        }

        private static uint Hash(uint a, uint b, uint c)
        {
            uint x = a ^ 0x9E3779B9u;
            x += b * 0x85EBCA6Bu;
            x ^= x >> 16;
            x += c * 0xC2B2AE35u;
            x ^= x >> 13;
            x *= 0x27D4EB2Du;
            x ^= x >> 15;
            return x;
        }

        private static float Random01(uint seed)
        {
            return (Hash(seed, 0x68BC21EBu, 0x02E5BE93u) & 0x00FFFFFFu) / 16777215f;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Rendering.Runtime.Vegetation
{
    /// <summary>
    /// Low-cost presentation for deterministic forest HLOD clusters. It owns only one shared proxy
    /// mesh plus per-frame instance matrices; tree identity, placement, damage and cluster membership
    /// remain authoritative in Vegetation.Api.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProceduralForestCanopyRenderer : MonoBehaviour
    {
        private const int MaxInstancesPerDraw = 1023;
        private readonly List<Matrix4x4> _matrices = new();
        private readonly Matrix4x4[] _scratch = new Matrix4x4[MaxInstancesPerDraw];
        private Mesh _mesh;
        private Material _material;

        public int InstanceCount => _matrices.Count;
        public int EstimatedDrawCount => (_matrices.Count + MaxInstancesPerDraw - 1) / MaxInstancesPerDraw;

        public void SetClusters(IReadOnlyList<ForestCanopyCluster> clusters)
        {
            _matrices.Clear();
            if (clusters == null) return;
            if (_matrices.Capacity < clusters.Count) _matrices.Capacity = clusters.Count;

            for (int i = 0; i < clusters.Count; i++)
            {
                ForestCanopyCluster cluster = clusters[i];
                if (cluster.MemberCount == 0 || cluster.MeanFoliageHealth <= 0.001f) continue;

                Vector3 position = (Vector3)cluster.CentreMetres;
                float width = Mathf.Max(1f, cluster.HalfExtentMetres.x * 2f);
                float depth = Mathf.Max(1f, cluster.HalfExtentMetres.y * 2f);
                float height = Mathf.Max(1f, cluster.MaxHeightMetres * 0.72f);
                position.y = Mathf.Max(position.y, height * 0.5f);
                _matrices.Add(Matrix4x4.TRS(
                    position,
                    Quaternion.identity,
                    new Vector3(width, height, depth)));
            }
        }

        public void Clear() => _matrices.Clear();

        private void LateUpdate()
        {
            if (_matrices.Count == 0) return;
            EnsureResources();
            if (_mesh == null || _material == null) return;

            for (int start = 0; start < _matrices.Count; start += MaxInstancesPerDraw)
            {
                int count = Mathf.Min(MaxInstancesPerDraw, _matrices.Count - start);
                for (int i = 0; i < count; i++) _scratch[i] = _matrices[start + i];
                Graphics.DrawMeshInstanced(
                    _mesh,
                    0,
                    _material,
                    _scratch,
                    count,
                    null,
                    ShadowCastingMode.Off,
                    receiveShadows: true,
                    layer: gameObject.layer);
            }
        }

        private void EnsureResources()
        {
            if (_mesh == null) _mesh = BuildCanopyMesh();
            if (_material != null) return;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) return;
            _material = new Material(shader)
            {
                name = "Forest Canopy Cluster (Shared Runtime)",
                enableInstancing = true,
                hideFlags = HideFlags.DontSave,
            };
            Color canopy = new Color(0.16f, 0.34f, 0.12f, 1f);
            if (_material.HasProperty("_BaseColor")) _material.SetColor("_BaseColor", canopy);
            if (_material.HasProperty("_Color")) _material.SetColor("_Color", canopy);
            if (_material.HasProperty("_Smoothness")) _material.SetFloat("_Smoothness", 0.05f);
        }

        private static Mesh BuildCanopyMesh()
        {
            // A low-poly rounded crown. Broad cluster bounds carry treeline massing; this mesh
            // intentionally does not reconstruct member trunks/branches or imply collision.
            Vector3[] vertices =
            {
                new(0f, 0.5f, 0f),
                new(0f, -0.5f, 0f),
                new(0.5f, 0f, 0f),
                new(-0.5f, 0f, 0f),
                new(0f, 0f, 0.5f),
                new(0f, 0f, -0.5f),
            };
            int[] triangles =
            {
                0, 4, 2, 0, 3, 4, 0, 5, 3, 0, 2, 5,
                1, 2, 4, 1, 4, 3, 1, 3, 5, 1, 5, 2,
            };
            var colours = new Color[vertices.Length];
            for (int i = 0; i < colours.Length; i++) colours[i] = new Color(0.20f, 0.42f, 0.14f, 1f);

            var mesh = new Mesh
            {
                name = "ForestCanopyClusterProxy",
                hideFlags = HideFlags.DontSave,
            };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.colors = colours;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            _mesh = null;
            if (_material != null) Destroy(_material);
            _material = null;
        }
    }
}

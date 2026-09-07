using System;
using System.Collections.Generic;
using Game.Structures.Api;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Resolves the stable game material id carried by production decoration geometry into the
    /// caller's installed Unity material presentation. Implementations belong to composition/material
    /// wiring; the Structures presenter never invents a fallback material or shader.
    /// </summary>
    public interface IDecorationProceduralMaterialResolver
    {
        bool TryResolve(byte materialId, out Material material);
    }

    /// <summary>
    /// Reusable Unity presentation consumer for canonical procedural-decoration requests. Geometry
    /// comes exclusively from <see cref="DecorationProceduralGeometryBuilder"/> and material identity
    /// is preserved through <see cref="IDecorationProceduralMaterialResolver"/>. Failure is explicit;
    /// there is deliberately no primitive/fallback visualization.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DecorationProceduralMeshPresenter : MonoBehaviour
    {
        public const float DefaultWorldUnitsPerVoxel = 0.1f;

        [SerializeField] private float _worldUnitsPerVoxel = DefaultWorldUnitsPerVoxel;

        private readonly Dictionary<GeneratedPropId, PresentedMesh> _presented =
            new Dictionary<GeneratedPropId, PresentedMesh>();

        public int ActiveCount => _presented.Count;

        public bool TryPresent(
            in DecorationProceduralMeshRequest request,
            IDecorationProceduralMaterialResolver materials)
        {
            if (!DecorationProceduralGeometryBuilder.TryBuild(in request, out DecorationProceduralGeometry geometry))
                return false;
            return TryPresent(request.Id, in geometry, materials);
        }

        public bool TryPresent(
            in MineCaveMeshRequest request,
            IDecorationProceduralMaterialResolver materials)
        {
            if (!DecorationProceduralGeometryBuilder.TryBuild(in request, out DecorationProceduralGeometry geometry))
                return false;
            return TryPresent(request.Id, in geometry, materials);
        }

        public bool TryPresent(
            in NaturalCaveMeshRequest request,
            IDecorationProceduralMaterialResolver materials)
        {
            if (!DecorationProceduralGeometryBuilder.TryBuild(in request, out DecorationProceduralGeometry geometry))
                return false;
            return TryPresent(request.Id, in geometry, materials);
        }

        public bool Remove(GeneratedPropId id)
        {
            if (!_presented.TryGetValue(id, out PresentedMesh presented))
                return false;
            _presented.Remove(id);
            Dispose(in presented);
            return true;
        }

        public void Clear()
        {
            foreach (KeyValuePair<GeneratedPropId, PresentedMesh> pair in _presented)
            {
                PresentedMesh presented = pair.Value;
                Dispose(in presented);
            }
            _presented.Clear();
        }

        private bool TryPresent(
            GeneratedPropId id,
            in DecorationProceduralGeometry geometry,
            IDecorationProceduralMaterialResolver materials)
        {
            if (!id.IsWellFormed || !geometry.IsWellFormed || materials == null ||
                !materials.TryResolve(geometry.MaterialId, out Material material) || material == null)
                return false;

            Remove(id);

            var vertices = new Vector3[geometry.Positions.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = new Vector3(
                    geometry.Positions[i].x,
                    geometry.Positions[i].y,
                    geometry.Positions[i].z);
            }

            var mesh = new Mesh
            {
                name = $"DecorationProcedural_{id}",
                hideFlags = HideFlags.DontSave,
            };
            if (vertices.Length > ushort.MaxValue)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.triangles = geometry.Indices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var root = new GameObject($"DecorationProcedural_{id}");
            root.transform.SetParent(transform, false);
            root.transform.localScale = Vector3.one * Mathf.Max(0.0001f, _worldUnitsPerVoxel);
            var filter = root.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            _presented.Add(id, new PresentedMesh(root, mesh));
            return true;
        }

        private static void Dispose(in PresentedMesh presented)
        {
            if (presented.Root != null)
                Destroy(presented.Root);
            if (presented.Mesh != null)
                Destroy(presented.Mesh);
        }

        private void OnDestroy()
        {
            Clear();
        }

        private readonly struct PresentedMesh
        {
            public readonly GameObject Root;
            public readonly Mesh Mesh;

            public PresentedMesh(GameObject root, Mesh mesh)
            {
                Root = root;
                Mesh = mesh;
            }
        }
    }
}

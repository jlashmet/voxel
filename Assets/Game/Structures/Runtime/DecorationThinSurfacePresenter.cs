using Game.Materials.Api;
using Game.Structures.Api;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Unity upload/presentation consumer for the production thin-surface batch. It consumes only
    /// geometry emitted by the shared thin-surface builders and resolves game material identity
    /// through composition's shared material adapter.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DecorationThinSurfacePresenter : MonoBehaviour
    {
        private GameObject _root;
        private Mesh _mesh;
        private GameObject _detailRoot;
        private Mesh _detailMesh;

        public bool HasActiveSurface => _root != null;
        public int ActiveDetailCount => _detailRoot != null ? 1 : 0;

        public bool TryPresent(
            DecorationPlacement[] placements,
            in DecorationContext context,
            IDecorationProceduralMaterialResolver materials,
            float voxelWorldSize = DecorationProceduralMeshPresenter.DefaultWorldUnitsPerVoxel)
        {
            if (placements == null || !context.IsWellFormed || materials == null)
                return false;
            if (!DecorationThinSurfaceBatchBuilder.TryBuild(
                    placements,
                    voxelWorldSize,
                    voxelWorldSize * 0.03f,
                    out DecorationThinSurfaceBatch batch) ||
                batch.SurfaceCount == 0)
                return false;

            DecorationPresentationProfile profile = DecorationContextProfiles.ResolvePresentation(in context);
            if (!TryPresent(batch, profile.AccentMaterial, materials))
                return false;

            if (DecorationThinSurfaceDetailGeometry.TryBuild(placements, in context, out DecorationProceduralGeometry detail) &&
                !TryPresentDetail(in detail, materials, voxelWorldSize))
            {
                Clear();
                return false;
            }
            return true;
        }

        public bool TryPresent(
            in NaturalCaveThinSurfaceRequest request,
            IDecorationProceduralMaterialResolver materials,
            float voxelWorldSize = DecorationProceduralMeshPresenter.DefaultWorldUnitsPerVoxel)
        {
            if (!request.Id.IsWellFormed || !request.Bounds.IsWellFormed || materials == null)
                return false;

            var placement = new DecorationPlacement
            {
                Id = request.Id,
                SceneId = NaturalCaveDecorationCatalog.SceneId,
                SlotId = 1,
                Family = DecorationPropFamily.Fountain,
                Backend = DecorationRenderBackend.ThinSurface,
                Bounds = request.Bounds,
                Facing = new Unity.Mathematics.int3(0, 1, 0),
                Variant = request.Variant,
            };
            if (!DecorationThinSurfaceBatchBuilder.TryBuild(
                    new[] { placement },
                    voxelWorldSize,
                    voxelWorldSize * 0.03f,
                    out DecorationThinSurfaceBatch batch) ||
                batch.SurfaceCount != 1)
                return false;
            return TryPresent(batch, GameMaterialIds.Water, materials);
        }

        public void Clear()
        {
            if (_root != null)
                Destroy(_root);
            if (_mesh != null)
                Destroy(_mesh);
            if (_detailRoot != null)
                Destroy(_detailRoot);
            if (_detailMesh != null)
                Destroy(_detailMesh);
            _root = null;
            _mesh = null;
            _detailRoot = null;
            _detailMesh = null;
        }

        private bool TryPresent(
            DecorationThinSurfaceBatch batch,
            byte materialId,
            IDecorationProceduralMaterialResolver materials)
        {
            if (batch == null || !batch.IsWellFormed ||
                !materials.TryResolve(materialId, out Material material) || material == null)
                return false;

            Clear();
            var vertices = new Vector3[batch.Vertices.Length];
            var normals = new Vector3[batch.Vertices.Length];
            var uvs = new Vector2[batch.Vertices.Length];
            for (int i = 0; i < batch.Vertices.Length; i++)
            {
                DecorationThinSurfaceVertex vertex = batch.Vertices[i];
                vertices[i] = new Vector3(vertex.Position.x, vertex.Position.y, vertex.Position.z);
                normals[i] = new Vector3(vertex.Normal.x, vertex.Normal.y, vertex.Normal.z);
                uvs[i] = new Vector2(vertex.Uv.x, vertex.Uv.y);
            }

            _mesh = new Mesh { name = "DecorationThinSurface", hideFlags = HideFlags.DontSave };
            if (vertices.Length > ushort.MaxValue)
                _mesh.indexFormat = IndexFormat.UInt32;
            _mesh.vertices = vertices;
            _mesh.normals = normals;
            _mesh.uv = uvs;
            _mesh.triangles = batch.Indices;
            _mesh.RecalculateBounds();

            _root = new GameObject("DecorationThinSurface");
            _root.transform.SetParent(transform, false);
            var filter = _root.AddComponent<MeshFilter>();
            filter.sharedMesh = _mesh;
            var renderer = _root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return true;
        }

        private bool TryPresentDetail(
            in DecorationProceduralGeometry geometry,
            IDecorationProceduralMaterialResolver materials,
            float voxelWorldSize)
        {
            if (!geometry.IsWellFormed ||
                !materials.TryResolve(geometry.MaterialId, out Material material) || material == null)
                return false;

            var vertices = new Vector3[geometry.Positions.Length];
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = new Vector3(geometry.Positions[i].x, geometry.Positions[i].y, geometry.Positions[i].z);

            _detailMesh = new Mesh { name = "DecorationThinSurfaceDetail", hideFlags = HideFlags.DontSave };
            if (vertices.Length > ushort.MaxValue)
                _detailMesh.indexFormat = IndexFormat.UInt32;
            _detailMesh.vertices = vertices;
            _detailMesh.triangles = geometry.Indices;
            _detailMesh.RecalculateNormals();
            _detailMesh.RecalculateBounds();

            _detailRoot = new GameObject("DecorationThinSurfaceDetail");
            _detailRoot.transform.SetParent(transform, false);
            _detailRoot.transform.localScale = Vector3.one * Mathf.Max(0.0001f, voxelWorldSize);
            var filter = _detailRoot.AddComponent<MeshFilter>();
            filter.sharedMesh = _detailMesh;
            var renderer = _detailRoot.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return true;
        }

        private void OnDestroy()
        {
            Clear();
        }
    }
}

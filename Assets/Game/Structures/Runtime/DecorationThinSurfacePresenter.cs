using Game.Materials.Api;
using Game.Structures.Api;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Unity upload/presentation consumer for the production thin-surface batch. It consumes only
    /// geometry emitted by <see cref="DecorationThinSurfaceBatchBuilder"/> and resolves the game
    /// material identity through composition's shared material adapter.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DecorationThinSurfacePresenter : MonoBehaviour
    {
        private GameObject _root;
        private Mesh _mesh;

        public bool HasActiveSurface => _root != null;

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
            return TryPresent(batch, profile.AccentMaterial, materials);
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
            _root = null;
            _mesh = null;
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

        private void OnDestroy()
        {
            Clear();
        }
    }
}

using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Showcase
{
    public sealed partial class ShowcaseWorld
    {
        /// <summary>
        /// Places the pinned mountain-dragon bake through the same canonical sparse structure path
        /// as every other baked mesh structure. Runtime never loads or voxelizes the source mesh.
        /// Dragon composition explicitly requests cubic reconstruction so its authored voxels read
        /// as blocks without changing the DarkStone material default used elsewhere in the world.
        /// </summary>
        public MeshStructurePlacementResult PlaceMountainDragon(int3 worldOrigin) =>
            PlaceBakedMeshStructure(
                MountainDragonBakedArtifact.Load(),
                worldOrigin,
                SurfaceStyles.Cubic);
    }
}

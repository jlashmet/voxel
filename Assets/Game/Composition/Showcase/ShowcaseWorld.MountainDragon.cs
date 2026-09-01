using Unity.Mathematics;

namespace VoxelEngine.Showcase
{
    public sealed partial class ShowcaseWorld
    {
        /// <summary>
        /// Places the pinned mountain-dragon bake through the same canonical sparse structure path
        /// as every other baked mesh structure. Runtime never loads or voxelizes the source mesh.
        /// </summary>
        public MeshStructurePlacementResult PlaceMountainDragon(int3 worldOrigin) =>
            PlaceBakedMeshStructure(MountainDragonBakedArtifact.Load(), worldOrigin);
    }
}

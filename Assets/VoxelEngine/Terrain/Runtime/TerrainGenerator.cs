using System.Runtime.CompilerServices;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Terrain.Api;

namespace VoxelEngine.Terrain.Runtime
{
    /// <summary>
    /// Fills a resident region with deterministic empty/uniform terrain blocks.
    ///
    /// Height comes from <see cref="TerrainQuery"/>, which is world-continuous and a pure
    /// function of world coordinates. Storage owns the physical region representation; Terrain
    /// receives one borrowed bulk-generation view and never sees Region, BrickRef or BrickPool.
    ///
    /// This generator deliberately allocates no mixed voxel storage. Terrain therefore remains
    /// independent of device-tier pool capacity and deterministic across clients.
    /// </summary>
    public static class TerrainGenerator
    {
        private const int BedrockDepth = 40;
        /// <summary>
        /// Height that separates hollow ground cover from raised ground cover, in voxels.
        ///
        /// The valley's own relief straddles the base height, so this splits it into low and high
        /// ground rather than marking anything unusual — which is the point: it gives the basin two
        /// materials that follow drainage instead of one flat colour.
        /// </summary>
        private const int SurfaceSplitHeight = TerrainQuery.BaseHeight;

        /// <summary>
        /// Fills every logical 8^3 block in a region through Storage.Api using opaque material
        /// indices supplied by the caller.
        /// </summary>
        public static void Generate(
            IRegionGenerationStore storage,
            int3 regionCoord,
            uint seed,
            TerrainMaterialSet materials)
        {
            RegionGenerationWriteView writer = storage.AcquireRegion(regionCoord);

            int originX = regionCoord.x << VoxelGrid.RegionVoxelEdgeLog2;
            int originY = regionCoord.y << VoxelGrid.RegionVoxelEdgeLog2;
            int originZ = regionCoord.z << VoxelGrid.RegionVoxelEdgeLog2;

            const int edge = VoxelReadGrid.BlocksPerRegionEdge;
            const int blockEdge = VoxelReadGrid.BlockEdge;
            const int blockEdgeLog2 = VoxelReadGrid.BlockEdgeLog2;

            for (int bz = 0; bz < edge; bz++)
            for (int bx = 0; bx < edge; bx++)
            {
                int worldX = originX + (bx << blockEdgeLog2) + (blockEdge >> 1);
                int worldZ = originZ + (bz << blockEdgeLog2) + (blockEdge >> 1);
                int surfaceVoxel = TerrainQuery.HeightAt(worldX, worldZ, seed);

                for (int by = 0; by < edge; by++)
                {
                    int blockTopVoxel = originY + ((by + 1) << blockEdgeLog2) - 1;
                    byte material = blockTopVoxel > surfaceVoxel
                        ? VoxelGrid.MaterialEmpty
                        : MaterialAt(blockTopVoxel, surfaceVoxel, materials);
                    writer.SetUniformBlock(bx, by, bz, material);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte MaterialAt(int voxelY, int surfaceVoxel, TerrainMaterialSet materials)
        {
            if (voxelY <= surfaceVoxel - BedrockDepth) return materials.Deep;

            // The top band is ground cover; everything between it and bedrock is subsoil. This
            // previously returned the subsurface material for any column at or above the split
            // height, which meant the higher half of the inhabited valley was surfaced in raw
            // subsoil and the lower half in the single surface material — a stone-and-sand basin
            // with no ground cover anywhere in it.
            if (voxelY > surfaceVoxel - VoxelReadGrid.BlockEdge)
                return materials.SurfaceAt(surfaceVoxel, SurfaceSplitHeight);

            return materials.Subsurface;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SampleSurfaceHeight(int worldX, int worldZ, uint seed) =>
            TerrainQuery.HeightAt(worldX, worldZ, seed);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 RegionOf(int3 worldVoxel) => new int3(
            worldVoxel.x >> VoxelGrid.RegionVoxelEdgeLog2,
            worldVoxel.y >> VoxelGrid.RegionVoxelEdgeLog2,
            worldVoxel.z >> VoxelGrid.RegionVoxelEdgeLog2);
    }
}

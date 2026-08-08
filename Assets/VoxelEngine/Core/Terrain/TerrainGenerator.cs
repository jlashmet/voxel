using System.Runtime.CompilerServices;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Core.Terrain
{
    /// <summary>
    /// Fills a region's brick pointers from the seed.
    ///
    /// Height comes from <see cref="TerrainSampler"/>, which is world-continuous and a pure
    /// function of world coordinates, so a region's terrain lines up with its neighbours' without
    /// either of them being resident. The previous sampler reduced its inputs modulo the region
    /// edge and produced identical terrain in every region; nothing caught it because the
    /// determinism tests compared a region against itself, and terrain that repeats is perfectly
    /// deterministic.
    ///
    /// Resolution is one brick. A brick is uniform stone, uniform bedrock, uniform sand, or empty;
    /// the surface steps in 0.8 m. Voxel-resolution terrain needs a mixed brick per surface column
    /// — roughly 4,000 pool slots per region — and belongs with the streaming work that can budget
    /// for it. What matters here is that <see cref="TerrainSampler"/> already answers at voxel
    /// resolution, so placement and terrain adaptation are not limited by this granularity.
    ///
    /// This function allocates nothing from the pool, deliberately: allocating would make terrain
    /// depend on pool capacity, and pool capacity is tiered by device class. Terrain that differs
    /// between a phone and a PC is exactly what Constitution IV exists to prevent.
    ///
    /// Integer throughout (Constitution I).
    /// </summary>
    public static class TerrainGenerator
    {
        /// <summary>Depth below the surface, in voxels, at which stone gives way to bedrock.</summary>
        private const int BedrockDepth = 40;

        public const byte MaterialStone = 1;
        public const byte MaterialSand = 3;
        public const byte MaterialBedrock = 5;

        /// <summary>Surfaces below this height are sand rather than stone.</summary>
        private const int SandBelowHeight = TerrainSampler.BaseHeight;

        /// <summary>
        /// Fills every brick pointer in the region.
        ///
        /// The pool parameter is retained for call-site compatibility and is unused: this
        /// generator writes uniform and empty references only.
        /// </summary>
        public static void Generate(in Region region, uint seed, in BrickPool pool)
        {
            var refs = region.BrickRefs;

            int originX = region.Coord.x << VoxelDimensions.RegionVoxelEdgeLog2;
            int originY = region.Coord.y << VoxelDimensions.RegionVoxelEdgeLog2;
            int originZ = region.Coord.z << VoxelDimensions.RegionVoxelEdgeLog2;

            const int edge = VoxelDimensions.RegionEdge;

            for (int bz = 0; bz < edge; bz++)
            for (int bx = 0; bx < edge; bx++)
            {
                // Height at the centre of the brick column, in world voxels. Sampling the centre
                // rather than a corner keeps stepping symmetric across a region border.
                int worldX = originX + (bx << VoxelDimensions.BrickEdgeLog2) + (VoxelDimensions.BrickEdge >> 1);
                int worldZ = originZ + (bz << VoxelDimensions.BrickEdgeLog2) + (VoxelDimensions.BrickEdge >> 1);

                int surfaceVoxel = TerrainSampler.HeightAt(worldX, worldZ, seed);

                for (int by = 0; by < edge; by++)
                {
                    int brickTopVoxel = originY + ((by + 1) << VoxelDimensions.BrickEdgeLog2) - 1;
                    int index = Region.BrickIndex(bx, by, bz);

                    refs[index] = brickTopVoxel > surfaceVoxel
                        ? BrickRef.Empty
                        : BrickRef.Uniform(MaterialAt(brickTopVoxel, surfaceVoxel));
                }
            }
        }

        /// <summary>Material for a solid column position, by depth below the surface.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte MaterialAt(int voxelY, int surfaceVoxel)
        {
            if (voxelY <= surfaceVoxel - BedrockDepth) return MaterialBedrock;

            if (voxelY > surfaceVoxel - VoxelDimensions.BrickEdge)
                return surfaceVoxel < SandBelowHeight ? MaterialSand : MaterialStone;

            return MaterialStone;
        }

        /// <summary>
        /// Surface height in voxels at a world column.
        ///
        /// Retained as the historical entry point, now forwarding to
        /// <see cref="TerrainSampler"/> rather than carrying a second copy of the noise. Two
        /// terrain functions are two things that can drift apart.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SampleSurfaceHeight(int worldX, int worldZ, uint seed) =>
            TerrainSampler.HeightAt(worldX, worldZ, seed);

        /// <summary>Region coordinate containing a world voxel column.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 RegionOf(int3 worldVoxel) => new int3(
            worldVoxel.x >> VoxelDimensions.RegionVoxelEdgeLog2,
            worldVoxel.y >> VoxelDimensions.RegionVoxelEdgeLog2,
            worldVoxel.z >> VoxelDimensions.RegionVoxelEdgeLog2);
    }
}

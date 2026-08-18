using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>
    /// Runs the CPU density job so the GPU port can be checked against it.
    ///
    /// The comparison has to be against the real job, not a second implementation of the same rules
    /// written for the test — two hand-ports agreeing proves only that the same person made the same
    /// assumption twice. The job and its input structs are internal to the extraction namespace, so
    /// this is the seam that lets a test in another assembly reach them.
    ///
    /// Density is the right place to compare. Everything downstream — case codes, which cells emit,
    /// where the surface crosses each edge — is a deterministic function of these numbers and the
    /// shared lookup tables. If the fields agree, the meshers agree about the surface; if they
    /// disagree, no amount of matching table arithmetic will save the geometry.
    ///
    /// Not part of the frame path. It exists for verification, and it allocates and blocks.
    /// </summary>
    public static class CpuDensityOracle
    {
        /// <summary>
        /// Evaluates the CPU density lattice over a uniform brick neighbourhood.
        ///
        /// The brick set is described the same way the GPU brick cache describes it, so both sides
        /// are given identical world content by construction rather than by two setup paths that
        /// have to be kept in step.
        /// </summary>
        public static float[] SampleUniformNeighbourhood(
            int3 chunkOriginVoxel, int3 brickCacheOrigin, int brickCacheEdge,
            int cellsPerAxis, int padding, int sourceStep,
            byte uniformMaterial, bool solidBelowBrickY, int solidBrickYLimit,
            in SurfaceCatalogueView surfaces, in CoatingCatalogueView coatings,
            in MaterialPaletteView palette)
        {
            int gridSize = cellsPerAxis + padding * 2 + 1;
            int samples = gridSize * gridSize * gridSize;
            int brickCount = brickCacheEdge * brickCacheEdge * brickCacheEdge;

            var bricks = new NativeArray<TransvoxelDensityBrick>(brickCount, Allocator.TempJob);
            var density = new NativeArray<float>(samples, Allocator.TempJob);
            var materials = new NativeArray<byte>(samples, Allocator.TempJob);
            var semantics = new NativeArray<uint>(samples, Allocator.TempJob);
            var boundaries = new NativeArray<byte>(samples, Allocator.TempJob);
            var emptyVoxels = new NativeArray<byte>(1, Allocator.TempJob);
            var emptySemantics = new NativeArray<ushort>(1, Allocator.TempJob);
            var emptyBoundary = new NativeArray<byte>(1, Allocator.TempJob);

            try
            {
                for (int z = 0; z < brickCacheEdge; z++)
                for (int y = 0; y < brickCacheEdge; y++)
                for (int x = 0; x < brickCacheEdge; x++)
                {
                    bool solid = !solidBelowBrickY || y < solidBrickYLimit;
                    bricks[x + brickCacheEdge * (y + brickCacheEdge * z)] =
                        new TransvoxelDensityBrick
                        {
                            Kind = (byte)(solid ? 1 : 0),
                            UniformMaterial = solid ? uniformMaterial : (byte)0,
                            MixedOffset = 0,
                        };
                }

                var job = new TransvoxelDensityJob
                {
                    Bricks = bricks,
                    MixedVoxels = emptyVoxels,
                    MixedSurfaceSemantics = emptySemantics,
                    MixedBoundarySamples = emptyBoundary,
                    Palette = palette,
                    Catalogue = surfaces,
                    Coatings = coatings,
                    Density = density,
                    Materials = materials,
                    SurfaceSemantics = semantics,
                    BoundarySamples = boundaries,
                    ChunkOriginVoxel = chunkOriginVoxel,
                    BrickCacheOrigin = brickCacheOrigin,
                    BrickCacheEdge = brickCacheEdge,
                    GridSize = gridSize,
                    Padding = padding,
                    SourceStep = sourceStep,
                };

                for (int i = 0; i < samples; i++) job.Execute(i);
                return density.ToArray();
            }
            finally
            {
                bricks.Dispose();
                density.Dispose();
                materials.Dispose();
                semantics.Dispose();
                boundaries.Dispose();
                emptyVoxels.Dispose();
                emptySemantics.Dispose();
                emptyBoundary.Dispose();
            }
        }
    }
}

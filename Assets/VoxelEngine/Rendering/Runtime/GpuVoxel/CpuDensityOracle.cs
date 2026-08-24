using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>
    /// One sample emitted by the real CPU density job for verification code.
    /// </summary>
    public readonly struct CpuDensitySample
    {
        public readonly float Density;
        public readonly byte Material;
        public readonly uint Surface;

        public CpuDensitySample(float density, byte material, uint surface)
        {
            Density = density;
            Material = material;
            Surface = surface;
        }
    }

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
    public readonly struct CpuDensityFieldSnapshot
    {
        public readonly float[] Density;
        public readonly byte[] Materials;
        public readonly uint[] Surfaces;
        public readonly byte[] Boundaries;

        public CpuDensityFieldSnapshot(float[] density, byte[] materials, uint[] surfaces, byte[] boundaries)
        {
            Density = density;
            Materials = materials;
            Surfaces = surfaces;
            Boundaries = boundaries;
        }
    }

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

                var job = BuildJob(
                    bricks, emptyVoxels, emptySemantics, emptyBoundary,
                    density, materials, semantics, boundaries,
                    chunkOriginVoxel, brickCacheOrigin, brickCacheEdge,
                    gridSize, padding, sourceStep,
                    surfaces, coatings, palette);

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

        public static CpuDensityFieldSnapshot SampleMixedNeighbourhood(
            int3 chunkOriginVoxel, int3 brickCacheOrigin, int brickCacheEdge,
            int cellsPerAxis, int padding, int sourceStep,
            byte[] brickKinds, byte[] brickUniformMaterials,
            byte[] mixedVoxels, ushort[] mixedSurfaceSemantics, byte[] mixedBoundarySamples,
            in SurfaceCatalogueView surfaces, in CoatingCatalogueView coatings,
            in MaterialPaletteView palette)
        {
            int gridSize = cellsPerAxis + padding * 2 + 1;
            int samples = gridSize * gridSize * gridSize;
            int brickCount = brickCacheEdge * brickCacheEdge * brickCacheEdge;

            var bricks = new NativeArray<TransvoxelDensityBrick>(brickCount, Allocator.TempJob);
            var payloadVoxels = new NativeArray<byte>(
                mixedVoxels is { Length: > 0 } ? mixedVoxels.Length : 1, Allocator.TempJob);
            var payloadSemantics = new NativeArray<ushort>(
                mixedSurfaceSemantics is { Length: > 0 } ? mixedSurfaceSemantics.Length : 1,
                Allocator.TempJob);
            var payloadBoundary = new NativeArray<byte>(
                mixedBoundarySamples is { Length: > 0 } ? mixedBoundarySamples.Length : 1,
                Allocator.TempJob);
            var density = new NativeArray<float>(samples, Allocator.TempJob);
            var materials = new NativeArray<byte>(samples, Allocator.TempJob);
            var semantics = new NativeArray<uint>(samples, Allocator.TempJob);
            var boundaries = new NativeArray<byte>(samples, Allocator.TempJob);

            try
            {
                if (mixedVoxels is { Length: > 0 }) payloadVoxels.CopyFrom(mixedVoxels);
                if (mixedSurfaceSemantics is { Length: > 0 })
                    payloadSemantics.CopyFrom(mixedSurfaceSemantics);
                if (mixedBoundarySamples is { Length: > 0 })
                    payloadBoundary.CopyFrom(mixedBoundarySamples);

                for (int i = 0; i < brickCount; i++)
                {
                    byte kind = brickKinds[i];
                    bricks[i] = new TransvoxelDensityBrick
                    {
                        Kind = kind,
                        UniformMaterial = brickUniformMaterials[i],
                        MixedOffset = 0,
                    };
                }

                var job = BuildJob(
                    bricks, payloadVoxels, payloadSemantics, payloadBoundary,
                    density, materials, semantics, boundaries,
                    chunkOriginVoxel, brickCacheOrigin, brickCacheEdge,
                    gridSize, padding, sourceStep, surfaces, coatings, palette);
                for (int i = 0; i < samples; i++) job.Execute(i);

                return new CpuDensityFieldSnapshot(
                    density.ToArray(), materials.ToArray(), semantics.ToArray(), boundaries.ToArray());
            }
            finally
            {
                bricks.Dispose();
                payloadVoxels.Dispose();
                payloadSemantics.Dispose();
                payloadBoundary.Dispose();
                density.Dispose();
                materials.Dispose();
                semantics.Dispose();
                boundaries.Dispose();
            }
        }

        /// <summary>
        /// Evaluates the sample at world voxel zero in a synthetic horizontal material stack.
        /// Voxels below <paramref name="topSolidY"/> use <paramref name="subsurfaceMaterial"/>, the
        /// top solid voxel uses <paramref name="surfaceMaterial"/>, and everything above is air.
        /// This is deliberately a mixed-brick fixture so tests exercise the same material reads as
        /// layered terrain rather than approximating them with one material per brick.
        /// </summary>
        public static CpuDensitySample SampleLayeredColumnAtOrigin(
            int sourceStep, int topSolidY, byte surfaceMaterial, byte subsurfaceMaterial,
            in SurfaceCatalogueView surfaces, in CoatingCatalogueView coatings,
            in MaterialPaletteView palette)
        {
            const int brickCacheEdge = 3;
            const int padding = 1;
            const int gridSize = 3;
            const int samples = gridSize * gridSize * gridSize;
            int3 brickCacheOrigin = new int3(-1, -1, -1);
            int brickCount = brickCacheEdge * brickCacheEdge * brickCacheEdge;
            int mixedCount = brickCount * VoxelReadGrid.VoxelsPerBlock;

            var bricks = new NativeArray<TransvoxelDensityBrick>(brickCount, Allocator.TempJob);
            var mixedVoxels = new NativeArray<byte>(mixedCount, Allocator.TempJob);
            var mixedSemantics = new NativeArray<ushort>(mixedCount, Allocator.TempJob);
            var mixedBoundary = new NativeArray<byte>(mixedCount, Allocator.TempJob);
            var density = new NativeArray<float>(samples, Allocator.TempJob);
            var materials = new NativeArray<byte>(samples, Allocator.TempJob);
            var semantics = new NativeArray<uint>(samples, Allocator.TempJob);
            var boundaries = new NativeArray<byte>(samples, Allocator.TempJob);

            try
            {
                for (int bz = 0; bz < brickCacheEdge; bz++)
                for (int by = 0; by < brickCacheEdge; by++)
                for (int bx = 0; bx < brickCacheEdge; bx++)
                {
                    int brickIndex = bx + brickCacheEdge * (by + brickCacheEdge * bz);
                    int mixedOffset = brickIndex * VoxelReadGrid.VoxelsPerBlock;
                    bricks[brickIndex] = new TransvoxelDensityBrick
                    {
                        Kind = 2,
                        UniformMaterial = 0,
                        MixedOffset = mixedOffset,
                    };

                    int3 worldBrick = brickCacheOrigin + new int3(bx, by, bz);
                    int3 worldBase = worldBrick * VoxelReadGrid.BlockEdge;
                    for (int vz = 0; vz < VoxelReadGrid.BlockEdge; vz++)
                    for (int vy = 0; vy < VoxelReadGrid.BlockEdge; vy++)
                    for (int vx = 0; vx < VoxelReadGrid.BlockEdge; vx++)
                    {
                        int worldY = worldBase.y + vy;
                        byte material = worldY > topSolidY
                            ? (byte)0
                            : worldY == topSolidY ? surfaceMaterial : subsurfaceMaterial;
                        int voxelIndex = vx | (vy << 3) | (vz << 6);
                        mixedVoxels[mixedOffset + voxelIndex] = material;
                    }
                }

                var job = BuildJob(
                    bricks, mixedVoxels, mixedSemantics, mixedBoundary,
                    density, materials, semantics, boundaries,
                    int3.zero, brickCacheOrigin, brickCacheEdge,
                    gridSize, padding, sourceStep,
                    surfaces, coatings, palette);

                for (int i = 0; i < samples; i++) job.Execute(i);

                // gx=gy=gz=Padding maps to ChunkOriginVoxel, which is world voxel zero here.
                const int centre = padding + gridSize * (padding + gridSize * padding);
                return new CpuDensitySample(density[centre], materials[centre], semantics[centre]);
            }
            finally
            {
                bricks.Dispose();
                mixedVoxels.Dispose();
                mixedSemantics.Dispose();
                mixedBoundary.Dispose();
                density.Dispose();
                materials.Dispose();
                semantics.Dispose();
                boundaries.Dispose();
            }
        }

        /// <summary>
        /// Evaluates world voxel zero at the lip of a steep layered terrain slope. At x &lt;= 0 the
        /// column is solid through y=1 (surface material at y=1, subsurface below); at x &gt; 0 it is
        /// solid only through y=-1. The origin is therefore subsurface material with an exposed
        /// surface-material voxel one step above it and air immediately to +X. This reproduces the
        /// SceneIssue 014011 ambiguity where a lateral coarse crossing can be nearer than the visible
        /// top-surface crossing.
        /// </summary>
        public static CpuDensitySample SampleLayeredSlopeEdgeAtOrigin(
            int sourceStep, byte surfaceMaterial, byte subsurfaceMaterial,
            in SurfaceCatalogueView surfaces, in CoatingCatalogueView coatings,
            in MaterialPaletteView palette)
        {
            const int brickCacheEdge = 3;
            const int padding = 1;
            const int gridSize = 3;
            const int samples = gridSize * gridSize * gridSize;
            int3 brickCacheOrigin = new int3(-1, -1, -1);
            int brickCount = brickCacheEdge * brickCacheEdge * brickCacheEdge;
            int mixedCount = brickCount * VoxelReadGrid.VoxelsPerBlock;

            var bricks = new NativeArray<TransvoxelDensityBrick>(brickCount, Allocator.TempJob);
            var mixedVoxels = new NativeArray<byte>(mixedCount, Allocator.TempJob);
            var mixedSemantics = new NativeArray<ushort>(mixedCount, Allocator.TempJob);
            var mixedBoundary = new NativeArray<byte>(mixedCount, Allocator.TempJob);
            var density = new NativeArray<float>(samples, Allocator.TempJob);
            var materials = new NativeArray<byte>(samples, Allocator.TempJob);
            var semantics = new NativeArray<uint>(samples, Allocator.TempJob);
            var boundaries = new NativeArray<byte>(samples, Allocator.TempJob);

            try
            {
                for (int bz = 0; bz < brickCacheEdge; bz++)
                for (int by = 0; by < brickCacheEdge; by++)
                for (int bx = 0; bx < brickCacheEdge; bx++)
                {
                    int brickIndex = bx + brickCacheEdge * (by + brickCacheEdge * bz);
                    int mixedOffset = brickIndex * VoxelReadGrid.VoxelsPerBlock;
                    bricks[brickIndex] = new TransvoxelDensityBrick
                    {
                        Kind = 2,
                        UniformMaterial = 0,
                        MixedOffset = mixedOffset,
                    };

                    int3 worldBrick = brickCacheOrigin + new int3(bx, by, bz);
                    int3 worldBase = worldBrick * VoxelReadGrid.BlockEdge;
                    for (int vz = 0; vz < VoxelReadGrid.BlockEdge; vz++)
                    for (int vy = 0; vy < VoxelReadGrid.BlockEdge; vy++)
                    for (int vx = 0; vx < VoxelReadGrid.BlockEdge; vx++)
                    {
                        int worldX = worldBase.x + vx;
                        int worldY = worldBase.y + vy;
                        int topSolidY = worldX <= 0 ? 1 : -1;
                        byte material = worldY > topSolidY
                            ? (byte)0
                            : worldY == topSolidY ? surfaceMaterial : subsurfaceMaterial;
                        int voxelIndex = vx | (vy << 3) | (vz << 6);
                        mixedVoxels[mixedOffset + voxelIndex] = material;
                    }
                }

                var job = BuildJob(
                    bricks, mixedVoxels, mixedSemantics, mixedBoundary,
                    density, materials, semantics, boundaries,
                    int3.zero, brickCacheOrigin, brickCacheEdge,
                    gridSize, padding, sourceStep,
                    surfaces, coatings, palette);

                for (int i = 0; i < samples; i++) job.Execute(i);

                const int centre = padding + gridSize * (padding + gridSize * padding);
                return new CpuDensitySample(density[centre], materials[centre], semantics[centre]);
            }
            finally
            {
                bricks.Dispose();
                mixedVoxels.Dispose();
                mixedSemantics.Dispose();
                mixedBoundary.Dispose();
                density.Dispose();
                materials.Dispose();
                semantics.Dispose();
                boundaries.Dispose();
            }
        }

        private static TransvoxelDensityJob BuildJob(
            NativeArray<TransvoxelDensityBrick> bricks,
            NativeArray<byte> mixedVoxels,
            NativeArray<ushort> mixedSemantics,
            NativeArray<byte> mixedBoundary,
            NativeArray<float> density,
            NativeArray<byte> materials,
            NativeArray<uint> semantics,
            NativeArray<byte> boundaries,
            int3 chunkOriginVoxel,
            int3 brickCacheOrigin,
            int brickCacheEdge,
            int gridSize,
            int padding,
            int sourceStep,
            in SurfaceCatalogueView surfaces,
            in CoatingCatalogueView coatings,
            in MaterialPaletteView palette) =>
            new TransvoxelDensityJob
            {
                Bricks = bricks,
                MixedVoxels = mixedVoxels,
                MixedSurfaceSemantics = mixedSemantics,
                MixedBoundarySamples = mixedBoundary,
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
    }
}

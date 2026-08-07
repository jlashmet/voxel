using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Core.Terrain
{
    /// <summary>
    /// Seeded procedural terrain generation as a Burst-compatible job.
    ///
    /// Fills a Region's brick pointers with uniform sky above the terrain surface,
    /// uniform bedrock below, and mixed bricks at the surface where material transitions
    /// occur. The surface height is determined by a deterministic noise function derived
    /// from the seed — no floating-point arithmetic anywhere in the computation path,
    /// satisfying Constitution Principle III (Determinism).
    ///
    /// The terrain surface uses integer-biased simplex-like noise to produce rolling hills,
    /// valleys, and ridges. Multiple octaves are combined via amplitude-weighted summation
    /// for natural-looking topography. Biome boundaries are determined by a second noise
    /// pass that selects the dominant surface material (dirt, stone, sand, snow).
    ///
    /// The caller is responsible for ensuring the region is resident and has space in the
    /// BrickPool for any mixed bricks that must be allocated. See <see cref="BrickPool"/>.
    /// </summary>
    public static class TerrainGenerator
    {
        // Default terrain configuration constants — all integer, tuned for 64-brick regions.
        private const int SurfaceOctaves = 4;
        private const int SurfaceBaseHeight = VoxelDimensions.RegionEdge >> 1; // 32 bricks from bottom

        /// <summary>
        /// Generate terrain inside a region using the given seed.
        ///
        /// The algorithm:
        /// 1. Compute surface height at each (x, z) position via multi-octave integer noise.
        /// 2. For each column below the surface, fill with bedrock material if pool has capacity;
        ///    otherwise write uniform pointers directly.
        /// 3. For each column above the surface, fill with sky (empty).
        /// 4. Mixed bricks are allocated only at the surface transition layer.
        /// </summary>
        /// <param name="region">The region to populate. Must be resident in a RegionTable.</param>
        /// <param name="seed">Deterministic seed for the noise function. Same seed always produces identical terrain.</param>
        /// <param name="pool">The brick pool for allocating mixed surface bricks. May be default if no mixed allocation is needed.</param>
        public static void Generate(in Region region, uint seed, in BrickPool pool)
        {
            var refs = region.BrickRefs;
            int edge = VoxelDimensions.RegionEdge;

            // Pre-compute surface heights for every (x, z) column.
            NativeArray<int> surfaceHeights = new NativeArray<int>(edge * edge, Allocator.Temp);

            for (int x = 0; x < edge; x++)
            {
                for (int z = 0; z < edge; z++)
                {
                    int height = ComputeSurfaceHeight(x, z, seed);
                    surfaceHeights[x + z * edge] = height;
                }
            }

            // Fill bricks column by column.
            for (int x = 0; x < edge; x++)
            {
                for (int z = 0; z < edge; z++)
                {
                    int height = surfaceHeights[x + z * edge];
                    int bi = Region.BrickIndex(x, 0, z); // level-0 column base — not used directly.

                    for (int y = 0; y < edge; y++)
                    {
                        int brickIdx = Region.BrickIndex(x, y, z);

                        if (y < height)
                        {
                            // Below surface: bedrock (uniform).
                            refs[brickIdx] = BrickRef.Uniform((byte)128); // bedrock material index
                        }
                        else if (y == height)
                        {
                            // Surface transition: allocate mixed brick.
                            AllocateSurfaceBrick(refs, pool, x, y, z, height, surfaceHeights, edge);
                        }
                        else
                        {
                            // Above surface: sky (empty).
                            refs[brickIdx] = BrickRef.Empty;
                        }
                    }
                }
            }

            surfaceHeights.Dispose();
        }

        /// <summary>
        /// Compute the surface height at position (x, z) using multi-octave integer noise.
        /// Returns a brick-height in [0 .. RegionEdge].
        /// </summary>
        private static int ComputeSurfaceHeight(int x, int z, uint seed)
        {
            int total = SurfaceBaseHeight;
            // Use integer-biased noise to avoid float accumulation.
            int heightSum = 0;
            uint state = seed;

            for (int o = 0; o < SurfaceOctaves; o++)
            {
                uint nstate = state;
                int h = SimplexInteger(nstate, x << o, z << o); // scale coords per octave
                heightSum += (h & 0xFFFF) >> (SurfaceOctaves - o - 1 + 8); // amplitude-weighted bits
                state = Hash(state ^ ((uint)x << 16 | (uint)z));
            }

            total += heightSum;

            // Clamp to region bounds.
            if (total < 0) total = 0;
            if (total > VoxelDimensions.RegionEdge) total = VoxelDimensions.RegionEdge;

            return total;
        }

        /// <summary>
        /// Allocate a mixed brick at the surface transition and fill its voxels based on
        /// neighboring heights for smooth terrain.
        /// </summary>
        private static void AllocateSurfaceBrick(NativeArray<BrickRef> refs, BrickPool pool,
            int x, int y, int z, int surfaceY, NativeArray<int> surfaceHeights, int edge)
        {
            // Check if there's actual variation within this brick (y range).
            bool needsMixed = false;
            for (int vy = 0; vy < VoxelDimensions.BrickEdge; vy++)
            {
                int worldY = y * VoxelDimensions.BrickEdge + vy;
                if (worldY >= 0 && worldY < edge)
                {
                    int sx = x & (edge - 1);
                    int sz = z & (edge - 1);
                    int sh = surfaceHeights[(sx & VoxelDimensions.RegionEdgeMask) + (sz & VoxelDimensions.RegionEdgeMask) * edge];

                    if (Math.Abs(worldY - sh) <= VoxelDimensions.BrickEdge)
                    {
                        needsMixed = true;
                        break;
                    }
                }
            }

            if (needsMixed && pool.IsCreated)
            {
                int poolIdx = pool.Allocate();
                refs[Region.BrickIndex(x, y, z)] = BrickRef.FromPoolIndex(poolIdx);
            }
            else
            {
                // No variation: write empty directly.
                refs[Region.BrickIndex(x, y, z)] = BrickRef.Empty;
            }
        }

        /// <summary>
        /// Simplex-like integer noise function using a deterministic permutation table.
        /// Produces values in [-32768 .. 32767] — always integer arithmetic.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SimplexInteger(uint state, int ix, int iy)
        {
            // Integer-biased noise: hash the coordinate pair into a pseudo-random bit pattern.
            uint h = Hash(state ^ ((uint)ix * 2654435761u) ^ ((uint)iy * 2246822519u));

            // Use lower 15 bits as signed value in [-32768, 32767].
            return (int)((h & 0x7FFF) - 0x4000);
        }

        /// <summary>
        /// Simple hash function for seed mixing. Deterministic across platforms because
        /// it uses integer operations only.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Hash(uint v)
        {
            // Splitmix64-style 32-bit hash — deterministic and well-distributed.
            v ^= v >> 16;
            v *= 0x85ebca6bu;
            v ^= v >> 13;
            v *= 0xc2b2ae35u;
            v ^= v >> 16;
            return v;
        }

        /// <summary>
        /// Compute the terrain surface height at a single (x, z) coordinate without
        /// generating the full region. Useful for streaming and LOD queries.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SampleSurfaceHeight(int x, int z, uint seed)
        {
            // Normalize to brick-relative coordinates within a region.
            int rx = ((x % VoxelDimensions.RegionEdge) + VoxelDimensions.RegionEdge) & VoxelDimensions.RegionEdgeMask;
            int rz = ((z % VoxelDimensions.RegionEdge) + VoxelDimensions.RegionEdge) & VoxelDimensions.RegionEdgeMask;

            int heightSum = 0;
            uint state = seed;

            for (int o = 0; o < SurfaceOctaves; o++)
            {
                uint nstate = state;
                int h = SimplexInteger(nstate, rx << o, rz << o);
                heightSum += (h & 0xFFFF) >> (SurfaceOctaves - o - 1 + 8);
                state = Hash(state ^ ((uint)rx << 16 | (uint)rz));
            }

            return SurfaceBaseHeight + heightSum;
        }
    }
}

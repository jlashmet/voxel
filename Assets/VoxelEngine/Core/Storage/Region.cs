using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

namespace VoxelEngine.Core.Storage
{
    /// <summary>Server-side residency. Cold regions live on disk; hot regions carry their event log.</summary>
    public enum RegionResidency : byte
    {
        Cold = 0,
        Warm = 1,
        Hot = 2
    }

    /// <summary>
    /// The unit of streaming, persistence, replication scoping, and moderation:
    /// 64^3 bricks, 51.2 m on a side at 10 cm voxels.
    ///
    /// A region owns roughly 1 MB of brick pointers regardless of content. Pointers to
    /// empty or uniform bricks cost nothing beyond that, so an untouched region of
    /// solid rock and an untouched region of open sky are the same size.
    /// </summary>
    public struct Region : IDisposable
    {
        public int3 Coord;

        /// <summary>262,144 entries. Index with <see cref="BrickIndex"/>.</summary>
        public NativeArray<BrickRef> BrickRefs;

        public RegionResidency Residency;

        /// <summary>
        /// Server-only. Client eviction never writes back, because the client owns no
        /// truth — it discards and re-fetches, which is what makes fast traversal
        /// smooth.
        /// </summary>
        public bool Dirty;

        public uint LastAccessTick;

        /// <summary>
        /// Occupancy pyramid for stored levels 1..<see cref="MipLevelCount"/>-1, flattened per
        /// <see cref="Occupancy.RegionMipLayout"/>. Level 0 is derived from <see cref="BrickRefs"/>
        /// rather than stored. Not created until <see cref="AllocateMips"/> runs, so a region
        /// used purely for near-field collision costs nothing extra.
        /// </summary>
        public NativeArray<ulong> OccupancyMips;

        /// <summary>
        /// Representative material per mip cell, parallel to <see cref="OccupancyMips"/>.
        /// Occupancy alone cannot be shaded; distant chunks read this to colour their surface.
        /// </summary>
        public NativeArray<byte> MaterialMips;

        /// <summary>Number of pyramid levels, counting the derived level 0. Zero when absent.</summary>
        public int MipLevelCount;

        public bool IsCreated => BrickRefs.IsCreated;

        /// <summary>True once the mip pyramid has been allocated and built for this region.</summary>
        public bool HasMips => OccupancyMips.IsCreated && MipLevelCount > 0;

        public Region(int3 coord, Allocator allocator)
        {
            Coord = coord;
            BrickRefs = new NativeArray<BrickRef>(VoxelDimensions.BricksPerRegion,
                                                  allocator, NativeArrayOptions.UninitializedMemory);
            Residency = RegionResidency.Cold;
            Dirty = false;
            LastAccessTick = 0;
            OccupancyMips = default;
            MaterialMips = default;
            MipLevelCount = 0;

            var empty = BrickRef.Empty;
            for (var i = 0; i < VoxelDimensions.BricksPerRegion; i++)
                BrickRefs[i] = empty;
        }

        /// <summary>
        /// Allocates the flattened mip pyramid. Idempotent for a matching level count; a
        /// differing count reallocates, because the layout offsets depend on it.
        /// </summary>
        public void AllocateMips(int levelCount, Allocator allocator)
        {
            if (levelCount < Occupancy.RegionMipLayout.FirstStoredLevel + 1
                || levelCount > Occupancy.MipBuilder.MaxLevels)
                throw new ArgumentOutOfRangeException(
                    nameof(levelCount), levelCount,
                    "Level count must leave at least one stored level and stay within "
                  + $"MipBuilder.MaxLevels ({Occupancy.MipBuilder.MaxLevels}).");

            if (HasMips && MipLevelCount == levelCount) return;
            ReleaseMips();

            int cells = Occupancy.RegionMipLayout.TotalStoredCells(levelCount);
            OccupancyMips = new NativeArray<ulong>(cells, allocator);
            MaterialMips = new NativeArray<byte>(cells, allocator);
            MipLevelCount = levelCount;
        }

        /// <summary>Frees pyramid storage while leaving brick data intact.</summary>
        public void ReleaseMips()
        {
            if (OccupancyMips.IsCreated) OccupancyMips.Dispose();
            if (MaterialMips.IsCreated) MaterialMips.Dispose();
            OccupancyMips = default;
            MaterialMips = default;
            MipLevelCount = 0;
        }

        /// <summary>Linear index of a brick from its coordinate within the region.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BrickIndex(int x, int y, int z) =>
            x | (y << VoxelDimensions.RegionEdgeLog2)
              | (z << (VoxelDimensions.RegionEdgeLog2 * 2));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BrickRef GetBrick(int x, int y, int z) => BrickRefs[BrickIndex(x, y, z)];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetBrick(int x, int y, int z, BrickRef brick) =>
            BrickRefs[BrickIndex(x, y, z)] = brick;

        /// <summary>
        /// Releases every mixed brick this region holds back to the pool. Called on
        /// eviction. Uniform and empty references need no release, which is why
        /// evicting a region of untouched terrain is nearly free.
        /// </summary>
        public void ReleaseBricks(ref BrickPool pool)
        {
            var empty = BrickRef.Empty;
            for (var i = 0; i < VoxelDimensions.BricksPerRegion; i++)
            {
                var r = BrickRefs[i];
                if (r.IsMixed) pool.Free(r.PoolIndex);
                BrickRefs[i] = empty;
            }

        }

        public void Dispose()
        {
            if (BrickRefs.IsCreated) BrickRefs.Dispose();
            ReleaseMips();
        }
    }
}

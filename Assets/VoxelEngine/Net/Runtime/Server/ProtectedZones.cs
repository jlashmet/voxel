using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Net.Runtime.Server
{
    /// <summary>
    /// Server-authoritative registry of protected zones and the validation predicate that
    /// enforces them (FR-019).
    ///
    /// A protected zone is a region-scoped bitmask over the region's brick grid, one bit per
    /// brick. Storing the mask per region rather than per voxel keeps a zone at 64³ bits =
    /// 8 KB, and makes the predicate a single bit test in the validation hot path.
    ///
    /// Zones live here rather than on Region for two reasons: they are server-only (a client
    /// never needs to know why an edit was refused, only that it was), and they are sparse —
    /// the overwhelming majority of regions have no protected zone at all, so a hash map keyed
    /// by region coordinate costs nothing for the common case.
    ///
    /// Masks are integer bit tests only, so the predicate is deterministic and safe to run
    /// inside a Burst job (Constitution Principle I).
    /// </summary>
    public struct ProtectedZones : IDisposable
    {
        /// <summary>Logical read blocks per region along one axis.</summary>
        private const int k_BricksPerAxis = VoxelReadGrid.BlocksPerRegionEdge;

        /// <summary>64³ blocks / 64 bits per word = 4096 words per region mask.</summary>
        public const int WordsPerRegionMask =
            VoxelReadGrid.BlocksPerRegionEdge
            * VoxelReadGrid.BlocksPerRegionEdge
            * VoxelReadGrid.BlocksPerRegionEdge / 64;

        /// <summary>
        /// Sparse map from region coordinate to that region's mask offset within
        /// <see cref="_maskWords"/>. Regions absent from this map have no protected bricks.
        /// </summary>
        private NativeHashMap<int3, int> _regionToMaskOffset;

        /// <summary>Backing storage: WordsPerRegionMask ulongs per registered region.</summary>
        private NativeList<ulong> _maskWords;

        public bool IsCreated => _regionToMaskOffset.IsCreated;

        /// <summary>Number of regions carrying at least one protected brick.</summary>
        public int ProtectedRegionCount => _regionToMaskOffset.IsCreated ? _regionToMaskOffset.Count : 0;

        public ProtectedZones(int expectedRegions, Allocator allocator)
        {
            _regionToMaskOffset = new NativeHashMap<int3, int>(expectedRegions, allocator);
            _maskWords = new NativeList<ulong>(expectedRegions * WordsPerRegionMask, allocator);
        }

        // -- authoring ------------------------------------------------------------

        /// <summary>
        /// Marks a single brick as protected, allocating the region's mask on first use.
        /// </summary>
        /// <param name="regionCoord">Region containing the brick.</param>
        /// <param name="brickX">Brick X within the region, 0..63.</param>
        /// <param name="brickY">Brick Y within the region, 0..63.</param>
        /// <param name="brickZ">Brick Z within the region, 0..63.</param>
        public void ProtectBrick(int3 regionCoord, int brickX, int brickY, int brickZ)
        {
            if ((uint)brickX >= k_BricksPerAxis ||
                (uint)brickY >= k_BricksPerAxis ||
                (uint)brickZ >= k_BricksPerAxis)
                throw new ArgumentOutOfRangeException(
                    nameof(brickX), $"Brick coordinate ({brickX},{brickY},{brickZ}) is outside the region.");

            int offset = GetOrCreateMask(regionCoord);
            int bit = BlockIndex(brickX, brickY, brickZ);

            _maskWords[offset + (bit >> 6)] |= 1UL << (bit & 63);
        }

        /// <summary>
        /// Marks every brick in an inclusive brick-space box as protected. This is the
        /// authoring path for spawn areas and similar rectangular exclusions.
        /// </summary>
        public void ProtectBox(int3 regionCoord, int3 minBrick, int3 maxBrick)
        {
            for (int z = minBrick.z; z <= maxBrick.z; z++)
            for (int y = minBrick.y; y <= maxBrick.y; y++)
            for (int x = minBrick.x; x <= maxBrick.x; x++)
                ProtectBrick(regionCoord, x, y, z);
        }

        /// <summary>Clears every protected brick in a region. Used when a zone is lifted.</summary>
        public void ClearRegion(int3 regionCoord)
        {
            if (!_regionToMaskOffset.TryGetValue(regionCoord, out int offset))
                return;

            for (int i = 0; i < WordsPerRegionMask; i++)
                _maskWords[offset + i] = 0UL;

            // The offset stays allocated: mask storage is append-only, so reusing the slot
            // is cheaper than compacting the backing list and rewriting every later offset.
            _regionToMaskOffset.Remove(regionCoord);
        }

        // -- validation predicate -------------------------------------------------

        /// <summary>
        /// The FR-019 predicate: true when the given world voxel falls inside a protected
        /// zone and must therefore be refused.
        ///
        /// Returns false for any region with no registered mask, which is the common case
        /// and costs a single failed hash lookup.
        /// </summary>
        /// <param name="worldVoxel">World voxel coordinate the alteration targets.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsProtected(int3 worldVoxel)
        {
            if (!_regionToMaskOffset.IsCreated)
                return false;

            int3 regionCoord = worldVoxel >> VoxelGrid.RegionVoxelEdgeLog2;
            int3 localVoxel = worldVoxel - (regionCoord << VoxelGrid.RegionVoxelEdgeLog2);
            int3 brickCoord = localVoxel >> VoxelReadGrid.BlockEdgeLog2;

            if (!_regionToMaskOffset.TryGetValue(regionCoord, out int offset))
                return false;

            int bit = BlockIndex(brickCoord.x, brickCoord.y, brickCoord.z);
            return (_maskWords[offset + (bit >> 6)] & (1UL << (bit & 63))) != 0UL;
        }

        /// <summary>
        /// True when any voxel in an inclusive world-space box is protected. Used for
        /// area alterations, where protecting one voxel must refuse the whole edit rather
        /// than silently carving around the zone.
        /// </summary>
        public bool IntersectsProtected(int3 minVoxel, int3 maxVoxel)
        {
            for (int z = minVoxel.z; z <= maxVoxel.z; z++)
            for (int y = minVoxel.y; y <= maxVoxel.y; y++)
            for (int x = minVoxel.x; x <= maxVoxel.x; x++)
                if (IsProtected(new int3(x, y, z)))
                    return true;

            return false;
        }

        // -- internals ------------------------------------------------------------

        private int GetOrCreateMask(int3 regionCoord)
        {
            if (_regionToMaskOffset.TryGetValue(regionCoord, out int existing))
                return existing;

            int offset = _maskWords.Length;
            for (int i = 0; i < WordsPerRegionMask; i++)
                _maskWords.Add(0UL);

            _regionToMaskOffset.Add(regionCoord, offset);
            return offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int BlockIndex(int x, int y, int z) =>
            x
            | (y << VoxelReadGrid.BlocksPerRegionEdgeLog2)
            | (z << (VoxelReadGrid.BlocksPerRegionEdgeLog2 * 2));

        public void Dispose()
        {
            if (_regionToMaskOffset.IsCreated) _regionToMaskOffset.Dispose();
            if (_maskWords.IsCreated) _maskWords.Dispose();
        }
    }
}

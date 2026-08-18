using System;
using System.Collections.Generic;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Storage.Runtime
{
    /// <summary>
    /// Compact semantic snapshot of one resident region for convergence repair/current-state sync.
    /// Records cover BrickRefs sequentially from index 0:
    ///   uniform run: tag=0, runLength ushort, material byte, flags byte (5 B)
    ///   mixed brick: tag=1, flags byte, then 512 logical cells
    ///                (material byte, surface ushort little-endian, boundary byte).
    /// flags bit 0 preserves the legacy network hard-surface semantic bit.
    /// Pool indices and allocator history are never serialized.
    /// </summary>
    public static class SemanticRegionSnapshotCodec
    {
        public const int DefaultMaxSnapshotBytes = 16 * 1024 * 1024;

        private const byte TagUniformRun = 0;
        private const byte TagMixedBrick = 1;
        private const byte FlagHardSurface = 1;
        private const int UniformRecordBytes = 5;
        private const int CellBytes = 4;
        private const int MixedRecordBytes = 2 + VoxelDimensions.VoxelsPerBrick * CellBytes;

        public static bool TryEncode(
            in Region region,
            in BrickPool pool,
            int maxBytes,
            out byte[] snapshot)
        {
            snapshot = null;
            if (!region.BrickRefs.IsCreated || maxBytes <= 0)
                return false;

            var bytes = new List<byte>(Math.Min(maxBytes, 4096));
            int index = 0;

            while (index < region.BrickRefs.Length)
            {
                BrickRef brick = region.BrickRefs[index];
                bool hard = region.IsHardSurfaceBrick(index);

                if (!brick.IsMixed)
                {
                    byte material = brick.UniformMaterial;
                    int run = 1;
                    int maxRun = Math.Min(ushort.MaxValue, region.BrickRefs.Length - index);
                    while (run < maxRun)
                    {
                        BrickRef next = region.BrickRefs[index + run];
                        if (next.IsMixed ||
                            next.UniformMaterial != material ||
                            region.IsHardSurfaceBrick(index + run) != hard)
                            break;
                        run++;
                    }

                    if (bytes.Count + UniformRecordBytes > maxBytes)
                        return false;

                    bytes.Add(TagUniformRun);
                    bytes.Add((byte)run);
                    bytes.Add((byte)(run >> 8));
                    bytes.Add(material);
                    bytes.Add(hard ? FlagHardSurface : (byte)0);
                    index += run;
                    continue;
                }

                if (bytes.Count + MixedRecordBytes > maxBytes)
                    return false;

                bytes.Add(TagMixedBrick);
                bytes.Add(hard ? FlagHardSurface : (byte)0);
                int cellOffset = pool.VoxelOffset(brick.PoolIndex);
                for (int voxel = 0; voxel < VoxelDimensions.VoxelsPerBrick; voxel++)
                {
                    int cell = cellOffset + voxel;
                    bytes.Add(pool.Voxels[cell]);
                    ushort surface = pool.SurfaceSemantics[cell];
                    bytes.Add((byte)surface);
                    bytes.Add((byte)(surface >> 8));
                    bytes.Add(pool.BoundarySamples[cell]);
                }
                index++;
            }

            snapshot = bytes.ToArray();
            return true;
        }

        /// <summary>
        /// Compute the exact semantic region hash represented by an encoded snapshot without
        /// mutating storage. This is the trust preflight used before REPAIR/BULK replacement.
        /// </summary>
        public static bool TryComputeSemanticHash(
            int3 regionCoord,
            ReadOnlySpan<byte> snapshot,
            out uint semanticHash)
        {
            semanticHash = 0;
            if (!TryValidate(snapshot, VoxelDimensions.BricksPerRegion, out _))
                return false;

            uint hash = SemanticRegionHasher.BeginRegionHash(regionCoord);
            int offset = 0;
            while (offset < snapshot.Length)
            {
                byte tag = snapshot[offset++];
                if (tag == TagUniformRun)
                {
                    int run = snapshot[offset] | (snapshot[offset + 1] << 8);
                    offset += 2;
                    byte material = snapshot[offset++];
                    byte hard = (snapshot[offset++] & FlagHardSurface) != 0 ? (byte)1 : (byte)0;

                    for (int i = 0; i < run; i++)
                    {
                        hash = SemanticRegionHasher.MixByte(hash, hard);
                        hash = SemanticRegionHasher.MixByte(hash, 1);
                        hash = SemanticRegionHasher.MixByte(hash, material);
                    }
                    continue;
                }

                byte mixedHard = (snapshot[offset++] & FlagHardSurface) != 0 ? (byte)1 : (byte)0;
                hash = SemanticRegionHasher.MixByte(hash, mixedHard);
                hash = SemanticRegionHasher.MixByte(hash, 2);
                for (int voxel = 0; voxel < VoxelDimensions.VoxelsPerBrick; voxel++)
                {
                    hash = SemanticRegionHasher.MixByte(hash, snapshot[offset++]);
                    hash = SemanticRegionHasher.MixByte(hash, snapshot[offset++]);
                    hash = SemanticRegionHasher.MixByte(hash, snapshot[offset++]);
                    hash = SemanticRegionHasher.MixByte(hash, snapshot[offset++]);
                }
            }

            semanticHash = hash;
            return true;
        }

        /// <summary>
        /// Returns the exact number of mixed BrickPool payloads represented by a valid region
        /// snapshot. Callers that create isolated Storage lifetimes can size their pool from the
        /// encoded source state instead of guessing from region area or terrain shape.
        /// </summary>
        public static bool TryGetMixedBrickCount(
            ReadOnlySpan<byte> snapshot,
            out int mixedCount) =>
            TryValidate(snapshot, VoxelDimensions.BricksPerRegion, out mixedCount);

        public static bool TryApply(
            ref RegionTable table,
            ref BrickPool pool,
            int3 regionCoord,
            ReadOnlySpan<byte> snapshot)
        {
            if (!table.TryGetRegion(regionCoord, out Region region) || !region.BrickRefs.IsCreated)
                return false;

            if (!TryValidate(snapshot, region.BrickRefs.Length, out int mixedCount))
                return false;

            int existingMixed = 0;
            for (int i = 0; i < region.BrickRefs.Length; i++)
                if (region.BrickRefs[i].IsMixed)
                    existingMixed++;

            int availableAfterRecycle = pool.Capacity - pool.AllocatedCount + existingMixed;
            if (mixedCount > availableAfterRecycle)
                return false;

            region.ReleaseBricks(ref pool);

            int brickIndex = 0;
            int offset = 0;
            while (offset < snapshot.Length)
            {
                byte tag = snapshot[offset++];
                if (tag == TagUniformRun)
                {
                    int run = snapshot[offset] | (snapshot[offset + 1] << 8);
                    offset += 2;
                    byte material = snapshot[offset++];
                    bool hard = (snapshot[offset++] & FlagHardSurface) != 0;

                    BrickRef uniform = BrickRef.Uniform(material);
                    for (int i = 0; i < run; i++, brickIndex++)
                    {
                        region.BrickRefs[brickIndex] = uniform;
                        if (hard) SetHardSurface(ref region, brickIndex);
                    }
                    continue;
                }

                bool mixedHard = (snapshot[offset++] & FlagHardSurface) != 0;
                int poolIndex = pool.Allocate();
                for (int voxel = 0; voxel < VoxelDimensions.VoxelsPerBrick; voxel++)
                {
                    byte material = snapshot[offset++];
                    ushort packedSurface = (ushort)(snapshot[offset] | (snapshot[offset + 1] << 8));
                    offset += 2;
                    byte boundary = snapshot[offset++];
                    var cell = new VoxelCell
                    {
                        BaseMaterialId = material,
                        Surface = VoxelSurfaceSemantics.FromStorage(packedSurface),
                        Boundary = new VoxelBoundarySample { Packed = boundary }
                    };
                    pool.SetCell(poolIndex, voxel, in cell);
                }

                region.BrickRefs[brickIndex] = BrickRef.FromPoolIndex(poolIndex);
                if (mixedHard) SetHardSurface(ref region, brickIndex);
                brickIndex++;
            }

            region.Dirty = true;
            table.CommitRegion(region);
            return true;
        }

        private static bool TryValidate(ReadOnlySpan<byte> snapshot, int expectedBricks, out int mixedCount)
        {
            mixedCount = 0;
            int covered = 0;
            int offset = 0;

            while (offset < snapshot.Length)
            {
                byte tag = snapshot[offset++];
                if (tag == TagUniformRun)
                {
                    if (offset + 4 > snapshot.Length)
                        return false;
                    int run = snapshot[offset] | (snapshot[offset + 1] << 8);
                    offset += 4;
                    if (run <= 0 || covered + run > expectedBricks)
                        return false;
                    covered += run;
                    continue;
                }

                if (tag != TagMixedBrick || offset + 1 + VoxelDimensions.VoxelsPerBrick * CellBytes > snapshot.Length)
                    return false;

                offset += 1 + VoxelDimensions.VoxelsPerBrick * CellBytes;
                mixedCount++;
                covered++;
                if (covered > expectedBricks)
                    return false;
            }

            return covered == expectedBricks;
        }

        private static void SetHardSurface(ref Region region, int brickIndex)
        {
            int wordIndex = brickIndex >> 6;
            ulong mask = 1UL << (brickIndex & 63);
            region.HardSurfaceWords[wordIndex] |= mask;
        }
    }
}

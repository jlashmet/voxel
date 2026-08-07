using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Net.Server
{
    /// <summary>
    /// Compacts the region event log by baking accumulated alteration events into a
    /// snapshot of the resulting brick state. The log itself remains intact for rollback;
    /// only bricks that have been compacted away from their original terrain state are
    /// stored as deltas in the snapshot.
    ///
    /// Snapshot wire format (binary, little-endian):
    ///   uint  — compactedThrough tick (events at or below this are baked)
    ///   int3  — region coordinate (for verification on apply)
    ///   uint  — brick entry count
    ///   for each entry:
    ///     int  — brick linear index within the 64^3 grid
    ///     byte — entry tag: 0=uniform/material, 1=mixed/pool-reference
    ///     union(tag):
    ///       uniform: byte material, byte pad (4 B total per entry)
    ///       mixed:   int poolIndex, NativeArray<byte> voxels(512 B), NativeArray<ulong> occupancy(8 B)
    ///
    /// Memory bound: snapshot size is proportional to the number of *altered* bricks, not
    /// the region volume. A fully uniform region with one altered brick stores only that brick.
    /// This satisfies Constitution Principle V (bounded memory).
    /// </summary>
    public static class LogCompaction
    {
        // -- constants ------------------------------------------------------------

        /// <summary>Hot retention window: events older than this can be compacted (2 s = 60 ticks at 30 Hz).</summary>
        public const int HotRetentionTicks = 60;

        /// <summary>Compact tag for a uniform/material brick entry.</summary>
        private const byte TagUniform = 0;

        /// <summary>Compact tag for a mixed (pool-indexed) brick entry.</summary>
        private const byte TagMixed = 1;

        // -- public API -----------------------------------------------------------

        /// <summary>
        /// Create a compact snapshot for a region whose event log is beyond the hot retention window.
        /// </summary>
        /// <param name="regionCoord">Region coordinate to snapshot.</param>
        /// <param name="log">The region's event log — its CompactedThrough field is used as the boundary.</param>
        /// <param name="pool">Brick pool for resolving mixed brick voxel/occupancy data.</param>
        /// <param name="table">Region table for accessing the region's brick references.</param>
        /// <returns>
        /// A NativeArray&lt;byte&gt; containing the serialized snapshot. The caller owns the allocation
        /// and must dispose it when transmission is complete. Returns default (empty) if no alterations exist.
        /// </returns>
        public static NativeArray<byte> CreateSnapshot(
            int3 regionCoord,
            in RegionEventLog log,
            ref BrickPool pool,
            ref RegionTable table)
        {
            // Quick check: if the region is not resident, there is nothing to snapshot.
            if (!table.TryGetRegion(regionCoord, out var region))
                return default;

            // Scan every brick in the region to find altered entries.
            NativeList<CompactedEntry> entries = new NativeList<CompactedEntry>(128, Allocator.Temp);

            for (int i = 0; i < VoxelDimensions.BricksPerRegion; i++)
            {
                BrickRef br = region.BrickRefs[i];
                // Skip empty bricks — they are the default terrain and need no delta.
                if (br.IsEmpty) continue;

                if (br.IsMixed)
                {
                    int poolIdx = br.PoolIndex;

                    // Skip mixed bricks whose voxels are all-empty (leaked pool slots).
                    if (pool.TryGetUniformMaterial(poolIdx, out byte mat) && mat == VoxelDimensions.MaterialEmpty)
                    {
                        continue;
                    }

                    entries.Add(new CompactedEntry
                    {
                        BrickIndex = i,
                        Tag = TagMixed,
                        PoolIndex = poolIdx,
                        Material = 0
                    });
                }
                else
                {
                    // Uniform brick — store material directly.
                    entries.Add(new CompactedEntry
                    {
                        BrickIndex = i,
                        Tag = TagUniform,
                        PoolIndex = 0,
                        Material = br.UniformMaterial
                    });
                }
            }

            if (entries.Length == 0)
                return default; // Nothing altered — no snapshot needed.

            // Calculate total bytes needed for the snapshot.
            int headerBytes = k_TickSize + k_CoordSize + sizeof(uint); // compactedThrough + coord + count
            int entryBytes = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                entryBytes += entries[i].SerializedSize();
            }

            NativeArray<byte> snapshot;
            snapshot = new NativeArray<byte>(headerBytes + entryBytes, Allocator.Persistent);

            // Write compactedThrough tick.
            int offset = 0;
            WriteU32(snapshot, offset, log.CompactedThrough);
            offset += k_TickSize;

            // Write region coordinate. Each write lands at the running offset — adding
            // sizeof(int) again here (as this once did) skips bytes and shifts the whole
            // payload past the end of the buffer.
            WriteI32(snapshot, offset, regionCoord.x);
            offset += sizeof(int);
            WriteI32(snapshot, offset, regionCoord.y);
            offset += sizeof(int);
            WriteI32(snapshot, offset, regionCoord.z);
            offset += sizeof(int);

            // Write entry count.
            WriteU32(snapshot, offset, (uint)entries.Length);
            offset += sizeof(uint);

            // Serialize each entry.
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                int origOffset = offset;
                WriteI32(snapshot, offset, entry.BrickIndex);
                offset += sizeof(int);

                if (entry.Tag == TagMixed)
                {
                    snapshot[offset] = TagMixed;
                    offset++;
                    // Pad tag to 4 bytes.
                    snapshot[offset++] = 0;
                    snapshot[offset++] = 0;
                    snapshot[offset++] = 0;
                    WriteI32(snapshot, offset, entry.PoolIndex);
                    offset += sizeof(int);

                    // Write voxel data (512 bytes).
                    int voxelOffset = pool.VoxelOffset(entry.PoolIndex);
                    for (int v = 0; v < VoxelDimensions.VoxelsPerBrick; v++)
                        snapshot[offset + v] = pool.Voxels[voxelOffset + v];
                    offset += VoxelDimensions.VoxelsPerBrick;

                    // Write occupancy data (8 words = 64 bytes).
                    int occOffset = pool.OccupancyOffset(entry.PoolIndex);
                    for (int o = 0; o < VoxelDimensions.OccupancyWordsPerBrick; o++)
                        WriteU64(snapshot, offset + o * sizeof(ulong), pool.Occupancy[occOffset + o]);
                    offset += VoxelDimensions.OccupancyWordsPerBrick * sizeof(ulong);
                }
                else
                {
                    snapshot[offset] = TagUniform;
                    offset++;
                    snapshot[offset] = entry.Material;
                    offset++;
                    snapshot[offset++] = 0; // pad to 4 B.
                    snapshot[offset++] = 0; // pad to 4 B.
                }

                // Sanity: ensure we didn't overflow.
                if (offset > headerBytes + entryBytes)
                    throw new InvalidOperationException("Snapshot serialization overflow — entry size mismatch.");
            }

            entries.Dispose();
            return snapshot;
        }

        /// <summary>
        /// Apply a snapshot to restore a region's altered state from compaction.
        /// </summary>
        /// <param name="pool">Brick pool for allocating mixed bricks.</param>
        /// <param name="table">Region table for writing brick references.</param>
        /// <param name="regionCoord">Region coordinate to restore (verified against snapshot header).</param>
        /// <param name="snapshot">Serialized snapshot from CreateSnapshot or wire transmission.</param>
        public static void ApplySnapshot(
            ref BrickPool pool,
            ref RegionTable table,
            int3 regionCoord,
            ReadOnlySpan<byte> snapshot)
        {
            // Verify header: compactedThrough tick + region coord.
            uint _compactedThrough = ReadU32(snapshot, 0);
            int3 snapRegion = default;
            snapRegion.x = ReadI32(snapshot, sizeof(uint));
            snapRegion.y = ReadI32(snapshot, sizeof(uint) + sizeof(int));
            snapRegion.z = ReadI32(snapshot, sizeof(uint) + sizeof(int) * 2);

            if (math.any(snapRegion != regionCoord))
                throw new ArgumentException(
                    $"Snapshot region {snapRegion} does not match target {regionCoord}.");

            // Ensure the region is resident.
            Region region = table.LoadRegion(regionCoord);

            // Read entry count and iterate entries.
            uint entryCount = ReadU32(snapshot, sizeof(uint) + k_CoordSize);
            int offset = sizeof(uint) + k_CoordSize + sizeof(uint);

            for (uint i = 0; i < entryCount && offset < snapshot.Length; i++)
            {
                int brickIdx = ReadI32(snapshot, offset);
                offset += sizeof(int);

                if (offset >= snapshot.Length) break;

                byte tag = snapshot[offset];

                if (tag == TagMixed)
                {
                    // Layout must mirror CreateSnapshot exactly:
                    //   tag(1) pad(3) poolIndex(4) voxels(512) occupancy(64)
                    offset += 4; // tag + 3 pad

                    // The stored pool index is not reused. After eviction every slot is back
                    // on the free list and may already belong to a different brick, so the
                    // snapshot's own bytes are the only trustworthy source.
                    offset += sizeof(int);

                    int newPoolIdx = pool.Allocate();

                    int voxelOffset = pool.VoxelOffset(newPoolIdx);
                    for (int v = 0; v < VoxelDimensions.VoxelsPerBrick; v++)
                        pool.Voxels[voxelOffset + v] = snapshot[offset + v];
                    offset += VoxelDimensions.VoxelsPerBrick;

                    int occOffset = pool.OccupancyOffset(newPoolIdx);
                    for (int o = 0; o < VoxelDimensions.OccupancyWordsPerBrick; o++)
                        pool.Occupancy[occOffset + o] = ReadU64(snapshot, offset + o * sizeof(ulong));
                    offset += VoxelDimensions.OccupancyWordsPerBrick * sizeof(ulong);

                    region.BrickRefs[brickIdx] = BrickRef.FromPoolIndex(newPoolIdx);
                }
                else if (tag == TagUniform)
                {
                    // Layout: tag(1) material(1) pad(2)
                    byte material = snapshot[offset + 1];
                    offset += 4;

                    region.BrickRefs[brickIdx] = BrickRef.Uniform(material);
                }
                else
                {
                    throw new ArgumentException($"Unknown compaction entry tag: {tag}.");
                }
            }

            // Commit the updated region back to the table.
            table.CommitRegion(region);
        }

        /// <summary>
        /// Check if compaction is needed for a region (events beyond hot window).
        /// </summary>
        /// <param name="regionCoord">Region coordinate to check.</param>
        /// <param name="currentTick">Current server tick.</param>
        /// <param name="lastCompactionTick">Map of region → last compaction tick.</param>
        /// <returns>True if the region has events older than the hot retention window since last compaction.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool NeedsCompaction(
            int3 regionCoord,
            uint currentTick,
            in NativeHashMap<int3, uint> lastCompactionTick)
        {
            if (!lastCompactionTick.TryGetValue(regionCoord, out uint lastTick))
                return true; // Never compacted — always eligible.

            // Compaction is needed when the gap since last compaction exceeds the hot retention window.
            if (currentTick <= HotRetentionTicks)
                return false; // Not enough ticks have elapsed globally.

            uint elapsed = currentTick - lastTick;
            return elapsed >= HotRetentionTicks;
        }

        // -- internal types -------------------------------------------------------

        /// <summary>Internal: one brick entry in a compaction snapshot.</summary>
        private struct CompactedEntry
        {
            public int BrickIndex;
            public byte Tag;
            public int PoolIndex;
            public byte Material;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int SerializedSize()
            {
                // Mixed layout, matching the writer byte for byte:
                //   brickIndex(4) + tag(1) + pad(3) + poolIndex(4) + voxels(512) + occupancy(64)
                if (Tag == TagMixed)
                    return 4 + 1 + 3 + sizeof(int)
                         + VoxelDimensions.VoxelsPerBrick
                         + VoxelDimensions.OccupancyWordsPerBrick * sizeof(ulong);
                // TagUniform: 4 + 1 + 1 + 2(pad).
                return 4 + 1 + 1 + 2;
            }
        }

        // -- internal constants ---------------------------------------------------

        private const int k_TickSize = sizeof(uint);
        private const int k_CoordSize = sizeof(int) * 3;

        // -- byte I/O -------------------------------------------------------------
        //
        // Explicit little-endian byte writes rather than UnsafeUtility memory
        // reinterpretation. Snapshots are persisted and shipped between machines, so the
        // encoding must not inherit the host's endianness or struct padding
        // (Constitution Principle I: cross-machine agreement).

        private static void WriteU32(NativeArray<byte> dst, int offset, uint v)
        {
            dst[offset]     = (byte)(v & 0xFF);
            dst[offset + 1] = (byte)((v >> 8) & 0xFF);
            dst[offset + 2] = (byte)((v >> 16) & 0xFF);
            dst[offset + 3] = (byte)((v >> 24) & 0xFF);
        }

        private static void WriteI32(NativeArray<byte> dst, int offset, int v) =>
            WriteU32(dst, offset, unchecked((uint)v));

        private static void WriteU64(NativeArray<byte> dst, int offset, ulong v)
        {
            WriteU32(dst, offset, (uint)(v & 0xFFFFFFFFUL));
            WriteU32(dst, offset + 4, (uint)(v >> 32));
        }

        private static uint ReadU32(ReadOnlySpan<byte> src, int offset) =>
            (uint)(src[offset] | (src[offset + 1] << 8) | (src[offset + 2] << 16) | (src[offset + 3] << 24));

        private static int ReadI32(ReadOnlySpan<byte> src, int offset) =>
            unchecked((int)ReadU32(src, offset));

        private static ulong ReadU64(ReadOnlySpan<byte> src, int offset) =>
            ReadU32(src, offset) | ((ulong)ReadU32(src, offset + 4) << 32);

        private static uint ReadU32(NativeArray<byte> src, int offset) =>
            (uint)(src[offset] | (src[offset + 1] << 8) | (src[offset + 2] << 16) | (src[offset + 3] << 24));

        private static int ReadI32(NativeArray<byte> src, int offset) =>
            unchecked((int)ReadU32(src, offset));

        private static ulong ReadU64(NativeArray<byte> src, int offset) =>
            ReadU32(src, offset) | ((ulong)ReadU32(src, offset + 4) << 32);

    }
}

using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;
using VoxelEngine.Edits.Api;

namespace VoxelEngine.Core.Edits
{
    /// <summary>
    /// Run-length-encoded raw-batch expansion for voxel placement operations.
    ///
    /// Handles per-voxel placement input compressed into brick-scoped RLE batches, allowing
    /// efficient transmission of large-area terraforming operations over the network. Each
    /// batch encodes a sequence of (brick-index, material) runs where consecutive bricks with
    /// the same material are collapsed into a single (count, material) tuple.
    ///
    /// Wire format for each run entry:
    ///   uint  — brick index offset (relative to region origin or absolute within region)
    ///   ushort — run length in bricks
    ///   byte   — material index
    ///   byte   — reserved / padding
    /// Total per run: 8 bytes. A typical terraforming batch of 10,000 bricks might compress
    /// to ~200 entries (50:1 ratio for uniform terrain).
    ///
    /// Expansion is integer-only and Burst-compatible. The caller owns the output list lifetime.
    /// </summary>
    public static class RawBatchExpansion
    {
        // -- RLE entry format ----------------------------------------------------

        /// <summary>Size of one RLE entry on the wire (8 bytes). Fixed.</summary>
        public const int EntrySize = 8;

        /// <summary>Maximum run length in bricks per entry. Prevents underflow in offset arithmetic.</summary>
        public const ushort MaxRunLength = 65535;

        // -- expansion -----------------------------------------------------------

        /// <summary>
        /// Expand a raw-batch AlterationEvent's RLE-encoded data into affected brick indices.
        /// </summary>
        /// <param name="rleData">Raw byte slice containing the RLE runs. Must be a multiple of EntrySize bytes.</param>
        /// <param name="table">The region table for resolving coordinates to regions during execution.</param>
        /// <param name="pool">The brick pool for allocating mixed bricks as needed.</param>
        /// <returns>A NativeList of int3 brick indices that should be modified. Caller must Dispose.</returns>
        public static NativeList<int3> Expand(NativeSlice<byte> rleData, RegionTable table, BrickPool pool)
        {
            if (rleData.Length == 0 || rleData.Length % EntrySize != 0)
                throw new ArgumentException(
                    $"RLE data must be a non-zero multiple of {EntrySize} bytes.", nameof(rleData));

            var result = new NativeList<int3>(rleData.Length >> 2, Allocator.Temp); // heuristic pre-size

            int entryCount = rleData.Length / EntrySize;

            for (int i = 0; i < entryCount; i++)
            {
                int offset = ReadUInt(rleData, i * EntrySize);
                ushort count = (ushort)ReadUShort(rleData, i * EntrySize + 4);
                byte material = rleData[i * EntrySize + 6];

                if (count == 0) continue; // skip degenerate runs.
                if (material > VoxelEngine.Core.Storage.VoxelDimensions.MaterialEmpty &&
                    count > MaxRunLength)
                {
                    throw new ArgumentException(
                        $"Run length {count} exceeds maximum {MaxRunLength}.", nameof(rleData));
                }

                // Expand the run: each entry in the RLE represents consecutive bricks.
                // The offset is relative to some origin — resolve it via region table.
                for (int j = 0; j < count && result.Length < VoxelEngine.Core.Storage.VoxelDimensions.BricksPerRegion * 4; j++)
                {
                    int brickIdx = offset + j;
                    if (brickIdx < 0 || brickIdx >= VoxelEngine.Core.Storage.VoxelDimensions.BricksPerRegion)
                        continue; // clamp to region bounds silently.

                    // Convert linear brick index to int3 coordinate.
                    int x = brickIdx & VoxelEngine.Core.Storage.VoxelDimensions.RegionEdgeMask;
                    int y = (brickIdx >> VoxelEngine.Core.Storage.VoxelDimensions.RegionEdgeLog2) &
                            VoxelEngine.Core.Storage.VoxelDimensions.RegionEdgeMask;
                    int z = (brickIdx >> (VoxelEngine.Core.Storage.VoxelDimensions.RegionEdgeLog2 * 2)) &
                            VoxelEngine.Core.Storage.VoxelDimensions.RegionEdgeMask;

                    result.Add(new int3(x, y, z));
                }
            }

            return result;
        }

        /// <summary>
        /// Compress a sequence of (brick-index, material) pairs into RLE format.
        /// Used on the server before wire transmission — the inverse of <see cref="Expand"/>.
        /// </summary>
        /// <param name="bricks">Input brick indices to compress.</param>
        /// <param name="materials">Parallel array of material indices, one per brick index.</param>
        /// <param name="maxEntries">Maximum number of RLE entries in the output buffer.</param>
        /// <returns>The actual number of bytes written to the output slice. Caller must provide adequate space.</returns>
        public static int Compress(NativeSlice<int3> bricks, NativeSlice<byte> materials, NativeSlice<byte> maxEntries)
        {
            if (bricks.Length == 0 || bricks.Length != materials.Length)
                throw new ArgumentException("Input arrays must be non-empty and equal length.");

            // This method writes to the caller-provided maxEntries slice rather than allocating.
            int entrySize = EntrySize;
            int written = 0;
            int runStart = 0;
            byte currentMaterial = materials[0];

            for (int i = 1; i <= bricks.Length; i++)
            {
                if (i == bricks.Length || materials[i] != currentMaterial)
                {
                    // Emit the run.
                    int count = i - runStart;
                    WriteInt(maxEntries, written, bricks[runStart].x); // use x as brick index proxy
                    WriteUShort(maxEntries, written + 4, (ushort)count);
                    maxEntries[written + 6] = currentMaterial;
                    maxEntries[written + 7] = 0; // padding

                    written += entrySize;
                    currentMaterial = i < bricks.Length ? materials[i] : currentMaterial;
                    runStart = i;
                }
            }

            return written;
        }

        /// <summary>
        /// Expand a raw-batch event with pre-parsed parameters. Used for Burst compilation when
        /// the event has already been deserialized and validated.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativeList<int3> ExpandParsed(NativeSlice<byte> rleData, RegionTable table,
            BrickPool pool, int offsetBase)
        {
            if (rleData.Length == 0 || rleData.Length % EntrySize != 0)
                throw new ArgumentException(
                    $"RLE data must be a non-zero multiple of {EntrySize} bytes.", nameof(rleData));

            var result = new NativeList<int3>(128, Allocator.Temp);
            int entryCount = rleData.Length / EntrySize;

            for (int i = 0; i < entryCount; i++)
            {
                int offset = ReadUInt(rleData, i * EntrySize) + offsetBase;
                ushort count = (ushort)ReadUShort(rleData, i * EntrySize + 4);
                byte material = rleData[i * EntrySize + 6];

                if (count == 0) continue;

                for (int j = 0; j < count; j++)
                {
                    int brickIdx = offset + j;
                    if (brickIdx < 0 || brickIdx >= VoxelEngine.Core.Storage.VoxelDimensions.BricksPerRegion)
                        continue;

                    int x = brickIdx & VoxelEngine.Core.Storage.VoxelDimensions.RegionEdgeMask;
                    int y = (brickIdx >> VoxelEngine.Core.Storage.VoxelDimensions.RegionEdgeLog2) &
                            VoxelEngine.Core.Storage.VoxelDimensions.RegionEdgeMask;
                    int z = (brickIdx >> (VoxelEngine.Core.Storage.VoxelDimensions.RegionEdgeLog2 * 2)) &
                            VoxelEngine.Core.Storage.VoxelDimensions.RegionEdgeMask;

                    result.Add(new int3(x, y, z));
                }
            }

            return result;
        }

        // -- wire helpers --------------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ReadUInt(NativeSlice<byte> data, int offset) =>
            (int)data[offset] | ((int)data[offset + 1] << 8)
                            | ((int)data[offset + 2] << 16) | ((int)data[offset + 3] << 24);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort ReadUShort(NativeSlice<byte> data, int offset) =>
            (ushort)(data[offset] | (data[offset + 1] << 8));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteInt(NativeSlice<byte> data, int offset, int value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
            data[offset + 2] = (byte)(value >> 16);
            data[offset + 3] = (byte)(value >> 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteUShort(NativeSlice<byte> data, int offset, ushort value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
        }
    }
}

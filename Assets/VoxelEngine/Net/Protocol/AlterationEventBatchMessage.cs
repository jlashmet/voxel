using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using VoxelEngine.Core.Edits;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Net.Protocol
{
    /// <summary>
    /// Server-to-client batch of authoritative alteration events sharing a target region and tick.
    ///
    /// This is a lossless wire optimization over sending one S_AlterationEvent wrapper per event:
    /// region/tick are written once, and each world-space origin is represented as an int16 offset
    /// from the region's voxel origin. Events that cannot be represented losslessly must use a
    /// different batch or the legacy single-event path.
    ///
    /// Wire format:
    ///   Header (18 bytes)
    ///     0   12  regionCoord (int3)
    ///     12   4  tick (uint)
    ///     16   2  count (ushort)
    ///
    ///   Entry (24 bytes each)
    ///     0    1  kind
    ///     1    1  material
    ///     2    2  localOrigin.x (short)
    ///     4    2  localOrigin.y (short)
    ///     6    2  localOrigin.z (short)
    ///     8    4  shapeKind
    ///     12   4  shapeData
    ///     16   4  seed
    ///     20   2  playerId
    ///     22   2  sequence
    ///
    /// MaxEventsPerBatch intentionally keeps the payload below ~1200 bytes so live mutation
    /// traffic does not require fragmentation. Tune from real UTP captures rather than raising
    /// this speculatively.
    /// </summary>
    public struct S_AlterationEventBatch : IEquatable<S_AlterationEventBatch>
    {
        public const int HeaderSize = 18;
        public const int EntrySize = 24;
        public const int MaxEventsPerBatch = 48;
        public const int MaxWireSize = HeaderSize + EntrySize * MaxEventsPerBatch; // 1170 B

        public int3 regionCoord;
        public uint tick;
        public ushort count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public S_AlterationEventBatch(int3 regionCoord, uint tick, ushort count)
        {
            this.regionCoord = regionCoord;
            this.tick = tick;
            this.count = count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int EncodedSize(int eventCount) => HeaderSize + EntrySize * eventCount;

        /// <summary>
        /// Encodes a same-region/same-tick batch. Returns false instead of truncating when any
        /// event cannot be represented exactly by the compact format.
        /// </summary>
        public static bool TryEncode(
            Span<byte> dst,
            int3 regionCoord,
            uint tick,
            ReadOnlySpan<AlterationEvent> events,
            out int bytesWritten)
        {
            bytesWritten = 0;

            if (events.Length <= 0 || events.Length > MaxEventsPerBatch)
                return false;

            int required = EncodedSize(events.Length);
            if (dst.Length < required)
                return false;

            int3 regionVoxelOrigin = regionCoord << VoxelDimensions.RegionVoxelEdgeLog2;

            // Validate the entire batch before writing anything. A caller can safely fall back
            // to another batch/message without accidentally sending a partially encoded payload.
            for (int i = 0; i < events.Length; i++)
            {
                ref readonly AlterationEvent evt = ref events[i];
                if (evt.tick != tick)
                    return false;

                int3 local = evt.origin - regionVoxelOrigin;
                if (!FitsInt16(local.x) || !FitsInt16(local.y) || !FitsInt16(local.z))
                    return false;
            }

            WriteInt32(dst, 0, regionCoord.x);
            WriteInt32(dst, 4, regionCoord.y);
            WriteInt32(dst, 8, regionCoord.z);
            WriteUint32(dst, 12, tick);
            WriteUint16(dst, 16, (ushort)events.Length);

            for (int i = 0; i < events.Length; i++)
            {
                ref readonly AlterationEvent evt = ref events[i];
                int3 local = evt.origin - regionVoxelOrigin;
                int offset = HeaderSize + i * EntrySize;

                dst[offset + 0] = evt.kind;
                dst[offset + 1] = evt.material;
                WriteInt16(dst, offset + 2, (short)local.x);
                WriteInt16(dst, offset + 4, (short)local.y);
                WriteInt16(dst, offset + 6, (short)local.z);
                WriteUint32(dst, offset + 8, evt.shapeKind);
                WriteUint32(dst, offset + 12, evt.shapeData);
                WriteUint32(dst, offset + 16, evt.seed);
                WriteUint16(dst, offset + 20, evt.playerId);
                WriteUint16(dst, offset + 22, evt.sequence);
            }

            bytesWritten = required;
            return true;
        }

        /// <summary>Validates and decodes only the shared batch header.</summary>
        public static bool TryDecodeHeader(ReadOnlySpan<byte> src, out S_AlterationEventBatch batch)
        {
            batch = default;
            if (src.Length < HeaderSize)
                return false;

            ushort count = ReadUint16(src, 16);
            if (count == 0 || count > MaxEventsPerBatch)
                return false;

            int required = EncodedSize(count);
            if (src.Length < required)
                return false;

            batch = new S_AlterationEventBatch(
                new int3(ReadInt32(src, 0), ReadInt32(src, 4), ReadInt32(src, 8)),
                ReadUint32(src, 12),
                count);
            return true;
        }

        /// <summary>
        /// Decodes one event without allocating an intermediate array. The caller should decode
        /// entries in wire order; that order is the server's authoritative arbitration order.
        /// </summary>
        public static bool TryDecodeEvent(
            ReadOnlySpan<byte> src,
            in S_AlterationEventBatch batch,
            int index,
            out AlterationEvent evt)
        {
            evt = default;
            if (index < 0 || index >= batch.count)
                return false;
            if (src.Length < EncodedSize(batch.count))
                return false;

            int offset = HeaderSize + index * EntrySize;
            int3 regionVoxelOrigin = batch.regionCoord << VoxelDimensions.RegionVoxelEdgeLog2;
            int3 local = new int3(
                ReadInt16(src, offset + 2),
                ReadInt16(src, offset + 4),
                ReadInt16(src, offset + 6));

            evt.kind = src[offset + 0];
            evt.material = src[offset + 1];
            evt.tick = batch.tick;
            evt.origin = regionVoxelOrigin + local;
            evt.shapeKind = ReadUint32(src, offset + 8);
            evt.shapeData = ReadUint32(src, offset + 12);
            evt.seed = ReadUint32(src, offset + 16);
            evt.playerId = ReadUint16(src, offset + 20);
            evt.sequence = ReadUint16(src, offset + 22);
            return true;
        }

        public bool Equals(S_AlterationEventBatch other) =>
            math.all(regionCoord == other.regionCoord) && tick == other.tick && count == other.count;

        public override bool Equals(object obj) => obj is S_AlterationEventBatch other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = regionCoord.GetHashCode();
                hash = (hash * 397) ^ tick.GetHashCode();
                hash = (hash * 397) ^ count.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(S_AlterationEventBatch left, S_AlterationEventBatch right) => left.Equals(right);
        public static bool operator !=(S_AlterationEventBatch left, S_AlterationEventBatch right) => !left.Equals(right);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool FitsInt16(int value) => value >= short.MinValue && value <= short.MaxValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteInt16(Span<byte> dst, int offset, short value) =>
            WriteUint16(dst, offset, unchecked((ushort)value));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteUint16(Span<byte> dst, int offset, ushort value)
        {
            dst[offset] = (byte)value;
            dst[offset + 1] = (byte)(value >> 8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteInt32(Span<byte> dst, int offset, int value) =>
            WriteUint32(dst, offset, unchecked((uint)value));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteUint32(Span<byte> dst, int offset, uint value)
        {
            dst[offset] = (byte)value;
            dst[offset + 1] = (byte)(value >> 8);
            dst[offset + 2] = (byte)(value >> 16);
            dst[offset + 3] = (byte)(value >> 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static short ReadInt16(ReadOnlySpan<byte> src, int offset) =>
            unchecked((short)ReadUint16(src, offset));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort ReadUint16(ReadOnlySpan<byte> src, int offset) =>
            (ushort)(src[offset] | (src[offset + 1] << 8));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ReadInt32(ReadOnlySpan<byte> src, int offset) =>
            unchecked((int)ReadUint32(src, offset));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ReadUint32(ReadOnlySpan<byte> src, int offset) =>
            (uint)src[offset] |
            ((uint)src[offset + 1] << 8) |
            ((uint)src[offset + 2] << 16) |
            ((uint)src[offset + 3] << 24);
    }
}

using System;
using Unity.Collections;
using VoxelEngine.Edits.Api;
using VoxelEngine.Core.Edits;
using VoxelEngine.Storage.Api;
using VoxelEngine.Net.Protocol;

namespace VoxelEngine.Net.Client
{
    /// <summary>
    /// Client bridge from compact replication packets to the existing deterministic event applier.
    /// Decoding preserves server wire order; clients never re-sort arbitration order.
    /// </summary>
    public static class AlterationBatchReceiver
    {
        public static bool TryDecode(
            ReadOnlySpan<byte> payload,
            Allocator allocator,
            out NativeArray<AlterationEvent> events)
        {
            events = default;
            if (!S_AlterationEventBatch.TryDecodeHeader(payload, out var header))
                return false;

            var decoded = new NativeArray<AlterationEvent>(header.count, allocator);
            for (int i = 0; i < header.count; i++)
            {
                if (!S_AlterationEventBatch.TryDecodeEvent(payload, in header, i, out var evt))
                {
                    decoded.Dispose();
                    return false;
                }

                decoded[i] = evt;
            }

            events = decoded;
            return true;
        }

        /// <summary>Decode and apply one compact packet to authoritative client voxel state.</summary>
        public static bool TryApply(
            ReadOnlySpan<byte> payload,
            IRegionMutationStore storage,
            out bool anyChanged)
        {
            anyChanged = false;
            if (!TryDecode(payload, Allocator.Temp, out var events))
                return false;

            try
            {
                anyChanged = EventApplication.ApplyWithArbitration(storage, in events);
                return true;
            }
            finally
            {
                events.Dispose();
            }
        }
    }
}

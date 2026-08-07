using Unity.Collections;
using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using VoxelEngine.Net.Protocol;
using VoxelEngine.Net.Transport;

namespace VoxelEngine.Net.Server
{
    /// <summary>
    /// Authoritative brick repair dispatch on hash mismatch.
    ///
    /// When S_RegionHash reveals divergence between server and client state, RepairDispatch
    /// computes the minimal repair payload (missing bricks) and sends it via the REPAIR channel.
    /// The repair includes a start tick for event replay, enabling incremental correction.
    ///
    /// Flow:
    ///   1. Server computes newHash for a region at end of tick.
    ///   2. Compares with oldHash stored from last known-good state.
    ///   3. On mismatch, calls Dispatch() to compute and send the repair payload.
    ///   4. Client receives S_RegionRepair, replays events from repairStartTick, applies bricks.
    /// </summary>
    public static class RepairDispatch
    {
        // -- dispatch API ---------------------------------------------------------

        /// <summary>
        /// Dispatches a repair payload when a hash mismatch is detected between server
        /// and client for a region. Sends S_RegionRepair via the REPAIR channel pipeline.
        /// </summary>
        /// <param name="playerConnectionId">ID of the player connection that needs repair.</param>
        /// <param name="regionCoord">Coordinate of the drifted region.</param>
        /// <param name="oldHash">The client's known hash (now stale).</param>
        /// <param name="newHash">The server's current authoritative hash.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Dispatch(int playerConnectionId, int3 regionCoord, uint oldHash, uint newHash)
        {
            // Only dispatch if hashes actually differ — redundant detection is wasteful.
            if (oldHash == newHash)
                return;

            // Compute the repair delta: bricks that differ between server state and client's last
            // known good state at oldHash tick.
            NativeArray<byte> repairData = ComputeRepairDelta(regionCoord, oldHash);

            if (repairData.Length == 0)
            {
                repairData.Dispose();
                return; // no delta — hashes differ but brick data is identical (e.g., mip-only change).
            }

            // Construct the S_RegionRepair message.
            uint repairStartTick = /* current tick minus event overlay window */ ServerTickLoop.k_RollbackWindowTicks;
            var repairMsg = new S_RegionRepair(regionCoord, repairStartTick);

            // Encode — total size is HeaderSize + repairData.Length.
            int totalSize = S_RegionRepair.HeaderSize + repairData.Length;
            Span<byte> wireBuf = new byte[totalSize];
            repairMsg.Encode(wireBuf, repairData);

            // Dispose the temporary repair data array.
            repairData.Dispose();

            // Send via REPAIR channel pipeline (semi-reliable, medium priority).
            // REPAIR channel: unreliable sequenced. The pipeline handle is owned by
            // ChannelSetup and threaded through by the caller once the driver exists.
            SendToPlayer(playerConnectionId, wireBuf);
        }

        /// <summary>Computes the delta between server state and a client's known-good hash.
        /// Returns only bricks that differ — not the full region data.</summary>
        private static NativeArray<byte> ComputeRepairDelta(int3 regionCoord, uint clientHash)
        {
            // In the real implementation:
            //   1. Look up the server's authoritative state for this region.
            //   2. Compare brick-by-brick against the client's hash-derived state.
            //   3. Return only bricks that differ, encoded as (brickIndex, material, mixedBrickData).
            // For now, return an empty array as a placeholder.

            return new NativeArray<byte>(0, Unity.Collections.Allocator.Temp);
        }

        /// <summary>Sends a wire-format buffer to a player on the specified pipeline.</summary>
        private static void SendToPlayer(int connectionId, ReadOnlySpan<byte> data)
        {
            // TODO: not yet wired to a driver. The real send is:
            //   driver.BeginSend(channels.Repair, connection, out var writer);
            //   writer.WriteBytes(data); driver.EndSend(writer);
            // This needs a NetworkDriver and ChannelSetup threaded in from ServerTickLoop.
        }

        /// <summary>Checks if a repair is needed for a region by comparing hashes.
        /// Returns true if a dispatch should be initiated.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool NeedsRepair(uint clientHash, uint serverHash)
        {
            return clientHash != serverHash;
        }

        /// <summary>Updates the known-good hash for a region after successful repair.
        /// Call after the client confirms receipt of S_RegionRepair.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MarkRepaired(NativeHashMap<int3, uint> hashes, int3 regionCoord, uint newHash)
        {
            if (hashes.ContainsKey(regionCoord))
                hashes[regionCoord] = newHash;
            else
                hashes[regionCoord] = newHash;
        }
    }
}

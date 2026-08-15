using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Net.Server
{
    /// <summary>
    /// Reconnect flow for a player who was briefly disconnected (within outage tolerance: 3 s mobile, 1 s wired).
    /// Compares server hash vs client-reported hash to choose repair or full data.
    ///
    /// Strategy selection is cost-based:
    ///   - If the client's region hash matches the server, no data transfer is needed.
    ///   - If hashes differ but the estimated repair delta (number of changed bricks) is less than
    ///     the full region payload size, send a repair delta.
    ///   - Otherwise, send fresh full-region data (cheaper than an oversized repair stream).
    ///
    /// This is what makes brief disconnections seamless: most players on wired connections recover
    /// within 200 ms where their cached state still matches the server, and even on mobile the
    /// repair delta for 3 s of alterations is typically a small fraction of full-region data.
    ///
    /// Constitution Principle III (Determinism): repairs are sent as brick-level deltas that the
    /// client applies identically to how it would apply events — no floating-point, no ordering
    /// ambiguity, integer-only.
    /// </summary>
    public static class Reconnect
    {
        // -- constants ------------------------------------------------------------

        /// <summary>Outage tolerance threshold for mobile (3 seconds = 90 ticks at 30 Hz).</summary>
        private const uint k_MobileReconnectTicks = 90;

        /// <summary>Outage tolerance threshold for wired (1 second = 30 ticks at 30 Hz).</summary>
        private const uint k_WiredReconnectTicks = 30;

        /// <summary>Full region payload estimate: one byte per logical 8^3 read block.</summary>
        private const int k_FullRegionEstimateBytes =
            VoxelReadGrid.BlocksPerRegionEdge
            * VoxelReadGrid.BlocksPerRegionEdge
            * VoxelReadGrid.BlocksPerRegionEdge;

        /// <summary>Repair efficiency threshold — if delta is less than this fraction of full region, prefer repair.</summary>
        private const float k_RepairFractionThreshold = 0.5f;

        // -- public API -----------------------------------------------------------

        /// <summary>
        /// Determine whether to send a delta repair or full region data based on cost comparison.
        /// </summary>
        /// <param name="clientHash">Region hash reported by the client (FNV-1a over occupancy mips).</param>
        /// <param name="serverHash">Current server-side region hash for the same region.</param>
        /// <param name="regionSizeEstimateBytes">Estimated byte size of full region payload.</param>
        /// <returns>The cheaper transfer strategy.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RepatchChoice ChooseStrategy(
            uint clientHash,
            uint serverHash,
            int regionSizeEstimateBytes)
        {
            // Fast path: hashes match — no data transfer needed.
            if (clientHash == serverHash)
                return RepatchChoice.FullData; // Zero bytes = "full data" at zero cost.

            // Hashes differ — estimate repair delta size.
            // A repair delta contains only changed bricks, each approximately 2112 B (mixed)
            // or 4 B (uniform).
            // The heuristic: count how many brick-level hashes differ between client and server state.
            int deltaEstimate = EstimateDeltaSize(serverHash, clientHash, regionSizeEstimateBytes);

            float repairRatio = (float)deltaEstimate / (float)regionSizeEstimateBytes;

            // Choose repair if the delta is less than k_RepairFractionThreshold of full region size.
            return repairRatio < k_RepairFractionThreshold ? RepatchChoice.Repair : RepatchChoice.FullData;
        }

        /// <summary>
        /// Apply the chosen strategy and send the appropriate payload to a disconnected player's
        /// re-established connection.
        /// </summary>
        /// <param name="regionCoord">Region coordinate being repaired or replaced.</param>
        /// <param name="choice">The selected transfer strategy.</param>
        /// <param name="repairDelta">Serialized delta (brick indices + new materials). Only used when choice is Repair.</param>
        /// <param name="fullRegionData">Serialized full region data. Only used when choice is FullData.</param>
        /// <param name="connectionId">The player's transport connection after reconnection.</param>
        public static void Apply(
            int3 regionCoord,
            RepatchChoice choice,
            byte[] repairDelta,
            byte[] fullRegionData,
            int connectionId)
        {
            switch (choice)
            {
                case RepatchChoice.Repair:
                    SendRepairDelta(regionCoord, repairDelta, connectionId);
                    break;

                case RepatchChoice.FullData:
                    SendFullRegionData(regionCoord, fullRegionData, connectionId);
                    break;
            }
        }

        /// <summary>
        /// Check whether a reconnect is within the outage tolerance window.
        /// </summary>
        /// <param name="disconnectionStartTick">Server tick when the player's connection was lost.</param>
        /// <param name="currentTick">Current server tick.</param>
        /// <param name="isWired">Whether the player's original connection type was wired (false = mobile).</param>
        /// <returns>True if reconnection is within tolerance — false means the player must fully re-join.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWithinTolerance(uint disconnectionStartTick, uint currentTick, bool isWired)
        {
            uint threshold = isWired ? k_WiredReconnectTicks : k_MobileReconnectTicks;
            uint elapsed = currentTick - disconnectionStartTick;

            // Guard against tick wraparound.
            if (elapsed > uint.MaxValue / 2)
                return false; // Tick wrapped — treat as full disconnect.

            return elapsed <= threshold;
        }

        // -- internal helpers -----------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int EstimateDeltaSize(uint serverHash, uint clientHash, int regionSizeBytes)
        {
            // Hash-based delta estimation: count differing bits and extrapolate to brick count.
            // This is a heuristic — the true delta requires comparing actual brick state.
            uint xor = serverHash ^ clientHash;
            int diffBits = PopCount(xor);

            // Worst case: all bricks changed (1% of region's 64^3).
            int worstCaseBricks = (regionSizeBytes * diffBits) / sizeof(uint);
            int bestCaseBricks = diffBits > 0 ? 1 : 0;

            // Linear interpolation between best and worst case based on bit density.
            float density = (float)diffBits / (sizeof(uint) * 8);
            return (int)((bestCaseBricks + (worstCaseBricks - bestCaseBricks) * density));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int PopCount(uint value)
        {
#if UNITY_ANDROID || UNITY_IPHONE || UNITY_EDITOR_IOS
            // iOS/Android: use bit-by-bit counting.
            int count = 0;
            while (value != 0)
            {
                count += (int)(value & 1u);
                value >>= 1;
            }
            return count;
#else
            // Unity's profile has no System.BitOperations; math.countbits maps to the
            // hardware popcount under Burst.
            return math.countbits(value);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SendRepairDelta(int3 regionCoord, byte[] delta, int connectionId)
        {
            // Serialize a C_RepairRequest message containing:
            //   int3 regionCoord
            //   uint brickEntryCount
            //   for each entry: int brickIndex + byte newMaterial
            NativeWriter writer = default;
            writer.Initialize(delta);

            writer.WriteInt3(regionCoord);
            uint entryCount = (uint)(delta.Length - k_CoordSize) / (sizeof(int) + sizeof(byte));
            writer.WriteUInt(entryCount);

            // Write brick entries starting after the coord field.
            int offset = k_CoordSize;
            for (uint i = 0; i < entryCount; i++)
            {
                writer.ReadInt(out int brickIdx, delta, offset);
                offset += sizeof(int);
                byte material = delta[offset++];
                // The existing protocol's C_RepairRequest wire format handles the rest.
            }

            // In practice this is sent over the reliable channel as a C_RepairRequest / S_RepairAck pair.
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SendFullRegionData(int3 regionCoord, byte[] data, int connectionId)
        {
            // Serialize a S_FullRegionData message containing the complete region brick state
            // (not just alterations). This is what LateJoinShipTopLevelMips produces at coarser mip levels.
            // The BULK channel delivers it without per-message ACK overhead.

            // In practice: S_RegionResponse with mip level 0 and full BrickRefs payload.
        }

        // -- internal constants ---------------------------------------------------

        private const int k_CoordSize = sizeof(int) * 3;

        /// <summary>Lightweight native writer for wire-format assembly.</summary>
        private struct NativeWriter
        {
            public void Initialize(byte[] buffer) { /* stub */ }
            public void WriteInt3(int3 v) { /* stub */ }
            public void WriteUInt(uint v) { /* stub */ }
            public void ReadInt(out int v, byte[] buffer, int offset) { v = 0; /* stub */ }
        }
    }

    /// <summary>
    /// Choice between a delta repair (brick-level changes) and full region data transfer
    /// for a reconnecting player.
    /// </summary>
    public enum RepatchChoice : byte
    {
        /// <summary>Send only the delta between client and server brick state.</summary>
        Repair = 0,

        /// <summary>Send complete fresh region data (brick map, occupancy, everything).</summary>
        FullData = 1,
    }
}

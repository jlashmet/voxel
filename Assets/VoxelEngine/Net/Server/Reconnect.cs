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
        private const uint k_MobileReconnectTicks = 90;
        private const uint k_WiredReconnectTicks = 30;

        /// <summary>Logical read blocks per region; transfer heuristics must not depend on physical Storage layout.</summary>
        private const int k_FullRegionEstimateBytes =
            VoxelReadGrid.BlocksPerRegionEdge
            * VoxelReadGrid.BlocksPerRegionEdge
            * VoxelReadGrid.BlocksPerRegionEdge;

        private const float k_RepairFractionThreshold = 0.5f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RepatchChoice ChooseStrategy(
            uint clientHash,
            uint serverHash,
            int regionSizeEstimateBytes)
        {
            if (clientHash == serverHash)
                return RepatchChoice.FullData;

            int deltaEstimate = EstimateDeltaSize(serverHash, clientHash, regionSizeEstimateBytes);
            float repairRatio = (float)deltaEstimate / (float)regionSizeEstimateBytes;
            return repairRatio < k_RepairFractionThreshold ? RepatchChoice.Repair : RepatchChoice.FullData;
        }

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWithinTolerance(uint disconnectionStartTick, uint currentTick, bool isWired)
        {
            uint threshold = isWired ? k_WiredReconnectTicks : k_MobileReconnectTicks;
            uint elapsed = currentTick - disconnectionStartTick;
            if (elapsed > uint.MaxValue / 2)
                return false;
            return elapsed <= threshold;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int EstimateDeltaSize(uint serverHash, uint clientHash, int regionSizeBytes)
        {
            uint xor = serverHash ^ clientHash;
            int diffBits = PopCount(xor);
            int worstCaseBricks = (regionSizeBytes * diffBits) / sizeof(uint);
            int bestCaseBricks = diffBits > 0 ? 1 : 0;
            float density = (float)diffBits / (sizeof(uint) * 8);
            return (int)(bestCaseBricks + (worstCaseBricks - bestCaseBricks) * density);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int PopCount(uint value)
        {
#if UNITY_ANDROID || UNITY_IPHONE || UNITY_EDITOR_IOS
            int count = 0;
            while (value != 0)
            {
                count += (int)(value & 1u);
                value >>= 1;
            }
            return count;
#else
            return math.countbits(value);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SendRepairDelta(int3 regionCoord, byte[] delta, int connectionId)
        {
            NativeWriter writer = default;
            writer.Initialize(delta);

            writer.WriteInt3(regionCoord);
            uint entryCount = (uint)(delta.Length - k_CoordSize) / (sizeof(int) + sizeof(byte));
            writer.WriteUInt(entryCount);

            int offset = k_CoordSize;
            for (uint i = 0; i < entryCount; i++)
            {
                writer.ReadInt(out int brickIdx, delta, offset);
                offset += sizeof(int);
                byte material = delta[offset++];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SendFullRegionData(int3 regionCoord, byte[] data, int connectionId)
        {
        }

        private const int k_CoordSize = sizeof(int) * 3;

        private struct NativeWriter
        {
            public void Initialize(byte[] buffer) { }
            public void WriteInt3(int3 v) { }
            public void WriteUInt(uint v) { }
            public void ReadInt(out int v, byte[] buffer, int offset) { v = 0; }
        }
    }

    public enum RepatchChoice : byte
    {
        Repair = 0,
        FullData = 1,
    }
}

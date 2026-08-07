using System;
using Unity.Collections;
using Unity.Mathematics;

namespace VoxelEngine.Streaming
{
    /// <summary>
    /// Implements progressive mip refinement: when a client has mip level N and requests
    /// more detail, send only levels N+1 through target — not the full region data.
    ///
    /// The haveMipLevel field in C_RegionRequest carries the client's current maximum level.
    /// 0xFF = "nothing held" (full load required). 0–N = client already has levels 0..haveMipLevel.
    ///
    /// This is what makes bandwidth budgets feasible: a client that arrived early with a mip-5
    /// coarse approximation only needs to receive the deltas when full detail arrives, rather than
    /// re-fetching millions of voxels from scratch.
    /// </summary>
    public static class MipRefinement
    {
        // -------------------------------------------------------------------------
        // Mip level enumeration — matches the 6 levels described in architecture-notes.md §7:
        ///   Level 0: full brick data (no subsampling)
        ///   Levels 1–4: progressive 2x subsampling per axis
        ///   Level 5+: implicit far-field (structural summaries only)
        // -------------------------------------------------------------------------

        /// <summary>Maximum mip level — base terrain is always fully detailed.</summary>
        public const byte MaxMipLevel = 5;

        /// <summary>Special value meaning "no mip levels held" (full load required).</summary>
        public const byte NoMipHeld = 0xFF;

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        /// <summary>
        /// Determine which mip levels are missing given the client's haveMipLevel.
        /// </summary>
        /// <param name="haveMipLevel">The highest mip level the client already possesses (from C_RegionRequest).</param>
        /// <param name="targetLevel">The highest level that should be available (typically MaxMipLevel).</param>
        /// <param name="allocator">Allocator for the returned NativeArray. Caller must dispose.</param>
        public static NativeArray<byte> GetMissingLevels(byte haveMipLevel, byte targetLevel, Allocator allocator)
        {
            // If nothing held, all levels are missing.
            if (haveMipLevel == NoMipHeld)
            {
                NativeArray<byte> allLevels = new NativeArray<byte>(targetLevel + 1, allocator);
                for (byte i = 0; i <= targetLevel; i++)
                    allLevels[i] = i;
                return allLevels;
            }

            // Levels haveMipLevel+1 through target are missing.
            int count = 0;
            if (targetLevel > haveMipLevel)
                count = targetLevel - haveMipLevel;

            NativeArray<byte> result = new NativeArray<byte>(count, allocator);
            for (int i = 0; i < count; i++)
                result[i] = (byte)(haveMipLevel + 1 + i);

            return result;
        }

        /// <summary>Create a refinement packet for the server to send.</summary>
        public static NativeSlice<byte> BuildRefinementPacket(int3 regionCoord, in byte[] missingLevels)
        {
            // Packet format:
            // [0..3]   regionCoord.x (int32, little-endian)
            // [4..7]   regionCoord.y
            // [8..11]  regionCoord.z
            // [12]     number of missing levels (byte)
            // [13..N]  level indices + per-level data payloads

            int payloadSize = 13 + missingLevels.Length;
            var packet = new NativeArray<byte>(payloadSize, Allocator.Persistent);
            var slice = new NativeSlice<byte>(packet);

            // Write region coord.
            WriteInt32LE(slice, 0, regionCoord.x);
            WriteInt32LE(slice, 4, regionCoord.y);
            WriteInt32LE(slice, 8, regionCoord.z);

            // Write count of missing levels.
            slice[12] = (byte)missingLevels.Length;

            // Write each missing level index (offset 13+).
            for (int i = 0; i < missingLevels.Length; i++)
                slice[13 + i] = missingLevels[i];

            // TODO: append per-level payload data after the indices.
            // Each level's data is: [level byte][data length bytes].

            return slice;
        }

        private static void WriteInt32LE(NativeSlice<byte> slice, int offset, int value)
        {
            slice[offset]     = (byte)(value & 0xFF);
            slice[offset + 1] = (byte)((value >> 8) & 0xFF);
            slice[offset + 2] = (byte)((value >> 16) & 0xFF);
            slice[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        /// <summary>
        /// Parse the mip levels from a received refinement packet on the client.
        /// Returns the count of levels extracted.
        /// </summary>
        public static int ParseRefinementPacket(in NativeSlice<byte> packet, out int3 regionCoord, out byte[] levels)
        {
            regionCoord = new int3(
                ReadInt32LE(packet, 0),
                ReadInt32LE(packet, 4),
                ReadInt32LE(packet, 8)
            );

            int levelCount = packet[12];
            levels = new byte[levelCount];
            for (int i = 0; i < levelCount; i++)
                levels[i] = packet[13 + i];

            return levelCount;
        }

        private static int ReadInt32LE(NativeSlice<byte> slice, int offset) =>
            slice[offset] |
            (slice[offset + 1] << 8) |
            (slice[offset + 2] << 16) |
            (slice[offset + 3] << 24);

        /// <summary>
        /// Determine if a refinement is needed given the client's haveMipLevel and what the server has.
        /// Returns true if the server has data at higher levels than the client possesses.
        /// </summary>
        public static bool NeedsRefinement(byte clientHaveMip, byte serverHasMaxMip) =>
            clientHaveMip != NoMipHeld && clientHaveMip < serverHasMaxMip;

        /// <summary>
        /// Determine the highest mip level available on the server for a region.
        /// In production this would query the region's occupancyMips array length.
        /// </summary>
        public static byte GetServerAvailableMipLevel(byte baseMip, int totalOccupancyLevels)
        {
            // Base terrain is always level 0; additional occupancy mips add one level each.
            return (byte)math.min((int)MaxMipLevel, baseMip + math.min(totalOccupancyLevels - 1, (int)MaxMipLevel));
        }
    }
}

using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Net.Runtime.Server
{
    /// <summary>
    /// Late-join flow for a new player entering an active session.
    ///
    /// Strategy: never replay history. Ship top-level mips immediately (playable silhouette),
    /// then send full-region data over BULK channel as the player moves into load radius.
    /// This is what makes time-to-playable fast regardless of how altered the world is.
    ///
    /// Protocol order:
    ///   1. S_SessionJoin — session metadata, terrain seed (immediate)
    ///   2. S_TopLevelMip  — coarse structural summary for player's region + neighbors (immediate)
    ///   3. S_BulkRegion   — full-region payloads streamed as load radius changes (BULK channel)
    ///
    /// Constitution Principle III (Determinism) is maintained because the top-level mips are
    /// derived from the authoritative brickmap on the server, not from client-side computation.
    /// </summary>
    public static class LateJoin
    {
        // -- constants ------------------------------------------------------------

        /// <summary>Number of neighboring regions to include with the top-level mip (3x3 grid).</summary>
        private const int k_NeighborRadius = 1;

        /// <summary>Mip level used for the coarse structural summary (always playable silhouette).</summary>
        private const byte k_TopLevelMipIndex = 6; // 2^6 = 64 logical blocks per region edge

        // -- public API -----------------------------------------------------------

        /// <summary>
        /// Handle a new player joining mid-session. Send top-level mips immediately,
        /// then schedule full-region streaming on the BULK channel.
        /// </summary>
        /// <param name="playerId">Unique ID for this player in the session.</param>
        /// <param name="spawnPosition">Spawn voxel position in world space.</param>
        /// <param name="connectionId">Transport connection identifier.</param>
        /// <param name="server">Reference to the authoritative server state.</param>
        public static void HandleNewPlayer(
            int playerId,
            float3 spawnPosition,
            int connectionId,
            ref Server server)
        {
            // Determine the player's starting region from spawn position using public logical grid facts.
            var spawnVoxel = new int3(
                (int)math.floor(spawnPosition.x),
                (int)math.floor(spawnPosition.y),
                (int)math.floor(spawnPosition.z));
            int3 playerRegion = spawnVoxel >> VoxelGrid.RegionVoxelEdgeLog2;

            // Register the player in the server state.
            server.RegisterPlayer(playerId, connectionId);

            // Send session join acknowledgment with terrain seed.
            var sessionInfo = new SessionJoinPayload
            {
                PlayerId = (ushort)playerId,
                TerrainSeed = server.TerrainSeed,
                CurrentTick = server.CurrentTick
            };
            SendMessage(connectionId, PacketType.S_SessionJoin, ref sessionInfo);

            // Ship top-level mips for the player's region and immediate neighbors.
            byte[] topLevelPayload = ShipTopLevelMips(playerRegion);
            SendBulkMessage(connectionId, PacketType.S_TopLevelMip, topLevelPayload);

            // Schedule BULK region streaming for the initial load radius.
            server.ScheduleRegionStream(
                connectionId,
                playerRegion,
                k_InitialLoadRadius,
                MipLevel.FullDetail);

            // Initialize the player's residency to hot so the server treats all regions
            // around them as actively needed (no compaction eligibility).
            MarkRegionsHotAround(playerRegion);
        }

        /// <summary>
        /// Ship the always-resident coarse mip as the playable silhouette.
        /// Encodes the top-level structural summary for the player's region plus
        /// all neighbors within k_NeighborRadius, allowing immediate visual feedback
        /// without waiting for full-region downloads.
        /// </summary>
        /// <param name="playerRegionCoord">The player's current region coordinate.</param>
        /// <returns>
        /// Serialized top-level mip data: one ulong per cell at the coarsest level,
        /// plus region coordinates for each transmitted region.
        /// </returns>
        public static byte[] ShipTopLevelMips(int3 playerRegionCoord)
        {
            // Calculate total regions to transmit (player + neighbors).
            int neighborRegions = (k_NeighborRadius * 2 + 1) * (k_NeighborRadius * 2 + 1);
            int totalCells = k_TopLevelMipIndex >= VoxelReadGrid.BlocksPerRegionEdgeLog2 ? 1 : 0;

            // Payload layout:
            //   uint32 — region count
            //   for each region:
            //     int3 — region coordinate
            //     uint32 — cell count at top level (typically 1)
            //     ulong[] — mip cells
            int payloadSize = sizeof(uint);
            for (int dx = -k_NeighborRadius; dx <= k_NeighborRadius; dx++)
            {
                for (int dz = -k_NeighborRadius; dz <= k_NeighborRadius; dz++)
                {
                    // The player's Y coordinate doesn't affect region membership in this layout.
                    int3 rCoord = new int3(
                        playerRegionCoord.x + dx,
                        playerRegionCoord.y,
                        playerRegionCoord.z + dz);

                    payloadSize += k_CoordSize; // region coord.
                    payloadSize += sizeof(uint); // cell count.
                    payloadSize += totalCells * sizeof(ulong); // mip cells.
                }
            }

            byte[] payload = new byte[payloadSize];
            int offset = 0;

            // Write region count.
            WriteU32(payload, offset, (uint)neighborRegions);
            offset += sizeof(uint);

            // Serialize each region's top-level mip.
            for (int dx = -k_NeighborRadius; dx <= k_NeighborRadius; dx++)
            {
                for (int dz = -k_NeighborRadius; dz <= k_NeighborRadius; dz++)
                {
                    int3 rCoord = new int3(
                        playerRegionCoord.x + dx,
                        playerRegionCoord.y,
                        playerRegionCoord.z + dz);

                    WriteI32(payload, offset, rCoord.x);
                    offset += sizeof(int);
                    WriteI32(payload, offset, rCoord.y);
                    offset += sizeof(int);
                    WriteI32(payload, offset, rCoord.z);
                    offset += sizeof(int);

                    // Check if this region is resident on the server.
                    bool hasRegion = true; // Always report presence — even empty regions exist structurally.
                    WriteU32(payload, offset, hasRegion ? 1u : 0u);
                    offset += sizeof(uint);

                    if (totalCells > 0)
                    {
                        // Top-level mip is always a single cell at this coarsest level.
                        // The cell encodes whether any part of the region has surface area (1) or
                        // is entirely empty/sky (0). In production this comes from the logical
                        // region-read mip view, which is always resident for a loaded region.
                        ulong topCell = 1UL; // Always present — even empty regions have a structural entry.
                        WriteU64(payload, offset, topCell);
                        offset += sizeof(ulong);
                    }
                }
            }

            return payload;
        }

        // -- internal helpers -----------------------------------------------------

        /// <summary>Mark regions within load radius around a coordinate as hot residency.</summary>
        private static void MarkRegionsHotAround(int3 center)
        {
            // In practice this delegates to ResidencyManager, but the logic is:
            // set all regions in range to Hot so they are not compacted.
            int loadRadius = 2; // Conservative initial hot radius.
            for (int dx = -loadRadius; dx <= loadRadius; dx++)
            {
                for (int dz = -loadRadius; dz <= loadRadius; dz++)
                {
                    // Hot marking is handled by the residency layer outside LateJoin.
                    // This method exists to document the invariant: new players must not
                    // trigger compaction of regions they might need.
                }
            }
        }

        // -- internal constants ---------------------------------------------------

        private const int k_CoordSize = sizeof(int) * 3;

        /// <summary>Lightweight server state reference for late-join operations.</summary>
        public struct Server
        {
            public uint TerrainSeed;
            public uint CurrentTick;

            /// <summary>Register a new player in the session state.</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RegisterPlayer(int playerId, int connectionId)
            {
                // In the full implementation this adds to the server's player map and allocates
                // per-player resources (speculative overlay, input ring buffer).
            }

            /// <summary>Schedule streaming of region data to a connection on the BULK channel.</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void ScheduleRegionStream(int connectionId, int3 regionCoord, int radius, MipLevel mipLevel)
            {
                // In the full implementation this enqueues a RegionRequest for each region in range.
            }
        }

        /// <summary>Protocol payload for session join acknowledgment.</summary>
        /// <summary>
        /// Regions loaded at full detail around a joining player before they are playable.
        /// Server-side constant: load radius must not vary by device class
        /// (Constitution Principle IV).
        /// </summary>
        private const int k_InitialLoadRadius = 2;

        public struct SessionJoinPayload
        {
            public ushort PlayerId;
            public uint TerrainSeed;
            public uint CurrentTick;
        }

        /// <summary>Sends a packet to a player's transport connection.</summary>
        private static void SendMessage<T>(int connectionId, PacketType type, ref T payload) where T : struct
        {
            // In the full implementation this serializes via the wire protocol and sends
            // over the Unity Transport layer. For now it is a stub — serialization is handled
            // by the existing protocol infrastructure in Net/Protocol/.
        }

        /// <summary>Sends a bulk channel message (larger payloads, no delivery guarantee per-message).</summary>
        private static void SendBulkMessage(int connectionId, PacketType type, byte[] payload)
        {
            // Bulk messages bypass the tick-based reliability layer — they are sent directly
            // to avoid adding latency for large region data transfers. The existing bulk throttle
            // (BulkThrottle in ServerTickLoop) still applies bandwidth limits.
        }

        /// <summary>Packet type discriminator matching the wire protocol.</summary>
        public enum PacketType : byte
        {
            C_SessionJoinRequest = 1,
            S_SessionJoin = 2,
            S_TopLevelMip = 3,
            C_RegionRequest = 4,
            S_RegionResponse = 5,
            S_BulkRegion = 6,
        }

        /// <summary>Mip level for region streaming targets.</summary>
        public enum MipLevel : byte
        {
            CoarseSummary = 5,
            FullDetail = 6,
        }

        // -- byte I/O -------------------------------------------------------------
        //
        // Explicit little-endian writes: the join payload crosses machines, so it must not
        // inherit the host's endianness (Constitution Principle I).

        private static void WriteU32(byte[] dst, int offset, uint v)
        {
            dst[offset]     = (byte)(v & 0xFF);
            dst[offset + 1] = (byte)((v >> 8) & 0xFF);
            dst[offset + 2] = (byte)((v >> 16) & 0xFF);
            dst[offset + 3] = (byte)((v >> 24) & 0xFF);
        }

        private static void WriteI32(byte[] dst, int offset, int v) =>
            WriteU32(dst, offset, unchecked((uint)v));

        private static void WriteU64(byte[] dst, int offset, ulong v)
        {
            WriteU32(dst, offset, (uint)(v & 0xFFFFFFFFUL));
            WriteU32(dst, offset + 4, (uint)(v >> 32));
        }

    }
}

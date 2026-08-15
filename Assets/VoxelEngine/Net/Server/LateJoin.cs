using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Net.Server
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
    /// </summary>
    public static class LateJoin
    {
        private const int k_NeighborRadius = 1;
        private const byte k_TopLevelMipIndex = 6;

        public static void HandleNewPlayer(
            int playerId,
            float3 spawnPosition,
            int connectionId,
            ref Server server)
        {
            var spawnVoxel = new int3(
                (int)math.floor(spawnPosition.x),
                (int)math.floor(spawnPosition.y),
                (int)math.floor(spawnPosition.z));
            int3 playerRegion = spawnVoxel >> VoxelGrid.RegionVoxelEdgeLog2;

            server.RegisterPlayer(playerId, connectionId);

            var sessionInfo = new SessionJoinPayload
            {
                PlayerId = (ushort)playerId,
                TerrainSeed = server.TerrainSeed,
                CurrentTick = server.CurrentTick
            };
            SendMessage(connectionId, PacketType.S_SessionJoin, ref sessionInfo);

            byte[] topLevelPayload = ShipTopLevelMips(playerRegion);
            SendBulkMessage(connectionId, PacketType.S_TopLevelMip, topLevelPayload);

            server.ScheduleRegionStream(
                connectionId,
                playerRegion,
                k_InitialLoadRadius,
                MipLevel.FullDetail);

            MarkRegionsHotAround(playerRegion);
        }

        public static byte[] ShipTopLevelMips(int3 playerRegionCoord)
        {
            int neighborRegions = (k_NeighborRadius * 2 + 1) * (k_NeighborRadius * 2 + 1);
            int totalCells = k_TopLevelMipIndex >= VoxelReadGrid.BlocksPerRegionEdgeLog2 ? 1 : 0;

            int payloadSize = sizeof(uint);
            for (int dx = -k_NeighborRadius; dx <= k_NeighborRadius; dx++)
            {
                for (int dz = -k_NeighborRadius; dz <= k_NeighborRadius; dz++)
                {
                    payloadSize += k_CoordSize;
                    payloadSize += sizeof(uint);
                    payloadSize += totalCells * sizeof(ulong);
                }
            }

            byte[] payload = new byte[payloadSize];
            int offset = 0;

            WriteU32(payload, offset, (uint)neighborRegions);
            offset += sizeof(uint);

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

                    bool hasRegion = true;
                    WriteU32(payload, offset, hasRegion ? 1u : 0u);
                    offset += sizeof(uint);

                    if (totalCells > 0)
                    {
                        ulong topCell = 1UL;
                        WriteU64(payload, offset, topCell);
                        offset += sizeof(ulong);
                    }
                }
            }

            return payload;
        }

        private static void MarkRegionsHotAround(int3 center)
        {
            int loadRadius = 2;
            for (int dx = -loadRadius; dx <= loadRadius; dx++)
            {
                for (int dz = -loadRadius; dz <= loadRadius; dz++)
                {
                }
            }
        }

        private const int k_CoordSize = sizeof(int) * 3;

        public struct Server
        {
            public uint TerrainSeed;
            public uint CurrentTick;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RegisterPlayer(int playerId, int connectionId)
            {
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void ScheduleRegionStream(int connectionId, int3 regionCoord, int radius, MipLevel mipLevel)
            {
            }
        }

        private const int k_InitialLoadRadius = 2;

        public struct SessionJoinPayload
        {
            public ushort PlayerId;
            public uint TerrainSeed;
            public uint CurrentTick;
        }

        private static void SendMessage<T>(int connectionId, PacketType type, ref T payload) where T : struct
        {
        }

        private static void SendBulkMessage(int connectionId, PacketType type, byte[] payload)
        {
        }

        public enum PacketType : byte
        {
            C_SessionJoinRequest = 1,
            S_SessionJoin = 2,
            S_TopLevelMip = 3,
            C_RegionRequest = 4,
            S_RegionResponse = 5,
            S_BulkRegion = 6,
        }

        public enum MipLevel : byte
        {
            CoarseSummary = 5,
            FullDetail = 6,
        }

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

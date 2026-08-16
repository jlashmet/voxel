using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel
{
    /// <summary>
    /// Compact feature-preserving summary of one authoritative 8^3 voxel block for coarse
    /// rendering. The block is split into eight 4^3 subcells; each occupied subcell retains a
    /// representative solid material. This is deliberately not the Storage occupancy mip's
    /// any-solid projection: independent features on opposite sides of a block stay independent.
    /// </summary>
    public struct SurfaceBlockHlodSummary
    {
        public byte OccupiedSubcells;
        public ulong PackedMaterials;

        public bool IsOccupied(int subcell) =>
            (OccupiedSubcells & (1 << subcell)) != 0;

        public byte MaterialAt(int subcell) =>
            (byte)(PackedMaterials >> (subcell * 8));
    }

    /// <summary>
    /// Pure Burst-compatible summarization shared by the asynchronous HLOD build job and focused
    /// tests. One bit/material covers a 4^3 voxel subcell (0.4 m with the production voxel size),
    /// preserving openings and thin authored features far better than treating an entire 8^3
    /// block as one any-solid density sample.
    /// </summary>
    public static class SurfaceBlockHlodSummaryBuilder
    {
        public const int BlockEdge = 8;
        public const int SubcellEdge = 4;
        public const int SubcellsPerAxis = 2;
        public const int VoxelsPerBlock = BlockEdge * BlockEdge * BlockEdge;

        public static SurfaceBlockHlodSummary Empty => default;

        public static SurfaceBlockHlodSummary Uniform(byte material)
        {
            if (!IsSolid(material)) return default;
            ulong packed = material;
            packed |= packed << 8;
            packed |= packed << 16;
            packed |= packed << 32;
            return new SurfaceBlockHlodSummary
            {
                OccupiedSubcells = byte.MaxValue,
                PackedMaterials = packed,
            };
        }

        public static SurfaceBlockHlodSummary Mixed(NativeArray<byte> voxels, int offset)
        {
            SurfaceBlockHlodSummary summary = default;
            for (int sz = 0; sz < SubcellsPerAxis; sz++)
            for (int sy = 0; sy < SubcellsPerAxis; sy++)
            for (int sx = 0; sx < SubcellsPerAxis; sx++)
            {
                int subcell = sx | (sy << 1) | (sz << 2);
                byte representative = 0;
                int minX = sx * SubcellEdge;
                int minY = sy * SubcellEdge;
                int minZ = sz * SubcellEdge;

                // Deterministic first-solid representative. Material choice is presentation-only;
                // occupancy preservation is the load-bearing part of this summary. A later HLOD
                // material refinement can change this without changing geometry ownership.
                for (int z = minZ; z < minZ + SubcellEdge && representative == 0; z++)
                for (int y = minY; y < minY + SubcellEdge && representative == 0; y++)
                for (int x = minX; x < minX + SubcellEdge; x++)
                {
                    byte material = voxels[offset + VoxelIndex(x, y, z)];
                    if (!IsSolid(material)) continue;
                    representative = material;
                    break;
                }

                if (representative == 0) continue;
                summary.OccupiedSubcells |= (byte)(1 << subcell);
                summary.PackedMaterials |= (ulong)representative << (subcell * 8);
            }
            return summary;
        }

        private static int VoxelIndex(int x, int y, int z) =>
            x | (y << 3) | (z << 6);

        private static bool IsSolid(byte material) =>
            material != 0 && material != 11 && material != 16;
    }

    /// <summary>
    /// Compresses the immutable exact-snapshot brick metadata/payloads into coarse HLOD summaries
    /// off the frame thread. Mixed payloads are the same COW-pinned Storage arrays already used by
    /// exact extraction; the result itself is compact renderer-owned data suitable for a later
    /// surface-block mesher.
    /// </summary>
    [BurstCompile]
    internal struct SurfaceBlockHlodSummaryJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<TransvoxelDensityBrick> Bricks;
        [NativeDisableContainerSafetyRestriction, ReadOnly]
        public NativeArray<byte> MixedVoxels;
        [WriteOnly] public NativeArray<SurfaceBlockHlodSummary> Summaries;

        public void Execute(int index)
        {
            TransvoxelDensityBrick brick = Bricks[index];
            Summaries[index] = brick.Kind switch
            {
                0 => SurfaceBlockHlodSummaryBuilder.Empty,
                1 => SurfaceBlockHlodSummaryBuilder.Uniform(brick.UniformMaterial),
                _ => SurfaceBlockHlodSummaryBuilder.Mixed(MixedVoxels, brick.MixedOffset),
            };
        }
    }
}

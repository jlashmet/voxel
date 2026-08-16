using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel
{
    /// <summary>
    /// Feature-preserving summary of one authoritative 8^3 voxel block for distant rendering.
    /// The block is split into sixty-four 2^3 subcells. Occupancy stays one bit per subcell and
    /// each occupied subcell retains a representative material. This doubles linear resolution
    /// over the former 4^3 summaries while remaining dramatically smaller than the 512-cell source.
    /// </summary>
    public struct SurfaceBlockHlodSummary
    {
        public ulong OccupiedSubcells;
        public ulong PackedMaterials0;
        public ulong PackedMaterials1;
        public ulong PackedMaterials2;
        public ulong PackedMaterials3;
        public ulong PackedMaterials4;
        public ulong PackedMaterials5;
        public ulong PackedMaterials6;
        public ulong PackedMaterials7;

        public bool IsOccupied(int subcell) =>
            (OccupiedSubcells & (1UL << subcell)) != 0;

        public byte MaterialAt(int subcell)
        {
            int shift = (subcell & 7) * 8;
            ulong packed = (subcell >> 3) switch
            {
                0 => PackedMaterials0,
                1 => PackedMaterials1,
                2 => PackedMaterials2,
                3 => PackedMaterials3,
                4 => PackedMaterials4,
                5 => PackedMaterials5,
                6 => PackedMaterials6,
                _ => PackedMaterials7,
            };
            return (byte)(packed >> shift);
        }

        public void Set(int subcell, byte material)
        {
            if (material == 0) return;
            OccupiedSubcells |= 1UL << subcell;
            int shift = (subcell & 7) * 8;
            ulong value = (ulong)material << shift;
            switch (subcell >> 3)
            {
                case 0: PackedMaterials0 |= value; break;
                case 1: PackedMaterials1 |= value; break;
                case 2: PackedMaterials2 |= value; break;
                case 3: PackedMaterials3 |= value; break;
                case 4: PackedMaterials4 |= value; break;
                case 5: PackedMaterials5 |= value; break;
                case 6: PackedMaterials6 |= value; break;
                default: PackedMaterials7 |= value; break;
            }
        }
    }

    /// <summary>
    /// Pure Burst-compatible summarization shared by the asynchronous HLOD build job and focused
    /// tests. One bit/material covers a 2^3 voxel subcell (0.2 m with the production voxel size),
    /// so windows, crenellations, tower profiles and other castle-scale details do not disappear
    /// merely because they share an 8^3 storage block.
    /// </summary>
    public static class SurfaceBlockHlodSummaryBuilder
    {
        public const int BlockEdge = 8;
        public const int SubcellEdge = 2;
        public const int SubcellsPerAxis = 4;
        public const int VoxelsPerBlock = BlockEdge * BlockEdge * BlockEdge;

        public static SurfaceBlockHlodSummary Empty => default;

        public static SurfaceBlockHlodSummary Uniform(byte material)
        {
            if (!IsSolid(material)) return default;
            ulong repeated = material;
            repeated |= repeated << 8;
            repeated |= repeated << 16;
            repeated |= repeated << 32;
            return new SurfaceBlockHlodSummary
            {
                OccupiedSubcells = ulong.MaxValue,
                PackedMaterials0 = repeated,
                PackedMaterials1 = repeated,
                PackedMaterials2 = repeated,
                PackedMaterials3 = repeated,
                PackedMaterials4 = repeated,
                PackedMaterials5 = repeated,
                PackedMaterials6 = repeated,
                PackedMaterials7 = repeated,
            };
        }

        public static SurfaceBlockHlodSummary Mixed(NativeArray<byte> voxels, int offset)
        {
            SurfaceBlockHlodSummary summary = default;
            for (int sz = 0; sz < SubcellsPerAxis; sz++)
            for (int sy = 0; sy < SubcellsPerAxis; sy++)
            for (int sx = 0; sx < SubcellsPerAxis; sx++)
            {
                int subcell = sx
                            + sy * SubcellsPerAxis
                            + sz * SubcellsPerAxis * SubcellsPerAxis;
                byte representative = 0;
                int minX = sx * SubcellEdge;
                int minY = sy * SubcellEdge;
                int minZ = sz * SubcellEdge;

                // Deterministic first-solid representative. Material choice is presentation-only;
                // occupancy preservation is the load-bearing part of this summary.
                for (int z = minZ; z < minZ + SubcellEdge && representative == 0; z++)
                for (int y = minY; y < minY + SubcellEdge && representative == 0; y++)
                for (int x = minX; x < minX + SubcellEdge; x++)
                {
                    byte material = voxels[offset + VoxelIndex(x, y, z)];
                    if (!IsSolid(material)) continue;
                    representative = material;
                    break;
                }

                summary.Set(subcell, representative);
            }
            return summary;
        }

        private static int VoxelIndex(int x, int y, int z) =>
            x | (y << 3) | (z << 6);

        private static bool IsSolid(byte material) =>
            material != 0 && material != 11 && material != 16;
    }

    /// <summary>
    /// Compresses immutable exact-snapshot block payloads into coarse HLOD summaries off the frame
    /// thread. Mixed payloads are the same COW-pinned Storage arrays used by exact extraction.
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

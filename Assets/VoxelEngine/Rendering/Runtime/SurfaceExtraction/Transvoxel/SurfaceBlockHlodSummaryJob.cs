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
                int minX = sx * SubcellEdge;
                int minY = sy * SubcellEdge;
                int minZ = sz * SubcellEdge;

                // Vote among the exposed solid in each X/Z column. A terrain slope can have one
                // high stone edge beside three slightly lower grass caps; choosing the globally
                // highest voxel lets that one edge paint the whole HLOD patch gray. Column voting
                // keeps buried material out and rejects that spatial outlier without changing
                // occupancy or geometric resolution.
                byte material0 = 0, material1 = 0, material2 = 0, material3 = 0;
                int count0 = 0, count1 = 0, count2 = 0, count3 = 0;
                for (int z = minZ; z < minZ + SubcellEdge; z++)
                for (int x = minX; x < minX + SubcellEdge; x++)
                {
                    byte exposed = 0;
                    for (int y = minY + SubcellEdge - 1; y >= minY; y--)
                    {
                        byte candidate = voxels[offset + VoxelIndex(x, y, z)];
                        if (!IsSolid(candidate)) continue;
                        exposed = candidate;
                        break;
                    }
                    if (exposed == 0) continue;
                    if (material0 == 0 || material0 == exposed)
                    {
                        material0 = exposed;
                        count0++;
                    }
                    else if (material1 == 0 || material1 == exposed)
                    {
                        material1 = exposed;
                        count1++;
                    }
                    else if (material2 == 0 || material2 == exposed)
                    {
                        material2 = exposed;
                        count2++;
                    }
                    else
                    {
                        material3 = exposed;
                        count3++;
                    }
                }

                byte representative = material0;
                int bestCount = count0;
                if (count1 > bestCount) { representative = material1; bestCount = count1; }
                if (count2 > bestCount) { representative = material2; bestCount = count2; }
                if (count3 > bestCount) representative = material3;
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

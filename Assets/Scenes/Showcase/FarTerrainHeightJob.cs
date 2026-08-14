using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using VoxelEngine.Core.Terrain;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Samples the terrain height field over one clipmap ring's lattice.
    ///
    /// Reads the same integer <see cref="TerrainSampler.HeightAt"/> the voxel generator uses, so
    /// the far mesh and the voxel surface are two sample rates of one height field rather than
    /// two authored representations that could drift apart.
    /// </summary>
    [BurstCompile]
    internal struct FarTerrainHeightJob : IJobParallelFor
    {
        public int2 Origin;
        public int Spacing;
        public int VertsPerAxis;
        public uint Seed;

        [WriteOnly] public NativeArray<int> Heights;

        public void Execute(int index)
        {
            int x = index % VertsPerAxis;
            int z = index / VertsPerAxis;
            Heights[index] = TerrainSampler.HeightAt(Origin.x + x * Spacing,
                                                     Origin.y + z * Spacing,
                                                     Seed);
        }
    }
}

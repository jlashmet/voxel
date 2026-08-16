using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;

namespace VoxelEngine.Showcase
{
    [BurstCompile]
    internal struct ShowcaseHeightJob : IJobParallelFor
    {
        public Unity.Collections.NativeArray<int> Heights;
        public int MinX;
        public int MinZ;
        public int Width;
        public uint Seed;

        public void Execute(int index)
        {
            int x = index % Width;
            int z = index / Width;
            Heights[index] = TerrainSampler.HeightAt(MinX + x, MinZ + z, Seed);
        }
    }
}

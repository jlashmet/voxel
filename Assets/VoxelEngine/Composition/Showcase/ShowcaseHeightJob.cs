using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;

namespace VoxelEngine.Showcase
{
    [BurstCompile]
    internal struct ShowcaseHeightJob : IJobParallelFor
    {
        [WriteOnly] public NativeArray<int> Heights;
        public int2 Origin;
        public int Edge;
        public uint Seed;

        public void Execute(int index)
        {
            int x = index % Edge;
            int z = index / Edge;
            Heights[index] = TerrainSampler.HeightAt(Origin.x + x, Origin.y + z, Seed);
        }
    }
}

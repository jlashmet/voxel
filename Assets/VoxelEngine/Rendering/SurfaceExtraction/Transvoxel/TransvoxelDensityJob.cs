using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace VoxelEngine.Rendering.SurfaceExtraction.Transvoxel
{
    internal struct TransvoxelDensityBrick
    {
        // 0 = empty / hard-owned, 1 = uniform, 2 = mixed payload in MixedVoxels.
        public byte Kind;
        public byte UniformMaterial;
        public int MixedOffset;
    }

    /// <summary>
    /// Evaluates the 35^3 smooth-field lattice for one 12.8 m Transvoxel chunk.
    ///
    /// The main thread snapshots only the bricks surrounding the chunk and packs mixed-brick voxel
    /// payloads into a compact array. The job therefore performs no RegionTable hashing, no region
    /// lifetime access, and no BrickPool reads while gameplay can edit/evict the authoritative
    /// world. It is a pure read-only calculation over immutable snapshot data.
    /// </summary>
    [BurstCompile]
    internal struct TransvoxelDensityJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<TransvoxelDensityBrick> Bricks;
        [ReadOnly] public NativeArray<byte> MixedVoxels;

        [WriteOnly] public NativeArray<float> Density;
        [WriteOnly] public NativeArray<byte> Materials;

        public int3 ChunkOriginVoxel;
        public int3 BrickCacheOrigin;
        public int BrickCacheEdge;
        public int GridSize;
        public int Padding;
        public int SourceStep;

        public void Execute(int index)
        {
            int gx = index % GridSize;
            int yz = index / GridSize;
            int gy = yz % GridSize;
            int gz = yz / GridSize;

            int3 p = ChunkOriginVoxel
                   + (new int3(gx, gy, gz) - Padding) * SourceStep;

            Density[index] = SampleField(p, out byte material);
            Materials[index] = material;
        }

        private float SampleField(int3 p, out byte dominantMaterial)
        {
            byte centre = ReadMaterial(p);
            float mass = IsSmoothFieldMaterial(centre) ? 0.40f : 0f;
            dominantMaterial = IsSmoothFieldMaterial(centre) ? centre : (byte)0;

            mass += Add(p + new int3( 1,0,0), 0.06f, ref dominantMaterial);
            mass += Add(p + new int3(-1,0,0), 0.06f, ref dominantMaterial);
            mass += Add(p + new int3(0, 1,0), 0.06f, ref dominantMaterial);
            mass += Add(p + new int3(0,-1,0), 0.06f, ref dominantMaterial);
            mass += Add(p + new int3(0,0, 1), 0.06f, ref dominantMaterial);
            mass += Add(p + new int3(0,0,-1), 0.06f, ref dominantMaterial);

            mass += Add(p + new int3( 2,0,0), 0.04f, ref dominantMaterial);
            mass += Add(p + new int3(-2,0,0), 0.04f, ref dominantMaterial);
            mass += Add(p + new int3(0, 2,0), 0.04f, ref dominantMaterial);
            mass += Add(p + new int3(0,-2,0), 0.04f, ref dominantMaterial);
            mass += Add(p + new int3(0,0, 2), 0.04f, ref dominantMaterial);
            mass += Add(p + new int3(0,0,-2), 0.04f, ref dominantMaterial);

            return mass - 0.5f;
        }

        private float Add(int3 p, float weight, ref byte dominantMaterial)
        {
            byte material = ReadMaterial(p);
            if (!IsSmoothFieldMaterial(material)) return 0f;
            if (dominantMaterial == 0) dominantMaterial = material;
            return weight;
        }

        private byte ReadMaterial(int3 p)
        {
            // Arithmetic right shift gives floor division for negative world coordinates.
            int3 worldBrick = p >> 3;
            int3 localBrick = worldBrick - BrickCacheOrigin;
            if ((uint)localBrick.x >= (uint)BrickCacheEdge
                || (uint)localBrick.y >= (uint)BrickCacheEdge
                || (uint)localBrick.z >= (uint)BrickCacheEdge)
                return 0;

            int brickIndex = localBrick.x
                           + BrickCacheEdge * (localBrick.y + BrickCacheEdge * localBrick.z);
            TransvoxelDensityBrick brick = Bricks[brickIndex];
            if (brick.Kind == 0) return 0;
            if (brick.Kind == 1) return brick.UniformMaterial;

            int vx = p.x & 7;
            int vy = p.y & 7;
            int vz = p.z & 7;
            int voxelIndex = vx | (vy << 3) | (vz << 6);
            return MixedVoxels[brick.MixedOffset + voxelIndex];
        }

        private static bool IsSmoothFieldMaterial(byte material) =>
            material == 1 || material == 3 || material == 5 || material == 6
            || material == 10 || material == 13 || material == 14;
    }
}

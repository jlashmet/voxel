using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Rendering.SurfaceExtraction.Transvoxel
{
    /// <summary>Exact planar face masks read directly from a compact immutable brick snapshot.</summary>
    [BurstCompile]
    internal struct SnapshotFacetedMaskJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<TransvoxelDensityBrick> Bricks;
        [ReadOnly] public NativeArray<byte> MixedVoxels;
        [ReadOnly] public NativeArray<ushort> MixedSurfaceSemantics;
        [ReadOnly] public NativeArray<byte> MixedBoundarySamples;
        public MaterialPalette Palette;
        public SurfaceCatalogue Catalogue;
        public CoatingCatalogue Coatings;
        public int3 ChunkOriginVoxel;
        public int3 BrickCacheOrigin;
        public int BrickCacheEdge;
        public int CellsPerAxis;
        [NativeDisableParallelForRestriction, WriteOnly] public NativeArray<uint> FaceMasks;

        public void Execute(int index)
        {
            int cellsPerPlane = CellsPerAxis * CellsPerAxis;
            int plane = index / cellsPerPlane;
            int inPlane = index - plane * cellsPerPlane;
            int a = inPlane % CellsPerAxis;
            int b = inPlane / CellsPerAxis;
            int layer = plane % CellsPerAxis;
            int face = plane / CellsPerAxis;
            int axis = face >> 1;
            int sign = (face & 1) == 0 ? -1 : 1;
            int axisA = (axis + 1) % 3;
            int axisB = (axis + 2) % 3;
            int3 local = int3.zero;
            local[axis] = layer;
            local[axisA] = a;
            local[axisB] = b;
            int3 voxel = ChunkOriginVoxel + local;
            byte material = Read(voxel, out uint surface, out byte boundary);
            SurfaceStyleDefinition style = Catalogue.Get((ushort)surface);
            bool displaced = Coatings.Get((byte)(surface >> 16)).Displacement != 0;
            bool faceted = IsSolid(material)
                && (style.Reconstruction == SurfaceReconstruction.Sharp
                    || style.Reconstruction == SurfaceReconstruction.Cubic
                    || style.Reconstruction == SurfaceReconstruction.Planar
                       && !new VoxelBoundarySample { Packed = boundary }.AppliesAlong(axis)
                       && !displaced);
            if (!faceted) { FaceMasks[index] = 0; return; }
            int3 neighbour = voxel;
            neighbour[axis] += sign;
            byte adjacent = Read(neighbour, out _, out byte neighbourBoundary);
            FaceMasks[index] = IsSolid(adjacent)
                || new VoxelBoundarySample { Packed = neighbourBoundary }.AppliesAlong(axis)
                ? 0u : Pack(material, surface) + 1u;
        }

        private byte Read(int3 voxel, out uint surface, out byte boundary)
        {
            int3 worldBrick = voxel >> VoxelDimensions.BrickEdgeLog2;
            int3 localBrick = worldBrick - BrickCacheOrigin;
            if (math.any(localBrick < 0) || math.any(localBrick >= BrickCacheEdge))
            { surface = 0; boundary = 0; return 0; }
            int brickIndex = localBrick.x
                           + BrickCacheEdge * (localBrick.y + BrickCacheEdge * localBrick.z);
            TransvoxelDensityBrick brick = Bricks[brickIndex];
            if (brick.Kind == 0) { surface = 0; boundary = 0; return 0; }
            if (brick.Kind == 1)
            {
                boundary = 0;
                surface = Palette.GetDefaultSurfaceStyle(brick.UniformMaterial);
                return brick.UniformMaterial;
            }
            int3 local = voxel & VoxelDimensions.BrickEdgeMask;
            int voxelIndex = local.x | (local.y << 3) | (local.z << 6);
            int payload = brick.MixedOffset + voxelIndex;
            byte material = MixedVoxels[payload];
            surface = VoxelSurfaceSemantics.FromStorage(MixedSurfaceSemantics[payload]).Packed;
            if ((ushort)surface == SurfaceStyles.MaterialDefault)
                surface = (surface & 0xffff0000u) | Palette.GetDefaultSurfaceStyle(material);
            boundary = MixedBoundarySamples[payload];
            return material;
        }

        private static bool IsSolid(byte material) =>
            material != 0 && material != 11 && material != 16;
        private static uint Pack(byte material, uint surface) => material
            | (((surface >> 16) & 0xffu) << 8)
            | ((surface & 0xffu) << 16)
            | (((surface >> 24) & 0xffu) << 24);
    }
}

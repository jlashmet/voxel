using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel
{
    /// <summary>
    /// Builds all six exact planar face masks from compact block metadata plus COW-pinned
    /// immutable Storage payloads. Cell coordinates are mapped through SourceStep so every LOD
    /// samples and emits faceted geometry in the same world-voxel coordinate system.
    /// </summary>
    [BurstCompile]
    internal struct SnapshotFacetedMaskJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<TransvoxelDensityBrick> Bricks;
        [NativeDisableContainerSafetyRestriction, ReadOnly]
        public NativeArray<byte> MixedVoxels;
        [NativeDisableContainerSafetyRestriction, ReadOnly]
        public NativeArray<ushort> MixedSurfaceSemantics;
        [NativeDisableContainerSafetyRestriction, ReadOnly]
        public NativeArray<byte> MixedBoundarySamples;
        public MaterialPaletteView Palette;
        public SurfaceCatalogueView Catalogue;
        public CoatingCatalogueView Coatings;
        public int3 ChunkOriginVoxel;
        public int3 BrickCacheOrigin;
        public int BrickCacheEdge;
        public int CellsPerAxis;
        public int SourceStep;
        [NativeDisableParallelForRestriction, WriteOnly] public NativeArray<uint> FaceMasks;

        public void Execute(int index)
        {
            int cellsPerPlane = CellsPerAxis * CellsPerAxis;
            int3 local = new(index % CellsPerAxis,
                             index / CellsPerAxis % CellsPerAxis,
                             index / cellsPerPlane);
            int step = math.max(1, SourceStep);
            int3 voxel = ChunkOriginVoxel + local * step;
            byte material = Read(voxel, out uint surface, out byte boundary);
            SurfaceStyleReadDefinition style = Catalogue.Get((ushort)surface);
            bool displaced = Coatings.Get((byte)(surface >> 16)).Displacement != 0;
            bool solid = IsSolid(material);
            uint encoded = Pack(material, surface) + 1u;
            var boundarySample = new VoxelBoundarySample { Packed = boundary };
            for (int axis = 0; axis < 3; axis++)
            {
                int axisA = (axis + 1) % 3;
                int axisB = (axis + 2) % 3;
                int a = local[axisA];
                int b = local[axisB];
                int layer = local[axis];
                bool faceted = solid
                    && (style.Reconstruction == SurfaceReconstruction.Sharp
                        || style.Reconstruction == SurfaceReconstruction.Cubic
                        || style.Reconstruction == SurfaceReconstruction.Planar
                           && !boundarySample.AppliesAlong(axis) && !displaced);
                for (int side = 0; side < 2; side++)
                {
                    int face = axis * 2 + side;
                    int output = (face * CellsPerAxis + layer) * cellsPerPlane
                               + a + b * CellsPerAxis;
                    if (!faceted)
                    {
                        FaceMasks[output] = 0;
                        continue;
                    }
                    int3 neighbour = voxel;
                    neighbour[axis] += side == 0 ? -step : step;
                    byte adjacent = Read(neighbour, out _, out _);
                    // Face ownership belongs to the occupied cell. Empty-side boundary metadata
                    // may be an unrelated primitive's halo and cannot erase an exact occupancy face.
                    FaceMasks[output] = IsSolid(adjacent) ? 0u : encoded;
                }
            }
        }

        private byte Read(int3 voxel, out uint surface, out byte boundary)
        {
            int3 worldBrick = voxel >> VoxelReadGrid.BlockEdgeLog2;
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
            int3 local = voxel & VoxelReadGrid.BlockEdgeMask;
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

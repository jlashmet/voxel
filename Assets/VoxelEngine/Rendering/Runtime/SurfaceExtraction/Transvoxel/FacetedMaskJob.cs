using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel
{
    /// <summary>
    /// Builds all six exact exposed-face masks in one parallel pass over the chunk's cells.
    /// Rectangle merging stays deterministic because every cell owns six disjoint output slots.
    /// </summary>
    [BurstCompile]
    internal struct FacetedMaskJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<byte> Materials;
        [ReadOnly] public NativeArray<uint> SurfaceSemantics;
        [ReadOnly] public NativeArray<byte> BoundarySamples;
        public SurfaceCatalogueView Catalogue;
        public CoatingCatalogueView Coatings;
        public int CellsPerAxis;
        public int GridSize;
        public int Padding;
        [NativeDisableParallelForRestriction, WriteOnly] public NativeArray<uint> FaceMasks;

        public void Execute(int index)
        {
            int cellsPerPlane = CellsPerAxis * CellsPerAxis;
            int3 local = new(index % CellsPerAxis,
                             index / CellsPerAxis % CellsPerAxis,
                             index / cellsPerPlane);
            int3 grid = local + Padding;
            int sample = GridIndex(grid);
            byte material = Materials[sample];
            uint surface = SurfaceSemantics[sample];
            byte boundary = BoundarySamples[sample];
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
                    int3 neighbourGrid = grid;
                    neighbourGrid[axis] += side == 0 ? -1 : 1;
                    int neighbourSample = GridIndex(neighbourGrid);
                    byte neighbour = Materials[neighbourSample];
                    // The occupied cell decides whether its face is continuous or faceted. An
                    // empty neighbour can carry a halo from a different curved primitive (for
                    // example rounded veneer in front of planar wall backing); that metadata must
                    // not steal the exact solid-to-empty occupancy face from this planar cell.
                    FaceMasks[output] = IsSolid(neighbour) ? 0u : encoded;
                }
            }
        }

        private int GridIndex(int3 p) => p.x + GridSize * (p.y + GridSize * p.z);
        private static bool IsSolid(byte material) =>
            material != 0 && material != 11 && material != 16;
        private static uint Pack(byte material, uint surface) => material
            | (((surface >> 16) & 0xffu) << 8)
            | ((surface & 0xffu) << 16)
            | (((surface >> 24) & 0xffu) << 24);
    }
}

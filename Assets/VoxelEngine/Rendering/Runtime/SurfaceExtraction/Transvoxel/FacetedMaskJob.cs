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

                bool planarCap = style.Reconstruction == SurfaceReconstruction.Planar
                    && !boundarySample.AppliesAlong(axis)
                    && !displaced;
                if (planarCap && boundarySample.IsAuthored
                    && (TransvoxelTopologyJob.IsExtrusionCapRimSample(boundarySample, axis)
                        || HasInPlaneOccupancyTransition(grid, axis)))
                {
                    // The cap interior stays an exact greedy plane. At its analytic perimeter,
                    // however, a whole-voxel rectangle would overdraw continuous topology and put
                    // the staircase back on top of the projected SDF rim. A diagonal-only in-plane
                    // transition is enough to prove that the contour crosses this cap neighbourhood;
                    // it must not be reduced to a six-neighbour or centre-distance test.
                    planarCap = false;
                }

                bool faceted = solid
                    && (style.Reconstruction == SurfaceReconstruction.Sharp
                        || style.Reconstruction == SurfaceReconstruction.Cubic
                        || planarCap);
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
                    byte neighbourBoundary = BoundarySamples[neighbourSample];
                    FaceMasks[output] = IsSolid(neighbour)
                        || new VoxelBoundarySample { Packed = neighbourBoundary }
                            .AppliesAlong(axis)
                        ? 0u : encoded;
                }
            }
        }

        private bool HasInPlaneOccupancyTransition(int3 grid, int faceAxis)
        {
            int axisA = (faceAxis + 1) % 3;
            int axisB = (faceAxis + 2) % 3;
            for (int b = -1; b <= 1; b++)
            for (int a = -1; a <= 1; a++)
            {
                if (a == 0 && b == 0) continue;
                int3 neighbour = grid;
                neighbour[axisA] += a;
                neighbour[axisB] += b;
                if (!IsSolid(Materials[GridIndex(neighbour)])) return true;
            }
            return false;
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

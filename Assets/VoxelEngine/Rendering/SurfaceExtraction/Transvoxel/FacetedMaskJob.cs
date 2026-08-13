using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Rendering.SurfaceExtraction.Transvoxel
{
    /// <summary>Builds exact exposed-face masks in parallel; rectangle merging stays deterministic.</summary>
    [BurstCompile]
    internal struct FacetedMaskJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<byte> Materials;
        [ReadOnly] public NativeArray<uint> SurfaceSemantics;
        [ReadOnly] public NativeArray<byte> BoundarySamples;
        public SurfaceCatalogue Catalogue;
        public CoatingCatalogue Coatings;
        public int CellsPerAxis;
        public int GridSize;
        public int Padding;
        [NativeDisableParallelForRestriction, WriteOnly] public NativeArray<uint> FaceMasks;

        public void Execute(int index)
        {
            int cellsPerPlane = CellsPerAxis * CellsPerAxis;
            int planeIndex = index / cellsPerPlane;
            int inPlane = index - planeIndex * cellsPerPlane;
            int a = inPlane % CellsPerAxis;
            int b = inPlane / CellsPerAxis;
            int layer = planeIndex % CellsPerAxis;
            int face = planeIndex / CellsPerAxis;
            int axis = face >> 1;
            int sign = (face & 1) == 0 ? -1 : 1;
            int axisA = (axis + 1) % 3;
            int axisB = (axis + 2) % 3;
            int3 local = int3.zero;
            local[axis] = layer;
            local[axisA] = a;
            local[axisB] = b;
            int3 grid = local + Padding;
            int sample = GridIndex(grid);
            byte material = Materials[sample];
            uint surface = SurfaceSemantics[sample];
            byte boundary = BoundarySamples[sample];
            SurfaceStyleDefinition style = Catalogue.Get((ushort)surface);
            bool displaced = Coatings.Get((byte)(surface >> 16)).Displacement != 0;
            bool boundaryAffectsFace = new VoxelBoundarySample { Packed = boundary }
                .AppliesAlong(axis);
            bool faceted = IsSolid(material)
                && (style.Reconstruction == SurfaceReconstruction.Sharp
                    || style.Reconstruction == SurfaceReconstruction.Cubic
                    || style.Reconstruction == SurfaceReconstruction.Planar
                       && !boundaryAffectsFace && !displaced);
            if (!faceted)
            {
                FaceMasks[index] = 0;
                return;
            }
            int3 neighbourGrid = grid;
            neighbourGrid[axis] += sign;
            int neighbourSample = GridIndex(neighbourGrid);
            byte neighbour = Materials[neighbourSample];
            byte neighbourBoundary = BoundarySamples[neighbourSample];
            FaceMasks[index] = IsSolid(neighbour)
                || new VoxelBoundarySample { Packed = neighbourBoundary }.AppliesAlong(axis)
                ? 0u : Pack(material, surface) + 1u;
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

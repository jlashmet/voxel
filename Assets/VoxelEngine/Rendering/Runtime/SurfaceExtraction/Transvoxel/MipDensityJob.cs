using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace VoxelEngine.Rendering.SurfaceExtraction.Transvoxel
{
    /// <summary>
    /// Evaluates the smooth-field lattice for one coarse-ring chunk from a mip snapshot.
    ///
    /// <para>This is the coarse-ring counterpart to <see cref="TransvoxelDensityJob"/>. The fine
    /// rings sample individual voxels through a padded brick cache whose size grows with the
    /// cube of the ring's stride; at a stride of eight voxels that cache would already be
    /// 39,000 bricks, and beyond that it is untenable. A coarse ring instead snapshots one mip
    /// cell per lattice sample, so its snapshot is a fixed <c>GridSize³</c> regardless of how
    /// much world the chunk spans — the property that makes whole-world view distance
    /// affordable.</para>
    ///
    /// <para>Mip cells carry occupancy and a representative material, but no authored surface
    /// semantics or boundary samples: those describe sub-voxel detail that a cell spanning
    /// metres cannot represent. Surface style therefore falls back to the palette default for
    /// the cell's material, which is what the faceted path already does when a voxel carries
    /// <see cref="SurfaceStyles.MaterialDefault"/>.</para>
    /// </summary>
    [BurstCompile]
    internal struct MipDensityJob : IJobParallelFor
    {
        /// <summary>One byte per lattice sample: non-zero where the mip cell is occupied.</summary>
        [ReadOnly] public NativeArray<byte> SampleOccupancy;
        /// <summary>Representative material per lattice sample, parallel to occupancy.</summary>
        [ReadOnly] public NativeArray<byte> SampleMaterials;

        public MaterialPaletteView Palette;

        [WriteOnly] public NativeArray<float> Density;
        [WriteOnly] public NativeArray<byte> Materials;
        [WriteOnly] public NativeArray<uint> SurfaceSemantics;
        [WriteOnly] public NativeArray<byte> BoundarySamples;

        public int GridSize;

        public void Execute(int index)
        {
            bool solid = SampleOccupancy[index] != 0;
            byte material = solid ? SampleMaterials[index] : VoxelGrid.MaterialEmpty;

            // A mip cell is binary, so the field is the plain marching-cubes step: half a cell
            // inside the surface for solid samples, half outside for empty ones. Interpolation
            // across the cell edge still places the vertex smoothly, which is what keeps a
            // coarse ring from looking like a voxel staircase.
            Density[index] = solid ? 0.5f : -0.5f;
            Materials[index] = material;
            SurfaceSemantics[index] = solid ? Palette.GetDefaultSurfaceStyle(material) : 0u;
            BoundarySamples[index] = 0;
        }
    }
}

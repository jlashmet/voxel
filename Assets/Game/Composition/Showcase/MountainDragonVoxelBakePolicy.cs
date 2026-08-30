using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Runtime.MeshImport;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Source-specific composition policy for the checked-in mountain-dragon authoring input.
    /// Shared mesh voxelization remains mesh-agnostic; this class owns the showcase resolution and
    /// source identity constraints used when producing the baked artifact.
    /// </summary>
    public static class MountainDragonVoxelBakePolicy
    {
        public const int ExpectedSourceTriangleCount = 29734;
        public const float SourceVoxelSize = 0.30f;
        public static readonly int3 MaximumStructureSize = new int3(127, 511, 127);

        public static MeshVoxelizationSettings CreateSettings(byte fallbackMaterial)
        {
            if (fallbackMaterial == 0)
                throw new ArgumentOutOfRangeException(nameof(fallbackMaterial));

            return new MeshVoxelizationSettings(
                voxelSize: SourceVoxelSize,
                fillInterior: true,
                fallbackMaterial: fallbackMaterial,
                maxDimensions: MaximumStructureSize,
                maxDenseCells: 2_000_000,
                thinFeaturePaddingVoxels: 0,
                openSurfacePolicy: MeshVoxelOpenSurfacePolicy.Reject);
        }

        public static void ValidateBakeEnvelope(BakedVoxelStructure bake)
        {
            if (bake == null) throw new ArgumentNullException(nameof(bake));
            if (bake.SourceTriangleCount != ExpectedSourceTriangleCount)
                throw new InvalidOperationException(
                    $"Mountain-dragon bake source changed: expected {ExpectedSourceTriangleCount} triangles, " +
                    $"got {bake.SourceTriangleCount}.");
            if (math.any(bake.Size > MaximumStructureSize))
                throw new InvalidOperationException(
                    $"Mountain-dragon bake exceeds structure bounds: {bake.Size} > {MaximumStructureSize}.");
            if (bake.Cells.Length == 0)
                throw new InvalidOperationException("Mountain-dragon bake contains no authored voxels.");
            if (!bake.InteriorFilled)
                throw new InvalidOperationException(
                    "Mountain-dragon bake is not volumetric. Repair source topology rather than accepting a shell.");
        }
    }
}

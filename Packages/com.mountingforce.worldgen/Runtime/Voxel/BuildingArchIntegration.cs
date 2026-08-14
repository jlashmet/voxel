using System;
using MountingForce.WorldGen.Architecture;
using VoxelEngine.Core.Features;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Concrete arch feature plus its local facade anchor. The frontage plane/facing is deliberately
    /// left to the building placement stage; this value only owns deterministic local composition.
    /// </summary>
    public readonly struct BuildingArchPlacement
    {
        public readonly int Storey;
        public readonly int Bay;
        public readonly int CenterOffsetVoxels;
        public readonly int BaseHeightVoxels;
        public readonly ArchBayFeatureDefinition Definition;

        public BuildingArchPlacement(
            int storey,
            int bay,
            int centerOffsetVoxels,
            int baseHeightVoxels,
            ArchBayFeatureDefinition definition)
        {
            Storey = storey;
            Bay = bay;
            CenterOffsetVoxels = centerOffsetVoxels;
            BaseHeightVoxels = baseHeightVoxels;
            Definition = definition;
        }
    }

    /// <summary>
    /// Adapter from Architecture's semantic ArchBay socket to VoxelEngine's reusable arch API.
    /// Scale follows the rest of WorldGen.Voxel: semantic decimetres multiplied by
    /// voxels-per-decimetre. Wall depth stays explicit because it is a realization/style choice.
    /// </summary>
    public static class BuildingArchIntegration
    {
        public static BuildingArchPlacement Compile(
            in BuildingDetailRequest request,
            int voxelsPerDecimetre,
            int wallDepthVoxels,
            in ArchFeatureStyle style,
            uint seed)
        {
            if (request.Kind != BuildingDetailSocketKind.ArchBay)
                throw new ArgumentException("Building detail request is not an ArchBay socket.", nameof(request));
            if (voxelsPerDecimetre <= 0)
                throw new ArgumentOutOfRangeException(nameof(voxelsPerDecimetre));
            if (wallDepthVoxels <= 0)
                throw new ArgumentOutOfRangeException(nameof(wallDepthVoxels));

            int clearSpan = checked(request.WidthDm * voxelsPerDecimetre);
            int clearHeight = checked(request.HeightDm * voxelsPerDecimetre);
            if (clearSpan < 4)
                throw new InvalidOperationException(
                    "ArchBay opening is narrower than the reusable arch feature minimum at this voxel scale.");
            if (clearHeight <= 0)
                throw new InvalidOperationException(
                    "ArchBay opening has no positive voxel height at this voxel scale.");

            int center = checked(request.CenterOffsetDm * voxelsPerDecimetre);
            int baseHeight = checked(request.BaseHeightDm * voxelsPerDecimetre);

            var archRequest = new ArchBayRequest(
                clearSpan: clearSpan,
                clearHeight: clearHeight,
                depth: wallDepthVoxels,
                seed: seed);
            ArchBayFeatureDefinition definition = ArchFeatureApi.CompileBay(archRequest, style);

            return new BuildingArchPlacement(
                request.Storey,
                request.Bay,
                center,
                baseHeight,
                definition);
        }
    }
}

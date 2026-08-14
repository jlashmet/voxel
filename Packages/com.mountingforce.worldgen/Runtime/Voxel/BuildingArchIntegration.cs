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
    /// Unit conversion and wall depth are explicit inputs so neither layer silently owns world scale.
    /// </summary>
    public static class BuildingArchIntegration
    {
        public static BuildingArchPlacement Compile(
            in BuildingDetailRequest request,
            int decimetresPerVoxel,
            int wallDepthVoxels,
            in ArchFeatureStyle style,
            uint seed)
        {
            if (request.Kind != BuildingDetailSocketKind.ArchBay)
                throw new ArgumentException("Building detail request is not an ArchBay socket.", nameof(request));
            if (decimetresPerVoxel <= 0)
                throw new ArgumentOutOfRangeException(nameof(decimetresPerVoxel));
            if (wallDepthVoxels <= 0)
                throw new ArgumentOutOfRangeException(nameof(wallDepthVoxels));

            // Opening dimensions round inward so voxel realization cannot exceed the architectural
            // opening. Anchors round to nearest voxel because they describe position, not clearance.
            int clearSpan = Math.Max(4, request.WidthDm / decimetresPerVoxel);
            int clearHeight = Math.Max(1, request.HeightDm / decimetresPerVoxel);
            int center = DivideRounded(request.CenterOffsetDm, decimetresPerVoxel);
            int baseHeight = DivideRounded(request.BaseHeightDm, decimetresPerVoxel);

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

        private static int DivideRounded(int value, int divisor)
        {
            if (value >= 0)
                return (value + divisor / 2) / divisor;
            return (value - divisor / 2) / divisor;
        }
    }
}

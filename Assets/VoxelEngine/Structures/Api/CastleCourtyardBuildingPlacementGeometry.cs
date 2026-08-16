using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Compatibility seam for planner/validator call sites that already pass the resolved pieces
    /// of a castle rather than a CastleSpatialPlan. The wall-relative public planner owns the only
    /// courtyard-building placement policy; this adapter merely assembles its required context.
    /// </summary>
    internal static class CastleCourtyardBuildingPlacementGeometry
    {
        internal static CastleCourtyardBuildingSpec[] Plan(
            in CastlePlan plan,
            int2[] outerWard,
            int2[] innerWard,
            in CastleGatePlacementSpec primaryGate,
            bool hasPosternGate,
            in CastleGatePlacementSpec posternGate,
            bool hasInnerGate,
            in CastleGatePlacementSpec innerGate,
            int2 keepCentre,
            bool hasWell,
            int2 wellCentre)
        {
            if (outerWard == null || outerWard.Length < 3)
                return Array.Empty<CastleCourtyardBuildingSpec>();

            CastleTopologyPlan topology = default;
            CastleGatePlacementSpec primary = primaryGate;
            CastleGatePlacementSpec postern = posternGate;
            CastleGatePlacementSpec inner = innerGate;
            var spatial = new CastleSpatialPlan(
                in topology,
                outerWard,
                innerWard ?? Array.Empty<int2>(),
                Array.Empty<CastleTowerPlacementSpec>(),
                in primary,
                hasPosternGate,
                in postern,
                hasInnerGate,
                in inner,
                hasWell,
                wellCentre,
                Array.Empty<CastleCourtyardBuildingSpec>(),
                keepCentre,
                false);

            return CastleCourtyardBuildingPlanner.Create(in plan, spatial);
        }
    }
}

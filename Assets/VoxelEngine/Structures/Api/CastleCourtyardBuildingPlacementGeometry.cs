using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Compatibility adapter for callers that already hold decomposed spatial geometry. Courtyard
    /// building semantics now live exclusively in <see cref="CastleCourtyardBuildingPlanner"/>;
    /// this type only packages the supplied geometry into the same immutable planning view.
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
            var spatial = new CastleSpatialPlan(
                in topology,
                outerWard,
                innerWard ?? Array.Empty<int2>(),
                Array.Empty<CastleTowerPlacementSpec>(),
                in primaryGate,
                hasPosternGate,
                in posternGate,
                hasInnerGate,
                in innerGate,
                hasWell,
                wellCentre,
                Array.Empty<CastleCourtyardBuildingSpec>(),
                keepCentre,
                false);

            return CastleCourtyardBuildingPlanner.Create(in plan, spatial);
        }
    }
}

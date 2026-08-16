using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Compatibility seam for planner/validator call sites that already pass the resolved pieces
    /// of a castle rather than a CastleSpatialPlan. The wall-relative public planner owns candidate
    /// placement; this adapter applies exact polygon containment and reserves the primary access
    /// corridor before geometry becomes part of the canonical spatial plan.
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

            CastleCourtyardBuildingSpec[] candidates =
                CastleCourtyardBuildingPlanner.Create(in plan, spatial);
            if (candidates.Length == 0)
                return candidates;

            CastleAccessRoute access = CastleAccessRoute.Create(in plan, spatial);
            var accepted = new List<CastleCourtyardBuildingSpec>(candidates.Length);
            for (int i = 0; i < candidates.Length; i++)
            {
                CastleCourtyardBuildingSpec candidate = candidates[i];
                int2[] footprint = Footprint(in candidate);
                if (!CastlePolygonGeometry.ContainsPolygon(outerWard, footprint))
                    continue;
                if (innerWard != null && innerWard.Length >= 3 &&
                    CastlePolygonGeometry.PolygonsOverlapOrTouch(innerWard, footprint))
                    continue;
                if (!access.ClearsBuilding(in candidate))
                    continue;

                candidate.Id = accepted.Count;
                accepted.Add(candidate);
            }

            return accepted.ToArray();
        }

        private static int2[] Footprint(in CastleCourtyardBuildingSpec building) =>
            new[]
            {
                building.FootprintCorner(0),
                building.FootprintCorner(1),
                building.FootprintCorner(2),
                building.FootprintCorner(3),
            };
    }
}

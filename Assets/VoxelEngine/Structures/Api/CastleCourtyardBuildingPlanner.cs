using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    public enum CastleCourtyardBuildingRole : byte
    {
        Service,
    }

    /// <summary>
    /// Planner-owned courtyard building footprint. The current service-building recipe remains
    /// axis-aligned in castle-local X/Z; Runtime consumes this data without choosing placement.
    /// </summary>
    public struct CastleCourtyardBuildingSpec
    {
        public int Id;
        public CastleCourtyardBuildingRole Role;
        public int2 Centre;
        public int2 HalfExtents;
        public int Height;
        public int2 EntranceDirection;
        public bool RoofRidgeAlongX;

        public int Width => HalfExtents.x * 2;
        public int Depth => HalfExtents.y * 2;

        public int2 EntranceCentre => new int2(
            Centre.x + EntranceDirection.x * HalfExtents.x,
            Centre.y + EntranceDirection.y * HalfExtents.y);

        public int2 FootprintCorner(int index)
        {
            switch (index & 3)
            {
                case 0: return Centre + new int2(-HalfExtents.x, -HalfExtents.y);
                case 1: return Centre + new int2( HalfExtents.x, -HalfExtents.y);
                case 2: return Centre + new int2( HalfExtents.x,  HalfExtents.y);
                default: return Centre + new int2(-HalfExtents.x,  HalfExtents.y);
            }
        }
    }

    /// <summary>
    /// Public façade over the single authoritative courtyard-building placement algorithm used by
    /// CastleSpatialPlanner and CastleSpatialPlanValidator. This exists for inspection/tooling and
    /// does not introduce a second placement policy.
    /// </summary>
    public static class CastleCourtyardBuildingPlanner
    {
        public static CastleCourtyardBuildingSpec[] Create(
            in CastlePlan plan,
            CastleSpatialPlan spatial)
        {
            if (spatial == null) throw new ArgumentNullException(nameof(spatial));
            if (spatial.KeepRequiresTerrainResolution)
                return Array.Empty<CastleCourtyardBuildingSpec>();

            CastleGatePlacementSpec primaryGate = spatial.PrimaryGate;
            CastleGatePlacementSpec posternGate = spatial.PosternGate;
            CastleGatePlacementSpec innerGate = spatial.InnerGate;
            return CastleCourtyardBuildingPlacementGeometry.Plan(
                in plan,
                spatial.OuterWardVertices,
                spatial.InnerWardVertices,
                in primaryGate,
                spatial.HasPosternGate,
                in posternGate,
                spatial.HasInnerGate,
                in innerGate,
                spatial.KeepCentre,
                spatial.HasWell,
                spatial.WellCentre);
        }
    }
}

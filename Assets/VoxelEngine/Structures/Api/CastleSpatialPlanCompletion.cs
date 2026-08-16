using System;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Final pure-data completion for spatial castle details that depend on already-resolved core
    /// geometry. Composition calls this after terrain-dependent keep placement is finished; Runtime
    /// receives the completed immutable layout and never chooses courtyard building locations.
    /// </summary>
    public static class CastleSpatialPlanCompletion
    {
        public static CastleSpatialPlan AttachCourtyardBuildings(
            in CastlePlan plan,
            CastleSpatialPlan spatial)
        {
            if (spatial == null) throw new ArgumentNullException(nameof(spatial));
            if (spatial.KeepRequiresTerrainResolution)
                return spatial;

            CastleCourtyardBuildingSpec[] buildings =
                CastleCourtyardBuildingPlanner.Create(in plan, spatial);
            CastleTopologyPlan topology = spatial.Topology;
            CastleGatePlacementSpec primaryGate = spatial.PrimaryGate;
            CastleGatePlacementSpec posternGate = spatial.PosternGate;
            CastleGatePlacementSpec innerGate = spatial.InnerGate;

            return new CastleSpatialPlan(
                in topology,
                (Unity.Mathematics.int2[])spatial.OuterWardVertices.Clone(),
                (Unity.Mathematics.int2[])spatial.InnerWardVertices.Clone(),
                (CastleTowerPlacementSpec[])spatial.Towers.Clone(),
                in primaryGate,
                spatial.HasPosternGate,
                in posternGate,
                spatial.HasInnerGate,
                in innerGate,
                spatial.HasWell,
                spatial.WellCentre,
                buildings,
                spatial.KeepCentre,
                false);
        }
    }
}

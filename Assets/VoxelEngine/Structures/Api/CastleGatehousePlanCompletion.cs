using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Attaches the dimensional primary-gatehouse recipe once CastlePlan and primary-gate geometry
    /// are available. Completed spatial plans carry both structural dimensions and frozen gate-tower
    /// slit phases so Runtime never derives authored gatehouse variation while mutating voxels.
    /// </summary>
    public static class CastleGatehousePlanCompletion
    {
        public static CastleSpatialPlan Attach(
            in CastlePlan dimensions,
            CastleSpatialPlan spatial)
        {
            if (spatial == null) throw new ArgumentNullException(nameof(spatial));

            CastleGatePlacementSpec primaryGate = spatial.PrimaryGate;
            CastleTopologyPlan topology = spatial.Topology;
            if (!topology.HasGatehousePlan)
            {
                topology.Gatehouse = CastleGatehousePlanner.Create(
                    in dimensions, in primaryGate);
                topology.HasGatehousePlan = true;
            }
            else
            {
                CastleGatehousePlan gatehouse = topology.Gatehouse;
                CastleGatehousePlanValidator.RequireValid(in gatehouse);
                CastleGatehousePlanValidator.RequireTowerDetails(
                    in gatehouse, dimensions.FloorHeight);
            }

            CastleGatePlacementSpec posternGate = spatial.PosternGate;
            CastleGatePlacementSpec innerGate = spatial.InnerGate;

            return new CastleSpatialPlan(
                in topology,
                spatial.OuterWardVertices != null
                    ? (int2[])spatial.OuterWardVertices.Clone()
                    : Array.Empty<int2>(),
                spatial.InnerWardVertices != null
                    ? (int2[])spatial.InnerWardVertices.Clone()
                    : Array.Empty<int2>(),
                spatial.Towers != null
                    ? (CastleTowerPlacementSpec[])spatial.Towers.Clone()
                    : Array.Empty<CastleTowerPlacementSpec>(),
                in primaryGate,
                spatial.HasPosternGate,
                in posternGate,
                spatial.HasInnerGate,
                in innerGate,
                spatial.HasWell,
                spatial.WellCentre,
                spatial.CourtyardBuildings != null
                    ? (CastleCourtyardBuildingSpec[])spatial.CourtyardBuildings.Clone()
                    : Array.Empty<CastleCourtyardBuildingSpec>(),
                spatial.KeepFloors != null
                    ? (CastleKeepFloorPlan[])spatial.KeepFloors.Clone()
                    : Array.Empty<CastleKeepFloorPlan>(),
                spatial.KeepCirculation,
                spatial.Dungeon,
                spatial.Cave,
                spatial.Landscape,
                spatial.KeepCentre,
                spatial.KeepRequiresTerrainResolution,
                spatial.CaveDecoration,
                spatial.KeepWindows != null
                    ? (CastleKeepWindowSpec[])spatial.KeepWindows.Clone()
                    : Array.Empty<CastleKeepWindowSpec>(),
                spatial.InnerTowers != null
                    ? (CastleTowerPlacementSpec[])spatial.InnerTowers.Clone()
                    : Array.Empty<CastleTowerPlacementSpec>());
        }
    }
}

using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Attaches the dimensional primary-gatehouse recipe once CastlePlan and primary-gate geometry
    /// are available. The resulting placement-complete snapshot also freezes keep-turret slit phases,
    /// whose historical recipe depends on the final world-space keep position.
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
                    in dimensions, in primaryGate, dimensions.Seed);
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

            var withGatehouse = new CastleSpatialPlan(
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

            return CastleKeepTurretPlanCompletion.Attach(in dimensions, withGatehouse);
        }
    }
}

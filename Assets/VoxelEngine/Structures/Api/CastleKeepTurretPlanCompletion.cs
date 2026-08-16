using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Freezes keep-turret slit phases after spatial keep placement is resolved. This preserves the
    /// historical world-position-derived slit pattern while keeping that authored choice out of Runtime.
    /// </summary>
    public static class CastleKeepTurretPlanCompletion
    {
        public static CastleSpatialPlan Attach(
            in CastlePlan dimensions,
            CastleSpatialPlan spatial)
        {
            if (spatial == null) throw new ArgumentNullException(nameof(spatial));
            if (spatial.KeepRequiresTerrainResolution) return spatial;

            CastleTopologyPlan topology = spatial.Topology;
            if (!CastleKeepTurretPlanValidator.TryValidate(topology.KeepTurrets, out _))
                throw new InvalidOperationException("Castle keep turret topology is invalid.");

            CastleSpatialProjection projection = CastleSpatialProjection.Create(in dimensions, spatial);
            CastlePlan keepPlan = projection.KeepPlan;
            CastleKeepTurretSpec[] turrets = topology.KeepTurrets.Snapshot();

            int baseX = keepPlan.Centre.x - keepPlan.KeepHalfX;
            int baseZ = keepPlan.Centre.z - keepPlan.KeepHalfZ + 60;
            int width = keepPlan.KeepHalfX * 2;
            int depth = keepPlan.KeepHalfZ * 2;
            int height = keepPlan.KeepHeight + 30;

            for (int i = 0; i < turrets.Length; i++)
            {
                int2 worldCentre = turrets[i].Corner switch
                {
                    CastleKeepTurretCorner.MinXMinZ => new int2(baseX, baseZ),
                    CastleKeepTurretCorner.MaxXMinZ => new int2(baseX + width, baseZ),
                    CastleKeepTurretCorner.MinXMaxZ => new int2(baseX, baseZ + depth),
                    CastleKeepTurretCorner.MaxXMaxZ => new int2(baseX + width, baseZ + depth),
                    _ => throw new InvalidOperationException(
                        $"Castle keep contains invalid turret corner {turrets[i].Corner}."),
                };

                turrets[i].Slits = CastleTowerSlitPlanner.Create(
                    worldCentre, height, keepPlan.FloorHeight);
            }

            topology.KeepTurrets = new CastleKeepTurretPlan(turrets);
            CastleGatePlacementSpec primaryGate = spatial.PrimaryGate;
            CastleGatePlacementSpec posternGate = spatial.PosternGate;
            CastleGatePlacementSpec innerGate = spatial.InnerGate;

            return new CastleSpatialPlan(
                in topology,
                spatial.OuterWardVertices != null ? (int2[])spatial.OuterWardVertices.Clone() : Array.Empty<int2>(),
                spatial.InnerWardVertices != null ? (int2[])spatial.InnerWardVertices.Clone() : Array.Empty<int2>(),
                spatial.Towers != null ? (CastleTowerPlacementSpec[])spatial.Towers.Clone() : Array.Empty<CastleTowerPlacementSpec>(),
                in primaryGate,
                spatial.HasPosternGate,
                in posternGate,
                spatial.HasInnerGate,
                in innerGate,
                spatial.HasWell,
                spatial.WellCentre,
                spatial.CourtyardBuildings != null ? (CastleCourtyardBuildingSpec[])spatial.CourtyardBuildings.Clone() : Array.Empty<CastleCourtyardBuildingSpec>(),
                spatial.KeepFloors != null ? (CastleKeepFloorPlan[])spatial.KeepFloors.Clone() : Array.Empty<CastleKeepFloorPlan>(),
                spatial.KeepCirculation,
                spatial.Dungeon,
                spatial.Cave,
                spatial.Landscape,
                spatial.KeepCentre,
                spatial.KeepRequiresTerrainResolution,
                spatial.CaveDecoration,
                spatial.KeepWindows != null ? (CastleKeepWindowSpec[])spatial.KeepWindows.Clone() : Array.Empty<CastleKeepWindowSpec>(),
                spatial.InnerTowers != null ? (CastleTowerPlacementSpec[])spatial.InnerTowers.Clone() : Array.Empty<CastleTowerPlacementSpec>());
        }
    }
}

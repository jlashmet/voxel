using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Freezes the world-position-dependent arrow-slit phases for every already-placed defensive
    /// tower. Height/roof variation must be attached first so the slit floor count matches the
    /// exact realized tower height. Runtime receives only the resulting immutable phase plans.
    /// </summary>
    public static class CastleTowerSlitCompletion
    {
        public static CastleSpatialPlan Attach(
            in CastlePlan plan,
            CastleSpatialPlan spatial)
        {
            if (spatial == null) throw new ArgumentNullException(nameof(spatial));

            CastleTowerPlacementSpec[] outer = CloneWithSlits(
                in plan,
                spatial.Towers,
                plan.TowerHeight);
            CastleTowerPlacementSpec[] inner = CloneWithSlits(
                in plan,
                spatial.InnerTowers,
                CastleInnerWardTowerPlanner.Height(in plan));

            CastleTopologyPlan topology = spatial.Topology;
            CastleGatePlacementSpec primaryGate = spatial.PrimaryGate;
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
                outer,
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
                spatial.CaveDecoration != null ? spatial.CaveDecoration.Snapshot() : null,
                spatial.KeepWindows != null
                    ? (CastleKeepWindowSpec[])spatial.KeepWindows.Clone()
                    : Array.Empty<CastleKeepWindowSpec>(),
                inner);
        }

        private static CastleTowerPlacementSpec[] CloneWithSlits(
            in CastlePlan plan,
            CastleTowerPlacementSpec[] towers,
            int baseHeight)
        {
            if (towers == null || towers.Length == 0)
                return Array.Empty<CastleTowerPlacementSpec>();

            var completed = (CastleTowerPlacementSpec[])towers.Clone();
            for (int i = 0; i < completed.Length; i++)
            {
                int height = baseHeight + math.max(0, completed[i].HeightVariation);
                int2 worldCentre = new int2(
                    plan.Centre.x + completed[i].Centre.x,
                    plan.Centre.z + completed[i].Centre.y);
                completed[i].Slits = CastleTowerSlitPlanner.Create(
                    worldCentre,
                    height,
                    plan.FloorHeight);
            }
            return completed;
        }
    }
}

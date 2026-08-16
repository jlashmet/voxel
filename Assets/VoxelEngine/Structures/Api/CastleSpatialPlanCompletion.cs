using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Final pure-data completion for castle details that depend on already-resolved core geometry.
    /// Composition calls this after terrain-dependent keep placement is finished; Runtime receives
    /// courtyard buildings and the designed dungeon graph without choosing either layout itself.
    /// </summary>
    public static class CastleSpatialPlanCompletion
    {
        public static CastleSpatialPlan CompleteResolved(
            in CastlePlan plan,
            CastleSpatialPlan spatial)
        {
            if (spatial == null) throw new ArgumentNullException(nameof(spatial));
            if (spatial.KeepRequiresTerrainResolution)
                return spatial;

            CastleSpatialPlan withBuildings = AttachCourtyardBuildings(in plan, spatial);
            return AttachDungeon(in plan, withBuildings);
        }

        public static CastleSpatialPlan AttachCourtyardBuildings(
            in CastlePlan plan,
            CastleSpatialPlan spatial)
        {
            if (spatial == null) throw new ArgumentNullException(nameof(spatial));
            if (spatial.KeepRequiresTerrainResolution)
                return spatial;

            CastleCourtyardBuildingSpec[] buildings =
                CastleCourtyardBuildingPlanner.Create(in plan, spatial);
            return Copy(spatial, buildings, spatial.Dungeon);
        }

        public static CastleSpatialPlan AttachDungeon(
            in CastlePlan plan,
            CastleSpatialPlan spatial)
        {
            if (spatial == null) throw new ArgumentNullException(nameof(spatial));
            if (spatial.KeepRequiresTerrainResolution)
                return spatial;

            CastleSpatialProjection projection = CastleSpatialProjection.Create(in plan, spatial);
            DungeonPlan dungeon = CastleDungeonPlanning.Create(in plan, in projection);
            return Copy(spatial, spatial.CourtyardBuildings, dungeon);
        }

        private static CastleSpatialPlan Copy(
            CastleSpatialPlan spatial,
            CastleCourtyardBuildingSpec[] buildings,
            DungeonPlan dungeon)
        {
            CastleTopologyPlan topology = spatial.Topology;
            CastleGatePlacementSpec primaryGate = spatial.PrimaryGate;
            CastleGatePlacementSpec posternGate = spatial.PosternGate;
            CastleGatePlacementSpec innerGate = spatial.InnerGate;

            return new CastleSpatialPlan(
                in topology,
                (int2[])spatial.OuterWardVertices.Clone(),
                (int2[])spatial.InnerWardVertices.Clone(),
                (CastleTowerPlacementSpec[])spatial.Towers.Clone(),
                in primaryGate,
                spatial.HasPosternGate,
                in posternGate,
                spatial.HasInnerGate,
                in innerGate,
                spatial.HasWell,
                spatial.WellCentre,
                buildings != null
                    ? (CastleCourtyardBuildingSpec[])buildings.Clone()
                    : Array.Empty<CastleCourtyardBuildingSpec>(),
                dungeon,
                spatial.KeepCentre,
                false);
        }
    }
}

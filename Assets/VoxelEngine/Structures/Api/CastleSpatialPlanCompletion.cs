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
            CastleSpatialPlan completed = AttachDungeon(in plan, withBuildings);
            RequireCompleted(in plan, completed);
            return completed;
        }

        public static CastleSpatialPlan AttachCourtyardBuildings(
            in CastlePlan plan,
            CastleSpatialPlan spatial)
        {
            if (spatial == null) throw new ArgumentNullException(nameof(spatial));
            if (spatial.KeepRequiresTerrainResolution)
                return spatial;

            CastleGatePlacementSpec primaryGate = spatial.PrimaryGate;
            CastleGatePlacementSpec posternGate = spatial.PosternGate;
            CastleGatePlacementSpec innerGate = spatial.InnerGate;
            CastleCourtyardBuildingSpec[] buildings =
                CastleCourtyardBuildingPlacementGeometry.Plan(
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
            if (!DungeonPlanValidator.TryValidate(dungeon, out DungeonPlanIssue issue))
            {
                throw new InvalidOperationException(
                    $"Castle dungeon completion produced an invalid plan: {issue}.");
            }

            return Copy(spatial, spatial.CourtyardBuildings, dungeon);
        }

        private static void RequireCompleted(
            in CastlePlan plan,
            CastleSpatialPlan completed)
        {
            if (!CastleSpatialPlanValidator.TryValidate(
                    in plan, completed, out CastleSpatialPlanIssue spatialIssue))
            {
                throw new InvalidOperationException(
                    $"Completed castle spatial plan is structurally invalid: {spatialIssue}.");
            }

            if (completed.Dungeon == null)
                throw new InvalidOperationException("Completed castle spatial plan has no dungeon plan.");

            if (!DungeonPlanValidator.TryValidate(
                    completed.Dungeon, out DungeonPlanIssue dungeonIssue))
            {
                throw new InvalidOperationException(
                    $"Completed castle dungeon plan is structurally invalid: {dungeonIssue}.");
            }

            CastleSpatialProjection projection = CastleSpatialProjection.Create(in plan, completed);
            if (!completed.Dungeon.Entrance.Equals(projection.TrapdoorCentre))
            {
                throw new InvalidOperationException(
                    "Completed castle dungeon entrance does not align with the projected trapdoor.");
            }
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

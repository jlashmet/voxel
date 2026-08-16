using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Final pure-data completion for castle details that depend on already-resolved core geometry.
    /// Composition calls this after terrain-dependent keep placement is finished; Runtime receives
    /// tower variation, keep-floor semantics, keep circulation, courtyard buildings, the designed
    /// dungeon graph, and natural cave topology without choosing authored details itself.
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

            CastleSpatialPlan withTowerVariation = AttachTowerVariation(in plan, spatial);
            CastleSpatialPlan withKeepFloors = AttachKeepFloors(in plan, withTowerVariation);
            CastleSpatialPlan withCirculation = AttachKeepCirculation(in plan, withKeepFloors);
            CastleSpatialPlan withBuildings = AttachCourtyardBuildings(in plan, withCirculation);
            CastleSpatialPlan withDungeon = AttachDungeon(in plan, withBuildings);
            CastleSpatialPlan completed = AttachCave(in plan, withDungeon);
            RequireCompleted(in plan, completed);
            return completed;
        }

        /// <summary>
        /// Freezes the historical tower height/roof variation into the plan. These use the exact
        /// seed streams previously consumed by the outer and inner tower realizers so moving the
        /// decisions into planning does not perturb existing castle appearance.
        /// </summary>
        public static CastleSpatialPlan AttachTowerVariation(
            in CastlePlan plan,
            CastleSpatialPlan spatial)
        {
            if (spatial == null) throw new ArgumentNullException(nameof(spatial));

            CastleTowerPlacementSpec[] towers = spatial.Towers != null
                ? (CastleTowerPlacementSpec[])spatial.Towers.Clone()
                : Array.Empty<CastleTowerPlacementSpec>();
            for (int i = 0; i < towers.Length; i++)
            {
                uint variationSeed = CastleSeedPartition.Derive(
                    plan.Seed,
                    CastleSeedDomain.Walls,
                    (uint)(0x2000 + towers[i].Id));
                towers[i].HeightVariation = 8 + (int)(variationSeed % 51u);
                towers[i].HasRoof = towers[i].Role == CastleTowerPlacementRole.Corner
                                 && ((variationSeed >> 8) & 1u) != 0u;
            }

            CastleSpatialPlan varied = Copy(
                spatial,
                towers,
                spatial.KeepFloors,
                spatial.CourtyardBuildings,
                spatial.Dungeon,
                spatial.Cave);

            CastleTowerPlacementSpec[] innerTowers = varied.InnerTowers;
            for (int i = 0; i < innerTowers.Length; i++)
            {
                uint variationSeed = CastleSeedPartition.Derive(
                    plan.Seed,
                    CastleSeedDomain.Walls,
                    (uint)(0x2A00 + innerTowers[i].Id));
                innerTowers[i].HeightVariation = 0;
                innerTowers[i].HasRoof = (variationSeed & 1u) != 0u;
            }

            return varied;
        }

        /// <summary>
        /// Freezes semantic keep-floor purposes before Runtime sees the castle. The current planner
        /// preserves the historical room recipe, but realization no longer infers purpose from a
        /// floor-number switch and future room variation can remain entirely planning-side.
        /// </summary>
        public static CastleSpatialPlan AttachKeepFloors(
            in CastlePlan plan,
            CastleSpatialPlan spatial)
        {
            if (spatial == null) throw new ArgumentNullException(nameof(spatial));
            if (spatial.KeepRequiresTerrainResolution)
                return spatial;

            CastleKeepFloorPlan[] floors =
                CastleKeepInteriorPlanner.Create(in plan).SnapshotFloors();
            return Copy(
                spatial,
                spatial.Towers,
                floors,
                spatial.CourtyardBuildings,
                spatial.Dungeon,
                spatial.Cave);
        }

        /// <summary>
        /// Freezes the keep entrance and vertical-circulation anchors in semantic keep-local
        /// coordinates. Runtime consumes this data instead of choosing stair locations itself.
        /// </summary>
        public static CastleSpatialPlan AttachKeepCirculation(
            in CastlePlan plan,
            CastleSpatialPlan spatial)
        {
            if (spatial == null) throw new ArgumentNullException(nameof(spatial));
            if (spatial.KeepRequiresTerrainResolution)
                return spatial;

            CastleKeepCirculationPlan circulation = CastleKeepCirculationPlanner.Create(in plan);
            return Copy(
                spatial,
                spatial.Towers,
                spatial.KeepFloors,
                spatial.CourtyardBuildings,
                spatial.Dungeon,
                spatial.Cave,
                circulation);
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
            return Copy(
                spatial,
                spatial.Towers,
                spatial.KeepFloors,
                buildings,
                spatial.Dungeon,
                spatial.Cave);
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

            // A cave is anchored to a particular dungeon threshold. Replacing the designed
            // dungeon necessarily invalidates any previously attached natural cave.
            return Copy(
                spatial,
                spatial.Towers,
                spatial.KeepFloors,
                spatial.CourtyardBuildings,
                dungeon,
                null);
        }

        public static CastleSpatialPlan AttachCave(
            in CastlePlan plan,
            CastleSpatialPlan spatial)
        {
            if (spatial == null) throw new ArgumentNullException(nameof(spatial));
            if (spatial.KeepRequiresTerrainResolution)
                return spatial;
            if (spatial.Dungeon == null)
                throw new InvalidOperationException("Castle cave completion requires a designed dungeon plan.");

            CavePlan cave = spatial.Dungeon.HasCaveExit
                ? CastleCavePlanning.Create(in plan, spatial.Dungeon)
                : null;
            if (cave != null && !CavePlanValidator.TryValidate(cave, out CavePlanIssue issue))
            {
                throw new InvalidOperationException(
                    $"Castle cave completion produced an invalid plan: {issue}.");
            }

            return Copy(
                spatial,
                spatial.Towers,
                spatial.KeepFloors,
                spatial.CourtyardBuildings,
                spatial.Dungeon,
                cave);
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

            CastleKeepFloorPlan[] floors = completed.KeepFloors;
            if (floors == null || floors.Length != plan.Floors)
                throw new InvalidOperationException("Completed castle has no complete keep-floor plan.");
            for (int i = 0; i < floors.Length; i++)
            {
                if (floors[i].FloorIndex != i)
                {
                    throw new InvalidOperationException(
                        $"Completed castle keep-floor plan is out of order at floor {i}.");
                }
            }

            CastleKeepCirculationPlan circulation = completed.KeepCirculation;
            if (!CastleKeepCirculationPlanner.TryValidate(
                    in plan, in circulation, out CastleKeepCirculationPlanIssue circulationIssue))
            {
                throw new InvalidOperationException(
                    $"Completed castle keep circulation is invalid: {circulationIssue}.");
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

            if (!completed.Dungeon.HasCaveExit)
            {
                if (completed.Cave != null)
                    throw new InvalidOperationException(
                        "Completed castle has a natural cave but its dungeon has no cave threshold.");
                return;
            }

            if (completed.Cave == null)
                throw new InvalidOperationException("Completed castle dungeon has no natural cave plan.");
            if (!CavePlanValidator.TryValidate(completed.Cave, out CavePlanIssue caveIssue))
            {
                throw new InvalidOperationException(
                    $"Completed castle cave plan is structurally invalid: {caveIssue}.");
            }

            DungeonRoomPlan threshold = completed.Dungeon.Rooms[completed.Dungeon.CaveThresholdRoomId];
            int3 caveEntrance = new int3(
                threshold.Centre.x,
                threshold.Centre.y - threshold.Size.y / 2,
                threshold.Centre.z);
            if (!completed.Cave.Entrance.Equals(caveEntrance))
            {
                throw new InvalidOperationException(
                    "Completed natural cave entrance does not align with the dungeon cave threshold.");
            }
        }

        private static CastleSpatialPlan Copy(
            CastleSpatialPlan spatial,
            CastleTowerPlacementSpec[] towers,
            CastleKeepFloorPlan[] keepFloors,
            CastleCourtyardBuildingSpec[] buildings,
            DungeonPlan dungeon,
            CavePlan cave,
            CastleKeepCirculationPlan? keepCirculation = null)
        {
            CastleTopologyPlan topology = spatial.Topology;
            CastleGatePlacementSpec primaryGate = spatial.PrimaryGate;
            CastleGatePlacementSpec posternGate = spatial.PosternGate;
            CastleGatePlacementSpec innerGate = spatial.InnerGate;

            var copy = new CastleSpatialPlan(
                in topology,
                (int2[])spatial.OuterWardVertices.Clone(),
                (int2[])spatial.InnerWardVertices.Clone(),
                towers != null
                    ? (CastleTowerPlacementSpec[])towers.Clone()
                    : Array.Empty<CastleTowerPlacementSpec>(),
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
                keepFloors != null
                    ? (CastleKeepFloorPlan[])keepFloors.Clone()
                    : Array.Empty<CastleKeepFloorPlan>(),
                keepCirculation ?? spatial.KeepCirculation,
                dungeon,
                cave,
                spatial.KeepCentre,
                false);

            CastleTowerPlacementSpec[] sourceInnerTowers = spatial.InnerTowers;
            CastleTowerPlacementSpec[] targetInnerTowers = copy.InnerTowers;
            if (sourceInnerTowers != null && sourceInnerTowers.Length == targetInnerTowers.Length)
                Array.Copy(sourceInnerTowers, targetInnerTowers, sourceInnerTowers.Length);

            return copy;
        }
    }
}

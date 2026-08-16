using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    public enum CastleBuildPreflightIssue : byte
    {
        None = 0,
        InvalidPlan,
        WriteBudgetExceeded,
        InvalidSpatialPlan,
        IncompleteSpatialPlan,
    }

    public enum CastleSpatialBuildReadinessIssue : byte
    {
        None = 0,
        KeepRequiresTerrainResolution,
        MissingDungeonPlan,
        InvalidDungeonPlan,
        DungeonEntranceMismatch,
    }

    /// <summary>Pure result of checking whether a castle plan is safe to realize.</summary>
    public readonly struct CastleBuildPreflightResult
    {
        public readonly CastleBuildPreflightIssue Issue;
        public readonly CastlePlanIssue PlanIssue;
        public readonly CastleSpatialPlanIssue SpatialPlanIssue;
        public readonly CastleSpatialBuildReadinessIssue ReadinessIssue;
        public readonly long EstimatedWrites;
        public readonly long WriteBudget;

        public CastleBuildPreflightResult(
            CastleBuildPreflightIssue issue,
            CastlePlanIssue planIssue,
            long estimatedWrites,
            long writeBudget)
            : this(
                issue,
                planIssue,
                CastleSpatialPlanIssue.None,
                CastleSpatialBuildReadinessIssue.None,
                estimatedWrites,
                writeBudget)
        {
        }

        public CastleBuildPreflightResult(
            CastleBuildPreflightIssue issue,
            CastlePlanIssue planIssue,
            CastleSpatialPlanIssue spatialPlanIssue,
            long estimatedWrites,
            long writeBudget)
            : this(
                issue,
                planIssue,
                spatialPlanIssue,
                CastleSpatialBuildReadinessIssue.None,
                estimatedWrites,
                writeBudget)
        {
        }

        public CastleBuildPreflightResult(
            CastleBuildPreflightIssue issue,
            CastlePlanIssue planIssue,
            CastleSpatialPlanIssue spatialPlanIssue,
            CastleSpatialBuildReadinessIssue readinessIssue,
            long estimatedWrites,
            long writeBudget)
        {
            Issue = issue;
            PlanIssue = planIssue;
            SpatialPlanIssue = spatialPlanIssue;
            ReadinessIssue = readinessIssue;
            EstimatedWrites = estimatedWrites;
            WriteBudget = writeBudget;
        }

        public bool IsValid => Issue == CastleBuildPreflightIssue.None;
    }

    /// <summary>
    /// Runtime-independent castle preflight. Planning policy owns the estimate; voxel realization
    /// owns the actual writes and remains protected by its hard brush budget as a second guard.
    /// </summary>
    public static class CastleBuildPreflight
    {
        private const double LegacyUndergroundEstimate = 1_500_000.0;

        /// <summary>
        /// Historical rectangular estimate retained byte-for-byte for compatibility callers.
        /// Spatial builds should use the overload that accepts CastleSpatialPlan.
        /// </summary>
        public static long EstimateWrites(in CastlePlan plan)
        {
            double plateauArea = math.PI_DBL * plan.PlateauRadius * plan.PlateauRadius;
            double siteCap = plateauArea * 3.0;

            double cliffArea = math.PI_DBL *
                ((plan.PlateauRadius + plan.CliffDrop) * (double)(plan.PlateauRadius + plan.CliffDrop)
                 - plan.PlateauRadius * (double)plan.PlateauRadius);
            double cliffCap = cliffArea * 4.0;

            double perimeter = 4.0 * (plan.BaileyHalfX + plan.BaileyHalfZ);
            double walls = perimeter * 240.0;
            double towers = 6.0 * math.PI_DBL * plan.TowerRadius * plan.TowerRadius * 30.0;
            double keep = plan.KeepHalfX * (double)plan.KeepHalfZ * plan.Floors * 4.0;
            double courtyard = plateauArea * 0.2;
            double underground = LegacyUndergroundEstimate;

            return (long)(siteCap + cliffCap + walls + towers + keep + courtyard + underground);
        }

        /// <summary>
        /// Estimates the realized topology rather than the legacy rectangular recipe. The result
        /// is an expensive-write equivalent used only as a conservative admission budget; bulk
        /// realization still enforces its hard brush budget independently.
        /// </summary>
        public static long EstimateWrites(in CastlePlan plan, CastleSpatialPlan spatialPlan)
        {
            if (spatialPlan == null) throw new ArgumentNullException(nameof(spatialPlan));

            double plateauArea = math.PI_DBL * plan.PlateauRadius * plan.PlateauRadius;
            double siteCap = plateauArea * 3.0;

            double outerRadius = plan.PlateauRadius + plan.CliffDrop;
            double cliffArea = math.PI_DBL *
                (outerRadius * outerRadius - plan.PlateauRadius * (double)plan.PlateauRadius);
            double cliffCap = cliffArea * 4.0;

            double perimeter = PolygonPerimeter(spatialPlan.OuterWardVertices)
                             + PolygonPerimeter(spatialPlan.InnerWardVertices);
            double walls = perimeter * 240.0;

            int towerCount = spatialPlan.Towers != null ? spatialPlan.Towers.Length : 0;
            double towers = towerCount * math.PI_DBL * plan.TowerRadius * plan.TowerRadius * 30.0;
            CastleTowerPlacementSpec[] innerTowerSpecs = spatialPlan.InnerTowers;
            int innerTowerRadius = CastleInnerWardTowerPlanner.Radius(in plan);
            double innerTowers = innerTowerSpecs.Length * math.PI_DBL
                               * innerTowerRadius * innerTowerRadius * 30.0;
            double gatehouseTowers = 2.0 * math.PI_DBL
                                   * plan.GateTowerRadius * plan.GateTowerRadius * 30.0;

            double keep = plan.KeepHalfX * (double)plan.KeepHalfZ * plan.Floors * 4.0;
            double courtyard = PolygonArea(spatialPlan.OuterWardVertices) * 0.2;
            double courtyardBuildings = CourtyardBuildingCost(spatialPlan.CourtyardBuildings);
            double underground = DungeonCost(spatialPlan.Dungeon);

            double primaryGateLeaf = CastleLayout.FrontGateWidth
                                   * (double)CastleLayout.FrontGateHeight
                                   * CastleLayout.FrontGateDepth;
            double posternLeaf = spatialPlan.HasPosternGate
                ? CastleLayout.PosternGateWidth * (double)CastleLayout.PosternGateHeight
                  * CastleLayout.PosternGateDepth
                : 0.0;

            return (long)(siteCap + cliffCap + walls + towers + innerTowers + gatehouseTowers + keep
                        + courtyard + courtyardBuildings + underground
                        + primaryGateLeaf + posternLeaf);
        }

        public static CastleBuildPreflightResult Evaluate(in CastlePlan plan, long writeBudget)
        {
            if (!CastlePlanValidator.TryValidate(in plan, out CastlePlanIssue planIssue))
            {
                return new CastleBuildPreflightResult(
                    CastleBuildPreflightIssue.InvalidPlan,
                    planIssue,
                    0,
                    writeBudget);
            }

            long estimate = EstimateWrites(in plan);
            return BudgetResult(estimate, writeBudget);
        }

        public static CastleBuildPreflightResult Evaluate(
            in CastlePlan plan,
            CastleSpatialPlan spatialPlan,
            long writeBudget)
        {
            if (!CastlePlanValidator.TryValidate(in plan, out CastlePlanIssue planIssue))
            {
                return new CastleBuildPreflightResult(
                    CastleBuildPreflightIssue.InvalidPlan,
                    planIssue,
                    CastleSpatialPlanIssue.None,
                    0,
                    writeBudget);
            }

            if (spatialPlan == null)
            {
                return new CastleBuildPreflightResult(
                    CastleBuildPreflightIssue.InvalidSpatialPlan,
                    CastlePlanIssue.None,
                    CastleSpatialPlanIssue.MissingOuterWard,
                    0,
                    writeBudget);
            }

            if (!CastleSpatialPlanValidator.TryValidate(
                    in plan, spatialPlan, out CastleSpatialPlanIssue spatialIssue))
            {
                return new CastleBuildPreflightResult(
                    CastleBuildPreflightIssue.InvalidSpatialPlan,
                    CastlePlanIssue.None,
                    spatialIssue,
                    0,
                    writeBudget);
            }

            long estimate = EstimateWrites(in plan, spatialPlan);
            return BudgetResult(estimate, writeBudget);
        }

        /// <summary>
        /// Admission check used by Runtime. Unlike the general spatial evaluation above, this
        /// requires site-aware planning completion: the keep must be resolved and the designed
        /// dungeon graph must be attached, valid, and anchored to the projected trapdoor.
        /// </summary>
        public static CastleBuildPreflightResult EvaluateRuntimeReady(
            in CastlePlan plan,
            CastleSpatialPlan spatialPlan,
            long writeBudget)
        {
            CastleBuildPreflightResult structural = Evaluate(in plan, spatialPlan, writeBudget);
            if (!structural.IsValid)
                return structural;

            if (spatialPlan.KeepRequiresTerrainResolution)
            {
                return ReadinessFailure(
                    CastleSpatialBuildReadinessIssue.KeepRequiresTerrainResolution,
                    writeBudget);
            }

            DungeonPlan dungeon = spatialPlan.Dungeon;
            if (dungeon == null)
            {
                return ReadinessFailure(
                    CastleSpatialBuildReadinessIssue.MissingDungeonPlan,
                    writeBudget);
            }

            if (!DungeonPlanValidator.TryValidate(dungeon, out _))
            {
                return ReadinessFailure(
                    CastleSpatialBuildReadinessIssue.InvalidDungeonPlan,
                    writeBudget);
            }

            CastleSpatialProjection projection = CastleSpatialProjection.Create(
                in plan, spatialPlan);
            if (!dungeon.Entrance.Equals(projection.TrapdoorCentre))
            {
                return ReadinessFailure(
                    CastleSpatialBuildReadinessIssue.DungeonEntranceMismatch,
                    writeBudget);
            }

            return structural;
        }

        private static CastleBuildPreflightResult ReadinessFailure(
            CastleSpatialBuildReadinessIssue readinessIssue,
            long writeBudget) =>
            new CastleBuildPreflightResult(
                CastleBuildPreflightIssue.IncompleteSpatialPlan,
                CastlePlanIssue.None,
                CastleSpatialPlanIssue.None,
                readinessIssue,
                0,
                writeBudget);

        private static CastleBuildPreflightResult BudgetResult(long estimate, long writeBudget)
        {
            if (estimate > writeBudget)
            {
                return new CastleBuildPreflightResult(
                    CastleBuildPreflightIssue.WriteBudgetExceeded,
                    CastlePlanIssue.None,
                    CastleSpatialPlanIssue.None,
                    estimate,
                    writeBudget);
            }

            return new CastleBuildPreflightResult(
                CastleBuildPreflightIssue.None,
                CastlePlanIssue.None,
                CastleSpatialPlanIssue.None,
                estimate,
                writeBudget);
        }

        private static double DungeonCost(DungeonPlan dungeon)
        {
            if (dungeon == null)
                return LegacyUndergroundEstimate;
            if (!DungeonPlanValidator.TryValidate(dungeon, out _))
                return LegacyUndergroundEstimate;

            DungeonRoomPlan[] rooms = dungeon.Rooms;
            double roomCost = 0.0;
            for (int i = 0; i < rooms.Length; i++)
            {
                int sx = math.max(0, rooms[i].Size.x);
                int sy = math.max(0, rooms[i].Size.y);
                int sz = math.max(0, rooms[i].Size.z);
                double volume = sx * (double)sy * sz;
                double floorArea = sx * (double)sz;

                // Room excavation is emitted through bulk primitives, while floor skins and later
                // semantic furnishing still carry per-voxel work. Price a conservative fraction
                // of the excavated volume plus several floor-area equivalents so larger planned
                // chambers increase admission cost without pretending every bulk-cleared voxel is
                // a slow-path write.
                roomCost += volume * 0.20 + floorArea * 4.0;
            }

            double connectionCost = 0.0;
            DungeonConnectionPlan[] connections = dungeon.Connections;
            for (int i = 0; i < connections.Length; i++)
            {
                DungeonConnectionPlan connection = connections[i];
                DungeonRoomPlan from = rooms[connection.FromRoomId];
                DungeonRoomPlan to = rooms[connection.ToRoomId];
                int3 delta = math.abs(to.Centre - from.Centre);

                if (connection.Kind == DungeonConnectionKind.Stair)
                {
                    // Spiral stairs include authored solid steps inside a cleared shaft, so the
                    // vertical run is intentionally priced more heavily than a bulk corridor.
                    connectionCost += delta.y * 1_000.0;
                    connectionCost += (delta.x + delta.z) * 20.0 * 30.0 * 0.18;
                    continue;
                }

                int width = connection.Kind == DungeonConnectionKind.SecretPassage ? 28 : 20;
                int height = connection.Kind == DungeonConnectionKind.SecretPassage ? 32 : 30;
                int horizontalLength = delta.x + delta.z;
                connectionCost += horizontalLength * (double)width * height * 0.18;
            }

            // Natural cave topology deliberately remains outside DungeonPlan, but a planned cave
            // threshold means CastleCaveRealizer will still author that continuation in stage 7.
            double caveCost = dungeon.HasCaveExit ? 400_000.0 : 0.0;
            return roomCost + connectionCost + caveCost;
        }

        private static double CourtyardBuildingCost(CastleCourtyardBuildingSpec[] buildings)
        {
            if (buildings == null) return 0.0;

            double cost = 0.0;
            for (int i = 0; i < buildings.Length; i++)
            {
                int width = math.max(0, buildings[i].Width);
                int depth = math.max(0, buildings[i].Depth);
                int height = math.max(0, buildings[i].Height);

                // Five-voxel masonry shell plus a six-layer-equivalent roof skin. This is
                // intentionally conservative; admission should grow whenever the planner adds
                // courtyard structures even though most shell work is later executed in bulk.
                cost += 2.0 * (width + depth) * height * 5.0;
                cost += width * (double)depth * 6.0;
            }
            return cost;
        }

        private static double PolygonPerimeter(int2[] polygon)
        {
            if (polygon == null || polygon.Length < 2) return 0.0;

            double perimeter = 0.0;
            for (int i = 0; i < polygon.Length; i++)
            {
                int2 a = polygon[i];
                int2 b = polygon[(i + 1) % polygon.Length];
                long dx = (long)b.x - a.x;
                long dz = (long)b.y - a.y;
                perimeter += Math.Sqrt(dx * (double)dx + dz * (double)dz);
            }
            return perimeter;
        }

        private static double PolygonArea(int2[] polygon)
        {
            if (polygon == null || polygon.Length < 3) return 0.0;

            long signedAreaTwice = 0;
            for (int i = 0; i < polygon.Length; i++)
            {
                int2 a = polygon[i];
                int2 b = polygon[(i + 1) % polygon.Length];
                signedAreaTwice += (long)a.x * b.y - (long)b.x * a.y;
            }

            double magnitude = signedAreaTwice < 0
                ? -(double)signedAreaTwice
                : signedAreaTwice;
            return magnitude * 0.5;
        }
    }
}

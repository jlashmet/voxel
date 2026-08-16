using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Creates a detached castle spatial-plan copy at trust boundaries. Planning models expose
    /// mutable arrays for lightweight composition/tests, so Runtime must not validate one caller
    /// object and then later copy from storage that the caller can still mutate.
    /// </summary>
    public static class CastleSpatialPlanSnapshot
    {
        public static CastleSpatialPlan CloneDetached(CastleSpatialPlan source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            CastleTopologyPlan topology = source.Topology;
            CastleGatePlacementSpec primaryGate = source.PrimaryGate;
            CastleGatePlacementSpec posternGate = source.PosternGate;
            CastleGatePlacementSpec innerGate = source.InnerGate;

            DungeonPlan dungeon = CloneDungeon(source.Dungeon);
            CavePlan cave = source.Cave?.Snapshot();
            CastleCaveDecorationPlan caveDecoration = source.CaveDecoration?.Snapshot();
            CastleLandscapePlan landscape = CloneLandscape(source.Landscape);

            var clone = new CastleSpatialPlan(
                in topology,
                Clone(source.OuterWardVertices),
                Clone(source.InnerWardVertices),
                Clone(source.Towers),
                in primaryGate,
                source.HasPosternGate,
                in posternGate,
                source.HasInnerGate,
                in innerGate,
                source.HasWell,
                source.WellCentre,
                Clone(source.CourtyardBuildings),
                Clone(source.KeepFloors),
                source.KeepCirculation,
                dungeon,
                cave,
                landscape,
                source.KeepCentre,
                source.KeepRequiresTerrainResolution,
                caveDecoration,
                Clone(source.KeepWindows));

            // The CastleSpatialPlan constructor derives inner tower identities from the cloned
            // ring. Preserve any planner-owned variation fields from the source without retaining
            // the source array itself.
            CastleTowerPlacementSpec[] sourceInner = source.InnerTowers;
            CastleTowerPlacementSpec[] targetInner = clone.InnerTowers;
            if (sourceInner != null && targetInner != null &&
                sourceInner.Length == targetInner.Length)
            {
                Array.Copy(sourceInner, targetInner, sourceInner.Length);
            }

            return clone;
        }

        public static CastleSpatialPlan CloneRuntimeReady(
            in CastlePlan dimensions,
            CastleSpatialPlan source)
        {
            CastleSpatialPlan clone = CloneDetached(source);
            if (!CastleSpatialPlanValidator.TryValidate(
                    in dimensions, clone, out CastleSpatialPlanIssue spatialIssue))
            {
                throw new InvalidOperationException(
                    $"Cannot snapshot invalid castle spatial plan: {spatialIssue}.");
            }

            if (!CastleSpatialBuildReadiness.TryValidate(
                    in dimensions, clone, out CastleSpatialBuildReadinessIssue readinessIssue))
            {
                throw new InvalidOperationException(
                    $"Cannot snapshot castle spatial plan that is not runtime-ready: {readinessIssue}.");
            }

            return clone;
        }

        private static DungeonPlan CloneDungeon(DungeonPlan source)
        {
            if (source == null) return null;
            return new DungeonPlan(
                source.Seed,
                source.Entrance,
                Clone(source.Rooms),
                Clone(source.Connections),
                source.EntranceRoomId,
                source.CaveThresholdRoomId);
        }

        private static CastleLandscapePlan CloneLandscape(CastleLandscapePlan source)
        {
            if (source == null) return null;
            return new CastleLandscapePlan(Clone(source.Decorations));
        }

        private static T[] Clone<T>(T[] source) =>
            source != null ? (T[])source.Clone() : null;
    }
}

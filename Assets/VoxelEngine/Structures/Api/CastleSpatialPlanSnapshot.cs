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
            if (topology.KeepTurrets != null)
            {
                topology.KeepTurrets = new CastleKeepTurretPlan(
                    topology.KeepTurrets.Snapshot());
            }

            CastleGatePlacementSpec primaryGate = source.PrimaryGate;
            CastleGatePlacementSpec posternGate = source.PosternGate;
            CastleGatePlacementSpec innerGate = source.InnerGate;

            DungeonPlan dungeon = CloneDungeon(source.Dungeon);
            CavePlan cave = source.Cave?.Snapshot();
            CastleCaveDecorationPlan caveDecoration = source.CaveDecoration?.Snapshot();
            CastleLandscapePlan landscape = CloneLandscape(source.Landscape);

            return new CastleSpatialPlan(
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
                CloneKeepFloors(source.KeepFloors),
                source.KeepCirculation,
                dungeon,
                cave,
                landscape,
                source.KeepCentre,
                source.KeepRequiresTerrainResolution,
                caveDecoration,
                Clone(source.KeepWindows),
                Clone(source.InnerTowers));
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

        private static CastleKeepFloorPlan[] CloneKeepFloors(CastleKeepFloorPlan[] source)
        {
            if (source == null) return null;

            var clone = new CastleKeepFloorPlan[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                CastleKeepFloorPlan floor = source[i];
                CastleRoomAccentPlan accents = floor.Accents != null
                    ? new CastleRoomAccentPlan(floor.Accents.Snapshot())
                    : null;
                clone[i] = new CastleKeepFloorPlan(
                    floor.FloorIndex,
                    floor.Purpose,
                    floor.HasPartition,
                    floor.SemanticSeed,
                    accents);
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

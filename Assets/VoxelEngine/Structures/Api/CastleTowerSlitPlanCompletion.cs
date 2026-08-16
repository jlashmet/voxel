using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    public enum CastleTowerSlitBuildReadinessIssue : byte
    {
        None,
        MissingOuterTowerPlan,
        MissingInnerTowerPlan,
        MissingSlitPlan,
        InvalidSlitPlan,
    }

    /// <summary>
    /// Freezes per-storey arrow-slit phases after tower placement and height variation are final.
    /// Runtime receives immutable slit phases and never rolls tower appearance from a seed.
    /// </summary>
    public static class CastleTowerSlitPlanCompletion
    {
        public static CastleSpatialPlan Attach(
            in CastlePlan plan,
            CastleSpatialPlan spatial)
        {
            if (spatial == null) throw new ArgumentNullException(nameof(spatial));

            CastleSpatialPlan completed = CastleSpatialPlanSnapshot.CloneDetached(spatial);
            AttachOuter(in plan, completed.Towers);
            AttachInner(in plan, completed.InnerTowers);

            if (!TryValidate(
                    in plan,
                    completed,
                    out CastleTowerSlitBuildReadinessIssue issue))
            {
                throw new InvalidOperationException(
                    $"Castle tower slit completion produced an invalid plan: {issue}.");
            }

            return completed;
        }

        public static bool TryValidate(
            in CastlePlan plan,
            CastleSpatialPlan spatial,
            out CastleTowerSlitBuildReadinessIssue issue)
        {
            if (spatial == null || spatial.Towers == null || spatial.Towers.Length == 0)
            {
                issue = CastleTowerSlitBuildReadinessIssue.MissingOuterTowerPlan;
                return false;
            }

            if (!TryValidateRing(
                    in plan,
                    spatial.Towers,
                    plan.TowerHeight,
                    out issue))
                return false;

            CastleTowerPlacementSpec[] inner = spatial.InnerTowers;
            bool expectsInner = spatial.Topology.Wards == CastleWardPattern.InnerAndOuterWards;
            if (expectsInner && (inner == null || inner.Length == 0))
            {
                issue = CastleTowerSlitBuildReadinessIssue.MissingInnerTowerPlan;
                return false;
            }

            if (inner != null && inner.Length != 0 &&
                !TryValidateRing(
                    in plan,
                    inner,
                    CastleInnerWardTowerPlanner.Height(in plan),
                    out issue))
                return false;

            issue = CastleTowerSlitBuildReadinessIssue.None;
            return true;
        }

        private static void AttachOuter(
            in CastlePlan plan,
            CastleTowerPlacementSpec[] towers)
        {
            if (towers == null) return;

            for (int i = 0; i < towers.Length; i++)
            {
                int height = plan.TowerHeight + math.max(0, towers[i].HeightVariation);
                int2 worldCentre = new int2(
                    plan.Centre.x + towers[i].Centre.x,
                    plan.Centre.z + towers[i].Centre.y);
                towers[i].Slits = CastleTowerSlitPlanner.Create(
                    worldCentre,
                    height,
                    plan.FloorHeight);
            }
        }

        private static void AttachInner(
            in CastlePlan plan,
            CastleTowerPlacementSpec[] towers)
        {
            if (towers == null) return;

            int baseHeight = CastleInnerWardTowerPlanner.Height(in plan);
            for (int i = 0; i < towers.Length; i++)
            {
                int height = baseHeight + math.max(0, towers[i].HeightVariation);
                int2 worldCentre = new int2(
                    plan.Centre.x + towers[i].Centre.x,
                    plan.Centre.z + towers[i].Centre.y);
                towers[i].Slits = CastleTowerSlitPlanner.Create(
                    worldCentre,
                    height,
                    plan.FloorHeight);
            }
        }

        private static bool TryValidateRing(
            in CastlePlan plan,
            CastleTowerPlacementSpec[] towers,
            int baseHeight,
            out CastleTowerSlitBuildReadinessIssue issue)
        {
            for (int i = 0; i < towers.Length; i++)
            {
                CastleTowerSlitPlan slits = towers[i].Slits;
                if (slits == null)
                {
                    issue = CastleTowerSlitBuildReadinessIssue.MissingSlitPlan;
                    return false;
                }

                int height = baseHeight + math.max(0, towers[i].HeightVariation);
                if (!CastleTowerSlitPlanValidator.TryValidate(
                        slits,
                        height,
                        plan.FloorHeight,
                        out _))
                {
                    issue = CastleTowerSlitBuildReadinessIssue.InvalidSlitPlan;
                    return false;
                }
            }

            issue = CastleTowerSlitBuildReadinessIssue.None;
            return true;
        }
    }
}

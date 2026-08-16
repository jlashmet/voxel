using System;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Semantic keep annex choices made before voxel realization. Geometry, dimensions, roofing,
    /// and furnishing remain downstream responsibilities.
    /// </summary>
    public readonly struct CastleKeepAnnexPlan
    {
        public readonly bool HasGreatHallWing;
        public readonly bool HasChapelWing;
        public readonly bool HasBellTower;
        public readonly bool HasRearOriel;

        public CastleKeepAnnexPlan(
            bool hasGreatHallWing,
            bool hasChapelWing,
            bool hasBellTower,
            bool hasRearOriel = true)
        {
            HasGreatHallWing = hasGreatHallWing;
            HasChapelWing = hasChapelWing;
            HasBellTower = hasBellTower;
            HasRearOriel = hasRearOriel;
        }
    }

    public enum CastleKeepAnnexPlanIssue : byte
    {
        None = 0,
        BellTowerWithoutChapel,
    }

    /// <summary>
    /// Freezes the current keep-annex recipe into explicit planning data. The initial planner is
    /// intentionally behavior-preserving: every castle receives the Great Hall wing, chapel wing,
    /// chapel bell tower, and rear timber oriel that Runtime historically built unconditionally.
    /// Future variation can change these choices without moving policy back into realization.
    /// </summary>
    public static class CastleKeepAnnexPlanner
    {
        /// <summary>
        /// Current behavior-preserving semantic recipe. This overload is used by topology planning,
        /// which deliberately runs before dimension/spatial realization.
        /// </summary>
        public static CastleKeepAnnexPlan Create() =>
            new CastleKeepAnnexPlan(
                hasGreatHallWing: true,
                hasChapelWing: true,
                hasBellTower: true,
                hasRearOriel: true);

        public static CastleKeepAnnexPlan Create(in CastlePlan plan)
        {
            if (plan.KeepHalfX <= 0 || plan.KeepHalfZ <= 0 || plan.KeepHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(plan), "Keep dimensions must be positive.");

            return Create();
        }
    }

    /// <summary>Pure structural validation for planned keep annex relationships.</summary>
    public static class CastleKeepAnnexPlanValidator
    {
        public static bool TryValidate(
            in CastleKeepAnnexPlan annexes,
            out CastleKeepAnnexPlanIssue issue)
        {
            if (annexes.HasBellTower && !annexes.HasChapelWing)
            {
                issue = CastleKeepAnnexPlanIssue.BellTowerWithoutChapel;
                return false;
            }

            issue = CastleKeepAnnexPlanIssue.None;
            return true;
        }

        public static void RequireValid(in CastleKeepAnnexPlan annexes)
        {
            if (TryValidate(in annexes, out CastleKeepAnnexPlanIssue issue))
                return;

            throw new InvalidOperationException($"Castle keep annex plan is invalid: {issue}.");
        }
    }
}

using System;
using Random = Unity.Mathematics.Random;

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
    /// Chooses semantic keep annexes before voxel realization. The parameterless/dimension
    /// overloads retain the historical all-annex recipe for compatibility callers; the seeded
    /// overload is the production topology planner and uses its own named keep substream so annex
    /// variation cannot perturb keep placement or any other castle planning decision.
    /// </summary>
    public static class CastleKeepAnnexPlanner
    {
        /// <summary>Historical compatibility recipe: every legacy annex is present.</summary>
        public static CastleKeepAnnexPlan Create() =>
            new CastleKeepAnnexPlan(
                hasGreatHallWing: true,
                hasChapelWing: true,
                hasBellTower: true,
                hasRearOriel: true);

        /// <summary>
        /// Deterministically chooses a varied annex combination for a semantic castle topology.
        /// At least one occupied/exterior annex is retained so the keep never degenerates to only
        /// its core block and roofline. Bell towers remain subordinate to a chapel.
        /// </summary>
        public static CastleKeepAnnexPlan Create(uint seed)
        {
            var rng = new Random(CastleSeedPartition.Derive(
                seed, CastleSeedDomain.Keep, 0xA66Eu));

            bool hasGreatHallWing = rng.NextInt(0, 100) < 72;
            bool hasChapelWing = rng.NextInt(0, 100) < 64;
            bool hasRearOriel = rng.NextInt(0, 100) < 58;

            // Keep a meaningful secondary volume even on the low-probability all-false draw.
            if (!hasGreatHallWing && !hasChapelWing && !hasRearOriel)
                hasGreatHallWing = true;

            bool hasBellTower = hasChapelWing && rng.NextInt(0, 100) < 58;
            return new CastleKeepAnnexPlan(
                hasGreatHallWing,
                hasChapelWing,
                hasBellTower,
                hasRearOriel);
        }

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

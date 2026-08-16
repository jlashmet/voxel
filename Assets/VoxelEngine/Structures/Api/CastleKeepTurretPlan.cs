using System;
using Random = Unity.Mathematics.Random;

namespace VoxelEngine.Structures.Api
{
    public enum CastleKeepTurretCorner : byte
    {
        MinXMinZ = 0,
        MaxXMinZ = 1,
        MinXMaxZ = 2,
        MaxXMaxZ = 3,
    }

    /// <summary>Topology-level roof composition for the four fixed keep-corner turrets.</summary>
    public enum CastleKeepTurretRoofPattern : byte
    {
        Historical,
        AllRoofed,
        MinZPair,
        MaxZPair,
        Diagonal,
        Bare,
    }

    public struct CastleKeepTurretSpec
    {
        public CastleKeepTurretCorner Corner;
        public bool HasRoof;
        public CastleTowerSlitPlan Slits;
    }

    /// <summary>
    /// Frozen authored variation for the four keep corner turrets. Their exact coordinates,
    /// radius, and height remain geometric consequences of CastlePlan; this plan owns the visual
    /// choices so Runtime does not invent turret styling while realizing the keep.
    /// </summary>
    public sealed class CastleKeepTurretPlan
    {
        private readonly CastleKeepTurretSpec[] _turrets;

        public CastleKeepTurretPlan(CastleKeepTurretSpec[] turrets)
        {
            _turrets = turrets != null
                ? (CastleKeepTurretSpec[])turrets.Clone()
                : Array.Empty<CastleKeepTurretSpec>();
        }

        public CastleKeepTurretSpec[] Snapshot() =>
            (CastleKeepTurretSpec[])_turrets.Clone();
    }

    public enum CastleKeepTurretPlanIssue : byte
    {
        None,
        MissingPlan,
        WrongTurretCount,
        InvalidCorner,
        DuplicateCorner,
        MissingSlitPlan,
        InvalidSlitPlan,
    }

    public static class CastleKeepTurretPlanValidator
    {
        public static bool TryValidate(
            CastleKeepTurretPlan plan,
            out CastleKeepTurretPlanIssue issue)
        {
            if (plan == null)
            {
                issue = CastleKeepTurretPlanIssue.MissingPlan;
                return false;
            }

            CastleKeepTurretSpec[] turrets = plan.Snapshot();
            if (turrets.Length != 4)
            {
                issue = CastleKeepTurretPlanIssue.WrongTurretCount;
                return false;
            }

            int seen = 0;
            for (int i = 0; i < turrets.Length; i++)
            {
                int corner = (int)turrets[i].Corner;
                if (corner < 0 || corner > 3)
                {
                    issue = CastleKeepTurretPlanIssue.InvalidCorner;
                    return false;
                }

                int bit = 1 << corner;
                if ((seen & bit) != 0)
                {
                    issue = CastleKeepTurretPlanIssue.DuplicateCorner;
                    return false;
                }
                seen |= bit;
            }

            issue = CastleKeepTurretPlanIssue.None;
            return true;
        }

        /// <summary>
        /// Runtime-ready validation performed only after spatial keep placement is resolved. The
        /// seed-only topology pass intentionally cannot freeze slit phases because their historical
        /// recipe depends on the final world-space turret centres.
        /// </summary>
        public static bool TryValidateSlits(
            in CastlePlan keepPlan,
            CastleKeepTurretPlan plan,
            out CastleKeepTurretPlanIssue issue)
        {
            if (!TryValidate(plan, out issue))
                return false;

            int height = keepPlan.KeepHeight + 30;
            CastleKeepTurretSpec[] turrets = plan.Snapshot();
            for (int i = 0; i < turrets.Length; i++)
            {
                if (turrets[i].Slits == null)
                {
                    issue = CastleKeepTurretPlanIssue.MissingSlitPlan;
                    return false;
                }

                if (!CastleTowerSlitPlanValidator.TryValidate(
                        turrets[i].Slits,
                        height,
                        keepPlan.FloorHeight,
                        out _))
                {
                    issue = CastleKeepTurretPlanIssue.InvalidSlitPlan;
                    return false;
                }
            }

            issue = CastleKeepTurretPlanIssue.None;
            return true;
        }
    }

    public static class CastleKeepTurretPlanner
    {
        /// <summary>
        /// Freezes topology-level keep-turret identity and a coherent roof composition. Slit phases
        /// are attached later by CastleKeepTurretPlanCompletion after the spatial keep centre is
        /// resolved; this planner never derives world-position-dependent slit choices.
        /// </summary>
        public static CastleKeepTurretPlan Create(uint seed)
        {
            var rng = new Random(CastleSeedPartition.Derive(
                seed, CastleSeedDomain.Keep, 0x54555252u));
            int roll = rng.NextInt(0, 100);

            CastleKeepTurretRoofPattern pattern = roll < 35
                ? CastleKeepTurretRoofPattern.AllRoofed
                : roll < 55
                    ? CastleKeepTurretRoofPattern.MinZPair
                    : roll < 75
                        ? CastleKeepTurretRoofPattern.MaxZPair
                        : roll < 90
                            ? CastleKeepTurretRoofPattern.Diagonal
                            : CastleKeepTurretRoofPattern.Bare;
            return CastleKeepTurretRecipe.Create(pattern);
        }
    }

    /// <summary>Named topology recipes for keep-turret roof composition.</summary>
    public static class CastleKeepTurretRecipe
    {
        public static CastleKeepTurretPlan Historical() =>
            Create(CastleKeepTurretRoofPattern.Historical);

        public static CastleKeepTurretPlan Create(CastleKeepTurretRoofPattern pattern)
        {
            int roofMask = pattern switch
            {
                CastleKeepTurretRoofPattern.Historical => 0b1111,
                CastleKeepTurretRoofPattern.AllRoofed => 0b1111,
                CastleKeepTurretRoofPattern.MinZPair => 0b0011,
                CastleKeepTurretRoofPattern.MaxZPair => 0b1100,
                CastleKeepTurretRoofPattern.Diagonal => 0b1001,
                CastleKeepTurretRoofPattern.Bare => 0,
                _ => throw new ArgumentOutOfRangeException(nameof(pattern)),
            };

            var turrets = new CastleKeepTurretSpec[4];
            for (int i = 0; i < turrets.Length; i++)
            {
                turrets[i] = new CastleKeepTurretSpec
                {
                    Corner = (CastleKeepTurretCorner)i,
                    HasRoof = (roofMask & (1 << i)) != 0,
                    Slits = null,
                };
            }

            var plan = new CastleKeepTurretPlan(turrets);
            if (!CastleKeepTurretPlanValidator.TryValidate(
                    plan, out CastleKeepTurretPlanIssue issue))
            {
                throw new InvalidOperationException(
                    $"Castle keep turret recipe is invalid: {issue}.");
            }
            return plan;
        }
    }
}

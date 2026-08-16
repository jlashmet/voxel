using System;

namespace VoxelEngine.Structures.Api
{
    public enum CastleKeepTurretCorner : byte
    {
        MinXMinZ = 0,
        MaxXMinZ = 1,
        MinXMaxZ = 2,
        MaxXMaxZ = 3,
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
        /// Freezes topology-level keep-turret identity and roof styling. Slit phases are attached
        /// later by CastleKeepTurretPlanCompletion after the spatial keep centre is resolved.
        /// </summary>
        public static CastleKeepTurretPlan Create(uint seed)
        {
            _ = seed;
            var turrets = new CastleKeepTurretSpec[4];
            for (int i = 0; i < turrets.Length; i++)
            {
                turrets[i] = new CastleKeepTurretSpec
                {
                    Corner = (CastleKeepTurretCorner)i,
                    HasRoof = true,
                    Slits = null,
                };
            }

            return new CastleKeepTurretPlan(turrets);
        }
    }
}

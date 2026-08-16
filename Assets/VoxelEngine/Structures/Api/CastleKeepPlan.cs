using System;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Complete semantic plan for the occupied keep after castle placement is known. The castle
    /// spatial plan owns where the keep sits; this aggregate owns what the keep contains.
    /// </summary>
    public sealed class CastleKeepPlan
    {
        private readonly CastleKeepFloorPlan[] _floors;

        public CastleKeepFloorPlan[] Floors => (CastleKeepFloorPlan[])_floors.Clone();
        public CastleKeepCirculationPlan Circulation { get; }
        public CastleKeepAnnexPlan Annexes { get; }

        internal CastleKeepPlan(
            CastleKeepFloorPlan[] floors,
            in CastleKeepCirculationPlan circulation,
            in CastleKeepAnnexPlan annexes)
        {
            _floors = floors != null
                ? (CastleKeepFloorPlan[])floors.Clone()
                : Array.Empty<CastleKeepFloorPlan>();
            Circulation = circulation;
            Annexes = annexes;
        }
    }

    public enum CastleKeepPlanIssue : byte
    {
        None = 0,
        InvalidFloors,
        InvalidCirculation,
        InvalidAnnexes,
    }

    /// <summary>
    /// Plans keep-internal semantics in one pure API pass. Runtime should consume this aggregate
    /// instead of independently deriving room purpose, circulation anchors, or annex presence.
    /// </summary>
    public static class CastleKeepPlanner
    {
        public static CastleKeepPlan Create(in CastlePlan plan)
        {
            CastleKeepInteriorPlan interior = CastleKeepInteriorPlanner.Create(in plan);
            CastleKeepFloorPlan[] floors = interior.SnapshotFloors();
            CastleKeepCirculationPlan circulation = CastleKeepCirculationPlanner.Create(in plan);
            CastleKeepAnnexPlan annexes = CastleKeepAnnexPlanner.Create(in plan);
            var keep = new CastleKeepPlan(floors, in circulation, in annexes);

            if (!CastleKeepPlanValidator.TryValidate(in plan, keep, out CastleKeepPlanIssue issue))
                throw new InvalidOperationException($"Planned castle keep is invalid: {issue}.");

            return keep;
        }
    }

    /// <summary>Cross-component validation for the semantic keep aggregate.</summary>
    public static class CastleKeepPlanValidator
    {
        public static bool TryValidate(
            in CastlePlan dimensions,
            CastleKeepPlan keep,
            out CastleKeepPlanIssue issue)
        {
            if (keep == null ||
                !CastleKeepFloorPlanValidator.TryValidate(
                    in dimensions, keep.Floors, out _))
            {
                issue = CastleKeepPlanIssue.InvalidFloors;
                return false;
            }

            CastleKeepCirculationPlan circulation = keep.Circulation;
            if (!CastleKeepCirculationPlanner.TryValidate(
                    in dimensions, in circulation, out _))
            {
                issue = CastleKeepPlanIssue.InvalidCirculation;
                return false;
            }

            CastleKeepAnnexPlan annexes = keep.Annexes;
            if (!CastleKeepAnnexPlanValidator.TryValidate(in annexes, out _))
            {
                issue = CastleKeepPlanIssue.InvalidAnnexes;
                return false;
            }

            issue = CastleKeepPlanIssue.None;
            return true;
        }

        public static void RequireValid(in CastlePlan dimensions, CastleKeepPlan keep)
        {
            if (TryValidate(in dimensions, keep, out CastleKeepPlanIssue issue))
                return;

            throw new InvalidOperationException($"Castle keep plan is invalid: {issue}.");
        }
    }
}

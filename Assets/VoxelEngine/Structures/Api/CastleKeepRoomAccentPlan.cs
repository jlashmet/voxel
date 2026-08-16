using System;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Immutable planner-owned variable accents for every semantic keep floor. Fixed furniture
    /// remains part of the room realization recipe; this aggregate freezes only choices that
    /// historically consumed the per-floor room RNG stream.
    /// </summary>
    public sealed class CastleKeepRoomAccentPlan
    {
        private readonly CastleRoomAccentPlan[] _floors;

        internal CastleKeepRoomAccentPlan(CastleRoomAccentPlan[] floors)
        {
            _floors = floors != null
                ? (CastleRoomAccentPlan[])floors.Clone()
                : Array.Empty<CastleRoomAccentPlan>();
        }

        public int FloorCount => _floors.Length;

        public CastleRoomAccentPlan Floor(int index)
        {
            if ((uint)index >= (uint)_floors.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _floors[index];
        }

        public CastleRoomAccentPlan[] SnapshotFloors() =>
            (CastleRoomAccentPlan[])_floors.Clone();
    }

    public enum CastleKeepRoomAccentPlanIssue : byte
    {
        None = 0,
        MissingPlan,
        FloorCountMismatch,
        InvalidFloorPlan,
    }

    /// <summary>Pure validation for the complete keep-room accent stack.</summary>
    public static class CastleKeepRoomAccentPlanValidator
    {
        public static bool TryValidate(
            in CastlePlan dimensions,
            CastleKeepRoomAccentPlan plan,
            out CastleKeepRoomAccentPlanIssue issue)
        {
            if (plan == null)
            {
                issue = CastleKeepRoomAccentPlanIssue.MissingPlan;
                return false;
            }

            if (plan.FloorCount != dimensions.Floors)
            {
                issue = CastleKeepRoomAccentPlanIssue.FloorCountMismatch;
                return false;
            }

            for (int floor = 0; floor < plan.FloorCount; floor++)
            {
                if (CastleRoomAccentPlanValidator.TryValidate(
                        in dimensions, plan.Floor(floor), out _))
                    continue;

                issue = CastleKeepRoomAccentPlanIssue.InvalidFloorPlan;
                return false;
            }

            issue = CastleKeepRoomAccentPlanIssue.None;
            return true;
        }
    }

    /// <summary>
    /// Freezes the variable room accents for the full keep from the already-planned semantic floor
    /// stack. Runtime can later consume this value without owning or replaying any random draws.
    /// </summary>
    public static class CastleKeepRoomAccentPlanner
    {
        public static CastleKeepRoomAccentPlan Create(
            in CastlePlan dimensions,
            CastleKeepInteriorPlan interior)
        {
            if (interior == null)
                throw new ArgumentNullException(nameof(interior));
            if (interior.FloorCount != dimensions.Floors)
            {
                throw new ArgumentException(
                    "Keep interior floor count must match the castle dimensions.",
                    nameof(interior));
            }

            var floors = new CastleRoomAccentPlan[interior.FloorCount];
            for (int floor = 0; floor < floors.Length; floor++)
            {
                CastleKeepFloorPlan floorPlan = interior.Floor(floor);
                if (floorPlan.FloorIndex != floor)
                {
                    throw new ArgumentException(
                        $"Keep floor identity mismatch at index {floor}.",
                        nameof(interior));
                }

                floors[floor] = CastleRoomAccentPlanner.Create(
                    in dimensions, in floorPlan);
            }

            var result = new CastleKeepRoomAccentPlan(floors);
            if (!CastleKeepRoomAccentPlanValidator.TryValidate(
                    in dimensions, result, out CastleKeepRoomAccentPlanIssue issue))
            {
                throw new InvalidOperationException(
                    $"Castle keep-room accent planning produced an invalid plan: {issue}.");
            }

            return result;
        }
    }
}

using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    public enum CastleAccessRouteIssue : byte
    {
        None,
        MissingOuterWard,
        MissingInnerWard,
        OuterRouteLeavesWard,
        InnerWardEnteredBeforeGate,
        InnerRouteLeavesWard,
    }

    /// <summary>
    /// Pure validation for the derived gate-to-keep access centreline. CastleAccessRoute owns the
    /// intended circulation waypoints; this validator proves those straight walkable segments do
    /// not escape a ward or cross an inner curtain anywhere except its planned gate.
    /// </summary>
    public static class CastleAccessRouteValidator
    {
        public static bool TryValidate(
            in CastleAccessRoute route,
            int2[] outerWard,
            int2[] innerWard,
            out CastleAccessRouteIssue issue)
        {
            if (outerWard == null || outerWard.Length < 3)
            {
                issue = CastleAccessRouteIssue.MissingOuterWard;
                return false;
            }

            bool hasInnerWard = route.WaypointCount == 3;
            if (hasInnerWard && (innerWard == null || innerWard.Length < 3))
            {
                issue = CastleAccessRouteIssue.MissingInnerWard;
                return false;
            }

            int2 primaryGate = route.Waypoint(0);
            int2 firstDestination = route.Waypoint(1);
            if (!SegmentStaysInside(primaryGate, firstDestination, outerWard))
            {
                issue = CastleAccessRouteIssue.OuterRouteLeavesWard;
                return false;
            }

            if (!hasInnerWard)
            {
                issue = CastleAccessRouteIssue.None;
                return true;
            }

            // The outer approach may touch the inner ward only at its final waypoint: the planned
            // inner gate. Hitting its boundary/interior earlier means the route crosses the curtain
            // somewhere else even if both endpoints themselves are otherwise valid.
            if (SegmentEntersPolygonBeforeEndpoint(primaryGate, firstDestination, innerWard))
            {
                issue = CastleAccessRouteIssue.InnerWardEnteredBeforeGate;
                return false;
            }

            if (!CastlePolygonGeometry.ContainsPoint(firstDestination, innerWard))
            {
                issue = CastleAccessRouteIssue.InnerRouteLeavesWard;
                return false;
            }

            int2 keepEntrance = route.Waypoint(2);
            if (!SegmentStaysInside(firstDestination, keepEntrance, innerWard))
            {
                issue = CastleAccessRouteIssue.InnerRouteLeavesWard;
                return false;
            }

            issue = CastleAccessRouteIssue.None;
            return true;
        }

        private static bool SegmentStaysInside(int2 start, int2 end, int2[] polygon)
        {
            int steps = StepCount(start, end);
            if (steps == 0)
                return CastlePolygonGeometry.ContainsPoint(start, polygon);

            for (int step = 0; step <= steps; step++)
            {
                if (!CastlePolygonGeometry.ContainsPoint(Sample(start, end, step, steps), polygon))
                    return false;
            }
            return true;
        }

        private static bool SegmentEntersPolygonBeforeEndpoint(
            int2 start,
            int2 end,
            int2[] polygon)
        {
            int steps = StepCount(start, end);
            for (int step = 0; step < steps; step++)
            {
                if (CastlePolygonGeometry.ContainsPoint(Sample(start, end, step, steps), polygon))
                    return true;
            }
            return false;
        }

        private static int StepCount(int2 start, int2 end) =>
            math.max(math.abs(end.x - start.x), math.abs(end.y - start.y));

        private static int2 Sample(int2 start, int2 end, int step, int steps)
        {
            if (steps <= 0) return start;
            float t = step / (float)steps;
            return new int2(
                (int)math.round(math.lerp(start.x, end.x, t)),
                (int)math.round(math.lerp(start.y, end.y, t)));
        }
    }
}

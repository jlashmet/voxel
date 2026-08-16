using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Integer polygon helpers shared by castle spatial planning, validation, and realization.
    /// These functions operate only on semantic X/Z coordinates and have no runtime or
    /// voxel-storage dependency.
    /// </summary>
    public static class CastlePolygonGeometry
    {
        /// <summary>Returns true when the local X/Z point lies inside or on the polygon boundary.</summary>
        public static bool ContainsPoint(int2 point, int2[] polygon) =>
            PointInOrOnPolygon(point, polygon);

        /// <summary>Returns true when the complete axis-aligned keep footprint fits in the polygon.</summary>
        public static bool ContainsKeepFootprint(
            in CastlePlan dimensions,
            int2 centre,
            int2[] polygon) =>
            KeepFootprintFits(in dimensions, centre, polygon);

        /// <summary>
        /// Returns true when every edge of <paramref name="subject"/> stays strictly inside
        /// <paramref name="container"/>. Merely checking corners is insufficient for a concave
        /// container because an indentation can cut through a subject edge between sample points.
        /// Boundary contact is rejected; authored structures are expected to keep their own wall
        /// clearance rather than rely on coincident polygon edges.
        /// </summary>
        public static bool ContainsPolygon(int2[] container, int2[] subject)
        {
            if (container == null || container.Length < 3 ||
                subject == null || subject.Length < 3)
                return false;

            for (int i = 0; i < subject.Length; i++)
            {
                if (!PointInOrOnPolygon(subject[i], container))
                    return false;
            }

            for (int subjectEdge = 0; subjectEdge < subject.Length; subjectEdge++)
            {
                int2 a = subject[subjectEdge];
                int2 b = subject[(subjectEdge + 1) % subject.Length];
                for (int containerEdge = 0; containerEdge < container.Length; containerEdge++)
                {
                    int2 c = container[containerEdge];
                    int2 d = container[(containerEdge + 1) % container.Length];
                    if (SegmentsIntersectOrTouch(a, b, c, d))
                        return false;
                }
            }

            // A concave indentation may lie wholly inside the subject after integer rounding even
            // when none of the subject vertices escapes the container. Reject that case directly.
            for (int i = 0; i < container.Length; i++)
            {
                if (PointInPolygonStrict(container[i], subject))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Returns true when two polygon interiors or boundaries share any point. This covers edge
        /// crossings/touches as well as complete containment in either direction.
        /// </summary>
        public static bool PolygonsOverlapOrTouch(int2[] first, int2[] second)
        {
            if (first == null || first.Length < 3 || second == null || second.Length < 3)
                return false;

            for (int firstEdge = 0; firstEdge < first.Length; firstEdge++)
            {
                int2 a = first[firstEdge];
                int2 b = first[(firstEdge + 1) % first.Length];
                for (int secondEdge = 0; secondEdge < second.Length; secondEdge++)
                {
                    int2 c = second[secondEdge];
                    int2 d = second[(secondEdge + 1) % second.Length];
                    if (SegmentsIntersectOrTouch(a, b, c, d))
                        return true;
                }
            }

            return PointInOrOnPolygon(first[0], second) ||
                   PointInOrOnPolygon(second[0], first);
        }

        /// <summary>
        /// Returns true only for a non-self-intersecting polygon ring. Adjacent edges may meet at
        /// their shared vertex; any crossing, overlap, or repeated non-adjacent vertex is invalid.
        /// </summary>
        public static bool IsSimplePolygon(int2[] polygon)
        {
            if (polygon == null || polygon.Length < 3)
                return false;

            for (int i = 0; i < polygon.Length; i++)
            {
                int iNext = (i + 1) % polygon.Length;
                int2 a = polygon[i];
                int2 b = polygon[iNext];
                if (a.Equals(b))
                    return false;

                for (int j = i + 1; j < polygon.Length; j++)
                {
                    int jNext = (j + 1) % polygon.Length;

                    // Neighbouring edges intentionally share one endpoint. The first and last
                    // edges are neighbours too because the ring is closed.
                    if (i == j || iNext == j || jNext == i)
                        continue;

                    if (SegmentsIntersectOrTouch(a, b, polygon[j], polygon[jNext]))
                        return false;
                }
            }

            return true;
        }

        internal static bool PointOnPerimeter(int2 point, int2[] polygon)
        {
            if (polygon == null || polygon.Length < 2) return false;

            for (int i = 0; i < polygon.Length; i++)
            {
                if (PointOnSegment(point, polygon[i], polygon[(i + 1) % polygon.Length]))
                    return true;
            }
            return false;
        }

        internal static bool PointOnSegment(int2 point, int2 a, int2 b)
        {
            long cross = Orient(a, b, point);
            if (cross != 0) return false;

            long dot = (long)(point.x - a.x) * (point.x - b.x) +
                       (long)(point.y - a.y) * (point.y - b.y);
            return dot <= 0;
        }

        internal static bool PointInOrOnPolygon(int2 point, int2[] polygon)
        {
            if (polygon == null || polygon.Length < 3) return false;

            bool inside = false;
            for (int i = 0, previous = polygon.Length - 1;
                 i < polygon.Length;
                 previous = i++)
            {
                int2 a = polygon[previous];
                int2 b = polygon[i];
                if (PointOnSegment(point, a, b)) return true;

                bool crossesY = (a.y > point.y) != (b.y > point.y);
                if (!crossesY) continue;

                double crossingX =
                    (double)(b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x;
                if (point.x < crossingX)
                    inside = !inside;
            }
            return inside;
        }

        /// <summary>
        /// True only when the complete semantic axis-aligned keep footprint is contained by the
        /// ward polygon. Corner containment alone is insufficient for concave wards, so this also
        /// rejects ward edges that cut through or indent into the keep rectangle.
        /// </summary>
        internal static bool KeepFootprintFits(
            in CastlePlan dimensions,
            int2 centre,
            int2[] polygon)
        {
            int hx = dimensions.KeepHalfX;
            int hz = dimensions.KeepHalfZ;
            if (hx <= 0 || hz <= 0 || polygon == null || polygon.Length < 3)
                return false;

            int minX = centre.x - hx;
            int maxX = centre.x + hx;
            int minZ = centre.y - hz;
            int maxZ = centre.y + hz;
            int2[] corners =
            {
                new int2(minX, minZ),
                new int2(maxX, minZ),
                new int2(maxX, maxZ),
                new int2(minX, maxZ),
            };

            for (int i = 0; i < corners.Length; i++)
            {
                if (!PointInOrOnPolygon(corners[i], polygon))
                    return false;
            }

            // A concave ward can have all four keep corners inside while an indentation crosses
            // the keep. Reject both polygon vertices inside the rectangle and proper boundary
            // crossings. Boundary contact itself remains valid for wall-integrated keeps.
            for (int i = 0; i < polygon.Length; i++)
            {
                int2 vertex = polygon[i];
                if (vertex.x > minX && vertex.x < maxX &&
                    vertex.y > minZ && vertex.y < maxZ)
                    return false;

                int2 next = polygon[(i + 1) % polygon.Length];
                for (int edge = 0; edge < corners.Length; edge++)
                {
                    int2 a = corners[edge];
                    int2 b = corners[(edge + 1) % corners.Length];
                    if (ProperlyIntersects(vertex, next, a, b))
                        return false;
                }

                // Also catch a ward edge whose endpoints merely touch the keep boundary while the
                // segment itself passes through the footprint interior.
                double midX = (vertex.x + next.x) * 0.5;
                double midZ = (vertex.y + next.y) * 0.5;
                if (midX > minX && midX < maxX && midZ > minZ && midZ < maxZ)
                    return false;
            }

            return true;
        }

        private static bool PointInPolygonStrict(int2 point, int2[] polygon) =>
            !PointOnPerimeter(point, polygon) && PointInOrOnPolygon(point, polygon);

        private static bool SegmentsIntersectOrTouch(int2 a, int2 b, int2 c, int2 d)
        {
            long abC = Orient(a, b, c);
            long abD = Orient(a, b, d);
            long cdA = Orient(c, d, a);
            long cdB = Orient(c, d, b);

            if (OppositeSigns(abC, abD) && OppositeSigns(cdA, cdB))
                return true;
            if (abC == 0 && PointOnSegment(c, a, b)) return true;
            if (abD == 0 && PointOnSegment(d, a, b)) return true;
            if (cdA == 0 && PointOnSegment(a, c, d)) return true;
            if (cdB == 0 && PointOnSegment(b, c, d)) return true;
            return false;
        }

        private static bool ProperlyIntersects(int2 a, int2 b, int2 c, int2 d)
        {
            long abC = Orient(a, b, c);
            long abD = Orient(a, b, d);
            long cdA = Orient(c, d, a);
            long cdB = Orient(c, d, b);
            return OppositeSigns(abC, abD) && OppositeSigns(cdA, cdB);
        }

        private static bool OppositeSigns(long a, long b) =>
            (a < 0 && b > 0) || (a > 0 && b < 0);

        private static long Orient(int2 a, int2 b, int2 c) =>
            (long)(b.x - a.x) * (c.y - a.y) -
            (long)(b.y - a.y) * (c.x - a.x);
    }
}

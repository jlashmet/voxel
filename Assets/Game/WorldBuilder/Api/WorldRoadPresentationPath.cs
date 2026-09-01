using System;
using System.Collections.Generic;

namespace Game.WorldBuilder.Api
{
    /// <summary>
    /// Deterministic presentation-only refinement of an authoritative resolved road polyline.
    /// The resolver points remain unchanged; this view removes bounded search-grid micro-turns,
    /// rounds remaining interior direction changes, and defines bounded cross-section offsets so
    /// semantic presentation queries and physical lowering agree without changing route authority.
    /// </summary>
    public static class WorldRoadPresentationPath
    {
        private const int CurveTrimPermille = 240;

        public static IReadOnlyList<ResolvedWorldRoadPoint> Build(ResolvedWorldRoad road)
            => Build(road, null);

        public static IReadOnlyList<ResolvedWorldRoadPoint> Build(
            ResolvedWorldRoad road,
            IReadOnlyList<WorldRoadJunction> junctions)
        {
            if (road == null) throw new ArgumentNullException(nameof(road));
            if (!road.IsResolved || road.Points.Count < 2)
                throw new ArgumentException("Road presentation requires resolved geometry.", nameof(road));
            if (road.Points.Count == 2)
                return new[] { road.Points[0], road.Points[1] };

            IReadOnlyList<ResolvedWorldRoadPoint> controls = SimplifyMicroTurns(road, junctions);
            if (controls.Count == 2)
                return new[] { controls[0], controls[1] };

            // Each remaining ordinary corner is represented by one bounded chamfer (entry -> exit).
            // Grid-search micro-turns are simplified first, so visual continuity improves while the
            // number of catalogue definitions stays bounded instead of multiplying each resolver turn.
            var result = new List<ResolvedWorldRoadPoint>(controls.Count * 2);
            AddDistinct(result, controls[0]);
            for (int i = 1; i + 1 < controls.Count; i++)
            {
                ResolvedWorldRoadPoint previous = controls[i - 1];
                ResolvedWorldRoadPoint corner = controls[i];
                ResolvedWorldRoadPoint next = controls[i + 1];
                if (IsJunction(corner, junctions))
                {
                    AddDistinct(result, corner);
                    continue;
                }

                int previousRun = PlanarDistance(previous, corner);
                int nextRun = PlanarDistance(corner, next);
                int trim = Math.Min(previousRun, nextRun) * CurveTrimPermille / 1000;
                trim = Math.Min(trim, road.Intent.Profile.CoreRadiusDm);
                if (trim < 2 || Collinear(previous, corner, next))
                {
                    AddDistinct(result, corner);
                    continue;
                }

                AddDistinct(result, MoveToward(corner, previous, trim, previousRun));
                AddDistinct(result, MoveToward(corner, next, trim, nextRun));
            }
            AddDistinct(result, controls[controls.Count - 1]);
            return result.ToArray();
        }

        public static int CrossSectionOffsetDm(int distanceDm, int coreDm, int outerDm)
        {
            if (coreDm <= 0) return 0;
            int crown = Clamp(coreDm / 12, 1, 3);
            if (distanceDm <= coreDm)
                return DivideRounded((long)crown * (coreDm - distanceDm), coreDm);

            int shoulderWidth = outerDm - coreDm;
            if (shoulderWidth <= 0) return 0;
            int shoulderDrop = Clamp(shoulderWidth / 10, 1, 3);
            return -DivideRounded(
                (long)shoulderDrop * (distanceDm - coreDm),
                shoulderWidth);
        }

        private static IReadOnlyList<ResolvedWorldRoadPoint> SimplifyMicroTurns(
            ResolvedWorldRoad road,
            IReadOnlyList<WorldRoadJunction> junctions)
        {
            IReadOnlyList<ResolvedWorldRoadPoint> source = road.Points;
            int minimumCore = Math.Max(1,
                road.Intent.Profile.CoreRadiusDm - road.Intent.Profile.EdgeVariationDm);
            int tolerance = Math.Max(1, minimumCore / 2);
            var result = new List<ResolvedWorldRoadPoint>(source.Count) { source[0] };

            int anchor = 0;
            while (anchor + 1 < source.Count)
            {
                int selected = anchor + 1;
                for (int candidate = anchor + 2; candidate < source.Count; candidate++)
                {
                    if (ContainsJunctionBetween(source, anchor, candidate, junctions)) break;
                    if (!WithinPresentationEnvelope(source, anchor, candidate, tolerance)) break;
                    selected = candidate;
                    if (IsJunction(source[candidate], junctions)) break;
                }
                AddDistinct(result, source[selected]);
                anchor = selected;
            }
            return result.ToArray();
        }

        private static bool WithinPresentationEnvelope(
            IReadOnlyList<ResolvedWorldRoadPoint> points,
            int fromIndex,
            int toIndex,
            int toleranceDm)
        {
            ResolvedWorldRoadPoint a = points[fromIndex];
            ResolvedWorldRoadPoint b = points[toIndex];
            long dx = (long)b.Xdm - a.Xdm;
            long dz = (long)b.Zdm - a.Zdm;
            long lengthSquared = dx * dx + dz * dz;
            if (lengthSquared <= 0) return false;
            long toleranceSquared = (long)toleranceDm * toleranceDm;

            for (int i = fromIndex + 1; i < toIndex; i++)
            {
                ResolvedWorldRoadPoint point = points[i];
                long dot = ((long)point.Xdm - a.Xdm) * dx
                    + ((long)point.Zdm - a.Zdm) * dz;
                if (dot <= 0 || dot >= lengthSquared) return false;
                long qx = (long)a.Xdm + DivideRounded(dx * dot, lengthSquared);
                long qz = (long)a.Zdm + DivideRounded(dz * dot, lengthSquared);
                long ex = (long)point.Xdm - qx;
                long ez = (long)point.Zdm - qz;
                if (ex * ex + ez * ez > toleranceSquared) return false;

                int expectedY = a.Ydm + DivideRounded(
                    ((long)b.Ydm - a.Ydm) * dot,
                    lengthSquared);
                if (Math.Abs(point.Ydm - expectedY) > toleranceDm) return false;
            }
            return true;
        }

        private static bool ContainsJunctionBetween(
            IReadOnlyList<ResolvedWorldRoadPoint> points,
            int fromIndex,
            int toIndex,
            IReadOnlyList<WorldRoadJunction> junctions)
        {
            for (int i = fromIndex + 1; i < toIndex; i++)
                if (IsJunction(points[i], junctions)) return true;
            return false;
        }

        private static bool IsJunction(
            ResolvedWorldRoadPoint point,
            IReadOnlyList<WorldRoadJunction> junctions)
        {
            if (junctions == null) return false;
            for (int i = 0; i < junctions.Count; i++)
                if (junctions[i].Xdm == point.Xdm && junctions[i].Zdm == point.Zdm)
                    return true;
            return false;
        }

        private static ResolvedWorldRoadPoint MoveToward(
            ResolvedWorldRoadPoint from,
            ResolvedWorldRoadPoint to,
            int distance,
            int run)
        {
            return new ResolvedWorldRoadPoint(
                from.Xdm + DivideRounded((long)(to.Xdm - from.Xdm) * distance, run),
                from.Ydm + DivideRounded((long)(to.Ydm - from.Ydm) * distance, run),
                from.Zdm + DivideRounded((long)(to.Zdm - from.Zdm) * distance, run));
        }

        private static bool Collinear(
            ResolvedWorldRoadPoint a,
            ResolvedWorldRoadPoint b,
            ResolvedWorldRoadPoint c)
        {
            long abx = (long)b.Xdm - a.Xdm;
            long abz = (long)b.Zdm - a.Zdm;
            long bcx = (long)c.Xdm - b.Xdm;
            long bcz = (long)c.Zdm - b.Zdm;
            return abx * bcz == abz * bcx;
        }

        private static int PlanarDistance(ResolvedWorldRoadPoint a, ResolvedWorldRoadPoint b)
        {
            long dx = (long)b.Xdm - a.Xdm;
            long dz = (long)b.Zdm - a.Zdm;
            return Math.Max(1, IntegerSqrt(dx * dx + dz * dz));
        }

        private static void AddDistinct(List<ResolvedWorldRoadPoint> points, ResolvedWorldRoadPoint point)
        {
            if (points.Count == 0 || !points[points.Count - 1].Equals(point)) points.Add(point);
        }

        private static int DivideRounded(long numerator, long denominator)
        {
            if (denominator <= 0) return 0;
            if (numerator >= 0) return (int)((numerator + denominator / 2) / denominator);
            return (int)(-((-numerator + denominator / 2) / denominator));
        }

        private static int Clamp(int value, int min, int max)
            => value < min ? min : value > max ? max : value;

        private static int IntegerSqrt(long value)
        {
            if (value <= 0) return 0;
            long low = 1;
            long high = Math.Min(value, 3037000499L);
            while (low <= high)
            {
                long middle = low + ((high - low) >> 1);
                if (middle <= value / middle) low = middle + 1;
                else high = middle - 1;
            }
            long root = high;
            long next = root + 1;
            if (next <= 3037000499L
                && next * next - value <= value - root * root)
                root = next;
            return root > int.MaxValue ? int.MaxValue : (int)root;
        }
    }
}

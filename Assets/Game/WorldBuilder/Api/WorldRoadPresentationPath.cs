using System;
using System.Collections.Generic;

namespace Game.WorldBuilder.Api
{
    /// <summary>
    /// Deterministic presentation-only refinement of an authoritative resolved road polyline.
    /// The resolver points remain unchanged; this view only rounds interior direction changes and
    /// defines bounded cross-section offsets so semantic presentation queries and physical lowering
    /// can agree without changing route authority.
    /// </summary>
    public static class WorldRoadPresentationPath
    {
        private const int CurveTrimPermille = 240;
        private const int QuadraticSteps = 4;

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

            var result = new List<ResolvedWorldRoadPoint>(road.Points.Count * 5);
            AddDistinct(result, road.Points[0]);
            for (int i = 1; i + 1 < road.Points.Count; i++)
            {
                ResolvedWorldRoadPoint previous = road.Points[i - 1];
                ResolvedWorldRoadPoint corner = road.Points[i];
                ResolvedWorldRoadPoint next = road.Points[i + 1];
                if (IsJunction(corner, junctions))
                {
                    AddDistinct(result, corner);
                    continue;
                }

                int previousRun = PlanarDistance(previous, corner);
                int nextRun = PlanarDistance(corner, next);
                int trim = Math.Min(previousRun, nextRun) * CurveTrimPermille / 1000;
                int maximumByProfile = road.Intent.Profile.CoreRadiusDm
                    + road.Intent.Profile.TransitionWidthDm;
                trim = Math.Min(trim, maximumByProfile);
                if (trim < 2 || Collinear(previous, corner, next))
                {
                    AddDistinct(result, corner);
                    continue;
                }

                ResolvedWorldRoadPoint entry = MoveToward(corner, previous, trim, previousRun);
                ResolvedWorldRoadPoint exit = MoveToward(corner, next, trim, nextRun);
                AddDistinct(result, entry);
                for (int step = 1; step < QuadraticSteps; step++)
                    AddDistinct(result, Quadratic(entry, corner, exit, step, QuadraticSteps));
                AddDistinct(result, exit);
            }
            AddDistinct(result, road.Points[road.Points.Count - 1]);
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

        private static ResolvedWorldRoadPoint Quadratic(
            ResolvedWorldRoadPoint a,
            ResolvedWorldRoadPoint control,
            ResolvedWorldRoadPoint b,
            int numerator,
            int denominator)
        {
            long inverse = denominator - numerator;
            long divisor = (long)denominator * denominator;
            return new ResolvedWorldRoadPoint(
                DivideRounded(inverse * inverse * a.Xdm
                    + 2L * inverse * numerator * control.Xdm
                    + (long)numerator * numerator * b.Xdm, divisor),
                DivideRounded(inverse * inverse * a.Ydm
                    + 2L * inverse * numerator * control.Ydm
                    + (long)numerator * numerator * b.Ydm, divisor),
                DivideRounded(inverse * inverse * a.Zdm
                    + 2L * inverse * numerator * control.Zdm
                    + (long)numerator * numerator * b.Zdm, divisor));
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

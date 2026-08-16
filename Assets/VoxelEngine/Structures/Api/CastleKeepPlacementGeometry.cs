using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Deterministic keep-placement geometry shared by castle spatial planning and validation.
    /// It answers only where a complete keep footprint may sit inside an already-planned ward.
    /// </summary>
    internal static class CastleKeepPlacementGeometry
    {
        internal static int2 FarthestKeepCentreAlong(
            in CastlePlan dimensions,
            float2 direction,
            int2[] ward)
        {
            if (ward == null || ward.Length < 3)
                return int2.zero;

            float length = math.length(direction);
            float2 axis = length > 0.001f
                ? direction / length
                : new float2(0f, 1f);

            float maxProjection = 0f;
            for (int i = 0; i < ward.Length; i++)
            {
                float projection = math.dot(new float2(ward[i].x, ward[i].y), axis);
                maxProjection = math.max(maxProjection, projection);
            }

            int keepExtent = math.max(dimensions.KeepHalfX, dimensions.KeepHalfZ);
            int searchLimit = (int)math.ceil(maxProjection) + keepExtent + 8;
            bool found = false;
            int2 best = default;
            float bestProjection = float.MinValue;
            int2 previous = new int2(int.MinValue, int.MinValue);

            for (int distance = 0; distance <= searchLimit; distance++)
            {
                int2 candidate = new int2(
                    (int)math.round(axis.x * distance),
                    (int)math.round(axis.y * distance));
                if (candidate.Equals(previous))
                    continue;
                previous = candidate;

                if (!CastlePolygonGeometry.KeepFootprintFits(
                        in dimensions, candidate, ward))
                    continue;

                float projection = math.dot(new float2(candidate.x, candidate.y), axis);
                if (found && projection <= bestProjection)
                    continue;

                found = true;
                best = candidate;
                bestProjection = projection;
            }

            return found ? best : int2.zero;
        }

        internal static bool IsFarthestKeepCentreAlong(
            in CastlePlan dimensions,
            int2 centre,
            float2 direction,
            int2[] ward) =>
            centre.Equals(FarthestKeepCentreAlong(in dimensions, direction, ward));

        /// <summary>
        /// Returns the authored Rear keep position: 78% of the deepest valid gate-to-rear placement,
        /// retracted toward the ward centre only if rounding or a concave edge would make it invalid.
        /// This mirrors the planner contract so validation can reject semantically drifted plans.
        /// </summary>
        internal static int2 RearKeepCentreAlong(
            in CastlePlan dimensions,
            float2 direction,
            int2[] ward)
        {
            int2 integrated = FarthestKeepCentreAlong(in dimensions, direction, ward);
            int2 desired = new int2(
                (int)math.round(integrated.x * 0.78f),
                (int)math.round(integrated.y * 0.78f));
            return RetractKeepCentreToWard(desired, in dimensions, ward);
        }

        internal static bool IsRearKeepCentreAlong(
            in CastlePlan dimensions,
            int2 centre,
            float2 direction,
            int2[] ward) =>
            centre.Equals(RearKeepCentreAlong(in dimensions, direction, ward));

        private static int2 RetractKeepCentreToWard(
            int2 desired,
            in CastlePlan dimensions,
            int2[] ward)
        {
            if (CastlePolygonGeometry.KeepFootprintFits(in dimensions, desired, ward))
                return desired;

            for (int step = 127; step >= 0; step--)
            {
                float t = step / 128f;
                int2 candidate = new int2(
                    (int)math.round(desired.x * t),
                    (int)math.round(desired.y * t));
                if (CastlePolygonGeometry.KeepFootprintFits(
                        in dimensions, candidate, ward))
                    return candidate;
            }

            return int2.zero;
        }
    }
}

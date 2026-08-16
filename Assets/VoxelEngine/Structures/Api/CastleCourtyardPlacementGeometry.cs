using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Pure courtyard placement rules shared by spatial planning and validation. Runtime receives
    /// the chosen coordinates and never makes these semantic placement decisions itself.
    /// </summary>
    internal static class CastleCourtyardPlacementGeometry
    {
        internal const int WellClearanceRadius = 20;

        internal static bool TryChooseWell(
            in CastlePlan plan,
            int2[] perimeter,
            in CastleGatePlacementSpec gate,
            int2 keepCentre,
            out int2 well)
        {
            float2 towardGate = new float2(
                gate.Centre.x - keepCentre.x,
                gate.Centre.y - keepCentre.y);
            float length = math.length(towardGate);
            float2 direction = length > 0.001f
                ? towardGate / length
                : new float2(0f, -1f);
            float2 tangent = new float2(-direction.y, direction.x);
            int baseDistance = math.max(plan.KeepHalfX, plan.KeepHalfZ) + 58;
            int preferredSide = (CastleSeedPartition.Derive(
                plan.Seed, CastleSeedDomain.Decor, 0xC048u) & 1u) == 0u ? -1 : 1;

            for (int ring = 0; ring < 4; ring++)
            {
                int distance = baseDistance + ring * 24;
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    int side = attempt == 0 ? preferredSide : -preferredSide;
                    int2 candidate = Round(
                        new float2(keepCentre.x, keepCentre.y)
                        + tangent * (side * distance));
                    if (!WellFits(in plan, perimeter, in gate, keepCentre, candidate))
                        continue;

                    well = candidate;
                    return true;
                }
            }

            well = default;
            return false;
        }

        internal static bool WellFits(
            in CastlePlan plan,
            int2[] perimeter,
            in CastleGatePlacementSpec gate,
            int2 keepCentre,
            int2 candidate)
        {
            int2[] probes =
            {
                candidate,
                candidate + new int2(WellClearanceRadius, 0),
                candidate + new int2(-WellClearanceRadius, 0),
                candidate + new int2(0, WellClearanceRadius),
                candidate + new int2(0, -WellClearanceRadius),
            };
            for (int i = 0; i < probes.Length; i++)
            {
                if (!CastlePolygonGeometry.ContainsPoint(probes[i], perimeter))
                    return false;
            }

            bool clearsKeep =
                math.abs(candidate.x - keepCentre.x) > plan.KeepHalfX + WellClearanceRadius
                || math.abs(candidate.y - keepCentre.y) > plan.KeepHalfZ + WellClearanceRadius;
            if (!clearsKeep)
                return false;

            int2 gateDelta = candidate - gate.Centre;
            return math.lengthsq(new float2(gateDelta.x, gateDelta.y)) >= 70f * 70f;
        }

        private static int2 Round(float2 value) =>
            new int2((int)math.round(value.x), (int)math.round(value.y));
    }
}

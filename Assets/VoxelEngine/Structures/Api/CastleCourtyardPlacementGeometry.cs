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

            // Preserve the authored near-keep placement where it fits so existing seeds do not
            // move just because a more general fallback exists.
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

            // Irregular and radial wards can be wide enough for a well while both preferred
            // tangent rays happen to hit a narrow part of the polygon. Search the whole courtyard
            // before declaring the semantic invariant impossible. A coarse pass handles the normal
            // fallback cheaply; the dense pass is only paid when grid alignment hides a small but
            // valid integer site.
            if (TryChooseFallbackWell(
                    in plan, perimeter, in gate, keepCentre, 8, out well))
                return true;
            return TryChooseFallbackWell(
                in plan, perimeter, in gate, keepCentre, 1, out well);
        }

        internal static bool WellFits(
            in CastlePlan plan,
            int2[] perimeter,
            in CastleGatePlacementSpec gate,
            int2 keepCentre,
            int2 candidate)
        {
            int wardClearance = WardBoundaryClearance(in plan);
            int2[] probes =
            {
                candidate,
                candidate + new int2(wardClearance, 0),
                candidate + new int2(-wardClearance, 0),
                candidate + new int2(0, wardClearance),
                candidate + new int2(0, -wardClearance),
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
            if (math.lengthsq(new float2(gateDelta.x, gateDelta.y)) < 70f * 70f)
                return false;

            // Every perimeter vertex owns a corner tower in both the outer and nested ward plans.
            // Clear those circular footprints explicitly instead of shrinking the entire usable
            // courtyard by tower radius; that preserves room for the required well in tight wards.
            int cornerTowerClearance = plan.TowerRadius + WellClearanceRadius;
            long cornerTowerClearanceSquared =
                (long)cornerTowerClearance * cornerTowerClearance;
            for (int i = 0; i < perimeter.Length; i++)
            {
                long dx = (long)candidate.x - perimeter[i].x;
                long dz = (long)candidate.y - perimeter[i].y;
                if (dx * dx + dz * dz < cornerTowerClearanceSquared)
                    return false;
            }

            CastleGatePlacementSpec innerGate = default;
            bool hasInnerGate = TryDeriveInnerGate(perimeter, in gate, out innerGate);
            CastleAccessRoute route = CastleAccessRoute.Create(
                in plan, in gate, hasInnerGate, in innerGate, keepCentre);
            return route.ClearsPoint(candidate, WellClearanceRadius);
        }

        private static bool TryChooseFallbackWell(
            in CastlePlan plan,
            int2[] perimeter,
            in CastleGatePlacementSpec gate,
            int2 keepCentre,
            int stride,
            out int2 well)
        {
            well = default;
            if (perimeter == null || perimeter.Length < 3 || stride <= 0)
                return false;

            int wardClearance = WardBoundaryClearance(in plan);
            int minX = perimeter[0].x + wardClearance;
            int maxX = perimeter[0].x - wardClearance;
            int minZ = perimeter[0].y + wardClearance;
            int maxZ = perimeter[0].y - wardClearance;
            for (int i = 1; i < perimeter.Length; i++)
            {
                minX = math.min(minX, perimeter[i].x + wardClearance);
                maxX = math.max(maxX, perimeter[i].x - wardClearance);
                minZ = math.min(minZ, perimeter[i].y + wardClearance);
                maxZ = math.max(maxZ, perimeter[i].y - wardClearance);
            }
            if (minX > maxX || minZ > maxZ)
                return false;

            bool found = false;
            long bestDistanceSquared = long.MaxValue;
            uint bestTieBreak = 0u;
            for (int z = minZ; z <= maxZ; z += stride)
            for (int x = minX; x <= maxX; x += stride)
            {
                int2 candidate = new int2(x, z);
                if (!WellFits(in plan, perimeter, in gate, keepCentre, candidate))
                    continue;

                long dx = (long)x - keepCentre.x;
                long dz = (long)z - keepCentre.y;
                long distanceSquared = dx * dx + dz * dz;
                uint elementId = unchecked(
                    (uint)x * 73856093u ^ (uint)z * 19349663u ^ 0x57454C4Cu);
                uint tieBreak = CastleSeedPartition.Derive(
                    plan.Seed, CastleSeedDomain.Decor, elementId);

                if (found && distanceSquared > bestDistanceSquared)
                    continue;
                if (found && distanceSquared == bestDistanceSquared && tieBreak <= bestTieBreak)
                    continue;

                found = true;
                well = candidate;
                bestDistanceSquared = distanceSquared;
                bestTieBreak = tieBreak;
            }

            return found;
        }

        private static bool TryDeriveInnerGate(
            int2[] perimeter,
            in CastleGatePlacementSpec primaryGate,
            out CastleGatePlacementSpec innerGate)
        {
            innerGate = default;
            int edge = primaryGate.EdgeIndex;
            if (perimeter == null || edge < 0 || edge >= perimeter.Length)
                return false;

            int2 a = perimeter[edge];
            int2 b = perimeter[(edge + 1) % perimeter.Length];
            int2 centre = new int2((a.x + b.x) / 2, (a.y + b.y) / 2);
            if (centre.Equals(primaryGate.Centre))
                return false;

            innerGate = primaryGate;
            innerGate.Centre = centre;
            return true;
        }

        private static int WardBoundaryClearance(in CastlePlan plan) =>
            WellClearanceRadius;

        private static int2 Round(float2 value) =>
            new int2((int)math.round(value.x), (int)math.round(value.y));
    }
}

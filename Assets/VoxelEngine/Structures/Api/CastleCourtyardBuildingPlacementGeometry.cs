using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Pure placement rules for the small service buildings that occupy the outer courtyard.
    /// The planner owns footprints, entrances, and roof axes; Runtime only realizes these specs.
    /// </summary>
    internal static class CastleCourtyardBuildingPlacementGeometry
    {
        internal const int DesiredBuildingCount = 3;
        private const int BuildingClearance = 14;
        private const int KeepClearance = 20;
        private const int GateClearance = 76;
        private const int WellClearance = 34;
        private const int SearchStride = 12;

        internal static CastleCourtyardBuildingSpec[] Plan(
            in CastlePlan plan,
            int2[] outerWard,
            int2[] innerWard,
            in CastleGatePlacementSpec primaryGate,
            bool hasPosternGate,
            in CastleGatePlacementSpec posternGate,
            bool hasInnerGate,
            in CastleGatePlacementSpec innerGate,
            int2 keepCentre,
            bool hasWell,
            int2 wellCentre)
        {
            if (outerWard == null || outerWard.Length < 3)
                return Array.Empty<CastleCourtyardBuildingSpec>();

            Bounds(outerWard, out int minX, out int maxX, out int minZ, out int maxZ);
            float2 outward = primaryGate.Outward;
            float outwardLength = math.length(outward);
            outward = outwardLength > 0.001f ? outward / outwardLength : new float2(0f, -1f);
            float2 rearward = -outward;
            float2 tangent = new float2(-outward.y, outward.x);
            float lateralSpacing = math.clamp(
                math.min(plan.BaileyHalfX, plan.BaileyHalfZ) * 0.38f,
                72f,
                118f);

            var buildings = new List<CastleCourtyardBuildingSpec>(DesiredBuildingCount);
            for (int id = 0; id < DesiredBuildingCount; id++)
            {
                var rng = new Random(CastleSeedPartition.Derive(
                    plan.Seed, CastleSeedDomain.Layout, (uint)(0x7100 + id)));
                int2 halfExtents = new int2(
                    rng.NextInt(34, 47),
                    rng.NextInt(28, 39));
                int height = rng.NextInt(56, 77);
                bool roofRidgeAlongX = rng.NextInt(0, 2) == 0;
                float targetLateral = (id - 1) * lateralSpacing;

                bool found = false;
                int2 best = default;
                float bestScore = float.MinValue;

                for (int z = minZ; z <= maxZ; z += SearchStride)
                for (int x = minX; x <= maxX; x += SearchStride)
                {
                    var candidate = new int2(x, z);
                    if (!BuildingFits(
                            in plan,
                            outerWard,
                            innerWard,
                            in primaryGate,
                            hasPosternGate,
                            in posternGate,
                            hasInnerGate,
                            in innerGate,
                            keepCentre,
                            hasWell,
                            wellCentre,
                            candidate,
                            halfExtents,
                            buildings))
                        continue;

                    float2 point = new float2(candidate.x, candidate.y);
                    float rear = math.dot(point, rearward);
                    float lateral = math.dot(point, tangent);
                    float score = rear * 1000f - math.abs(lateral - targetLateral) * 12f;
                    if (score <= bestScore) continue;

                    found = true;
                    best = candidate;
                    bestScore = score;
                }

                if (!found)
                    continue;

                buildings.Add(new CastleCourtyardBuildingSpec
                {
                    Id = id,
                    Role = CastleCourtyardBuildingRole.Service,
                    Centre = best,
                    HalfExtents = halfExtents,
                    Height = height,
                    EntranceDirection = CardinalDirection(keepCentre - best),
                    RoofRidgeAlongX = roofRidgeAlongX,
                });
            }

            return buildings.ToArray();
        }

        internal static bool BuildingFits(
            in CastlePlan plan,
            int2[] outerWard,
            int2[] innerWard,
            in CastleGatePlacementSpec primaryGate,
            bool hasPosternGate,
            in CastleGatePlacementSpec posternGate,
            bool hasInnerGate,
            in CastleGatePlacementSpec innerGate,
            int2 keepCentre,
            bool hasWell,
            int2 wellCentre,
            int2 centre,
            int2 halfExtents,
            IReadOnlyList<CastleCourtyardBuildingSpec> existing)
        {
            if (halfExtents.x <= 0 || halfExtents.y <= 0)
                return false;

            int wallClearance = math.max(10, plan.WallThickness / 2 + 10);
            int2 expanded = halfExtents + wallClearance;
            if (!RectangleFitsInsidePolygon(centre, expanded, outerWard))
                return false;

            if (innerWard != null && innerWard.Length >= 3 &&
                RectangleTouchesPolygon(centre, expanded, innerWard))
                return false;

            if (RectanglesOverlap(
                    centre,
                    halfExtents + KeepClearance,
                    keepCentre,
                    new int2(plan.KeepHalfX, plan.KeepHalfZ)))
                return false;

            if (PointTooCloseToRectangle(primaryGate.Centre, centre, halfExtents, GateClearance))
                return false;
            if (hasPosternGate &&
                PointTooCloseToRectangle(posternGate.Centre, centre, halfExtents, GateClearance))
                return false;
            if (hasInnerGate &&
                PointTooCloseToRectangle(innerGate.Centre, centre, halfExtents, GateClearance))
                return false;
            if (hasWell &&
                PointTooCloseToRectangle(wellCentre, centre, halfExtents, WellClearance))
                return false;

            if (existing != null)
            {
                for (int i = 0; i < existing.Count; i++)
                {
                    CastleCourtyardBuildingSpec other = existing[i];
                    if (RectanglesOverlap(
                            centre,
                            halfExtents + BuildingClearance,
                            other.Centre,
                            other.HalfExtents))
                        return false;
                }
            }

            return true;
        }

        private static int2 CardinalDirection(int2 delta)
        {
            if (math.abs(delta.x) >= math.abs(delta.y))
                return new int2(delta.x < 0 ? -1 : 1, 0);
            return new int2(0, delta.y < 0 ? -1 : 1);
        }

        private static void Bounds(
            int2[] polygon,
            out int minX,
            out int maxX,
            out int minZ,
            out int maxZ)
        {
            minX = maxX = polygon[0].x;
            minZ = maxZ = polygon[0].y;
            for (int i = 1; i < polygon.Length; i++)
            {
                minX = math.min(minX, polygon[i].x);
                maxX = math.max(maxX, polygon[i].x);
                minZ = math.min(minZ, polygon[i].y);
                maxZ = math.max(maxZ, polygon[i].y);
            }
        }

        private static bool RectangleFitsInsidePolygon(
            int2 centre,
            int2 halfExtents,
            int2[] polygon)
        {
            if (polygon == null || polygon.Length < 3)
                return false;

            int2[] corners = RectangleCorners(centre, halfExtents);
            for (int i = 0; i < corners.Length; i++)
            {
                if (!CastlePolygonGeometry.ContainsPoint(corners[i], polygon))
                    return false;
            }

            // Concave polygons can contain all four rectangle corners while an indentation cuts
            // through the footprint. Reject both embedded polygon vertices and edge crossings.
            for (int i = 0; i < polygon.Length; i++)
            {
                if (PointInsideRectangleStrict(polygon[i], centre, halfExtents))
                    return false;

                int2 next = polygon[(i + 1) % polygon.Length];
                for (int edge = 0; edge < corners.Length; edge++)
                {
                    if (SegmentsProperlyIntersect(
                            polygon[i], next, corners[edge], corners[(edge + 1) % corners.Length]))
                        return false;
                }
            }

            return true;
        }

        private static bool RectangleTouchesPolygon(
            int2 centre,
            int2 halfExtents,
            int2[] polygon)
        {
            int2[] corners = RectangleCorners(centre, halfExtents);
            for (int i = 0; i < corners.Length; i++)
            {
                if (CastlePolygonGeometry.ContainsPoint(corners[i], polygon))
                    return true;
            }

            for (int i = 0; i < polygon.Length; i++)
            {
                if (PointInsideRectangleInclusive(polygon[i], centre, halfExtents))
                    return true;

                int2 next = polygon[(i + 1) % polygon.Length];
                for (int edge = 0; edge < corners.Length; edge++)
                {
                    if (SegmentsIntersect(
                            polygon[i], next, corners[edge], corners[(edge + 1) % corners.Length]))
                        return true;
                }
            }

            return false;
        }

        private static int2[] RectangleCorners(int2 centre, int2 halfExtents) =>
            new[]
            {
                centre + new int2(-halfExtents.x, -halfExtents.y),
                centre + new int2( halfExtents.x, -halfExtents.y),
                centre + new int2( halfExtents.x,  halfExtents.y),
                centre + new int2(-halfExtents.x,  halfExtents.y),
            };

        private static bool RectanglesOverlap(
            int2 aCentre,
            int2 aHalf,
            int2 bCentre,
            int2 bHalf) =>
            math.abs(aCentre.x - bCentre.x) <= aHalf.x + bHalf.x &&
            math.abs(aCentre.y - bCentre.y) <= aHalf.y + bHalf.y;

        private static bool PointTooCloseToRectangle(
            int2 point,
            int2 centre,
            int2 halfExtents,
            int clearance)
        {
            int dx = math.max(0, math.abs(point.x - centre.x) - halfExtents.x);
            int dz = math.max(0, math.abs(point.y - centre.y) - halfExtents.y);
            return (long)dx * dx + (long)dz * dz < (long)clearance * clearance;
        }

        private static bool PointInsideRectangleStrict(int2 point, int2 centre, int2 halfExtents) =>
            math.abs(point.x - centre.x) < halfExtents.x &&
            math.abs(point.y - centre.y) < halfExtents.y;

        private static bool PointInsideRectangleInclusive(int2 point, int2 centre, int2 halfExtents) =>
            math.abs(point.x - centre.x) <= halfExtents.x &&
            math.abs(point.y - centre.y) <= halfExtents.y;

        private static bool SegmentsProperlyIntersect(int2 a, int2 b, int2 c, int2 d)
        {
            long abC = Orient(a, b, c);
            long abD = Orient(a, b, d);
            long cdA = Orient(c, d, a);
            long cdB = Orient(c, d, b);
            return OppositeSigns(abC, abD) && OppositeSigns(cdA, cdB);
        }

        private static bool SegmentsIntersect(int2 a, int2 b, int2 c, int2 d)
        {
            long abC = Orient(a, b, c);
            long abD = Orient(a, b, d);
            long cdA = Orient(c, d, a);
            long cdB = Orient(c, d, b);
            if (OppositeSigns(abC, abD) && OppositeSigns(cdA, cdB))
                return true;

            return (abC == 0 && CastlePolygonGeometry.PointOnSegment(c, a, b)) ||
                   (abD == 0 && CastlePolygonGeometry.PointOnSegment(d, a, b)) ||
                   (cdA == 0 && CastlePolygonGeometry.PointOnSegment(a, c, d)) ||
                   (cdB == 0 && CastlePolygonGeometry.PointOnSegment(b, c, d));
        }

        private static bool OppositeSigns(long a, long b) =>
            (a < 0 && b > 0) || (a > 0 && b < 0);

        private static long Orient(int2 a, int2 b, int2 c) =>
            (long)(b.x - a.x) * (c.y - a.y) -
            (long)(b.y - a.y) * (c.x - a.x);
    }
}

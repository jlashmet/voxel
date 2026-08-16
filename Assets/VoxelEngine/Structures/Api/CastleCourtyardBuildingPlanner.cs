using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    public enum CastleCourtyardBuildingPurpose : byte
    {
        Barracks,
        Stables,
        Stores,
    }

    /// <summary>
    /// One planner-owned courtyard building footprint. Coordinates are local X/Z relative to the
    /// castle centre; Tangent follows the supporting wall and Inward points into the ward.
    /// </summary>
    public struct CastleCourtyardBuildingSpec
    {
        public int Id;
        public CastleCourtyardBuildingPurpose Purpose;
        public int WallEdgeIndex;
        public int2 Centre;
        public float2 Tangent;
        public float2 Inward;
        public int Width;
        public int Depth;
        public int Height;

        public int2 FootprintCorner(int index)
        {
            float along = (index == 0 || index == 3) ? -Width * 0.5f : Width * 0.5f;
            float inward = (index == 0 || index == 1) ? -Depth * 0.5f : Depth * 0.5f;
            float2 point = new float2(Centre.x, Centre.y)
                         + Tangent * along
                         + Inward * inward;
            return Round(point);
        }

        /// <summary>Door centre on the courtyard-facing long side.</summary>
        public int2 DoorCentre => Round(
            new float2(Centre.x, Centre.y) + Inward * (Depth * 0.5f));

        private static int2 Round(float2 point) =>
            new int2((int)math.round(point.x), (int)math.round(point.y));
    }

    /// <summary>
    /// Pure semantic placement for secondary buildings in the outer ward. Runtime receives these
    /// footprints and never decides which wall is the rear, which side should hold stables, or how
    /// far a building must stay from the keep, gates, and well.
    /// </summary>
    public static class CastleCourtyardBuildingPlanner
    {
        private const int WallClearance = 16;
        private const int BuildingClearance = 16;
        private const int PrimaryGateClearance = 76;
        private const int PosternClearance = 48;
        private const int WellClearance = 28;
        private const int KeepClearance = 20;

        public static CastleCourtyardBuildingSpec[] Create(
            in CastlePlan plan,
            CastleSpatialPlan spatial)
        {
            if (spatial == null) throw new ArgumentNullException(nameof(spatial));
            if (spatial.KeepRequiresTerrainResolution)
                return Array.Empty<CastleCourtyardBuildingSpec>();
            if (spatial.OuterWardVertices == null || spatial.OuterWardVertices.Length < 3)
                return Array.Empty<CastleCourtyardBuildingSpec>();

            var result = new List<CastleCourtyardBuildingSpec>(3);
            TryAdd(in plan, spatial, CastleCourtyardBuildingPurpose.Stables, result);
            TryAdd(in plan, spatial, CastleCourtyardBuildingPurpose.Barracks, result);
            TryAdd(in plan, spatial, CastleCourtyardBuildingPurpose.Stores, result);

            for (int i = 0; i < result.Count; i++)
            {
                CastleCourtyardBuildingSpec item = result[i];
                item.Id = i;
                result[i] = item;
            }
            return result.ToArray();
        }

        private static void TryAdd(
            in CastlePlan plan,
            CastleSpatialPlan spatial,
            CastleCourtyardBuildingPurpose purpose,
            List<CastleCourtyardBuildingSpec> placed)
        {
            Dimensions(purpose, out int width, out int depth, out int height);
            int2[] perimeter = spatial.OuterWardVertices;
            float2 centroid = Centroid(perimeter);

            bool found = false;
            CastleCourtyardBuildingSpec best = default;
            double bestScore = double.MaxValue;

            for (int edge = 0; edge < perimeter.Length; edge++)
            {
                if (edge == spatial.PrimaryGate.EdgeIndex)
                    continue;
                if (spatial.HasPosternGate && edge == spatial.PosternGate.EdgeIndex)
                    continue;

                int2 a = perimeter[edge];
                int2 b = perimeter[(edge + 1) % perimeter.Length];
                float2 delta = new float2(b.x - a.x, b.y - a.y);
                float length = math.length(delta);
                if (length < width + 24f)
                    continue;

                float2 tangent = delta / length;
                float2 midpoint = new float2((a.x + b.x) * 0.5f, (a.y + b.y) * 0.5f);
                float2 outward = new float2(tangent.y, -tangent.x);
                if (math.dot(outward, midpoint - centroid) < 0f)
                    outward = -outward;
                float2 inward = -outward;

                // Trying several positions along a long wall lets the semantic preference win
                // without forcing a building into a keep, gate corridor, or concave indentation.
                float[] positions = { 0.32f, 0.5f, 0.68f };
                for (int positionIndex = 0; positionIndex < positions.Length; positionIndex++)
                {
                    float2 onWall = new float2(a.x, a.y) + delta * positions[positionIndex];
                    float inwardDistance = plan.WallThickness * 0.5f
                                          + depth * 0.5f
                                          + WallClearance;
                    int2 centre = Round(onWall + inward * inwardDistance);
                    var candidate = new CastleCourtyardBuildingSpec
                    {
                        Purpose = purpose,
                        WallEdgeIndex = edge,
                        Centre = centre,
                        Tangent = tangent,
                        Inward = inward,
                        Width = width,
                        Depth = depth,
                        Height = height,
                    };

                    if (!Fits(in plan, spatial, in candidate, placed))
                        continue;

                    double score = SemanticScore(in plan, spatial, purpose, in candidate,
                                                 positionIndex);
                    if (found && score >= bestScore)
                        continue;

                    found = true;
                    best = candidate;
                    bestScore = score;
                }
            }

            if (found)
                placed.Add(best);
        }

        private static bool Fits(
            in CastlePlan plan,
            CastleSpatialPlan spatial,
            in CastleCourtyardBuildingSpec candidate,
            List<CastleCourtyardBuildingSpec> placed)
        {
            int2[] outer = spatial.OuterWardVertices;
            int2[] inner = spatial.InnerWardVertices;

            // Corners, edge midpoints, and centre all remain in the outer ward. Sampling the edge
            // midpoints also rejects most concave-wall cuts that corner-only containment misses.
            int2[] samples = FootprintSamples(in candidate);
            for (int i = 0; i < samples.Length; i++)
            {
                if (!CastlePolygonGeometry.ContainsPoint(samples[i], outer))
                    return false;
                if (inner != null && inner.Length >= 3 &&
                    CastlePolygonGeometry.ContainsPoint(samples[i], inner))
                    return false;
            }

            Bounds(in candidate, BuildingClearance,
                   out int minX, out int maxX, out int minZ, out int maxZ);
            int keepMinX = spatial.KeepCentre.x - plan.KeepHalfX - KeepClearance;
            int keepMaxX = spatial.KeepCentre.x + plan.KeepHalfX + KeepClearance;
            int keepMinZ = spatial.KeepCentre.y - plan.KeepHalfZ - KeepClearance;
            int keepMaxZ = spatial.KeepCentre.y + plan.KeepHalfZ + KeepClearance;
            if (Overlaps(minX, maxX, minZ, maxZ,
                         keepMinX, keepMaxX, keepMinZ, keepMaxZ))
                return false;

            if (PointDistanceSquared(in candidate, spatial.PrimaryGate.Centre)
                < PrimaryGateClearance * PrimaryGateClearance)
                return false;
            if (spatial.HasPosternGate &&
                PointDistanceSquared(in candidate, spatial.PosternGate.Centre)
                < PosternClearance * PosternClearance)
                return false;
            if (spatial.HasWell &&
                PointDistanceSquared(in candidate, spatial.WellCentre)
                < WellClearance * WellClearance)
                return false;

            for (int i = 0; i < placed.Count; i++)
            {
                Bounds(in placed[i], BuildingClearance,
                       out int otherMinX, out int otherMaxX,
                       out int otherMinZ, out int otherMaxZ);
                if (Overlaps(minX, maxX, minZ, maxZ,
                             otherMinX, otherMaxX, otherMinZ, otherMaxZ))
                    return false;
            }

            return true;
        }

        private static double SemanticScore(
            in CastlePlan plan,
            CastleSpatialPlan spatial,
            CastleCourtyardBuildingPurpose purpose,
            in CastleCourtyardBuildingSpec candidate,
            int positionIndex)
        {
            int2 target = purpose == CastleCourtyardBuildingPurpose.Stables
                ? spatial.PrimaryGate.Centre
                : spatial.KeepCentre;
            long dx = (long)candidate.Centre.x - target.x;
            long dz = (long)candidate.Centre.y - target.y;
            double distance = dx * (double)dx + dz * (double)dz;

            uint elementId = (uint)(0xB100 + (int)purpose * 128
                                    + candidate.WallEdgeIndex * 8 + positionIndex);
            uint tie = CastleSeedPartition.Derive(
                plan.Seed, CastleSeedDomain.Layout, elementId);

            if (purpose == CastleCourtyardBuildingPurpose.Barracks)
                return tie;
            return distance * 65536.0 + (tie & 0xFFFFu);
        }

        private static int2[] FootprintSamples(in CastleCourtyardBuildingSpec spec)
        {
            int2 c0 = spec.FootprintCorner(0);
            int2 c1 = spec.FootprintCorner(1);
            int2 c2 = spec.FootprintCorner(2);
            int2 c3 = spec.FootprintCorner(3);
            return new[]
            {
                spec.Centre,
                c0, c1, c2, c3,
                Midpoint(c0, c1), Midpoint(c1, c2),
                Midpoint(c2, c3), Midpoint(c3, c0),
            };
        }

        private static void Bounds(
            in CastleCourtyardBuildingSpec spec,
            int padding,
            out int minX,
            out int maxX,
            out int minZ,
            out int maxZ)
        {
            int2 first = spec.FootprintCorner(0);
            minX = maxX = first.x;
            minZ = maxZ = first.y;
            for (int i = 1; i < 4; i++)
            {
                int2 corner = spec.FootprintCorner(i);
                minX = math.min(minX, corner.x);
                maxX = math.max(maxX, corner.x);
                minZ = math.min(minZ, corner.y);
                maxZ = math.max(maxZ, corner.y);
            }
            minX -= padding;
            maxX += padding;
            minZ -= padding;
            maxZ += padding;
        }

        private static long PointDistanceSquared(
            in CastleCourtyardBuildingSpec spec,
            int2 point)
        {
            float2 delta = new float2(point.x - spec.Centre.x, point.y - spec.Centre.y);
            float along = math.max(0f, math.abs(math.dot(delta, spec.Tangent)) - spec.Width * 0.5f);
            float inward = math.max(0f, math.abs(math.dot(delta, spec.Inward)) - spec.Depth * 0.5f);
            return (long)math.round(along * along + inward * inward);
        }

        private static bool Overlaps(
            int minX, int maxX, int minZ, int maxZ,
            int otherMinX, int otherMaxX, int otherMinZ, int otherMaxZ) =>
            minX <= otherMaxX && maxX >= otherMinX &&
            minZ <= otherMaxZ && maxZ >= otherMinZ;

        private static float2 Centroid(int2[] perimeter)
        {
            float2 centroid = float2.zero;
            for (int i = 0; i < perimeter.Length; i++)
                centroid += new float2(perimeter[i].x, perimeter[i].y);
            return centroid / perimeter.Length;
        }

        private static int2 Midpoint(int2 a, int2 b) =>
            new int2((a.x + b.x) / 2, (a.y + b.y) / 2);

        private static int2 Round(float2 point) =>
            new int2((int)math.round(point.x), (int)math.round(point.y));

        private static void Dimensions(
            CastleCourtyardBuildingPurpose purpose,
            out int width,
            out int depth,
            out int height)
        {
            switch (purpose)
            {
                case CastleCourtyardBuildingPurpose.Barracks:
                    width = 96; depth = 58; height = 64; return;
                case CastleCourtyardBuildingPurpose.Stables:
                    width = 86; depth = 54; height = 50; return;
                default:
                    width = 72; depth = 50; height = 48; return;
            }
        }
    }
}

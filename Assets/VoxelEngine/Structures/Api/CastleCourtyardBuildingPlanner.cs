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

        // Independent semantic choices: adding another courtyard purpose later must not reshuffle
        // whether an existing castle asked for stables, barracks, or stores.
        private const uint StablesChoice = 0x53544142u;   // STAB
        private const uint BarracksChoice = 0x42415252u;  // BARR
        private const uint StoresChoice = 0x53544F52u;    // STOR
        private const uint FallbackChoice = 0x59415244u;  // YARD

        public static CastleCourtyardBuildingSpec[] Create(
            in CastlePlan plan,
            CastleSpatialPlan spatial)
        {
            if (spatial == null) throw new ArgumentNullException(nameof(spatial));
            if (spatial.KeepRequiresTerrainResolution)
                return Array.Empty<CastleCourtyardBuildingSpec>();
            if (spatial.OuterWardVertices == null || spatial.OuterWardVertices.Length < 3)
                return Array.Empty<CastleCourtyardBuildingSpec>();

            bool includeStables = ChoosePurpose(plan.Seed, StablesChoice, 85);
            bool includeBarracks = ChoosePurpose(plan.Seed, BarracksChoice, 75);
            bool includeStores = ChoosePurpose(plan.Seed, StoresChoice, 65);
            if (!includeStables && !includeBarracks && !includeStores)
            {
                uint fallback = CastleSeedPartition.Derive(
                    plan.Seed, CastleSeedDomain.Layout, FallbackChoice) % 3u;
                includeStables = fallback == 0u;
                includeBarracks = fallback == 1u;
                includeStores = fallback == 2u;
            }

            var result = new List<CastleCourtyardBuildingSpec>(3);
            if (includeStables)
                TryAdd(in plan, spatial, CastleCourtyardBuildingPurpose.Stables, result);
            if (includeBarracks)
                TryAdd(in plan, spatial, CastleCourtyardBuildingPurpose.Barracks, result);
            if (includeStores)
                TryAdd(in plan, spatial, CastleCourtyardBuildingPurpose.Stores, result);

            for (int i = 0; i < result.Count; i++)
            {
                CastleCourtyardBuildingSpec item = result[i];
                item.Id = i;
                result[i] = item;
            }
            return result.ToArray();
        }

        private static bool ChoosePurpose(uint castleSeed, uint choiceId, uint percent)
        {
            uint choiceSeed = CastleSeedPartition.Derive(
                castleSeed, CastleSeedDomain.Layout, choiceId);
            return choiceSeed % 100u < percent;
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
            int2[] footprint = Footprint(in candidate);

            // Exact edge tests are required here. A concave ward indentation can cross a building
            // between all four corners, four edge midpoints, and the centre, so sampled containment
            // can approve a footprint that actually straddles the curtain wall.
            if (!CastlePolygonGeometry.ContainsPolygon(outer, footprint))
                return false;
            if (inner != null && inner.Length >= 3 &&
                CastlePolygonGeometry.PolygonsOverlapOrTouch(footprint, inner))
                return false;

            // Optional outer wall towers occupy edge midpoints, exactly where a wall-supported
            // building may otherwise score best. Clear the complete oriented footprint from every
            // planned outer tower rather than treating the curtain line itself as the only obstacle.
            CastleTowerPlacementSpec[] outerTowers = spatial.Towers;
            if (outerTowers != null && outerTowers.Length != 0)
            {
                int outerTowerClearance = plan.TowerRadius + BuildingClearance;
                long outerTowerClearanceSquared =
                    (long)outerTowerClearance * outerTowerClearance;
                for (int i = 0; i < outerTowers.Length; i++)
                {
                    if (PointDistanceSquared(in candidate, outerTowers[i].Centre)
                        < outerTowerClearanceSquared)
                        return false;
                }
            }

            // Inner towers occupy the corners of the secondary ring. A building that stays just
            // outside the inner polygon can still intersect a tower's circular footprint, so keep
            // the full planned footprint plus ordinary building clearance away from every corner.
            if (inner != null && inner.Length >= 3)
            {
                int innerTowerClearance = CastleInnerWardTowerPlanner.Radius(in plan)
                                        + BuildingClearance;
                long innerTowerClearanceSquared =
                    (long)innerTowerClearance * innerTowerClearance;
                for (int i = 0; i < inner.Length; i++)
                {
                    if (PointDistanceSquared(in candidate, inner[i]) < innerTowerClearanceSquared)
                        return false;
                }
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

            CastleAccessRoute access = CastleAccessRoute.Create(in plan, spatial);
            if (!access.ClearsBuilding(in candidate))
                return false;

            for (int i = 0; i < placed.Count; i++)
            {
                CastleCourtyardBuildingSpec other = placed[i];
                Bounds(in other, BuildingClearance,
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

        private static int2[] Footprint(in CastleCourtyardBuildingSpec spec) =>
            new[]
            {
                spec.FootprintCorner(0),
                spec.FootprintCorner(1),
                spec.FootprintCorner(2),
                spec.FootprintCorner(3),
            };

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

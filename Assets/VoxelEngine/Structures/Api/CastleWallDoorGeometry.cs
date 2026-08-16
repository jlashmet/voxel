using System.Collections.Generic;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Pure thick-segment footprint shared by runtime rasterization and application-side geometry
    /// queries. Keeping the capsule test here prevents interaction masks from drifting from voxels
    /// authored by VoxelWallRasterizer.
    /// </summary>
    public static class CastleSegmentFootprint
    {
        public static void Bounds(
            int2 start,
            int2 end,
            int thickness,
            out int2 min,
            out int2 max)
        {
            float radius = math.max(0.5f, thickness * 0.5f);
            min = new int2(
                (int)math.floor(math.min(start.x, end.x) - radius),
                (int)math.floor(math.min(start.y, end.y) - radius));
            max = new int2(
                (int)math.ceil(math.max(start.x, end.x) + radius),
                (int)math.ceil(math.max(start.y, end.y) + radius));
        }

        public static bool Contains(
            int2 point,
            int2 start,
            int2 end,
            int thickness)
        {
            if (thickness <= 0) return false;

            float2 a = new float2(start.x, start.y);
            float2 b = new float2(end.x, end.y);
            float2 p = new float2(point.x, point.y);
            float2 delta = b - a;
            float lengthSquared = math.lengthsq(delta);
            float along = lengthSquared > 0.0001f
                ? math.saturate(math.dot(p - a, delta) / lengthSquared)
                : 0f;
            float2 nearest = a + delta * along;
            float radius = math.max(0.5f, thickness * 0.5f);
            return math.lengthsq(p - nearest) <= radius * radius;
        }
    }

    /// <summary>
    /// Pure world-space geometry for a planned arched curtain-wall door. Runtime realization and
    /// interaction consume the same row spans, depth footprint, and iron-strap expansion.
    /// </summary>
    public readonly struct CastleWallDoorGeometry
    {
        public readonly CastleApproachFrame Frame;
        public readonly int2 PlanWorldCentre;
        public readonly int BaseY;
        public readonly int OpeningDepth;
        public readonly CastleWallDoorPlan Door;

        internal CastleWallDoorGeometry(
            in CastleApproachFrame frame,
            int2 planWorldCentre,
            int baseY,
            int openingDepth,
            in CastleWallDoorPlan door)
        {
            Frame = frame;
            PlanWorldCentre = planWorldCentre;
            BaseY = baseY;
            OpeningDepth = openingDepth;
            Door = door;
        }

        public int LeafWidth => Door.Width - Door.LeafWidthReduction;
        public int LeafHeight => Door.Height - Door.LeafHeightReduction;

        public bool TryGetOpeningRowSpan(int row, out int minOffset, out int maxOffset) =>
            TryGetArchRowSpan(Door.Width, Door.Height, row, out minOffset, out maxOffset);

        public bool TryGetLeafRowSpan(int row, out int minOffset, out int maxOffset) =>
            TryGetArchRowSpan(LeafWidth, LeafHeight, row, out minOffset, out maxOffset);

        public int2 WorldPoint(int tangentOffset)
        {
            int2 local = Frame.LocalPoint(tangentOffset, 0f);
            return PlanWorldCentre + local;
        }

        /// <summary>Player-facing point eight voxels outside the secondary door.</summary>
        public float3 InteractionPointVoxels
        {
            get
            {
                float2 worldGate = new float2(
                    PlanWorldCentre.x + Frame.GateCentre.x,
                    PlanWorldCentre.y + Frame.GateCentre.y);
                float2 point = worldGate + Frame.Outward * 8f;
                return new float3(point.x, BaseY, point.y);
            }
        }

        /// <summary>
        /// Enumerates exactly the authored leaf footprint, including the one-voxel-deeper iron
        /// straps. Intended for infrequent authored interaction, not hot realization loops.
        /// </summary>
        public int3[] LeafVoxels()
        {
            var voxels = new List<int3>(LeafWidth * LeafHeight * math.max(1, Door.Depth));
            for (int row = 0; row < LeafHeight; row++)
            {
                if (!TryGetLeafRowSpan(row, out int minOffset, out int maxOffset))
                    continue;

                int2 start = WorldPoint(minOffset);
                int2 end = WorldPoint(maxOffset);
                int depth = Door.Depth + (IsStrapRow(row) ? Door.StrapDepthExtra : 0);
                CastleSegmentFootprint.Bounds(start, end, depth, out int2 min, out int2 max);
                for (int z = min.y; z <= max.y; z++)
                for (int x = min.x; x <= max.x; x++)
                {
                    var point = new int2(x, z);
                    if (!CastleSegmentFootprint.Contains(point, start, end, depth))
                        continue;
                    voxels.Add(new int3(x, BaseY + row, z));
                }
            }
            return voxels.ToArray();
        }

        private bool IsStrapRow(int row)
        {
            if (row < Door.StrapFirstY) return false;
            int relative = row - Door.StrapFirstY;
            return relative % Door.StrapSpacing < Door.StrapThickness;
        }

        private static bool TryGetArchRowSpan(
            int width,
            int height,
            int row,
            out int minOffset,
            out int maxOffset)
        {
            minOffset = 0;
            maxOffset = -1;
            if (width <= 0 || height <= 0 || row < 0 || row >= height)
                return false;

            int half = width / 2;
            minOffset = -half;
            maxOffset = width - half - 1;
            int archBase = height - half;
            if (row <= archBase)
                return true;

            int dy = row - archBase;
            int radiusSquared = half * half;
            while (minOffset <= maxOffset &&
                   minOffset * minOffset + dy * dy > radiusSquared)
                minOffset++;
            while (maxOffset >= minOffset &&
                   maxOffset * maxOffset + dy * dy > radiusSquared)
                maxOffset--;
            return minOffset <= maxOffset;
        }
    }

    public static class CastleWallDoorGeometryResolver
    {
        public static CastleWallDoorGeometry Resolve(
            in CastlePlan plan,
            in CastleGatePlacementSpec gate,
            in CastleWallDoorPlan door)
        {
            CastleWallDoorPlanValidator.RequireValid(in door);
            CastleApproachFrame frame = CastleApproachFrame.FromGate(in gate);
            int openingDepth = math.max(1, plan.WallThickness + door.OpeningDepthExtra);
            return new CastleWallDoorGeometry(
                in frame,
                new int2(plan.Centre.x, plan.Centre.z),
                plan.Centre.y + plan.PlateauHeight + 1,
                openingDepth,
                in door);
        }
    }
}

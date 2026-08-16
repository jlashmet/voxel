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
    /// Pure world-space geometry for the current planned postern recipe. Runtime realization and
    /// application interaction consume the same rotated segment endpoints and depth footprint.
    /// </summary>
    public readonly struct CastlePosternGeometry
    {
        public readonly CastleApproachFrame Frame;
        public readonly int2 PlanWorldCentre;
        public readonly int BaseY;
        public readonly int2 OpeningStart;
        public readonly int2 OpeningEnd;
        public readonly int OpeningHeight;
        public readonly int OpeningDepth;
        public readonly int2 LeafStart;
        public readonly int2 LeafEnd;
        public readonly int LeafHeight;
        public readonly int LeafDepth;
        public readonly int FirstStrapY;
        public readonly int SecondStrapY;
        public readonly int StrapHeight;
        public readonly int StrapDepth;

        internal CastlePosternGeometry(
            in CastleApproachFrame frame,
            int2 planWorldCentre,
            int baseY,
            int2 openingStart,
            int2 openingEnd,
            int openingHeight,
            int openingDepth,
            int2 leafStart,
            int2 leafEnd,
            int leafHeight,
            int leafDepth,
            int firstStrapY,
            int secondStrapY,
            int strapHeight,
            int strapDepth)
        {
            Frame = frame;
            PlanWorldCentre = planWorldCentre;
            BaseY = baseY;
            OpeningStart = openingStart;
            OpeningEnd = openingEnd;
            OpeningHeight = openingHeight;
            OpeningDepth = openingDepth;
            LeafStart = leafStart;
            LeafEnd = leafEnd;
            LeafHeight = leafHeight;
            LeafDepth = leafDepth;
            FirstStrapY = firstStrapY;
            SecondStrapY = secondStrapY;
            StrapHeight = strapHeight;
            StrapDepth = strapDepth;
        }

        /// <summary>Player-facing point eight voxels outside the postern.</summary>
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
        /// Enumerates exactly the authored wooden leaf plus its deeper iron straps. This path is
        /// used only for authored interaction, so a compact allocation is preferable to a second
        /// mutation-specific geometry implementation in Composition.
        /// </summary>
        public int3[] LeafVoxels()
        {
            var voxels = new HashSet<int3>();
            AddSegmentVolume(voxels, LeafStart, LeafEnd, BaseY, LeafHeight, LeafDepth);
            AddSegmentVolume(
                voxels, LeafStart, LeafEnd, FirstStrapY, StrapHeight, StrapDepth);
            AddSegmentVolume(
                voxels, LeafStart, LeafEnd, SecondStrapY, StrapHeight, StrapDepth);

            var result = new int3[voxels.Count];
            voxels.CopyTo(result);
            return result;
        }

        private static void AddSegmentVolume(
            HashSet<int3> output,
            int2 start,
            int2 end,
            int baseY,
            int height,
            int thickness)
        {
            if (height <= 0 || thickness <= 0) return;
            CastleSegmentFootprint.Bounds(start, end, thickness, out int2 min, out int2 max);
            for (int z = min.y; z <= max.y; z++)
            for (int x = min.x; x <= max.x; x++)
            {
                var point = new int2(x, z);
                if (!CastleSegmentFootprint.Contains(point, start, end, thickness))
                    continue;
                for (int y = baseY; y < baseY + height; y++)
                    output.Add(new int3(x, y, z));
            }
        }
    }

    public static class CastlePosternGeometryResolver
    {
        public static CastlePosternGeometry Resolve(
            in CastlePlan plan,
            in CastleGatePlacementSpec postern)
        {
            CastleApproachFrame frame = CastleApproachFrame.FromGate(in postern);
            int2 planWorldCentre = new int2(plan.Centre.x, plan.Centre.z);
            int baseY = plan.Centre.y + plan.PlateauHeight + 1;

            int openingHalf = CastleLayout.PosternGateWidth / 2;
            int2 openingStart = planWorldCentre + frame.LocalPoint(-openingHalf, 0f);
            int2 openingEnd = planWorldCentre + frame.LocalPoint(openingHalf, 0f);

            int leafHalf = CastleLayout.PosternGateWidth / 2 - 2;
            int2 leafStart = planWorldCentre + frame.LocalPoint(-leafHalf, 0f);
            int2 leafEnd = planWorldCentre + frame.LocalPoint(leafHalf, 0f);
            int leafHeight = CastleLayout.PosternGateHeight - 4;

            return new CastlePosternGeometry(
                in frame,
                planWorldCentre,
                baseY,
                openingStart,
                openingEnd,
                CastleLayout.PosternGateHeight,
                plan.WallThickness + 4,
                leafStart,
                leafEnd,
                leafHeight,
                CastleLayout.PosternGateDepth,
                baseY + 10,
                baseY + 24,
                2,
                CastleLayout.PosternGateDepth + 1);
        }
    }
}

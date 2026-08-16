using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Realizes a closed castle perimeter from local X/Z vertices without making any topology
    /// decisions. Planning owns the polygon; this component owns only the wall geometry placed
    /// along its already-chosen edges.
    /// </summary>
    public static class CastlePerimeterRealizer
    {
        public static void Walls(ref VoxelBrush brush, in CastlePlan plan, int2[] localVertices)
        {
            if (localVertices == null || localVertices.Length < 3)
                return;

            int baseY = plan.Centre.y + plan.PlateauHeight;
            for (int i = 0; i < localVertices.Length; i++)
            {
                int2 localStart = localVertices[i];
                int2 localEnd = localVertices[(i + 1) % localVertices.Length];
                int2 start = new int2(plan.Centre.x + localStart.x, plan.Centre.z + localStart.y);
                int2 end = new int2(plan.Centre.x + localEnd.x, plan.Centre.z + localEnd.y);
                WallSegment(ref brush, in plan, start, end, baseY);
            }
        }

        private static void WallSegment(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int2 start,
            int2 end,
            int baseY)
        {
            int height = plan.WallHeight;
            int thickness = plan.WallThickness;
            if (height <= 0 || thickness <= 0)
                return;

            VoxelWallRasterizer.FillSegment(
                ref brush, start, end, baseY, height, thickness, Mat.Stone);

            int plinthHeight = math.min(22, height);
            VoxelWallRasterizer.FillSegment(
                ref brush, start, end, baseY, plinthHeight, thickness, Mat.DarkStone);

            if (height >= 4)
            {
                int courseY = baseY + (int)(height * 0.66f);
                VoxelWallRasterizer.FillSegment(
                    ref brush, start, end, courseY, 2, thickness, Mat.DarkStone);
            }

            VoxelWallRasterizer.FillSegment(
                ref brush, start, end, baseY + height, 1, thickness, Mat.Stone);

            CarveArrowSlits(ref brush, start, end, baseY, height, thickness);
            Crenellate(ref brush, start, end, baseY + height + 1, thickness);
        }

        private static void CarveArrowSlits(
            ref VoxelBrush brush,
            int2 start,
            int2 end,
            int baseY,
            int wallHeight,
            int wallThickness)
        {
            if (wallHeight < 70)
                return;

            float2 a = new float2(start.x, start.y);
            float2 delta = new float2(end.x - start.x, end.y - start.y);
            float length = math.length(delta);
            if (length < 1f)
                return;

            float2 tangent = delta / length;
            float2 normal = new float2(-tangent.y, tangent.x);
            float halfDepth = math.max(1f, wallThickness * 0.65f);

            for (float distance = 40f; distance < length - 20f; distance += 90f)
            {
                float2 centre = a + tangent * distance;
                float2 slitStart = centre - normal * halfDepth;
                float2 slitEnd = centre + normal * halfDepth;
                VoxelWallRasterizer.FillSegment(
                    ref brush,
                    Round(slitStart),
                    Round(slitEnd),
                    baseY + 40,
                    math.min(28, wallHeight - 40),
                    2,
                    Mat.Empty);
            }
        }

        private static void Crenellate(
            ref VoxelBrush brush,
            int2 start,
            int2 end,
            int parapetY,
            int wallThickness)
        {
            float2 a = new float2(start.x, start.y);
            float2 delta = new float2(end.x - start.x, end.y - start.y);
            float length = math.length(delta);
            if (length < 1f)
                return;

            float2 tangent = delta / length;
            const float merlon = 26f;
            const float gap = 18f;
            float period = merlon + gap;
            int thickness = math.max(2, math.min(8, wallThickness));

            for (float distance = 0f; distance < length; distance += period)
            {
                float endDistance = math.min(length, distance + merlon);
                int2 merlonStart = Round(a + tangent * distance);
                int2 merlonEnd = Round(a + tangent * endDistance);
                VoxelWallRasterizer.FillSegment(
                    ref brush, merlonStart, merlonEnd, parapetY, 20, thickness, Mat.Stone);
            }
        }

        private static int2 Round(float2 value) =>
            new int2((int)math.round(value.x), (int)math.round(value.y));
    }
}

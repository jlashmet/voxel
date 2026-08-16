using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using Random = Unity.Mathematics.Random;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Landscape dressing for spatially planned castles. Unlike the legacy showcase recipe this
    /// component does not invent a waterfall or a fixed rear/front axis; it decorates only the
    /// planned defensive perimeter and primary approach.
    /// </summary>
    internal static class CastleSpatialLandscapeRealizer
    {
        internal static void Build(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int2[] localPerimeter,
            in CastleApproachFrame approach)
        {
            if (localPerimeter == null || localPerimeter.Length < 3)
                return;

            int top = plan.Centre.y + plan.PlateauHeight;
            DressPerimeter(ref brush, in plan, localPerimeter, top);
            DressApproach(ref brush, in plan, in approach, top);
        }

        private static void DressPerimeter(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int2[] perimeter,
            int top)
        {
            float2 centroid = float2.zero;
            for (int i = 0; i < perimeter.Length; i++)
                centroid += new float2(perimeter[i].x, perimeter[i].y);
            centroid /= perimeter.Length;

            for (int edgeIndex = 0; edgeIndex < perimeter.Length; edgeIndex++)
            {
                int2 a = perimeter[edgeIndex];
                int2 b = perimeter[(edgeIndex + 1) % perimeter.Length];
                float2 start = new float2(a.x, a.y);
                float2 delta = new float2(b.x - a.x, b.y - a.y);
                float length = math.length(delta);
                if (length < 1f) continue;

                float2 tangent = delta / length;
                float2 outward = new float2(tangent.y, -tangent.x);
                float2 midpoint = start + delta * 0.5f;
                if (math.dot(outward, midpoint - centroid) < 0f)
                    outward = -outward;

                int samples = math.max(1, (int)math.floor(length / 90f));
                for (int sample = 0; sample < samples; sample++)
                {
                    uint seed = CastleSeedPartition.Derive(
                        plan.Seed,
                        CastleSeedDomain.Decor,
                        (uint)(0x5000 + edgeIndex * 64 + sample));
                    var rng = new Random(seed);
                    float t = (sample + 0.5f) / samples;
                    float2 onWall = start + delta * t;
                    float outsideDistance = plan.WallThickness * 0.5f + rng.NextFloat(8f, 19f);
                    int2 shrubLocal = Round(onWall + outward * outsideDistance);
                    int shrubX = plan.Centre.x + shrubLocal.x;
                    int shrubZ = plan.Centre.z + shrubLocal.y;
                    int shrubY = HighestSolid(ref brush, shrubX, shrubZ, top + 14, top - 100);
                    brush.Cone(
                        shrubX,
                        shrubY + 1,
                        shrubZ,
                        rng.NextInt(3, 7),
                        rng.NextInt(5, 11),
                        (sample & 1) == 0 ? Mat.Moss : Mat.Grass);

                    float rubbleAlong = rng.NextFloat(-12f, 13f);
                    float rubbleOut = outsideDistance + rng.NextFloat(7f, 18f);
                    int2 rubbleLocal = Round(
                        onWall + tangent * rubbleAlong + outward * rubbleOut);
                    int rubbleX = plan.Centre.x + rubbleLocal.x;
                    int rubbleZ = plan.Centre.z + rubbleLocal.y;
                    int rubbleY = HighestSolid(ref brush, rubbleX, rubbleZ, top + 14, top - 100);
                    brush.Box(
                        new int3(rubbleX, rubbleY + 1, rubbleZ),
                        new int3(rng.NextInt(4, 9), rng.NextInt(3, 7), rng.NextInt(4, 9)),
                        (sample % 3) == 0 ? Mat.Stone : Mat.DarkStone);
                }
            }
        }

        private static void DressApproach(
            ref VoxelBrush brush,
            in CastlePlan plan,
            in CastleApproachFrame approach,
            int top)
        {
            // Keep the bridge/road centre clear. Paired rocks and scrub establish the approach
            // direction without assuming that the gate faces a cardinal axis.
            float2[] offsets =
            {
                new float2(-128f, 74f),
                new float2(126f, 82f),
                new float2(-178f, 118f),
                new float2(184f, 126f),
                new float2(-102f, 166f),
                new float2(110f, 174f),
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                int2 local = approach.LocalPoint(offsets[i].x, offsets[i].y);
                int x = plan.Centre.x + local.x;
                int z = plan.Centre.z + local.y;
                int surface = HighestSolid(ref brush, x, z, top + 18, top - 170);

                uint seed = CastleSeedPartition.Derive(
                    plan.Seed, CastleSeedDomain.Decor, (uint)(0x6000 + i));
                var rng = new Random(seed);
                brush.Cone(
                    x,
                    surface + 1,
                    z,
                    rng.NextInt(4, 8),
                    rng.NextInt(6, 13),
                    (i & 1) == 0 ? Mat.DarkStone : Mat.Stone);

                int side = (i & 1) == 0 ? -1 : 1;
                int2 scrubLocal = approach.LocalPoint(
                    offsets[i].x + side * rng.NextInt(10, 21),
                    offsets[i].y - rng.NextInt(3, 15));
                int scrubX = plan.Centre.x + scrubLocal.x;
                int scrubZ = plan.Centre.z + scrubLocal.y;
                int scrubY = HighestSolid(ref brush, scrubX, scrubZ, top + 18, top - 170);
                brush.Cone(
                    scrubX,
                    scrubY + 1,
                    scrubZ,
                    rng.NextInt(3, 6),
                    rng.NextInt(4, 9),
                    Mat.Moss);
            }
        }

        private static int HighestSolid(ref VoxelBrush brush, int x, int z, int fromY, int minY)
        {
            for (int y = fromY; y >= minY; y--)
                if (brush.IsSolid(x, y, z)) return y;
            return minY;
        }

        private static int2 Round(float2 value) =>
            new int2((int)math.round(value.x), (int)math.round(value.y));
    }
}

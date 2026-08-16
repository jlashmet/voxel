using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Freezes stage-8 decoration placement and dimensions before Runtime begins mutation. The seed
    /// streams and draw order intentionally match the historical spatial landscape realizer so the
    /// planner/realizer boundary changes without perturbing existing castle appearance.
    /// </summary>
    public static class CastleLandscapePlanner
    {
        public static CastleLandscapePlan Create(
            in CastlePlan plan,
            int2[] localPerimeter,
            in CastleApproachFrame approach)
        {
            if (localPerimeter == null) throw new ArgumentNullException(nameof(localPerimeter));
            if (localPerimeter.Length < 3)
                throw new ArgumentException("Castle landscape planning requires a perimeter polygon.",
                                            nameof(localPerimeter));

            var decorations = new List<CastleLandscapeDecorationSpec>();
            PlanPerimeter(in plan, localPerimeter, decorations);
            PlanApproach(in plan, in approach, decorations);
            return new CastleLandscapePlan(decorations.ToArray());
        }

        private static void PlanPerimeter(
            in CastlePlan plan,
            int2[] perimeter,
            List<CastleLandscapeDecorationSpec> decorations)
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
                    int2 shrubCentre = Round(onWall + outward * outsideDistance);
                    int shrubRadius = rng.NextInt(3, 7);
                    int shrubHeight = rng.NextInt(5, 11);
                    decorations.Add(new CastleLandscapeDecorationSpec
                    {
                        Id = decorations.Count,
                        Kind = (sample & 1) == 0
                            ? CastleLandscapeDecorationKind.PerimeterMossShrub
                            : CastleLandscapeDecorationKind.PerimeterGrassShrub,
                        Centre = shrubCentre,
                        Radius = shrubRadius,
                        Height = shrubHeight,
                    });

                    float rubbleAlong = rng.NextFloat(-12f, 13f);
                    float rubbleOut = outsideDistance + rng.NextFloat(7f, 18f);
                    int2 rubbleCentre = Round(
                        onWall + tangent * rubbleAlong + outward * rubbleOut);
                    decorations.Add(new CastleLandscapeDecorationSpec
                    {
                        Id = decorations.Count,
                        Kind = (sample % 3) == 0
                            ? CastleLandscapeDecorationKind.PerimeterStoneRubble
                            : CastleLandscapeDecorationKind.PerimeterDarkStoneRubble,
                        Centre = rubbleCentre,
                        Size = new int3(
                            rng.NextInt(4, 9),
                            rng.NextInt(3, 7),
                            rng.NextInt(4, 9)),
                    });
                }
            }
        }

        private static void PlanApproach(
            in CastlePlan plan,
            in CastleApproachFrame approach,
            List<CastleLandscapeDecorationSpec> decorations)
        {
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
                uint seed = CastleSeedPartition.Derive(
                    plan.Seed, CastleSeedDomain.Decor, (uint)(0x6000 + i));
                var rng = new Random(seed);
                decorations.Add(new CastleLandscapeDecorationSpec
                {
                    Id = decorations.Count,
                    Kind = (i & 1) == 0
                        ? CastleLandscapeDecorationKind.ApproachDarkStoneRock
                        : CastleLandscapeDecorationKind.ApproachStoneRock,
                    Centre = approach.LocalPoint(offsets[i].x, offsets[i].y),
                    Radius = rng.NextInt(4, 8),
                    Height = rng.NextInt(6, 13),
                });

                int side = (i & 1) == 0 ? -1 : 1;
                int2 scrubCentre = approach.LocalPoint(
                    offsets[i].x + side * rng.NextInt(10, 21),
                    offsets[i].y - rng.NextInt(3, 15));
                decorations.Add(new CastleLandscapeDecorationSpec
                {
                    Id = decorations.Count,
                    Kind = CastleLandscapeDecorationKind.ApproachMossScrub,
                    Centre = scrubCentre,
                    Radius = rng.NextInt(3, 6),
                    Height = rng.NextInt(4, 9),
                });
            }
        }

        private static int2 Round(float2 value) =>
            new int2((int)math.round(value.x), (int)math.round(value.y));
    }
}

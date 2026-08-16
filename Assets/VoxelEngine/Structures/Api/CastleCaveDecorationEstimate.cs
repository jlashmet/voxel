using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Conservative slow-write estimate for CastlePlannedCaveDecorator. Generic cave carving is
    /// estimated separately by CaveBuildEstimate; this value covers only per-voxel cone primitives.
    /// Pool, causeway, and light-marker writes stay on bulk paths.
    /// </summary>
    public static class CastleCaveDecorationEstimate
    {
        /// <summary>Compatibility wrapper that plans the current deterministic decoration recipe.</summary>
        public static long Estimate(CavePlan cave)
        {
            if (cave == null) throw new ArgumentNullException(nameof(cave));
            CastleCaveDecorationPlan decoration = CastleCaveDecorationPlanner.Create(cave);
            return Estimate(cave, decoration);
        }

        /// <summary>Estimates the exact supplied decoration semantics without inferring placements.</summary>
        public static long Estimate(
            CavePlan cave,
            CastleCaveDecorationPlan decoration)
        {
            if (!CastleCaveDecorationPlanValidator.TryValidate(
                    cave, decoration, out CastleCaveDecorationPlanIssue issue))
            {
                throw new ArgumentException(
                    $"Cannot estimate invalid castle cave decoration plan: {issue}.",
                    nameof(decoration));
            }

            long writes = 0;
            CastleCaveDecorationSpec[] elements = decoration.Elements;
            for (int i = 0; i < elements.Length; i++)
            {
                CastleCaveDecorationSpec spec = elements[i];
                switch (spec.Kind)
                {
                    case CastleCaveDecorationKind.CrystalSpire:
                    case CastleCaveDecorationKind.MossSpire:
                    case CastleCaveDecorationKind.Stalagmite:
                    case CastleCaveDecorationKind.Stalactite:
                        writes += ConeUpperBound(spec.Radius, spec.Height);
                        break;
                }
            }

            return writes;
        }

        private static long ConeUpperBound(int radius, int height)
        {
            long diameter = radius * 2L + 1L;
            return diameter * diameter * math.max(0, height);
        }
    }
}

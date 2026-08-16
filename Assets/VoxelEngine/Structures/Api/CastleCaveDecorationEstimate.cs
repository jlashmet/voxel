using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Conservative slow-write estimate for CastlePlannedCaveDecorator. Generic cave carving is
    /// estimated separately by CaveBuildEstimate; this value covers only authored formations that
    /// use per-voxel cone primitives. Pool, causeway, and light-marker boxes use bulk writes.
    /// </summary>
    public static class CastleCaveDecorationEstimate
    {
        public static long Estimate(CavePlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (!CavePlanValidator.TryValidate(plan, out CavePlanIssue issue))
            {
                throw new ArgumentException(
                    $"Cannot estimate decoration for invalid cave plan: {issue}.", nameof(plan));
            }

            long writes = 0;
            for (int i = 0; i < plan.Chambers.Length; i++)
            {
                CaveChamberPlan chamber = plan.Chambers[i];
                int crystalHeight = math.clamp(chamber.Radii.y / 3, 7, 16);

                // Crystal/moss cluster: use filled-cylinder bounds even though VoxelBrush.Cone is
                // a shell. The deliberate overestimate keeps admission conservative if cone skin
                // thickness changes without changing the decoration topology.
                writes += ConeUpperBound(3, crystalHeight);
                writes += ConeUpperBound(2, math.max(5, crystalHeight - 5));
                writes += ConeUpperBound(2, math.max(6, crystalHeight - 3));

                int formationCount = i == plan.EntryChamberId ? 5 : 3;
                int formationHeight = math.clamp(chamber.Radii.y, 7, 27);
                writes += formationCount * ConeUpperBound(5, formationHeight);
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

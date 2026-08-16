using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Conservative expensive-write-equivalent estimate for a natural CavePlan. Chamber radii and
    /// passage length/section drive the estimate so larger or more connected caves carry a larger
    /// admission cost without treating batched bulk carving as individual authored voxel edits.
    /// </summary>
    public static class CaveBuildEstimate
    {
        public static long Estimate(CavePlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (!CavePlanValidator.TryValidate(plan, out CavePlanIssue issue))
                throw new ArgumentException($"Cannot estimate invalid cave plan: {issue}.", nameof(plan));

            double cost = 0.0;
            for (int i = 0; i < plan.Chambers.Length; i++)
            {
                int3 radii = plan.Chambers[i].Radii;
                double ellipsoidVolume = (4.0 / 3.0) * math.PI_DBL
                                       * radii.x * (double)radii.y * radii.z;
                cost += ellipsoidVolume * 0.20;
            }

            for (int i = 0; i < plan.Passages.Length; i++)
            {
                CavePassagePlan passage = plan.Passages[i];
                int3 from = plan.Chambers[passage.FromChamberId].Centre;
                int3 to = plan.Chambers[passage.ToChamberId].Centre;
                int3 delta = to - from;
                double distance = Math.Sqrt(
                    delta.x * (double)delta.x
                    + delta.y * (double)delta.y
                    + delta.z * (double)delta.z);
                double sweptVolume = math.max(1.0, distance)
                                   * passage.Width * (double)passage.Height;
                cost += sweptVolume * 0.25;
            }

            return (long)Math.Ceiling(cost);
        }
    }
}

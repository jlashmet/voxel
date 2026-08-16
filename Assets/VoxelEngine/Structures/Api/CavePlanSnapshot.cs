using System;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Produces an isolated, validated CavePlan copy for trust-boundary handoffs. CavePlan exposes
    /// mutable arrays for efficient deterministic planning, so incremental realization must not
    /// retain caller-owned chamber or passage arrays after admission.
    /// </summary>
    public static class CavePlanSnapshot
    {
        public static CavePlan CloneValidated(CavePlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (!CavePlanValidator.TryValidate(plan, out CavePlanIssue issue))
                throw new InvalidOperationException($"Cannot snapshot invalid cave plan: {issue}.");
            return plan.Snapshot();
        }
    }
}

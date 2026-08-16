using System;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Produces an isolated validated landscape copy for the Runtime trust boundary. Landscape
    /// planning exposes mutable arrays for lightweight planning/tests, so an in-flight build must
    /// never retain caller-owned decoration storage after preflight.
    /// </summary>
    public static class CastleLandscapePlanSnapshot
    {
        public static CastleLandscapePlan CloneValidated(CastleLandscapePlan landscape)
        {
            if (landscape == null) throw new ArgumentNullException(nameof(landscape));
            if (!CastleLandscapePlanValidator.TryValidate(
                    landscape, out CastleLandscapePlanIssue issue))
            {
                throw new InvalidOperationException(
                    $"Cannot snapshot invalid castle landscape plan: {issue}.");
            }

            return new CastleLandscapePlan(
                (CastleLandscapeDecorationSpec[])landscape.Decorations.Clone());
        }
    }
}

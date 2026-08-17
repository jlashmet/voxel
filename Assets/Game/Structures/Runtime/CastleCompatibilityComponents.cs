using Game.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Resolves the canonical compatibility castle config consumed by the active authoring path.
    /// Geometry policy lives in <see cref="CastlePresets"/> so runtime authorers share one config
    /// surface instead of maintaining parallel compatibility projections.
    /// </summary>
    public static class CastleCompatibilityComponents
    {
        public static CastleConfig Resolve(in CastlePlan plan) =>
            CastlePresets.Compatibility(in plan);
    }
}

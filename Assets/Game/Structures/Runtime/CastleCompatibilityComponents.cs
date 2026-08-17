using Game.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Legacy entry point retained for castle authoring overloads. It is only a resolver: canonical
    /// compatibility policy lives in <see cref="CastleComponentPresets"/> and the game material
    /// binding lives in <see cref="CastleStructurePalette"/>.
    /// </summary>
    public static class CastleCompatibilityComponents
    {
        public static CastleComponentConfig Resolve(in CastlePlan plan) =>
            CastleStructurePalette.ResolveCompatibility(in plan);
    }
}

using Game.Structures.Api;

namespace Game.Structures.Runtime
{
    public readonly struct DecorationRegionLookDevResult
    {
        public readonly DecorationRegionTheme Region;
        public readonly DecorationContext Context;
        public readonly DecorationPresentationProfile Presentation;
        public readonly DecorationPlacement[] Placements;

        public DecorationRegionLookDevResult(
            DecorationRegionTheme region,
            in DecorationContext context,
            in DecorationPresentationProfile presentation,
            DecorationPlacement[] placements)
        {
            Region = region;
            Context = context;
            Presentation = presentation;
            Placements = placements;
        }

        public bool IsWellFormed =>
            Region != DecorationRegionTheme.Unknown &&
            Context.IsWellFormed &&
            Placements != null &&
            Placements.Length > 0;
    }

    /// <summary>
    /// Creates directly comparable regional versions of one semantic room. This is intended for
    /// look-dev/debug tools and regression tests: same world/room seed, same guild scene identity,
    /// different settlement defaults, optional selection, density and presentation palette.
    /// </summary>
    public static class DecorationRegionLookDevComposition
    {
        public static bool TryResolveAdventurerGuildAcrossRegions(
            in DecorationSpace space,
            in DecorationContext baseContext,
            DecorationExclusion[] exclusions,
            out DecorationRegionLookDevResult[] results)
        {
            results = new DecorationRegionLookDevResult[0];
            if (!space.IsWellFormed || !baseContext.IsWellFormed ||
                space.SpaceId != baseContext.SpaceId || space.Kind != baseContext.SpaceKind)
                return false;

            const int regionCount = 6;
            var resolved = new DecorationRegionLookDevResult[regionCount];
            for (int i = 0; i < regionCount; i++)
            {
                DecorationRegionTheme region = (DecorationRegionTheme)(i + 1);
                DecorationContext context = DecorationRegionProfiles.ApplyDefaults(
                    in baseContext,
                    region,
                    localStyleVariation: baseContext.WorldSeed ^ ((uint)region * 0x9E3779B9u),
                    applyWealth: true);

                if (!DecorationExpansion300RegionResolver.TryResolve(
                        DecorationExpansion300SceneKind.AdventurerGuildHall,
                        region,
                        in space,
                        in context,
                        exclusions,
                        out DecorationPlacement[] placements))
                    return false;

                DecorationPresentationProfile presentation =
                    DecorationRegionContentPolicy.Presentation(in context, region);
                resolved[i] = new DecorationRegionLookDevResult(
                    region, in context, in presentation, placements);
            }

            results = resolved;
            return true;
        }
    }
}

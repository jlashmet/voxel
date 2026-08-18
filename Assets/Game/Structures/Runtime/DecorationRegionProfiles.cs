using Game.Materials.Api;
using Game.Structures.Api;

namespace Game.Structures.Runtime
{
    public enum DecorationRegionTheme : byte
    {
        Unknown = 0,
        Kentridge = 1,
        Hightown = 2,
        Moordell = 3,
        Rossdam = 4,
        FairyVillage = 5,
        OrcVillage = 6,
    }

    [System.Flags]
    public enum DecorationRegionContentTags : uint
    {
        None = 0,
        LivedIn = 1u << 0,
        Agrarian = 1u << 1,
        Merchant = 1u << 2,
        Sacred = 1u << 3,
        Scholar = 1u << 4,
        Noble = 1u << 5,
        Royal = 1u << 6,
        Enchanted = 1u << 7,
        Organic = 1u << 8,
        Craft = 1u << 9,
        Trophy = 1u << 10,
        Hunting = 1u << 11,
        Adventurer = 1u << 12,
        Funerary = 1u << 13,
    }

    /// <summary>
    /// Voxel-owned art-direction defaults for a named world region. Imported legacy contracts prove
    /// place identity and some social/aesthetic distinctions; material choices are modern art direction.
    /// </summary>
    public readonly struct DecorationRegionProfile
    {
        public readonly DecorationRegionTheme Region;
        public readonly DecorationStyleFamily StyleFamily;
        public readonly DecorationWealthTier DefaultWealth;
        public readonly byte PrimaryMaterial;
        public readonly byte SecondaryMaterial;
        public readonly byte AccentMaterial;
        public readonly byte MagicMaterial;
        public readonly DecorationRegionContentTags PreferredContent;
        public readonly int OrnamentBias;
        public readonly int ClutterBias;

        public DecorationRegionProfile(
            DecorationRegionTheme region,
            DecorationStyleFamily styleFamily,
            DecorationWealthTier defaultWealth,
            byte primaryMaterial,
            byte secondaryMaterial,
            byte accentMaterial,
            byte magicMaterial,
            DecorationRegionContentTags preferredContent,
            int ornamentBias,
            int clutterBias)
        {
            Region = region;
            StyleFamily = styleFamily;
            DefaultWealth = defaultWealth;
            PrimaryMaterial = primaryMaterial;
            SecondaryMaterial = secondaryMaterial;
            AccentMaterial = accentMaterial;
            MagicMaterial = magicMaterial;
            PreferredContent = preferredContent;
            OrnamentBias = ornamentBias;
            ClutterBias = clutterBias;
        }

        public bool IsWellFormed =>
            Region != DecorationRegionTheme.Unknown &&
            StyleFamily != DecorationStyleFamily.Unknown &&
            PrimaryMaterial != GameMaterialIds.Empty &&
            SecondaryMaterial != GameMaterialIds.Empty;

        public bool Prefers(DecorationRegionContentTags tags) =>
            (PreferredContent & tags) != 0;
    }

    public static class DecorationRegionProfiles
    {
        public static DecorationRegionProfile Resolve(DecorationRegionTheme region)
        {
            switch (region)
            {
                case DecorationRegionTheme.Kentridge:
                    return new DecorationRegionProfile(
                        region,
                        DecorationStyleFamily.Rustic,
                        DecorationWealthTier.Modest,
                        GameMaterialIds.Wood,
                        GameMaterialIds.MasonrySmall,
                        GameMaterialIds.Cloth,
                        GameMaterialIds.LitWindow,
                        DecorationRegionContentTags.LivedIn |
                        DecorationRegionContentTags.Agrarian |
                        DecorationRegionContentTags.Merchant |
                        DecorationRegionContentTags.Craft |
                        DecorationRegionContentTags.Adventurer,
                        ornamentBias: -1,
                        clutterBias: 2);

                case DecorationRegionTheme.Hightown:
                    return new DecorationRegionProfile(
                        region,
                        DecorationStyleFamily.Sacred,
                        DecorationWealthTier.Comfortable,
                        GameMaterialIds.Slate,
                        GameMaterialIds.MasonryMedium,
                        GameMaterialIds.Wood,
                        GameMaterialIds.Crystal,
                        DecorationRegionContentTags.Sacred |
                        DecorationRegionContentTags.Scholar |
                        DecorationRegionContentTags.Merchant |
                        DecorationRegionContentTags.Funerary,
                        ornamentBias: 0,
                        clutterBias: 0);

                case DecorationRegionTheme.Moordell:
                    return new DecorationRegionProfile(
                        region,
                        DecorationStyleFamily.Courtly,
                        DecorationWealthTier.Wealthy,
                        GameMaterialIds.MasonryMedium,
                        GameMaterialIds.Wood,
                        GameMaterialIds.Gold,
                        GameMaterialIds.Crystal,
                        DecorationRegionContentTags.Noble |
                        DecorationRegionContentTags.Merchant |
                        DecorationRegionContentTags.Enchanted |
                        DecorationRegionContentTags.Scholar,
                        ornamentBias: 3,
                        clutterBias: 1);

                case DecorationRegionTheme.Rossdam:
                    return new DecorationRegionProfile(
                        region,
                        DecorationStyleFamily.Courtly,
                        DecorationWealthTier.Noble,
                        GameMaterialIds.MasonryLarge,
                        GameMaterialIds.DarkStone,
                        GameMaterialIds.Gold,
                        GameMaterialIds.Crystal,
                        DecorationRegionContentTags.Royal |
                        DecorationRegionContentTags.Noble |
                        DecorationRegionContentTags.Sacred |
                        DecorationRegionContentTags.Enchanted |
                        DecorationRegionContentTags.Trophy,
                        ornamentBias: 4,
                        clutterBias: -1);

                case DecorationRegionTheme.FairyVillage:
                    return new DecorationRegionProfile(
                        region,
                        DecorationStyleFamily.Sacred,
                        DecorationWealthTier.Comfortable,
                        GameMaterialIds.Wood,
                        GameMaterialIds.Moss,
                        GameMaterialIds.FlowerPink,
                        GameMaterialIds.Crystal,
                        DecorationRegionContentTags.Enchanted |
                        DecorationRegionContentTags.Organic |
                        DecorationRegionContentTags.LivedIn |
                        DecorationRegionContentTags.Merchant,
                        ornamentBias: 2,
                        clutterBias: 2);

                case DecorationRegionTheme.OrcVillage:
                    return new DecorationRegionProfile(
                        region,
                        DecorationStyleFamily.Frontier,
                        DecorationWealthTier.Modest,
                        GameMaterialIds.Wood,
                        GameMaterialIds.DarkStone,
                        GameMaterialIds.Dirt,
                        GameMaterialIds.LitWindow,
                        DecorationRegionContentTags.Craft |
                        DecorationRegionContentTags.Trophy |
                        DecorationRegionContentTags.Hunting |
                        DecorationRegionContentTags.Enchanted |
                        DecorationRegionContentTags.LivedIn,
                        ornamentBias: -1,
                        clutterBias: 1);

                default:
                    return default;
            }
        }

        /// <summary>
        /// Applies only region defaults. Callers may override wealth/style afterwards for a specific
        /// building or room. This keeps a poor Moordell cellar and a rich Kentridge manor possible.
        /// </summary>
        public static DecorationContext ApplyDefaults(
            in DecorationContext context,
            DecorationRegionTheme region,
            uint localStyleVariation,
            bool applyWealth = true)
        {
            DecorationRegionProfile profile = Resolve(region);
            if (!profile.IsWellFormed)
                return context;

            DecorationContext result = context;
            result.StyleId = DecorationStyleIds.Compose(profile.StyleFamily, localStyleVariation);
            if (applyWealth)
                result.Wealth = profile.DefaultWealth;
            return result;
        }

        public static int ContentWeight(
            DecorationRegionTheme region,
            DecorationRegionContentTags contentTags,
            int baseWeight = 10)
        {
            DecorationRegionProfile profile = Resolve(region);
            if (!profile.IsWellFormed || contentTags == DecorationRegionContentTags.None)
                return baseWeight;
            return profile.Prefers(contentTags) ? baseWeight + 5 : baseWeight;
        }
    }
}

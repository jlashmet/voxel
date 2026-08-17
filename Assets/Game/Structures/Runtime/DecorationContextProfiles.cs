using Game.Materials.Api;
using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    /// <summary>Game-owned presentation policy for one broad decoration culture/style family.</summary>
    public readonly struct DecorationStyleProfile
    {
        public readonly DecorationStyleFamily Family;
        public readonly byte PrimaryMaterial;
        public readonly byte SoftMaterial;
        public readonly byte AccentMaterial;
        public readonly byte EmissiveMaterial;
        public readonly int SilhouetteBias;
        public readonly int OrnamentBias;

        public DecorationStyleProfile(
            DecorationStyleFamily family,
            byte primaryMaterial,
            byte softMaterial,
            byte accentMaterial,
            byte emissiveMaterial,
            int silhouetteBias,
            int ornamentBias)
        {
            Family = family;
            PrimaryMaterial = primaryMaterial;
            SoftMaterial = softMaterial;
            AccentMaterial = accentMaterial;
            EmissiveMaterial = emissiveMaterial;
            SilhouetteBias = silhouetteBias;
            OrnamentBias = ornamentBias;
        }
    }

    /// <summary>
    /// Resolved render-facing policy after style, wealth, and condition have been combined. The
    /// semantic placement resolver never depends on these game material IDs.
    /// </summary>
    public readonly struct DecorationPresentationProfile
    {
        public readonly DecorationStyleFamily Family;
        public readonly byte PrimaryMaterial;
        public readonly byte SoftMaterial;
        public readonly byte AccentMaterial;
        public readonly byte EmissiveMaterial;
        public readonly int Ornamentation;
        public readonly int DamageLevel;

        public DecorationPresentationProfile(
            DecorationStyleFamily family,
            byte primaryMaterial,
            byte softMaterial,
            byte accentMaterial,
            byte emissiveMaterial,
            int ornamentation,
            int damageLevel)
        {
            Family = family;
            PrimaryMaterial = primaryMaterial;
            SoftMaterial = softMaterial;
            AccentMaterial = accentMaterial;
            EmissiveMaterial = emissiveMaterial;
            Ornamentation = ornamentation;
            DamageLevel = damageLevel;
        }

        public bool UseBedPosts => Ornamentation >= 3 && DamageLevel <= 2;
        public bool UseLuxuryTrim => Ornamentation >= 4 && DamageLevel <= 2;
        public bool EmitsLight => DamageLevel <= 2;
    }

    public static class DecorationContextProfiles
    {
        public static DecorationStyleProfile ResolveStyle(uint styleId)
        {
            DecorationStyleFamily family = DecorationStyleIds.FamilyOf(styleId);
            if (family == DecorationStyleFamily.Unknown)
            {
                // Legacy/uncomposed StyleIds remain deterministic during migration.
                family = (DecorationStyleFamily)(1 + styleId % (uint)DecorationStyleFamily.Frontier);
            }

            switch (family)
            {
                case DecorationStyleFamily.Rustic:
                    return new DecorationStyleProfile(
                        family,
                        GameMaterialIds.Wood,
                        GameMaterialIds.Cloth,
                        GameMaterialIds.MasonrySmall,
                        GameMaterialIds.LitWindow,
                        0,
                        -1);
                case DecorationStyleFamily.Courtly:
                    return new DecorationStyleProfile(
                        family,
                        GameMaterialIds.Wood,
                        GameMaterialIds.Cloth,
                        GameMaterialIds.Gold,
                        GameMaterialIds.LitWindow,
                        1,
                        2);
                case DecorationStyleFamily.Martial:
                    return new DecorationStyleProfile(
                        family,
                        GameMaterialIds.DarkStone,
                        GameMaterialIds.Cloth,
                        GameMaterialIds.Gold,
                        GameMaterialIds.LitWindow,
                        -1,
                        0);
                case DecorationStyleFamily.Sacred:
                    return new DecorationStyleProfile(
                        family,
                        GameMaterialIds.Slate,
                        GameMaterialIds.Cloth,
                        GameMaterialIds.Gold,
                        GameMaterialIds.Crystal,
                        1,
                        1);
                case DecorationStyleFamily.Frontier:
                default:
                    return new DecorationStyleProfile(
                        DecorationStyleFamily.Frontier,
                        GameMaterialIds.Wood,
                        GameMaterialIds.Cloth,
                        GameMaterialIds.MasonrySmall,
                        GameMaterialIds.LitWindow,
                        -1,
                        -1);
            }
        }

        public static DecorationPresentationProfile ResolvePresentation(in DecorationContext context)
        {
            DecorationStyleProfile style = ResolveStyle(context.StyleId);
            return ResolvePresentation(in style, context.Wealth, context.Condition);
        }

        public static DecorationPresentationProfile Compatibility =>
            ResolvePresentation(
                ResolveStyle(DecorationStyleIds.Compose(DecorationStyleFamily.Courtly, 1u)),
                DecorationWealthTier.Wealthy,
                DecorationConditionTier.Maintained);

        public static DecorationPropDescriptor ApplySilhouette(
            in DecorationPropDescriptor descriptor,
            in DecorationContext context)
        {
            DecorationStyleProfile style = ResolveStyle(context.StyleId);
            DecorationPropDescriptor result = descriptor;

            switch (style.Family)
            {
                case DecorationStyleFamily.Rustic:
                    if (result.Family == DecorationPropFamily.Bed)
                        result.Size.z += 2;
                    else if (result.Family == DecorationPropFamily.Painting)
                        result.Size.x = math.max(8, result.Size.x - 2);
                    break;

                case DecorationStyleFamily.Courtly:
                    if (result.Family == DecorationPropFamily.Bed ||
                        result.Family == DecorationPropFamily.Dresser ||
                        result.Family == DecorationPropFamily.Rug)
                        result.Size.x += 2;
                    if (result.Family == DecorationPropFamily.Painting)
                        result.Size.y += 2;
                    break;

                case DecorationStyleFamily.Martial:
                    if (result.Family == DecorationPropFamily.Bed || result.Family == DecorationPropFamily.Rug)
                        result.Size.x = math.max(12, result.Size.x - 2);
                    if (result.Family == DecorationPropFamily.Dresser)
                        result.Size.y += 2;
                    break;

                case DecorationStyleFamily.Sacred:
                    if (result.Family == DecorationPropFamily.Painting)
                        result.Size.y += 4;
                    if (result.Family == DecorationPropFamily.WallTorch)
                        result.Size.y += 2;
                    break;

                case DecorationStyleFamily.Frontier:
                    if (result.Family == DecorationPropFamily.Dresser)
                        result.Size.x = math.max(10, result.Size.x - 2);
                    if (result.Family == DecorationPropFamily.Rug)
                        result.Size.z = math.max(20, result.Size.z - 4);
                    break;
            }

            uint contextDiscriminator =
                ((uint)style.Family << 24) ^
                ((uint)context.Wealth << 12) ^
                ((uint)context.Condition << 8);
            result.Variant = DecorationSeed.Derive(result.Variant, contextDiscriminator);
            return result;
        }

        /// <summary>
        /// Optional scene density budget. Required props never consume this budget. Noble rooms get
        /// one extra detail; wealthy courtly rooms also get one because that profile is intentionally
        /// decoration-heavy.
        /// </summary>
        public static int OptionalSceneBudget(in DecorationContext context)
        {
            if (context.Wealth == DecorationWealthTier.Noble)
                return 1;

            DecorationStyleProfile style = ResolveStyle(context.StyleId);
            return context.Wealth == DecorationWealthTier.Wealthy && style.OrnamentBias >= 2 ? 1 : 0;
        }

        private static DecorationPresentationProfile ResolvePresentation(
            DecorationStyleProfile style,
            DecorationWealthTier wealth,
            DecorationConditionTier condition)
        {
            int damage = 4 - (int)condition;
            int ornamentation = math.clamp(style.OrnamentBias + (int)wealth * 2 - damage * 2, 0, 8);

            byte primary = style.PrimaryMaterial;
            byte soft = style.SoftMaterial;
            byte accent = (int)wealth >= (int)DecorationWealthTier.Comfortable
                ? style.AccentMaterial
                : style.PrimaryMaterial;
            byte emissive = style.EmissiveMaterial;

            if (condition == DecorationConditionTier.Worn && ornamentation < 3)
                accent = GameMaterialIds.DarkStone;
            else if (condition == DecorationConditionTier.Abandoned)
            {
                soft = GameMaterialIds.Moss;
                accent = GameMaterialIds.DarkStone;
                emissive = GameMaterialIds.DarkStone;
            }
            else if (condition == DecorationConditionTier.Ruined)
            {
                soft = GameMaterialIds.Dirt;
                accent = GameMaterialIds.DarkStone;
                emissive = GameMaterialIds.DarkStone;
            }

            return new DecorationPresentationProfile(
                style.Family,
                primary,
                soft,
                accent,
                emissive,
                ornamentation,
                damage);
        }
    }
}

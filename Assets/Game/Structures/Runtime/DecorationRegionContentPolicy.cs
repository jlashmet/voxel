using Game.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Region-aware content and presentation policy. Semantic archetype identity remains global;
    /// regions alter preference and presentation without allocating duplicate IDs.
    /// </summary>
    public static class DecorationRegionContentPolicy
    {
        public static DecorationRegionContentTags Tags(DecorationExpansion300Kind kind)
        {
            ushort id = (ushort)kind;
            if (id >= 261 && id <= 280)
            {
                DecorationRegionContentTags tags = DecorationRegionContentTags.Trophy;
                if (kind == DecorationExpansion300Kind.MonsterNest ||
                    kind == DecorationExpansion300Kind.BeastBedding ||
                    kind == DecorationExpansion300Kind.MonsterFoodCache)
                    tags |= DecorationRegionContentTags.LivedIn;
                if (kind == DecorationExpansion300Kind.BoneTotem ||
                    kind == DecorationExpansion300Kind.ScentMarkerTotem)
                    tags |= DecorationRegionContentTags.Enchanted;
                return tags;
            }

            if (id >= 281 && id <= 300)
            {
                DecorationRegionContentTags tags = DecorationRegionContentTags.Adventurer;
                if (kind == DecorationExpansion300Kind.GuildStrongbox ||
                    kind == DecorationExpansion300Kind.MemberLockerBank ||
                    kind == DecorationExpansion300Kind.CaravanSupplyCrate)
                    tags |= DecorationRegionContentTags.Merchant;
                if (kind == DecorationExpansion300Kind.TravelCharmDisplay ||
                    kind == DecorationExpansion300Kind.WaystoneAttunementPedestal)
                    tags |= DecorationRegionContentTags.Enchanted;
                if (kind == DecorationExpansion300Kind.CartographersDesk ||
                    kind == DecorationExpansion300Kind.TrainingManualShelf)
                    tags |= DecorationRegionContentTags.Scholar;
                return tags;
            }

            return DecorationRegionContentTags.None;
        }

        public static ushort Weight(
            DecorationRegionTheme region,
            DecorationExpansion300Kind kind,
            ushort baseWeight)
        {
            if (region == DecorationRegionTheme.Unknown)
                return baseWeight;

            int weight = DecorationRegionProfiles.ContentWeight(region, Tags(kind), baseWeight);
            DecorationRegionProfile profile = DecorationRegionProfiles.Resolve(region);

            // Region personality can slightly change density without changing required content.
            if (profile.IsWellFormed && profile.ClutterBias > 0 && IsClutterLike(kind))
                weight += profile.ClutterBias;
            if (profile.IsWellFormed && profile.OrnamentBias > 1 && IsDisplayLike(kind))
                weight += profile.OrnamentBias / 2;

            if (weight < 1) weight = 1;
            if (weight > ushort.MaxValue) weight = ushort.MaxValue;
            return (ushort)weight;
        }

        public static DecorationPresentationProfile Presentation(
            in DecorationContext context,
            DecorationRegionTheme region)
        {
            DecorationPresentationProfile baseProfile = DecorationContextProfiles.ResolvePresentation(in context);
            DecorationRegionProfile regionProfile = DecorationRegionProfiles.Resolve(region);
            if (!regionProfile.IsWellFormed)
                return baseProfile;

            int ornament = baseProfile.Ornamentation + regionProfile.OrnamentBias;
            if (ornament < 0) ornament = 0;
            if (ornament > 8) ornament = 8;

            return new DecorationPresentationProfile(
                baseProfile.Family,
                regionProfile.PrimaryMaterial,
                regionProfile.SecondaryMaterial,
                regionProfile.AccentMaterial,
                regionProfile.MagicMaterial,
                ornament,
                baseProfile.DamageLevel);
        }

        /// <summary>
        /// Compatibility alias for newer emitters that naturally pass region before context.
        /// Semantic behavior is identical to <see cref="Presentation"/>.
        /// </summary>
        public static DecorationPresentationProfile ResolvePresentation(
            DecorationRegionTheme region,
            in DecorationContext context) => Presentation(in context, region);

        private static bool IsClutterLike(DecorationExpansion300Kind kind) =>
            kind == DecorationExpansion300Kind.TrophySkullPile ||
            kind == DecorationExpansion300Kind.GnawedBonePile ||
            kind == DecorationExpansion300Kind.MoltedShellPile ||
            kind == DecorationExpansion300Kind.HoardScrapPile ||
            kind == DecorationExpansion300Kind.ExpeditionSupplyRack ||
            kind == DecorationExpansion300Kind.BedrollRack ||
            kind == DecorationExpansion300Kind.RopeGearRack ||
            kind == DecorationExpansion300Kind.LanternGearRack;

        private static bool IsDisplayLike(DecorationExpansion300Kind kind) =>
            kind == DecorationExpansion300Kind.BoneTotem ||
            kind == DecorationExpansion300Kind.GuildTrophyWall ||
            kind == DecorationExpansion300Kind.MonsterContractBoard ||
            kind == DecorationExpansion300Kind.TravelCharmDisplay ||
            kind == DecorationExpansion300Kind.WaystoneAttunementPedestal;
    }
}

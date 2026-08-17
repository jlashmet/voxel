namespace Game.Structures.Runtime
{
    /// <summary>
    /// Guild-specific preference layer on top of settlement decoration profiles. This does not ban
    /// unusual guild/region combinations; it only exposes a deterministic preference signal for
    /// planners and look-dev tools.
    /// </summary>
    public static class GuildHouseRegionPolicy
    {
        public static int Preference(GuildHouseKind guild, DecorationRegionTheme region)
        {
            DecorationRegionProfile profile = DecorationRegionProfiles.Resolve(region);
            if (!profile.IsWellFormed) return 0;

            DecorationRegionContentTags desired = DesiredTags(guild);
            int score = profile.Prefers(desired) ? 3 : 0;

            if (guild == GuildHouseKind.Wizards && region == DecorationRegionTheme.Moordell) score += 2;
            if (guild == GuildHouseKind.Druids && region == DecorationRegionTheme.FairyVillage) score += 3;
            if (guild == GuildHouseKind.Knights && region == DecorationRegionTheme.Rossdam) score += 2;
            if ((guild == GuildHouseKind.Assassins || guild == GuildHouseKind.Thieves) && region == DecorationRegionTheme.Moordell) score += 1;
            if (guild == GuildHouseKind.Rangers && region == DecorationRegionTheme.Kentridge) score += 2;
            if (guild == GuildHouseKind.Clerics && region == DecorationRegionTheme.Hightown) score += 2;
            return score;
        }

        public static DecorationRegionContentTags DesiredTags(GuildHouseKind guild)
        {
            switch (guild)
            {
                case GuildHouseKind.Wizards:
                    return DecorationRegionContentTags.Scholar | DecorationRegionContentTags.Enchanted;
                case GuildHouseKind.Knights:
                    return DecorationRegionContentTags.Noble | DecorationRegionContentTags.Trophy;
                case GuildHouseKind.Assassins:
                    return DecorationRegionContentTags.Enchanted | DecorationRegionContentTags.Merchant;
                case GuildHouseKind.Druids:
                    return DecorationRegionContentTags.Organic | DecorationRegionContentTags.Sacred;
                case GuildHouseKind.Thieves:
                    return DecorationRegionContentTags.Merchant | DecorationRegionContentTags.LivedIn;
                case GuildHouseKind.Clerics:
                    return DecorationRegionContentTags.Sacred | DecorationRegionContentTags.Scholar;
                case GuildHouseKind.Rangers:
                    return DecorationRegionContentTags.Hunting | DecorationRegionContentTags.Organic;
                case GuildHouseKind.Bards:
                    return DecorationRegionContentTags.Merchant | DecorationRegionContentTags.Noble;
                case GuildHouseKind.Alchemists:
                    return DecorationRegionContentTags.Scholar | DecorationRegionContentTags.Craft;
                default:
                    return DecorationRegionContentTags.Adventurer | DecorationRegionContentTags.LivedIn;
            }
        }
    }
}

using Game.Structures.Runtime;
using NUnit.Framework;

namespace Game.Structures.Tests
{
    public sealed class GuildHouseRegionPolicyTests
    {
        [Test]
        public void SignatureGuildsPreferExpectedRegionsWithoutHardBans()
        {
            Assert.That(GuildHouseRegionPolicy.Preference(GuildHouseKind.Wizards, DecorationRegionTheme.Moordell),
                Is.GreaterThan(GuildHouseRegionPolicy.Preference(GuildHouseKind.Wizards, DecorationRegionTheme.Kentridge)));
            Assert.That(GuildHouseRegionPolicy.Preference(GuildHouseKind.Druids, DecorationRegionTheme.FairyVillage),
                Is.GreaterThan(GuildHouseRegionPolicy.Preference(GuildHouseKind.Druids, DecorationRegionTheme.Rossdam)));
            Assert.That(GuildHouseRegionPolicy.Preference(GuildHouseKind.Knights, DecorationRegionTheme.Rossdam), Is.GreaterThan(0));
            Assert.That(GuildHouseRegionPolicy.Preference(GuildHouseKind.Clerics, DecorationRegionTheme.Hightown), Is.GreaterThan(0));
        }
    }
}

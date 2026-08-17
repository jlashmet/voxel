using Game.Structures.Runtime;
using NUnit.Framework;

namespace Game.Structures.Tests
{
    public sealed class GuildHouseSettlementRosterPlannerTests
    {
        [Test]
        public void RosterSizeScalesWithSettlement()
        {
            Assert.That(GuildHouseSettlementRosterPlanner.Plan(DecorationRegionTheme.Kentridge, GuildSettlementScale.Hamlet, 1).Guilds.Length, Is.EqualTo(0));
            Assert.That(GuildHouseSettlementRosterPlanner.Plan(DecorationRegionTheme.Kentridge, GuildSettlementScale.Village, 1).Guilds.Length, Is.EqualTo(1));
            Assert.That(GuildHouseSettlementRosterPlanner.Plan(DecorationRegionTheme.Kentridge, GuildSettlementScale.Town, 1).Guilds.Length, Is.EqualTo(3));
            Assert.That(GuildHouseSettlementRosterPlanner.Plan(DecorationRegionTheme.Kentridge, GuildSettlementScale.City, 1).Guilds.Length, Is.EqualTo(6));
            Assert.That(GuildHouseSettlementRosterPlanner.Plan(DecorationRegionTheme.Kentridge, GuildSettlementScale.Capital, 1).Guilds.Length, Is.EqualTo(9));
        }

        [Test]
        public void RegionIdentityChangesRosterOrdering()
        {
            GuildSettlementRoster moordell = GuildHouseSettlementRosterPlanner.Plan(
                DecorationRegionTheme.Moordell, GuildSettlementScale.Town, 123u);
            GuildSettlementRoster fairy = GuildHouseSettlementRosterPlanner.Plan(
                DecorationRegionTheme.FairyVillage, GuildSettlementScale.Town, 123u);

            Assert.That(moordell.Guilds, Does.Contain(GuildHouseKind.Wizards));
            Assert.That(fairy.Guilds, Does.Contain(GuildHouseKind.Druids));
        }

        [Test]
        public void CityAndCapitalRetainCorePublicInstitutions()
        {
            GuildSettlementRoster city = GuildHouseSettlementRosterPlanner.Plan(
                DecorationRegionTheme.Hightown, GuildSettlementScale.City, 77u);
            GuildSettlementRoster capital = GuildHouseSettlementRosterPlanner.Plan(
                DecorationRegionTheme.Rossdam, GuildSettlementScale.Capital, 88u);

            Assert.That(city.Guilds, Does.Contain(GuildHouseKind.Adventurers));
            Assert.That(city.Guilds, Does.Contain(GuildHouseKind.Clerics));
            Assert.That(capital.Guilds, Does.Contain(GuildHouseKind.Adventurers));
            Assert.That(capital.Guilds, Does.Contain(GuildHouseKind.Clerics));
        }

        [Test]
        public void SameInputsProduceSameRoster()
        {
            GuildSettlementRoster a = GuildHouseSettlementRosterPlanner.Plan(
                DecorationRegionTheme.OrcVillage, GuildSettlementScale.City, 999u);
            GuildSettlementRoster b = GuildHouseSettlementRosterPlanner.Plan(
                DecorationRegionTheme.OrcVillage, GuildSettlementScale.City, 999u);
            Assert.That(b.Guilds, Is.EqualTo(a.Guilds));
        }
    }
}

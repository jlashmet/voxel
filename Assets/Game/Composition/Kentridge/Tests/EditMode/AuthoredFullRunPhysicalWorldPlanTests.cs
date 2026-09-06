using System;
using Game.Composition.Campaign.Content;
using Game.Composition.WorldBuilderWorldGen.Runtime;
using Game.Cutscenes.Api;
using MountingForce.WorldGen;
using NUnit.Framework;

namespace Game.Composition.Kentridge.Tests
{
    public sealed class AuthoredFullRunPhysicalWorldPlanTests
    {
        [Test]
        public void FullRunPlanConsumesCompiledHierarchyAndRecoveredPhysicalMacroWorld()
        {
            AuthoredFullRunCampaignContent content = BuildContent();

            AuthoredFullRunPhysicalWorldPlan plan = BuildPhysical(content);

            Assert.That(plan.Graph.HierarchyPlan.Settlements.Count, Is.GreaterThan(1),
                "The full-run adapter must consume the compiled multi-settlement hierarchy, not the opening-only graph.");
            for (var i = 0; i < plan.Graph.HierarchyPlan.Settlements.Count; i++)
            {
                Assert.That(
                    plan.TryGetPhysicalSettlement(
                        plan.Graph.HierarchyPlan.Settlements[i].Settlement,
                        out _),
                    Is.True,
                    "Every authored settlement must have a source-backed physical settlement; fake or dropped regions are forbidden.");
            }

            Assert.That(plan.Physical.Routes.Count, Is.GreaterThan(0));
            Assert.That(plan.Physical.RouteTileCount, Is.GreaterThan(0));
            Assert.That(plan.Physical.TryGetSettlement("moordell", out _), Is.True);
            Assert.That(plan.Physical.TryGetSettlement("rossdam", out _), Is.True);
            Assert.That(plan.Physical.TryGetRoute("south-fighting-area-1", "overworld-logan-castle", out _), Is.True,
                "The production physical plan must retain the real source-backed Logan approach route.");
        }

        [Test]
        public void FullRunGenerationResolvesAuthoredSitesAndNpcAssignmentsAgainstPhysicalHierarchy()
        {
            AuthoredFullRunCampaignContent content = BuildContent();
            AuthoredFullRunPhysicalWorldPlan physical = BuildPhysical(content);

            AuthoredFullRunCampaignGenerationPlan generation =
                AuthoredFullRunCampaignGenerator.Plan(physical);

            Assert.That(generation.Sites.IsResolved, Is.True);
            Assert.That(generation.Sites.Bindings.Count, Is.EqualTo(physical.Graph.SiteRoles.Count),
                "Every authored SiteRef must bind through the production SiteRoleResolver.");
            Assert.That(generation.NpcAssignments.Count, Is.EqualTo(physical.Graph.NpcPlacements.Count),
                "Every authored NPC placement must bind through NpcPlacementResolver rather than a player-scene lookup table.");

            Assert.That(generation.TryGetPhysicalAnchor(content.RorikConflictSite, out _), Is.True,
                "The region-owned Rorik conflict must resolve to a source-backed physical anchor.");
            Assert.That(generation.TryGetPhysicalAnchor(content.MoordellDistributionSite, out _), Is.True,
                "Moordell continuation content must resolve inside the real Moordell physical settlement.");
            Assert.That(generation.TryGetPhysicalAnchor(content.RossdamBattleSite, out _), Is.True,
                "Rossdam continuation content must resolve inside the real Rossdam physical settlement.");
            Assert.That(generation.TryGetPhysicalAnchor(content.LoganCastleLowerSite, out _), Is.True,
                "The Logan castle continuation must resolve against the recovered physical hierarchy.");

            Assert.That(generation.TryGetResolvedSite(content.MoordellDistributionSite, out var moordell), Is.True);
            Assert.That(generation.TryGetResolvedSite(content.RossdamBattleSite, out var rossdam), Is.True);
            Assert.That(moordell, Is.Not.EqualTo(rossdam),
                "Distinct authored settlements must not collapse to one generated site identity.");
        }

        private static AuthoredFullRunCampaignContent BuildContent() =>
            AuthoredFullRunCampaignContent.Build(
                new CutsceneDefinition(
                    "system26-full-run-destination",
                    CutsceneStageSetupDefinition.Empty,
                    Array.Empty<CutsceneStep>()));

        private static AuthoredFullRunPhysicalWorldPlan BuildPhysical(AuthoredFullRunCampaignContent content) =>
            AuthoredFullRunPhysicalWorldPlanner.Plan(
                content.Blueprint,
                0x4B454E54u,
                new Int2(0, 0),
                voxelsPerDecimetre: 1);
    }
}

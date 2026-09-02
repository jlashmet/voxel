using System.Linq;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class SecretCluePlannerBoundaryTests
    {
        [Test]
        public void OptionalClueWithMissingSourceIsOmittedWithoutFailingThePlan()
        {
            Fixture(out SiteRef resolvedSite, out SecretRef secret, out ResolvedSecretPlan secretPlan,
                out SiteRoleBinding[] sites);
            var absentSite = new SiteRef("absent-optional-source");
            SecretClueSpec clue = SecretClues.Define("optional-note", secret, value => value
                .Stage(1).Kind(SecretClueKind.Readable).Optional()
                .SourceAt(absentSite).Target(resolvedSite).Content("clues/optional"));

            SecretCluePlanningResult result = SecretCluePlanner.Resolve(
                9, new[] { clue }, new[] { secretPlan }, sites, null);

            Assert.That(result.IsResolved, Is.True);
            Assert.That(result.Clues, Is.Empty);
            Assert.That(result.Diagnostics, Is.Empty);
        }

        [Test]
        public void DuplicateClueIdFailsClosedWithInspectableDiagnostic()
        {
            Fixture(out SiteRef site, out SecretRef secret, out ResolvedSecretPlan secretPlan,
                out SiteRoleBinding[] sites);
            SecretClueSpec first = SecretClues.Define("same-id", secret, value => value
                .Stage(1).Kind(SecretClueKind.Environmental).SourceAt(site).Content("clues/one"));
            SecretClueSpec second = SecretClues.Define("same-id", secret, value => value
                .Stage(2).Kind(SecretClueKind.Readable).SourceAt(site).Content("clues/two"));

            SecretCluePlanningResult result = SecretCluePlanner.Resolve(
                9, new[] { first, second }, new[] { secretPlan }, sites, null);

            Assert.That(result.IsResolved, Is.False);
            Assert.That(result.Diagnostics.Any(x => x.Kind == SecretClueDiagnosticKind.DuplicateClueId), Is.True);
        }

        [Test]
        public void DuplicateStageForOneSecretFailsClosed()
        {
            Fixture(out SiteRef site, out SecretRef secret, out ResolvedSecretPlan secretPlan,
                out SiteRoleBinding[] sites);
            SecretClueSpec first = SecretClues.Define("first", secret, value => value
                .Stage(1).Kind(SecretClueKind.Environmental).SourceAt(site).Content("clues/one"));
            SecretClueSpec second = SecretClues.Define("second", secret, value => value
                .Stage(1).Kind(SecretClueKind.Readable).SourceAt(site).Content("clues/two"));

            SecretCluePlanningResult result = SecretCluePlanner.Resolve(
                9, new[] { first, second }, new[] { secretPlan }, sites, null);

            Assert.That(result.IsResolved, Is.False);
            Assert.That(result.Diagnostics.Any(x => x.Kind == SecretClueDiagnosticKind.DuplicateStage), Is.True);
        }

        [Test]
        public void RequiredClueWithoutCanonicalResolvedSecretFailsClosed()
        {
            Fixture(out SiteRef site, out SecretRef secret, out _, out SiteRoleBinding[] sites);
            SecretClueSpec clue = SecretClues.Define("required", secret, value => value
                .Stage(1).Kind(SecretClueKind.Inspectable).SourceAt(site).Content("clues/required"));

            SecretCluePlanningResult result = SecretCluePlanner.Resolve(
                9, new[] { clue }, null, sites, null);

            Assert.That(result.IsResolved, Is.False);
            Assert.That(result.Clues, Is.Empty);
            Assert.That(result.Diagnostics.Single().Kind,
                Is.EqualTo(SecretClueDiagnosticKind.MissingResolvedSecret));
        }

        [Test]
        public void RumorWithoutNpcSourceFailsInsteadOfTreatingSceneryAsDialogue()
        {
            Fixture(out SiteRef site, out SecretRef secret, out ResolvedSecretPlan secretPlan,
                out SiteRoleBinding[] sites);
            SecretClueSpec clue = SecretClues.Define("bad-rumor", secret, value => value
                .Stage(1).Kind(SecretClueKind.Rumor).SourceAt(site).Content("clues/rumor"));

            SecretCluePlanningResult result = SecretCluePlanner.Resolve(
                9, new[] { clue }, new[] { secretPlan }, sites, null);

            Assert.That(result.IsResolved, Is.False);
            Assert.That(result.Clues, Is.Empty);
            Assert.That(result.Diagnostics.Single().Kind,
                Is.EqualTo(SecretClueDiagnosticKind.InvalidRumorSource));
        }

        [Test]
        public void RumorNpcMustHaveConversationCapability()
        {
            Fixture(out SiteRef site, out SecretRef secret, out ResolvedSecretPlan secretPlan,
                out SiteRoleBinding[] sites);
            var npc = new NpcRef("silent-witness");
            var assignments = new[]
            {
                new NpcSiteAssignment(npc, site, sites.Single().Site, requiresConversation: false)
            };
            SecretClueSpec clue = SecretClues.Define("silent-rumor", secret, value => value
                .Stage(1).Kind(SecretClueKind.Rumor).SourceFrom(npc).Content("clues/silent"));

            SecretCluePlanningResult result = SecretCluePlanner.Resolve(
                9, new[] { clue }, new[] { secretPlan }, sites, assignments);

            Assert.That(result.IsResolved, Is.False);
            Assert.That(result.Clues, Is.Empty);
            Assert.That(result.Diagnostics.Single().Kind,
                Is.EqualTo(SecretClueDiagnosticKind.MissingRequiredSource));
        }

        private static void Fixture(
            out SiteRef site,
            out SecretRef secret,
            out ResolvedSecretPlan secretPlan,
            out SiteRoleBinding[] sites)
        {
            var game = Campaign.Create("secret-clue-boundaries");
            SiteRef localSite = game.World.RequireSite("hidden-site", value => value
                .Archetype(SiteArchetype.Ruin)
                .RequireCapability(SiteCapability.SecretCandidateHost));
            LootTableRef reward = game.Loot.Table("reward", loot => loot
                .RollCount(1, 1).Guaranteed(LootCategory.Currency));
            SecretRef localSecret = game.World.RequireSecret("hidden-cache", required => required
                .Inside(localSite)
                .Entrance(SecretEntranceType.DestroyableFalseWall)
                .RequireHiddenSpace()
                .RewardWith(reward));

            site = localSite;
            secret = localSecret;
            secretPlan = new ResolvedSecretPlan(
                localSecret,
                localSite,
                new SecretCandidateId("generated/room"),
                "generated/entrance",
                ContainerArchetype.TreasureChest,
                reward);
            sites = new[]
            {
                new SiteRoleBinding(localSite, new ResolvedSiteId("generated/site"))
            };
        }
    }
}

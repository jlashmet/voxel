using System.Linq;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class SecretDiscoveryPlannerTests
    {
        [Test]
        public void StandardAndMajorReadabilityPoliciesSelectPreSolveSemanticEvidence()
        {
            Fixture(out SiteRef approach, out SiteRef hidden, out SecretRef secret,
                out ResolvedSecretPlan canonical, out SiteRoleBinding[] sites);
            SecretRouteSpec route = NaturalRoute(secret, "cliff-climb");
            var anchors = new[]
            {
                Anchor("distant-ledge", approach, SecretClueAnchorRole.TraversalHint,
                    SecretClueChannel.Navigation),
                Anchor("water-flow", approach, SecretClueAnchorRole.ApproachEvidence,
                    SecretClueChannel.Environmental),
                Anchor("cave-shadow", approach, SecretClueAnchorRole.SightlineHint,
                    SecretClueChannel.Visual)
            };

            SecretDiscoveryPlanningResult standard = SecretDiscoveryPlanner.Resolve(
                101,
                new SecretDiscoverySpec(secret, SecretImportance.Standard, new[] { route }, anchors),
                new[] { canonical },
                sites);
            SecretDiscoveryPlanningResult major = SecretDiscoveryPlanner.Resolve(
                101,
                new SecretDiscoverySpec(secret, SecretImportance.Major, new[] { route }, anchors),
                new[] { canonical },
                sites);

            Assert.That(standard.IsResolved, Is.True, Join(standard));
            Assert.That(standard.Plan.Clues.Count, Is.EqualTo(1));
            Assert.That(major.IsResolved, Is.True, Join(major));
            Assert.That(major.Plan.Clues.Count, Is.EqualTo(2));
            Assert.That(major.Plan.Clues.Select(x => x.Channel).Distinct().Count(), Is.EqualTo(2));
            Assert.That(major.Plan.Clues.All(x => x.AnchorSiteRole.Equals(approach)), Is.True);
            Assert.That(major.Plan.Candidate, Is.EqualTo(canonical.Candidate));
            Assert.That(major.Plan.EntranceId, Is.EqualTo(canonical.EntranceId));
        }

        [Test]
        public void SameSeedAndInputsProduceIdenticalRouteAndCluePlan()
        {
            Fixture(out SiteRef approach, out _, out SecretRef secret,
                out ResolvedSecretPlan canonical, out SiteRoleBinding[] sites);
            var routes = new[]
            {
                NaturalRoute(secret, "natural"),
                new SecretRouteSpec(
                    new SecretRouteId("trapdoor"), secret, SecretRouteKind.Trapdoor,
                    SecretBypassPolicy.ProtectedShell, "roof-access", true,
                    new SecretBypassEvidence(false, 0, 0))
            };
            var anchors = new[]
            {
                Anchor("window", approach, SecretClueAnchorRole.ExteriorEvidence, SecretClueChannel.Spatial),
                Anchor("tracks", approach, SecretClueAnchorRole.ApproachEvidence, SecretClueChannel.Navigation),
                Anchor("draft", approach, SecretClueAnchorRole.AcousticHint, SecretClueChannel.Audio)
            };
            var spec = new SecretDiscoverySpec(secret, SecretImportance.Major, routes, anchors);

            SecretDiscoveryPlanningResult first = SecretDiscoveryPlanner.Resolve(987, spec, new[] { canonical }, sites);
            SecretDiscoveryPlanningResult second = SecretDiscoveryPlanner.Resolve(987, spec, new[] { canonical }, sites.Reverse().ToArray());

            Assert.That(first.IsResolved, Is.True, Join(first));
            Assert.That(second.IsResolved, Is.True, Join(second));
            Assert.That(second.Plan.Routes.Select(x => x.Id.Id), Is.EqualTo(first.Plan.Routes.Select(x => x.Id.Id)));
            Assert.That(second.Plan.Clues.Select(x => x.Id.Id), Is.EqualTo(first.Plan.Clues.Select(x => x.Id.Id)));
            Assert.That(second.Plan.Clues.Select(x => x.Channel), Is.EqualTo(first.Plan.Clues.Select(x => x.Channel)));
            Assert.That(second.Plan.Clues.Select(x => x.AnchorSite.Value), Is.EqualTo(first.Plan.Clues.Select(x => x.AnchorSite.Value)));
        }

        [Test]
        public void CircularOrPostSolveOnlyEvidenceCannotSatisfyRequiredReadability()
        {
            Fixture(out SiteRef approach, out _, out SecretRef secret,
                out ResolvedSecretPlan canonical, out SiteRoleBinding[] sites);
            SecretRouteSpec route = NaturalRoute(secret, "route");
            var circular = new SecretClueAnchorSpec(
                new SecretClueAnchorId("circular"), approach, SecretClueAnchorRole.RouteAdjacentEvidence,
                new[] { SecretClueChannel.Navigation }, true, SecretHiddenVolumeRelation.Outside,
                0f, 20f, route.Id, true, route.Id, true);

            SecretDiscoveryPlanningResult circularResult = SecretDiscoveryPlanner.Resolve(
                1,
                new SecretDiscoverySpec(secret, SecretImportance.Standard, new[] { route }, new[] { circular }),
                new[] { canonical },
                sites);
            Assert.That(circularResult.IsResolved, Is.False);
            Assert.That(circularResult.Diagnostics.Any(x => x.Kind == SecretDiscoveryDiagnosticKind.CircularClueDependency), Is.True);

            var postSolve = new SecretClueAnchorSpec(
                new SecretClueAnchorId("inside-only"), approach, SecretClueAnchorRole.NarrativeHint,
                new[] { SecretClueChannel.Narrative }, false, SecretHiddenVolumeRelation.Inside,
                0f, 20f);
            SecretDiscoveryPlanningResult hiddenResult = SecretDiscoveryPlanner.Resolve(
                1,
                new SecretDiscoverySpec(secret, SecretImportance.Standard, new[] { route }, new[] { postSolve }),
                new[] { canonical },
                sites);
            Assert.That(hiddenResult.IsResolved, Is.False);
            Assert.That(hiddenResult.Diagnostics.Any(x => x.Kind == SecretDiscoveryDiagnosticKind.InsufficientObservableClues), Is.True);
        }

        [Test]
        public void MultipleNaturalAndMechanismRoutesRetainOneStableDiscoveryIdentity()
        {
            Fixture(out SiteRef approach, out _, out SecretRef secret,
                out ResolvedSecretPlan canonical, out SiteRoleBinding[] sites);
            var natural = NaturalRoute(secret, "natural-climb");
            var breakable = new SecretRouteSpec(
                new SecretRouteId("crumbled-wall"), secret, SecretRouteKind.BreakableBarrier,
                SecretBypassPolicy.AuthoredBreakablesOnly, "ruin-wall", true,
                new SecretBypassEvidence(false, 24, 0));
            var anchor = Anchor("ledge", approach, SecretClueAnchorRole.TraversalHint, SecretClueChannel.Navigation);

            SecretDiscoveryPlanningResult result = SecretDiscoveryPlanner.Resolve(
                77,
                new SecretDiscoverySpec(secret, SecretImportance.Standard, new[] { natural, breakable }, new[] { anchor }),
                new[] { canonical },
                sites);

            Assert.That(result.IsResolved, Is.True, Join(result));
            Assert.That(result.Plan.Routes.Count, Is.EqualTo(2));
            Assert.That(result.Plan.Routes.All(x => x.Secret.Equals(secret)), Is.True);
            Assert.That(result.Plan.Routes.Single(x => x.Id.Equals(natural.Id)).RequiresInteractable, Is.False,
                "Natural traversal proves secret planning does not require an interactable route.");
            Assert.That(result.Plan.Secret, Is.EqualTo(secret));
        }

        [Test]
        public void VoxelBypassPolicyRejectsProtectedHolesAndBreakableLeakageButAllowsSystemicBypass()
        {
            Fixture(out SiteRef approach, out _, out SecretRef secret,
                out ResolvedSecretPlan canonical, out SiteRoleBinding[] sites);
            var anchor = Anchor("hint", approach, SecretClueAnchorRole.ApproachEvidence, SecretClueChannel.Environmental);

            SecretRouteSpec badProtected = new SecretRouteSpec(
                new SecretRouteId("protected"), secret, SecretRouteKind.Trapdoor,
                SecretBypassPolicy.ProtectedShell, "attic-shell", true,
                new SecretBypassEvidence(true, 0, 0));
            SecretDiscoveryPlanningResult protectedResult = SecretDiscoveryPlanner.Resolve(
                5, new SecretDiscoverySpec(secret, SecretImportance.Standard, new[] { badProtected }, new[] { anchor }),
                new[] { canonical }, sites);
            Assert.That(protectedResult.IsResolved, Is.False);
            Assert.That(protectedResult.Diagnostics.Any(x => x.Kind == SecretDiscoveryDiagnosticKind.ProtectedShellBypass), Is.True);

            SecretRouteSpec leakingBreakable = new SecretRouteSpec(
                new SecretRouteId("breakable"), secret, SecretRouteKind.BreakableBarrier,
                SecretBypassPolicy.AuthoredBreakablesOnly, "crypt-panel", true,
                new SecretBypassEvidence(false, 8, 2));
            SecretDiscoveryPlanningResult breakableResult = SecretDiscoveryPlanner.Resolve(
                5, new SecretDiscoverySpec(secret, SecretImportance.Standard, new[] { leakingBreakable }, new[] { anchor }),
                new[] { canonical }, sites);
            Assert.That(breakableResult.IsResolved, Is.False);
            Assert.That(breakableResult.Diagnostics.Any(x => x.Kind == SecretDiscoveryDiagnosticKind.AuthoredBreakableInvalid), Is.True);

            SecretRouteSpec systemic = new SecretRouteSpec(
                new SecretRouteId("systemic"), secret, SecretRouteKind.NaturalTraversal,
                SecretBypassPolicy.SystemicBypassAllowed, "cliff-face", false,
                new SecretBypassEvidence(true, 0, 12));
            SecretDiscoveryPlanningResult systemicResult = SecretDiscoveryPlanner.Resolve(
                5, new SecretDiscoverySpec(secret, SecretImportance.Standard, new[] { systemic }, new[] { anchor }),
                new[] { canonical }, sites);
            Assert.That(systemicResult.IsResolved, Is.True, Join(systemicResult));
            Assert.That(systemicResult.Plan.Secret, Is.EqualTo(secret),
                "Systemic bypass changes the legal route, not discovery identity.");
        }

        private static SecretRouteSpec NaturalRoute(SecretRef secret, string id) =>
            new SecretRouteSpec(
                new SecretRouteId(id), secret, SecretRouteKind.NaturalTraversal,
                SecretBypassPolicy.SystemicBypassAllowed, "natural-traversal", false,
                new SecretBypassEvidence(false, 0, 0));

        private static SecretClueAnchorSpec Anchor(
            string id,
            SiteRef site,
            SecretClueAnchorRole role,
            SecretClueChannel channel) =>
            new SecretClueAnchorSpec(
                new SecretClueAnchorId(id), site, role, new[] { channel }, true,
                SecretHiddenVolumeRelation.Outside, 1f, 80f);

        private static string Join(SecretDiscoveryPlanningResult result) =>
            string.Join(" | ", result.Diagnostics.Select(x => x.ToString()));

        private static void Fixture(
            out SiteRef approach,
            out SiteRef hidden,
            out SecretRef secret,
            out ResolvedSecretPlan canonical,
            out SiteRoleBinding[] sites)
        {
            var game = Campaign.Create("secret-discovery-planner");
            SiteRef localApproach = game.World.RequireSite("approach", value => value.Archetype(SiteArchetype.Ruin));
            SiteRef localHidden = game.World.RequireSite("hidden", value => value
                .Archetype(SiteArchetype.Ruin)
                .RequireCapability(SiteCapability.SecretCandidateHost));
            LootTableRef reward = game.Loot.Table("reward", loot => loot
                .RollCount(1, 1).Guaranteed(LootCategory.Currency));
            SecretRef localSecret = game.World.RequireSecret("secret", required => required
                .Inside(localHidden)
                .Entrance(SecretEntranceType.DestroyableFalseWall)
                .RequireHiddenSpace()
                .RewardWith(reward));

            approach = localApproach;
            hidden = localHidden;
            secret = localSecret;
            canonical = new ResolvedSecretPlan(
                localSecret, localHidden, new SecretCandidateId("generated/hidden-volume"),
                "generated/entrance", ContainerArchetype.TreasureChest, reward);
            sites = new[]
            {
                new SiteRoleBinding(localApproach, new ResolvedSiteId("generated/approach")),
                new SiteRoleBinding(localHidden, new ResolvedSiteId("generated/hidden"))
            };
        }
    }
}

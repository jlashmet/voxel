using System;
using System.Collections.Generic;
using System.Linq;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class SecretCluePlannerTests
    {
        [Test]
        public void RequiredHiddenCacheResolvesThreeStageChainAgainstCanonicalSecretPlan()
        {
            BuildFixture(out SiteRef trail, out SiteRef shelter, out SiteRef vault, out NpcRef rumorNpc,
                out SecretRef secret, out ResolvedSecretPlan secretPlan, out SiteRoleBinding[] sites,
                out NpcSiteAssignment[] npcs);

            SecretClueSpec[] clues =
            {
                SecretClues.Define("cache-tracks", secret, clue => clue
                    .Stage(1).Kind(SecretClueKind.Environmental).SourceAt(trail).Target(shelter)
                    .Content("clues/cache/tracks")),
                SecretClues.Define("cache-note", secret, clue => clue
                    .Stage(2).Kind(SecretClueKind.Readable).SourceAt(shelter).Target(vault)
                    .Content("clues/cache/note")),
                SecretClues.Define("cache-masonry", secret, clue => clue
                    .Stage(3).Kind(SecretClueKind.Inspectable).SourceAt(vault).Target(vault)
                    .Content("clues/cache/masonry"))
            };

            SecretCluePlanningResult result = SecretCluePlanner.Resolve(
                77, clues, new[] { secretPlan }, sites, npcs);

            Assert.That(result.IsResolved, Is.True, JoinDiagnostics(result));
            Assert.That(result.Clues.Select(x => x.Stage), Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(result.Clues.Select(x => x.Kind), Is.EqualTo(new[]
            {
                SecretClueKind.Environmental,
                SecretClueKind.Readable,
                SecretClueKind.Inspectable
            }));
            Assert.That(result.Clues.All(x => x.TargetCandidate.Equals(secretPlan.Candidate)), Is.True,
                "Every clue must lead through the canonical SecretPlanner target rather than selecting another hidden space.");
            Assert.That(result.Clues.All(x => x.TargetEntrance == secretPlan.EntranceId), Is.True);
            Assert.That(result.Clues[0].SourceRole, Is.EqualTo(trail));
            Assert.That(result.Clues[1].SourceRole, Is.EqualTo(shelter));
            Assert.That(result.Clues[2].SourceRole, Is.EqualTo(vault));
            Assert.That(result.Clues.All(x => x.MemoryTopic == "memory://secrets/hidden-cache"), Is.True);

            var authority = new SecretDiscoveryState();
            var ledger = new SecretDiscoveryLedger(authority);
            Assert.That(ledger.IsDiscovered(secretPlan), Is.False,
                "The generated world knows the target, but player memory must begin undiscovered.");
            ledger.Observe(result.Clues[0]);
            ledger.Observe(result.Clues[1]);
            ledger.Observe(result.Clues[2]);
            Assert.That(ledger.IsDiscovered(secretPlan), Is.False,
                "Observing clues alone does not silently reveal the secret target.");

            var rewardEvents = 0;
            authority.Discovered += _ => rewardEvents++;
            Assert.That(ledger.Discover(secretPlan), Is.True);
            Assert.That(ledger.Discover(secretPlan), Is.False,
                "Revisiting the same canonical candidate must not duplicate discovery credit.");
            Assert.That(rewardEvents, Is.EqualTo(1),
                "Reward consumers listening to canonical discovery must receive one event only.");

            SecretDiscoverySnapshot saved = ledger.Capture();
            var restoredAuthority = new SecretDiscoveryState();
            var restored = new SecretDiscoveryLedger(restoredAuthority);
            var restoredRewardEvents = 0;
            restoredAuthority.Discovered += _ => restoredRewardEvents++;
            restored.Restore(saved);
            Assert.That(restored.IsDiscovered(secretPlan), Is.True);
            Assert.That(restored.Discover(secretPlan), Is.False,
                "Reloading and revisiting the same candidate must remain idempotent.");
            Assert.That(restoredRewardEvents, Is.Zero,
                "Restore/revisit must not replay discovery reward events.");
            Assert.That(restored.HasObserved(result.Clues[0].Id), Is.True);
            Assert.That(restored.HasObserved(result.Clues[2].Id), Is.True);
        }

        [Test]
        public void RequiredClueFailsClosedWhenSemanticSourceWasNotResolved()
        {
            BuildFixture(out SiteRef trail, out _, out SiteRef vault, out _,
                out SecretRef secret, out ResolvedSecretPlan secretPlan, out SiteRoleBinding[] sites,
                out NpcSiteAssignment[] npcs);
            var missing = new SiteRef("missing-shelter-role");
            SecretClueSpec clue = SecretClues.Define("missing-source", secret, value => value
                .Stage(1).Kind(SecretClueKind.Readable).SourceAt(missing).Target(vault)
                .Content("clues/cache/missing"));

            SecretCluePlanningResult result = SecretCluePlanner.Resolve(
                1, new[] { clue }, new[] { secretPlan }, sites, npcs);

            Assert.That(result.IsResolved, Is.False);
            Assert.That(result.Clues, Is.Empty);
            Assert.That(result.Diagnostics.Single().Kind, Is.EqualTo(SecretClueDiagnosticKind.MissingRequiredSource));
        }

        [Test]
        public void RumorUsesResolvedConversationNpcAndStillTargetsCanonicalEntrance()
        {
            BuildFixture(out _, out _, out SiteRef vault, out NpcRef rumorNpc,
                out SecretRef secret, out ResolvedSecretPlan secretPlan, out SiteRoleBinding[] sites,
                out NpcSiteAssignment[] npcs);
            SecretClueSpec rumor = SecretClues.Define("road-rumor", secret, clue => clue
                .Stage(1).Kind(SecretClueKind.Rumor).SourceFrom(rumorNpc).Target(vault)
                .Content("clues/cache/rumor"));

            SecretCluePlanningResult result = SecretCluePlanner.Resolve(
                42, new[] { rumor }, new[] { secretPlan }, sites, npcs);

            Assert.That(result.IsResolved, Is.True, JoinDiagnostics(result));
            Assert.That(result.Clues.Single().SourceKind, Is.EqualTo(SecretClueSourceKind.Npc));
            Assert.That(result.Clues.Single().SourceNpc, Is.EqualTo(rumorNpc));
            Assert.That(result.Clues.Single().TargetCandidate, Is.EqualTo(secretPlan.Candidate));
            Assert.That(result.Clues.Single().TargetEntrance, Is.EqualTo(secretPlan.EntranceId));
        }

        [Test]
        public void SourceChoiceIsGenerationOrderIndependentAndCanVaryBySeed()
        {
            BuildFixture(out SiteRef trail, out SiteRef shelter, out SiteRef vault, out _,
                out SecretRef secret, out ResolvedSecretPlan secretPlan, out SiteRoleBinding[] sites,
                out NpcSiteAssignment[] npcs);

            SecretClueSpec forward = SecretClues.Define("variant-hint", secret, clue => clue
                .Stage(1).Kind(SecretClueKind.Environmental)
                .SourceAt(trail).SourceAt(shelter).SourceAt(vault).Target(vault)
                .Content("clues/cache/variant"));
            SecretClueSpec reversed = SecretClues.Define("variant-hint", secret, clue => clue
                .Stage(1).Kind(SecretClueKind.Environmental)
                .SourceAt(vault).SourceAt(shelter).SourceAt(trail).Target(vault)
                .Content("clues/cache/variant"));

            for (var seed = 0; seed < 16; seed++)
            {
                string a = ResolveSource(seed, forward, secretPlan, sites, npcs);
                string b = ResolveSource(seed, reversed, secretPlan, sites.Reverse().ToArray(), npcs);
                Assert.That(b, Is.EqualTo(a), "Selection must not depend on source or binding enumeration order.");
                Assert.That(ResolveSource(seed, forward, secretPlan, sites, npcs), Is.EqualTo(a));
            }

            var selected = new HashSet<string>(StringComparer.Ordinal);
            for (var seed = 0; seed < 64; seed++)
                selected.Add(ResolveSource(seed, forward, secretPlan, sites, npcs));
            Assert.That(selected.Count, Is.GreaterThan(1),
                "Equivalent authored clue sources should permit deterministic world-seed variation.");
        }

        private static string ResolveSource(
            int seed,
            SecretClueSpec clue,
            ResolvedSecretPlan secret,
            SiteRoleBinding[] sites,
            NpcSiteAssignment[] npcs)
        {
            SecretCluePlanningResult result = SecretCluePlanner.Resolve(
                seed, new[] { clue }, new[] { secret }, sites, npcs);
            Assert.That(result.IsResolved, Is.True, JoinDiagnostics(result));
            return result.Clues.Single().SourceSite.Value;
        }

        private static string JoinDiagnostics(SecretCluePlanningResult result) =>
            string.Join(" | ", result.Diagnostics.Select(x => x.ToString()));

        private static void BuildFixture(
            out SiteRef trail,
            out SiteRef shelter,
            out SiteRef vault,
            out NpcRef rumorNpc,
            out SecretRef secret,
            out ResolvedSecretPlan secretPlan,
            out SiteRoleBinding[] sites,
            out NpcSiteAssignment[] npcs)
        {
            var game = Campaign.Create("secret-clue-fixture");
            SiteRef localTrail = game.World.RequireSite("trail", site => site.Archetype(SiteArchetype.Ruin));
            SiteRef localShelter = game.World.RequireSite("shelter", site => site.Archetype(SiteArchetype.Ruin));
            SiteRef localVault = game.World.RequireSite("vault", site => site
                .Archetype(SiteArchetype.Ruin)
                .RequireCapability(SiteCapability.SecretCandidateHost));
            NpcRef localRumorNpc = game.World.RequireNpc("road-keeper", npc =>
                npc.PlaceAt(localShelter).RequireConversation());
            LootTableRef reward = game.Loot.Table("cache-loot", loot => loot
                .RollCount(1, 1).Guaranteed(LootCategory.Currency));
            SecretRef localSecret = game.World.RequireSecret("hidden-cache", required => required
                .Inside(localVault)
                .Entrance(SecretEntranceType.DestroyableFalseWall)
                .RequireHiddenSpace()
                .RewardWith(reward));

            trail = localTrail;
            shelter = localShelter;
            vault = localVault;
            rumorNpc = localRumorNpc;
            secret = localSecret;
            secretPlan = new ResolvedSecretPlan(
                localSecret,
                localVault,
                new SecretCandidateId("generated/hidden-cache-room"),
                "generated/false-wall-west",
                ContainerArchetype.TreasureChest,
                reward);
            sites = new[]
            {
                new SiteRoleBinding(localTrail, new ResolvedSiteId("generated/trail")),
                new SiteRoleBinding(localShelter, new ResolvedSiteId("generated/shelter")),
                new SiteRoleBinding(localVault, new ResolvedSiteId("generated/vault"))
            };
            npcs = new[]
            {
                new NpcSiteAssignment(
                    localRumorNpc,
                    localShelter,
                    new ResolvedSiteId("generated/shelter"),
                    requiresConversation: true)
            };
        }
    }
}
